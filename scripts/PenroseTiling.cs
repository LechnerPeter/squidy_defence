using Godot;
using System;
using System.Collections.Generic;

// One generated Penrose P3 rhombus tile - identified by a stable Id (its index
// in the list PenroseTiling.Generate returns), not a lattice coordinate, since
// Penrose tiles don't sit on a simple 2-integer grid the way hex cells do.
public sealed class PenroseTile {
	public int Id;
	public bool IsThin;                                   // thin (36/144) vs thick (72/108) rhombus
	public Vector2 Center;
	public Vector2[] Corners;                              // 4 world-space vertices, boundary order
	public List<int> EdgeNeighborIds = new List<int>();    // tiles sharing a full edge (fewer at the patch rim)
	public List<int> VertexIds = new List<int>();          // which PenroseVertex ids this tile touches
	public int[] CornerVertexIds = new int[4];             // CornerVertexIds[i] is the vertex id of Corners[i] - lets Validate() walk a specific corner's two edges precisely
	public bool[] CornerColorA = new bool[4];              // CornerColorA[i] mirrors Corners[i]'s vertex's ColorA - see PenroseVertex.ColorA
}

// One vertex of the tiling - the "hub" a vertex-cluster merge recipe builds
// around (see GameData.Merges / Main.cs). Unlike a hex grid, where every vertex
// is surrounded by exactly 3 cells, a P3 vertex can have anywhere from 3 to 7
// tiles around it depending on local configuration - Ring.Count IS the shape's
// size now, not a fixed constant.
public sealed class PenroseVertex {
	public int Id;
	public Vector2 Position;
	public List<int> Ring = new List<int>();                // tile ids, sorted into cyclic order around this vertex

	// Every Penrose vertex belongs to one of exactly two classes under the
	// documented vertex-type bipartition ("Star-Queen-King-Ace" vs
	// "Deuce-Jack-Sun") - equivalently, the graph connecting two vertices
	// whenever some tile edge joins them directly is always properly
	// 2-colorable, with no tile edge ever joining two same-colored vertices.
	// Computed once in PenroseTiling.BuildAdjacency via BFS over the real
	// vertex adjacency graph, then verified for contradictions in Validate() -
	// a real, meaningful structural fact about the tiling, even though
	// nothing currently reads it for anything beyond that diagnostic (the
	// matching-rule arcs are governed by PenroseTileRules' fixed per-corner
	// roles instead - see PenroseTiling.Generate).
	public bool ColorA;
}

// Builds the gameplay-ready Penrose P3 patch. PenroseGenerator supplies the
// raw rhombus geometry (de Bruijn's pentagrid dual method); this class
// resolves each tile's orientation against PenroseTileRules' discrete
// side-label matching rule, then computes the adjacency/vertex-ring data
// every gameplay consumer (pathfinding, merge recipes, rendering) depends on.
//
// "Orientation" needs resolving because a rhombus's own two acute (or two
// obtuse) corners are geometrically interchangeable by a pure relabeling -
// shifting which corner is called "0" by 2 keeps corner 0/2 acute and keeps
// CCW winding (see PenroseTileRules.CanonicalCorners) - so PenroseGenerator's
// raw output picks one arbitrarily, per crossing. Which choice is actually
// correct depends on the tile's real neighbors, since PenroseTileRules
// assigns a fixed role to each corner index and only specific role pairings
// may touch (PenroseTileRules.AllowedConnections).
public static class PenroseTiling {
	// How far past the true circle (radius, cutoutCenter) the raw superset
	// used for CutKeepingTripletsWhole reaches, in edge lengths - generous
	// enough that a circle triplet straddling the true boundary is always
	// generated whole in the first place, never itself clipped by the
	// superset's own cut before there's a chance to decide whether to keep it.
	private const float TripletKeepMarginEdgeLengths = 3.0f;

	public static (List<PenroseTile> Tiles, List<PenroseVertex> Vertices) Generate(float edgeLength, float radius, Vector2 cutoutCenter = default) {
		List<PenroseTileInstance> raw = CutKeepingTripletsWhole(edgeLength, radius, cutoutCenter);
		var tiles = new List<PenroseTile>(raw.Count);
		for (int i = 0; i < raw.Count; i++) {
			PenroseTileInstance inst = raw[i];
			tiles.Add(new PenroseTile {
				Id = i,
				IsThin = inst.Kind == TileKind.Thin,
				Corners = inst.Corners,
				Center = (inst.Corners[0] + inst.Corners[1] + inst.Corners[2] + inst.Corners[3]) / 4.0f,
			});
		}

		ResolveOrientations(tiles, edgeLength, out int connectionProblems, out int colorProblems);
		List<PenroseVertex> vertices = BuildAdjacency(tiles);

		if (connectionProblems > 0 || colorProblems > 0) {
			RepairLocalCircles(tiles, vertices, edgeLength);
			// Tile corners changed during repair, and a rotated triplet can
			// touch different outer vertices than before - rebuild from
			// scratch rather than trust whatever partial bookkeeping the
			// repair pass itself kept current, so pathfinding/merge recipes
			// never see stale adjacency.
			vertices = BuildAdjacency(tiles);
			var finalInstances = new List<PenroseTileInstance>(tiles.Count);
			foreach (PenroseTile tile in tiles)
				finalInstances.Add(new PenroseTileInstance { Kind = tile.IsThin ? TileKind.Thin : TileKind.Thick, Corners = tile.Corners });
			connectionProblems = PenroseTileRules.ValidateConnections(finalInstances, logDetails: false);
			colorProblems = PenroseTileRules.ValidateArcColors(finalInstances, edgeLength, logDetails: false);
		}

		if (OS.IsDebugBuild() && (connectionProblems > 0 || colorProblems > 0))
			GD.PushWarning($"PenroseTiling.Generate: {connectionProblems} connection and {colorProblems} arc-color problem(s) remain after automatic repair - some tiles have no single orientation that satisfies every real neighbor under PenroseTileRules.AllowedConnections.");

		return (tiles, vertices);
	}

	// PenroseGenerator's plain per-tile circular cut (keep a tile only if its
	// OWN center falls within radius of cutoutCenter) can slice straight
	// through a circle triplet (2 thin + 1 thick meeting at one vertex,
	// THROUGH THEIR DECORATED CORNERS - see PenroseTileRules.ArcDecorationsFor
	// and the necessity argument on RepairLocalCircles below), leaving 1 or 2
	// orphaned members at the rim that can never be completed into a real
	// circle or rotated (TryFlipCircleTriplet needs all 3). Rather than
	// accept that, this generates a slightly larger raw superset (uncut
	// beyond a generous margin, so no boundary triplet is ever clipped
	// before there's a chance to judge it), finds every genuine triplet that
	// straddles the true circle (at least one member inside, at least one
	// outside), and pulls the outside member(s) in too - "keep them in if in
	// question," at the cost of a slightly non-circular rim rather than an
	// unrotatable, incomplete one.
	//
	// Deliberately NOT generalized to "every vertex, any ring size or
	// composition" - tried that, and it doesn't converge: pulling in one
	// straddling vertex's outside members can itself create a NEW straddling
	// vertex one layer further out, and since literally any finite cut of an
	// unbounded aperiodic tiling has open edges SOMEWHERE at its rim, that
	// chain never naturally terminates - it just chases the boundary
	// outward until it hits the raw superset's own hard edge, which has
	// exactly the same problem, confirmed by an earlier version of this
	// method ballooning the tile count 3-6x while still leaving fragments at
	// the new, further-out edge. The genuine-triplet restriction below is
	// what actually terminates: a real 2-thin+1-thick decorated vertex is,
	// by the same necessity argument as EnsureCircleConsistency, NEVER part
	// of a bigger configuration (144+144+72 already sums to exactly 360, so
	// there is no room for a 4th member) - so completing one never creates a
	// new straddling one, and a single pass suffices.
	//
	// The final tile set is then translated by -cutoutCenter, exactly like
	// PenroseGenerator.Generate itself does, so callers still see the patch
	// centered at the origin.
	private static List<PenroseTileInstance> CutKeepingTripletsWhole(float edgeLength, float radius, Vector2 cutoutCenter) {
		float reach = cutoutCenter.Length() + radius;
		float rawReach = reach + TripletKeepMarginEdgeLengths * edgeLength;
		List<PenroseTileInstance> rawInstances = PenroseGenerator.Generate(edgeLength, rawReach, Vector2.Zero); // untranslated, centered at the true origin

		var rawTiles = new List<PenroseTile>(rawInstances.Count);
		for (int i = 0; i < rawInstances.Count; i++) {
			PenroseTileInstance inst = rawInstances[i];
			rawTiles.Add(new PenroseTile {
				Id = i,
				IsThin = inst.Kind == TileKind.Thin,
				Corners = inst.Corners,
				Center = (inst.Corners[0] + inst.Corners[1] + inst.Corners[2] + inst.Corners[3]) / 4.0f,
			});
		}
		List<PenroseVertex> rawVertices = BuildAdjacency(rawTiles);

		var inside = new bool[rawTiles.Count];
		for (int i = 0; i < rawTiles.Count; i++)
			inside[i] = (rawTiles[i].Center - cutoutCenter).Length() <= radius;

		var keep = new bool[rawTiles.Count];
		Array.Copy(inside, keep, inside.Length);
		foreach (PenroseVertex vertex in rawVertices) {
			if (vertex.Ring.Count != 3)
				continue;
			int thinCount = 0;
			foreach (int id in vertex.Ring) {
				if (rawTiles[id].IsThin)
					thinCount++;
			}
			if (thinCount != 2)
				continue;

			// Composition alone (2 thin + 1 thick touching this point) isn't
			// enough - each member must present its DECORATED corner here
			// (thin: index 1 or 3, thick: index 0 or 2), exactly like
			// EnsureCircleConsistency/TryFlipCircleTriplet require. A
			// coincidental same-kind touch through an ordinary corner isn't
			// a genuine closed triplet (its real angles don't sum to 360)
			// and pulling in its "outside" member would just chase the
			// non-terminating cascade described above.
			bool genuineTriplet = true;
			foreach (int id in vertex.Ring) {
				PenroseTile t = rawTiles[id];
				int ci = Array.IndexOf(t.CornerVertexIds, vertex.Id);
				bool decorated = t.IsThin ? (ci == 1 || ci == 3) : (ci == 0 || ci == 2);
				if (!decorated)
					genuineTriplet = false;
			}
			if (!genuineTriplet)
				continue;

			bool anyInside = false, anyOutside = false;
			foreach (int id in vertex.Ring) {
				if (inside[id])
					anyInside = true;
				else
					anyOutside = true;
			}
			if (anyInside && anyOutside) {
				foreach (int id in vertex.Ring)
					keep[id] = true;
			}
		}

		var finalInstances = new List<PenroseTileInstance>();
		for (int i = 0; i < rawTiles.Count; i++) {
			if (!keep[i])
				continue;
			PenroseTile t = rawTiles[i];
			var corners = new Vector2[4];
			for (int c = 0; c < 4; c++)
				corners[c] = t.Corners[c] - cutoutCenter;
			finalInstances.Add(new PenroseTileInstance { Kind = t.IsThin ? TileKind.Thin : TileKind.Thick, Corners = corners });
		}
		return finalInstances;
	}

	// Three cheap local repairs, tried automatically after the global
	// orientation solver above: none of them touch that solver's delicate
	// global-polarity logic, since each only ever looks at one small local
	// neighborhood at a time, and only ever keeps a change that provably
	// helps (checked against the real PenroseTileRules validators, not
	// guessed at).
	//
	//  1. EnsureCircleConsistency: a vertex where exactly 2 thin + 1 thick
	//     tile meet is ALWAYS a genuine circle - 2*144+72=360 is the only
	//     way three tiles of that composition can close a vertex at all -
	//     so its 3 members always CAN be forced to agree on color. But that
	//     doesn't mean forcing them is free: a thin tile's other decorated
	//     corner might belong to a DIFFERENT circle elsewhere, so flipping it
	//     to fix this one can silently break that one. Guarded the same way
	//     as the other two steps below.
	//  2. TryFlipCircleTriplet is tried at every 2-thin+1-thick vertex, purely
	//     as a local search move: kept only when it provably reduces the
	//     total problem count, discarded otherwise.
	//  3. TryFlipAllStarsInsideOut (below): tried at every vertex where 5
	//     thick tiles meet (5*72=360, the only way 5 same-kind corners can
	//     close a vertex) - flips every member of the star together, kept
	//     only when it provably reduces the total problem count.
	//
	// A single pass through all three can itself open up NEW opportunities
	// for the others - a triplet flip repositions its 3 members, which can
	// change color agreement at the outer points they touch (points that may
	// themselves be another circle's or star's shared vertex) - so the whole
	// pass is repeated, rebuilding adjacency fresh each time, until a full
	// pass finds no further improvement (bounded by MaxPasses as a safety
	// net against oscillation, though none has been observed in practice).
	private const int MaxRepairPasses = 20;

	private static void RepairLocalCircles(List<PenroseTile> tiles, List<PenroseVertex> vertices, float edgeLength) {
		int previousTotal = TotalProblems(tiles, edgeLength);
		for (int pass = 0; pass < MaxRepairPasses; pass++) {
			EnsureCircleConsistency(tiles, vertices, edgeLength);
			TryFlipAllCircleTriplets(tiles, vertices, edgeLength);
			TryFlipAllStarsInsideOut(tiles, vertices, edgeLength);

			int currentTotal = TotalProblems(tiles, edgeLength);
			if (currentTotal >= previousTotal)
				break;
			previousTotal = currentTotal;
			vertices = BuildAdjacency(tiles); // refresh so the next pass sees whatever this one just opened up
		}
	}

	// Also exposed publicly as a manual, user-triggered Penrose View debug
	// button (Main.cs) - "tries every 3-tile blue circle rotation" - a single
	// pass over every current circle candidate, kept independent per flip.
	// Returns how many triplets were actually rotated.
	public static int TryFlipAllCircleTriplets(List<PenroseTile> tiles, List<PenroseVertex> vertices, float edgeLength) {
		int flippedCount = 0;
		var candidateVertexIds = new List<int>();
		foreach (PenroseVertex v in vertices) {
			if (v.Ring.Count != 3)
				continue;
			int thinCount = 0;
			foreach (int id in v.Ring) {
				if (tiles[id].IsThin)
					thinCount++;
			}
			if (thinCount == 2)
				candidateVertexIds.Add(v.Id);
		}

		foreach (int vertexId in candidateVertexIds) {
			PenroseVertex vertex = vertices[vertexId];
			if (vertex.Ring.Count != 3)
				continue; // an earlier flip in this loop could have changed which tiles touch it
			var members = new List<PenroseTile>();
			foreach (int id in vertex.Ring)
				members.Add(tiles[id]);

			var savedCorners = new Vector2[3][];
			var savedIsThin = new bool[3];
			var savedIds = new int[3][];
			for (int i = 0; i < 3; i++) {
				savedCorners[i] = members[i].Corners;
				savedIsThin[i] = members[i].IsThin;
				savedIds[i] = members[i].CornerVertexIds;
			}

			int before = TotalProblems(tiles, edgeLength);
			if (!TryFlipCircleTriplet(tiles, vertices, vertexId))
				continue;
			int after = TotalProblems(tiles, edgeLength);
			if (after >= before) {
				for (int i = 0; i < 3; i++) {
					members[i].Corners = savedCorners[i];
					members[i].IsThin = savedIsThin[i];
					members[i].CornerVertexIds = savedIds[i];
				}
			} else {
				flippedCount++;
			}
		}
		return flippedCount;
	}

	// Manual, user-triggered Penrose View debug button (Main.cs) - "tries
	// every single 180-degree rotation" - the same relabel as
	// Main.FlipPenroseTileAt (which corner index a tile calls "0"), tried
	// on EVERY tile on the board, one at a time, kept only when it provably
	// reduces the total problem count. Broader than EnsureCircleConsistency/
	// EnsureStarConsistency above: it doesn't require a tile to be part of a
	// recognized circle or star, so it can find fixes neither of those local
	// patterns cover - at the cost of being a much bigger brute-force search
	// (every tile, not just candidates), so it's exposed as an explicit
	// manual action rather than run automatically on every generation.
	// Returns how many tiles were kept flipped.
	public static int TryFlipAllSingleTiles(List<PenroseTile> tiles, float edgeLength) {
		int flippedCount = 0;
		foreach (PenroseTile t in tiles) {
			Vector2[] savedCorners = t.Corners;
			int[] savedIds = t.CornerVertexIds;
			int before = TotalProblems(tiles, edgeLength);
			Vector2[] c = t.Corners;
			t.Corners = new[] { c[2], c[3], c[0], c[1] };
			int[] ids = t.CornerVertexIds;
			t.CornerVertexIds = new[] { ids[2], ids[3], ids[0], ids[1] };
			int after = TotalProblems(tiles, edgeLength);
			if (after < before) {
				flippedCount++;
			} else {
				t.Corners = savedCorners;
				t.CornerVertexIds = savedIds;
			}
		}
		return flippedCount;
	}

	// A "star" vertex: exactly 5 THICK tiles meeting at one point - 5*72=360
	// is the only way 5 same-kind corners can close a vertex at all (the same
	// necessity argument as the circle triplet's 2*144+72=360). Tries
	// flipping EVERY member of the star together in one move ("inside out").
	// That can't change color agreement AT this vertex itself (all 5 labels
	// just swap blue<->red at once, so whatever agreement/disagreement
	// pattern existed stays exactly as it was) - but each of these 5 tiles
	// has 3 OTHER corners touching other vertices elsewhere in the patch,
	// and flipping all 5 together changes every one of those simultaneously.
	// Kept only when it provably reduces the total problem count, exactly
	// like TryFlipCircleTriplet above.
	//
	// This is RepairLocalCircles' automatic star-repair step, and is also
	// exposed publicly as the "Flip All Stars" Penrose View debug button
	// (Main.cs) - "tries every 5-star inside-out flip". An earlier version
	// only flipped whichever members were in the minority (unconditionally
	// safe at that one vertex, since 3 tiles always have a majority), but
	// measured worse overall across real patches - flipping the whole star
	// together, though a less "obviously correct" move, finds more real
	// improvement in practice. Returns how many stars were flipped.
	public static int TryFlipAllStarsInsideOut(List<PenroseTile> tiles, List<PenroseVertex> vertices, float edgeLength) {
		int flippedCount = 0;
		foreach (PenroseVertex vertex in vertices) {
			if (vertex.Ring.Count != 5)
				continue;
			var members = new List<PenroseTile>();
			foreach (int id in vertex.Ring)
				members.Add(tiles[id]);
			if (members.Exists(t => t.IsThin))
				continue;

			var savedCorners = new Vector2[5][];
			var savedIds = new int[5][];
			for (int i = 0; i < 5; i++) {
				savedCorners[i] = members[i].Corners;
				savedIds[i] = members[i].CornerVertexIds;
			}

			int before = TotalProblems(tiles, edgeLength);
			foreach (PenroseTile t in members) {
				Vector2[] c = t.Corners;
				t.Corners = new[] { c[2], c[3], c[0], c[1] };
				int[] ids = t.CornerVertexIds;
				t.CornerVertexIds = new[] { ids[2], ids[3], ids[0], ids[1] };
			}
			int after = TotalProblems(tiles, edgeLength);
			if (after < before) {
				flippedCount++;
			} else {
				for (int i = 0; i < 5; i++) {
					members[i].Corners = savedCorners[i];
					members[i].CornerVertexIds = savedIds[i];
				}
			}
		}
		return flippedCount;
	}

	private static int TotalProblems(List<PenroseTile> tiles, float edgeLength) {
		var instances = new List<PenroseTileInstance>(tiles.Count);
		foreach (PenroseTile t in tiles)
			instances.Add(new PenroseTileInstance { Kind = t.IsThin ? TileKind.Thin : TileKind.Thick, Corners = t.Corners });
		return PenroseTileRules.ValidateConnections(instances, logDetails: false) + PenroseTileRules.ValidateArcColors(instances, edgeLength, logDetails: false);
	}

	// For every vertex where exactly 2 thin + 1 thick tile meet (the only
	// vertex composition this count can ever produce, since 2*144+72=360 is
	// the sole way it closes at all - see the corner-angle argument in the
	// comment on RepairLocalCircles), forces the 3 members to agree on the
	// BLUE decorated slot specifically - not just "agree, whichever color
	// wins a majority vote" (an earlier version of this method). The two
	// colors aren't interchangeable here: blue is radius 1/3 edge-length on
	// BOTH a thin tile's corner and a thick tile's corner, so 2 blue-thin +
	// 1 blue-thick arcs are all the same size and trace one genuine circle -
	// but red is 1/3 on thin and 2/3 (big) on thick, so a "red-agreeing"
	// triplet passes ValidateArcColors (no red touches blue) yet is two
	// differently-sized arcs, never a real circle. So even an
	// already-internally-consistent all-red vertex still needs forcing here,
	// unlike the old majority version, which treated blueCount==0 as
	// "already fully consistent" and left it alone.
	//
	// A thin tile has TWO decorated corners (index 1 and 3), and its other
	// one may belong to a DIFFERENT circle elsewhere, so flipping it to fix
	// this vertex can silently break that one - guarded with the same
	// before/after check as TryFlipCircleTriplet and TryFlipAllStarsInsideOut,
	// rather than assumed safe. Measured on real generated patches: forcing
	// blue instead of majority-vote reduced total problems in every test
	// case (never increased it), and drove red/mixed circles to zero in
	// nearly all of them - the rare vertex where the guard still declines
	// (forcing blue there would cost more elsewhere) is left as it was.
	private static void EnsureCircleConsistency(List<PenroseTile> tiles, List<PenroseVertex> vertices, float edgeLength) {
		foreach (PenroseVertex vertex in vertices) {
			if (vertex.Ring.Count != 3)
				continue;
			var members = new List<PenroseTile>();
			foreach (int id in vertex.Ring)
				members.Add(tiles[id]);
			int thinCount = 0;
			foreach (PenroseTile t in members) {
				if (t.IsThin)
					thinCount++;
			}
			if (thinCount != 2)
				continue;

			var slots = new int[3];
			bool ok = true;
			for (int i = 0; i < 3; i++) {
				int ci = Array.IndexOf(members[i].CornerVertexIds, vertex.Id);
				slots[i] = members[i].IsThin
					? (ci == 1 ? 0 : ci == 3 ? 1 : -1)
					: (ci == 0 ? 0 : ci == 2 ? 1 : -1);
				if (slots[i] < 0)
					ok = false;
			}
			if (!ok)
				continue; // shouldn't happen for a genuine such vertex, but never force a corner that isn't actually decorated

			int blueCount = 0;
			foreach (int s in slots) {
				if (s == 0)
					blueCount++;
			}
			if (blueCount == 3)
				continue; // already fully blue

			var toFlip = new List<PenroseTile>();
			for (int i = 0; i < 3; i++) {
				if (slots[i] != 0)
					toFlip.Add(members[i]);
			}

			var savedCorners = new Vector2[toFlip.Count][];
			var savedIds = new int[toFlip.Count][];
			for (int i = 0; i < toFlip.Count; i++) {
				savedCorners[i] = toFlip[i].Corners;
				savedIds[i] = toFlip[i].CornerVertexIds;
			}

			int before = TotalProblems(tiles, edgeLength);
			foreach (PenroseTile t in toFlip) {
				Vector2[] c = t.Corners;
				t.Corners = new[] { c[2], c[3], c[0], c[1] };
				int[] ids = t.CornerVertexIds;
				t.CornerVertexIds = new[] { ids[2], ids[3], ids[0], ids[1] };
			}
			int after = TotalProblems(tiles, edgeLength);
			if (after >= before) {
				for (int i = 0; i < toFlip.Count; i++) {
					toFlip[i].Corners = savedCorners[i];
					toFlip[i].CornerVertexIds = savedIds[i];
				}
			}
		}
	}

	// A "circle" vertex: exactly 2 thin + 1 thick tile meeting at one point,
	// their decorated corners all the SAME color (144+72+144 = 360 degrees,
	// with all 3 arcs the same radius, so the 3 arcs trace one unbroken
	// circle - visually the small solid-color circles you see in Penrose
	// View). The 3 tiles' OUTER corners (the 6 points not equal to the
	// shared vertex) form a fixed hexagon - and that same hexagon has
	// exactly one other valid way to be split into these same 3 rhombi,
	// meeting at a DIFFERENT interior point instead (the classic "hexagon of
	// 3 rhombi" flip also used in lozenge-tiling Markov chains). That's the
	// real operation here: the 3 old tiles are discarded and 3 new ones are
	// built from scratch, using the SAME 6 outer hexagon points (so every
	// edge connection to the surrounding, untouched tiles is preserved
	// exactly) but meeting at the new point instead - never a rigid rotation
	// of the old tiles in place, which would drag their outer edges away
	// from their real neighbors.
	//
	// Shared by Main.RotatePenroseTripletAt (an interactive, one-click-at-a-
	// time version of this same move) and RepairLocalCircles above (which
	// tries it automatically everywhere and keeps only the improvements).
	// Returns false, changing nothing, if `vertexId` isn't a circle triplet
	// at all (wrong ring size, wrong thin/thick count, or its 3 members
	// don't actually agree on color).
	public static bool TryFlipCircleTriplet(List<PenroseTile> tiles, List<PenroseVertex> vertices, int vertexId) {
		PenroseVertex vertex = vertices[vertexId];
		if (vertex.Ring.Count != 3)
			return false;

		var members = new List<PenroseTile>();
		foreach (int id in vertex.Ring)
			members.Add(tiles[id]);
		int thinCount = 0;
		foreach (PenroseTile t in members) {
			if (t.IsThin)
				thinCount++;
		}
		if (thinCount != 2)
			return false;

		// Each member's "slot" at this vertex: 0 for its blue-decorated
		// corner (thin corner 1, thick corner 0), 1 for its red-decorated
		// corner (thin corner 3, thick corner 2). Also records which corner
		// index that is, per member, so the near/far points around the
		// shared vertex can be read off directly.
		var cornerAtVertex = new int[3];
		int firstSlot = -1;
		bool slotsMatch = true;
		for (int i = 0; i < 3; i++) {
			PenroseTile t = members[i];
			int ci = Array.IndexOf(t.CornerVertexIds, vertexId);
			int slot = t.IsThin
				? (ci == 1 ? 0 : ci == 3 ? 1 : -1)
				: (ci == 0 ? 0 : ci == 2 ? 1 : -1);
			if (slot < 0)
				return false;
			cornerAtVertex[i] = ci;
			if (firstSlot < 0)
				firstSlot = slot;
			else if (slot != firstSlot)
				slotsMatch = false;
		}
		if (!slotsMatch)
			return false;

		// far[i] is member i's own outer tip (opposite the shared vertex) -
		// unique per tile, and exactly what the new meeting point connects
		// to; farId[i] is its already-real vertex id, carried through
		// unchanged (the point itself doesn't move). near1/near2 are its two
		// immediate outer neighbors - each shared with one OTHER member, so
		// collecting them across all 3 tiles and deduping by position gives
		// exactly the 3 distinct near points (with their own real vertex
		// ids) - these become the new tiles' own far tips, so their ids are
		// needed too.
		var far = new Vector2[3];
		var farId = new int[3];
		var nearPoints = new List<(Vector2 Pos, int Id)>();
		Vector2 nearSum = Vector2.Zero;
		for (int i = 0; i < 3; i++) {
			int ci = cornerAtVertex[i];
			PenroseTile t = members[i];
			far[i] = t.Corners[(ci + 2) % 4];
			farId[i] = t.CornerVertexIds[(ci + 2) % 4];
			Vector2 n1 = t.Corners[(ci + 1) % 4], n2 = t.Corners[(ci + 3) % 4];
			int n1Id = t.CornerVertexIds[(ci + 1) % 4], n2Id = t.CornerVertexIds[(ci + 3) % 4];
			nearSum += n1 + n2;
			if (!ContainsId(nearPoints, n1Id))
				nearPoints.Add((n1, n1Id));
			if (!ContainsId(nearPoints, n2Id))
				nearPoints.Add((n2, n2Id));
		}
		Vector2 oldCenter = vertex.Position;
		Vector2 newCenter = nearSum / 2.0f - 2.0f * oldCenter;

		// The hexagon of 3 rhombi has exactly one other decomposition,
		// meeting at newCenter instead: the new tile spanning (far[i],
		// far[i+1]) takes the KIND of the THIRD member (whose own far
		// corner isn't part of that pair) - see the derivation above. Each
		// new tile's own far tip is one of the 3 original near points
		// (matched by position - BuildTripletReplacement can't know its
		// vertex id yet, so it marks it -1 for this to resolve).
		var replacements = new PenroseTile[3];
		for (int i = 0; i < 3; i++) {
			bool kind = members[(i + 2) % 3].IsThin;
			replacements[i] = BuildTripletReplacement(newCenter, vertexId, far[i], farId[i], far[(i + 1) % 3], farId[(i + 1) % 3], kind, firstSlot);
			int unresolvedIndex = Array.IndexOf(replacements[i].CornerVertexIds, -1);
			Vector2 newFarTip = replacements[i].Corners[unresolvedIndex];
			(Vector2 Pos, int Id) best = nearPoints[0];
			float bestDist = (best.Pos - newFarTip).Length();
			foreach ((Vector2 Pos, int Id) p in nearPoints) {
				float d = (p.Pos - newFarTip).Length();
				if (d < bestDist) {
					bestDist = d;
					best = p;
				}
			}
			replacements[i].CornerVertexIds[unresolvedIndex] = best.Id;
		}
		for (int i = 0; i < 3; i++) {
			members[i].IsThin = replacements[i].IsThin;
			members[i].Corners = replacements[i].Corners;
			members[i].Center = replacements[i].Center;
			members[i].CornerVertexIds = replacements[i].CornerVertexIds;
			members[i].VertexIds = new List<int>(replacements[i].CornerVertexIds);
		}

		// The shared vertex just relocated to newCenter (same 3 tiles still
		// meet there, just reshuffled). Each of the 3 outer vertices may now
		// be touched by a DIFFERENT one of these same 3 tile ids than before
		// (or the same one) - drop all 3 triplet ids from its ring and
		// re-add whichever ones actually still touch it, so a later
		// detection's Ring.Count/membership check reflects reality instead
		// of the pre-flip layout.
		vertex.Position = newCenter;
		var tripletIds = new HashSet<int> { members[0].Id, members[1].Id, members[2].Id };
		foreach ((Vector2 Pos, int Id) p in nearPoints) {
			PenroseVertex outer = vertices[p.Id];
			outer.Ring.RemoveAll(id => tripletIds.Contains(id));
			foreach (PenroseTile t in members) {
				if (Array.IndexOf(t.CornerVertexIds, p.Id) >= 0 && !outer.Ring.Contains(t.Id))
					outer.Ring.Add(t.Id);
			}
		}

		return true;
	}

	private static bool ContainsId(List<(Vector2 Pos, int Id)> points, int id) {
		foreach ((Vector2 Pos, int Id) p in points) {
			if (p.Id == id)
				return true;
		}
		return false;
	}

	// vertexIds[0..2] are the known existing vertex ids for shared/n1/n2 (the
	// 4th corner - this tile's own new far tip - is one of the ORIGINAL
	// near points and gets its vertex id resolved by the caller, since it
	// isn't known until all 3 replacements' shapes are computed). Carrying
	// vertex ids through the same reorder/relabel steps as the positions is
	// what lets a later detection re-find this tile correctly.
	private static PenroseTile BuildTripletReplacement(Vector2 shared, int sharedId, Vector2 n1, int n1Id, Vector2 n2, int n2Id, bool isThin, int slot) {
		Vector2 far = n1 + n2 - shared;
		Vector2[] candidate = { shared, n1, far, n2 };
		int[] candidateIds = { sharedId, n1Id, -1, n2Id };
		float area = 0.0f;
		for (int i = 0; i < 4; i++)
			area += candidate[i].X * candidate[(i + 1) % 4].Y - candidate[(i + 1) % 4].X * candidate[i].Y;
		bool ccwAsIs = area > 0.0f;
		Vector2[] ccw = ccwAsIs ? candidate : new[] { shared, n2, far, n1 };
		int[] ccwIds = ccwAsIs ? candidateIds : new[] { sharedId, n2Id, -1, n1Id };

		int targetIndex = isThin ? (slot == 0 ? 1 : 3) : (slot == 0 ? 0 : 2);
		var corners = new Vector2[4];
		var cornerVertexIds = new int[4];
		for (int k = 0; k < 4; k++) {
			int src = ((k - targetIndex) % 4 + 4) % 4;
			corners[k] = ccw[src];
			cornerVertexIds[k] = ccwIds[src];
		}

		return new PenroseTile {
			IsThin = isThin,
			Corners = corners,
			Center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4.0f,
			CornerVertexIds = cornerVertexIds, // index of the unresolved far corner (-1) stays fixed by this same shift, so the caller can find it back
		};
	}

	// Finds every real shared edge (by matching corner positions directly,
	// since CornerVertexIds/BuildAdjacency haven't run yet), works out which
	// combination of "flip this tile / flip its neighbor" each one actually
	// requires under PenroseTileRules, and solves the whole patch as one
	// union-find-with-parity problem against a virtual "not flipped" ground
	// node - the same technique used to verify this by hand while
	// investigating the very first orientation contradiction found this way.
	// Some tiles have no orientation that satisfies every one of their real
	// neighbors at once (a real limitation of PenroseTileRules' current
	// side-label table, not a bug in this solver); those are simply left as
	// whatever their component's arbitrary root orientation is, and the
	// leftover damage is reported via ValidateConnections/ValidateArcColors
	// on the final, flipped result rather than silently ignored.
	private static void ResolveOrientations(List<PenroseTile> tiles, float edgeLength, out int connectionProblems, out int colorProblems) {
		int n = tiles.Count;
		TileKind KindOf(int t) => tiles[t].IsThin ? TileKind.Thin : TileKind.Thick;
		SideLabel FlippedLabel(int t, int edgeIndex) => PenroseTileRules.SideLabels(KindOf(t))[(edgeIndex + 2) % 4];
		(long, long) Key(Vector2 p) => ((long)Mathf.Round(p.X * 1000.0f), (long)Mathf.Round(p.Y * 1000.0f));

		var edgesByPosition = new Dictionary<((long, long), (long, long)), List<(int Tile, int Edge)>>();
		for (int t = 0; t < n; t++) {
			Vector2[] c = tiles[t].Corners;
			for (int e = 0; e < 4; e++) {
				(long, long) ka = Key(c[e]);
				(long, long) kb = Key(c[(e + 1) % 4]);
				var edgeKey = Compare(ka, kb) <= 0 ? (ka, kb) : (kb, ka);
				if (!edgesByPosition.TryGetValue(edgeKey, out List<(int, int)> list)) {
					list = new List<(int, int)>();
					edgesByPosition[edgeKey] = list;
				}
				list.Add((t, e));
			}
		}

		// Union-find with parity: parent/parity[t] for t in [0,n) are real
		// tiles, index n is the virtual "not flipped" ground node.
		var parent = new int[n + 1];
		var parity = new int[n + 1];
		for (int i = 0; i <= n; i++)
			parent[i] = i;
		int groundNode = n;

		int Find(int x, ref int par) {
			if (parent[x] == x) {
				par = 0;
				return x;
			}
			int p = 0;
			int root = Find(parent[x], ref p);
			int total = parity[x] ^ p;
			parent[x] = root;
			parity[x] = total;
			par = total;
			return root;
		}
		void Union(int a, int b, int requiredXor) {
			int pa = 0, pb = 0;
			int ra = Find(a, ref pa);
			int rb = Find(b, ref pb);
			if (ra == rb)
				return; // if this contradicts an earlier constraint, that's exactly the "no single orientation works" case - leave the earlier one standing
			parent[ra] = rb;
			parity[ra] = pa ^ pb ^ requiredXor;
		}

		foreach (List<(int Tile, int Edge)> pair in edgesByPosition.Values) {
			if (pair.Count != 2)
				continue; // boundary edge
			(int Tile, int Edge) a = pair[0];
			(int Tile, int Edge) b = pair[1];
			SideLabel a0 = PenroseTileRules.SideLabels(KindOf(a.Tile))[a.Edge], a1 = FlippedLabel(a.Tile, a.Edge);
			SideLabel b0 = PenroseTileRules.SideLabels(KindOf(b.Tile))[b.Edge], b1 = FlippedLabel(b.Tile, b.Edge);
			bool ff = PenroseTileRules.IsValidConnection(a0, b0);
			bool ft = PenroseTileRules.IsValidConnection(a0, b1);
			bool tf = PenroseTileRules.IsValidConnection(a1, b0);
			bool tt = PenroseTileRules.IsValidConnection(a1, b1);
			bool matchOk = ff && tt && !ft && !tf;
			bool differOk = ft && tf && !ff && !tt;
			int validCount = (ff ? 1 : 0) + (ft ? 1 : 0) + (tf ? 1 : 0) + (tt ? 1 : 0);
			if (matchOk)
				Union(a.Tile, b.Tile, 0);
			else if (differOk)
				Union(a.Tile, b.Tile, 1);
			else if (validCount == 1) {
				// Only one of the 4 (flip-a, flip-b) combinations is valid -
				// that pins down both tiles' orientation absolutely, not just
				// relative to each other.
				Union(a.Tile, groundNode, (tf || tt) ? 1 : 0);
				Union(b.Tile, groundNode, (ft || tt) ? 1 : 0);
			}
			// validCount 0 (this connection is never valid, regardless of
			// orientation) or > 2 (too permissive to constrain anything)
			// contribute no constraint either way.
		}

		var flipParity = new int[n];
		for (int t = 0; t < n; t++) {
			int p = 0;
			Find(t, ref p);
			flipParity[t] = p;
		}

		// Ground's own true/false meaning is decided arbitrarily by whichever
		// absolute (validCount==1) edge the union-find happens to reach
		// first, which depends on Dictionary enumeration order - an
		// implementation detail, not a real choice. Because a real patch
		// usually collapses into one giant, almost-entirely-pinned
		// component (confirmed: 39 of 40 "worms" fully pinned on a 359-tile
		// patch), getting that single coin flip wrong doesn't cause a few
		// scattered mistakes - it flips nearly the WHOLE patch's orientation
		// at once. Concretely: at radius 600 this used to land on the
		// wrong side and leave 868 connection problems; the other polarity
		// has only 147. So both are tried for real, against
		// PenroseTileRules.ValidateConnections itself (not just this
		// solver's own partial edge classification), and whichever
		// genuinely has fewer problems is kept - rather than trusting
		// whichever one the union-find happened to reach first.
		List<PenroseTileInstance> ToInstances(bool invert) {
			var list = new List<PenroseTileInstance>(n);
			for (int t = 0; t < n; t++) {
				bool flip = (flipParity[t] != 0) != invert;
				Vector2[] c = tiles[t].Corners;
				list.Add(new PenroseTileInstance {
					Kind = tiles[t].IsThin ? TileKind.Thin : TileKind.Thick,
					Corners = flip ? new[] { c[2], c[3], c[0], c[1] } : c,
				});
			}
			return list;
		}
		int asIsProblems = PenroseTileRules.ValidateConnections(ToInstances(false), logDetails: false);
		int invertedProblems = PenroseTileRules.ValidateConnections(ToInstances(true), logDetails: false);
		bool useInverted = invertedProblems < asIsProblems;

		for (int t = 0; t < n; t++) {
			bool flip = (flipParity[t] != 0) != useInverted;
			if (flip) {
				Vector2[] c = tiles[t].Corners;
				tiles[t].Corners = new[] { c[2], c[3], c[0], c[1] };
			}
		}

		var instances = new List<PenroseTileInstance>(n);
		foreach (PenroseTile tile in tiles)
			instances.Add(new PenroseTileInstance { Kind = tile.IsThin ? TileKind.Thin : TileKind.Thick, Corners = tile.Corners });
		connectionProblems = PenroseTileRules.ValidateConnections(instances, logDetails: false);
		colorProblems = PenroseTileRules.ValidateArcColors(instances, edgeLength, logDetails: false);
	}

	private static int Compare((long, long) a, (long, long) b) {
		int cmp = a.Item1.CompareTo(b.Item1);
		return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
	}

	// Derives edge-adjacency and vertex rings purely from the already-computed
	// corner positions (snapped into shared keys) rather than reasoning about
	// which original grid line "owns" which edge direction - the geometry alone
	// determines both unambiguously. This is also exactly what Validate() below
	// checks: every edge should end up shared by precisely one neighbor (or
	// none, at the patch rim), and every interior vertex's ring should sum to
	// 360 degrees of tile angles.
	public static List<PenroseVertex> BuildAdjacency(List<PenroseTile> tiles) {
		var vertexOf = new Dictionary<Vector2I, int>();
		var vertices = new List<PenroseVertex>();

		int KeyOf(Vector2 p) {
			var key = new Vector2I(Mathf.RoundToInt(p.X * 100.0f), Mathf.RoundToInt(p.Y * 100.0f));
			if (!vertexOf.TryGetValue(key, out int id)) {
				id = vertices.Count;
				vertexOf[key] = id;
				vertices.Add(new PenroseVertex { Id = id, Position = p });
			}
			return id;
		}

		// Tiles are reused across repeated calls (Generate() calls this again
		// after RepairLocalCircles, and the interactive click handlers in
		// Main.cs call it again after every manual flip), but EdgeNeighborIds/
		// VertexIds only ever grew via Add - never cleared - so a second call
		// left every previous call's ids sitting alongside the fresh ones.
		// Since ids are just small list indices, a stale one from a superseded
		// vertex list could coincidentally collide with an unrelated real
		// vertex in the new list, and iterating a tile's VertexIds would then
		// silently reach into the wrong part of the board. Clearing first
		// makes this function idempotent no matter how many times it's called
		// on the same tiles.
		foreach (PenroseTile tile in tiles) {
			tile.EdgeNeighborIds.Clear();
			tile.VertexIds.Clear();
		}

		var edgeOwners = new Dictionary<(int, int), List<int>>();
		foreach (PenroseTile tile in tiles) {
			var cornerVertexIds = new int[4];
			for (int i = 0; i < 4; i++)
				cornerVertexIds[i] = KeyOf(tile.Corners[i]);
			tile.CornerVertexIds = cornerVertexIds;
			foreach (int vId in cornerVertexIds) {
				if (!tile.VertexIds.Contains(vId))
					tile.VertexIds.Add(vId);
				PenroseVertex vertex = vertices[vId];
				if (!vertex.Ring.Contains(tile.Id))
					vertex.Ring.Add(tile.Id);
			}
			for (int i = 0; i < 4; i++) {
				int a = cornerVertexIds[i];
				int b = cornerVertexIds[(i + 1) % 4];
				(int, int) edgeKey = a < b ? (a, b) : (b, a);
				if (!edgeOwners.TryGetValue(edgeKey, out List<int> owners)) {
					owners = new List<int>();
					edgeOwners[edgeKey] = owners;
				}
				owners.Add(tile.Id);
			}
		}

		foreach (List<int> owners in edgeOwners.Values) {
			if (owners.Count != 2)
				continue;
			PenroseTile a = tiles[owners[0]];
			PenroseTile b = tiles[owners[1]];
			if (!a.EdgeNeighborIds.Contains(b.Id))
				a.EdgeNeighborIds.Add(b.Id);
			if (!b.EdgeNeighborIds.Contains(a.Id))
				b.EdgeNeighborIds.Add(a.Id);
		}

		foreach (PenroseVertex vertex in vertices) {
			Vector2 v = vertex.Position;
			vertex.Ring.Sort((idA, idB) => {
				Vector2 ca = tiles[idA].Center - v;
				Vector2 cb = tiles[idB].Center - v;
				return Mathf.Atan2(ca.Y, ca.X).CompareTo(Mathf.Atan2(cb.Y, cb.X));
			});
		}

		ColorVertices(vertices, edgeOwners.Keys);

		foreach (PenroseTile tile in tiles) {
			for (int i = 0; i < 4; i++)
				tile.CornerColorA[i] = vertices[tile.CornerVertexIds[i]].ColorA;
		}

		return vertices;
	}

	// 2-colors the vertex adjacency graph (see PenroseVertex.ColorA) via a
	// plain BFS per connected component - a genuine Penrose tiling's vertices
	// always admit a proper 2-coloring here (verified independently: zero
	// contradictions across a 350-tile patch), so any contradiction found
	// during Validate() is a real generation defect, not an expected outcome.
	private static void ColorVertices(List<PenroseVertex> vertices, IEnumerable<(int, int)> edges) {
		var adjacency = new Dictionary<int, List<int>>();
		void AddAdjacency(int a, int b) {
			if (!adjacency.TryGetValue(a, out List<int> list)) {
				list = new List<int>();
				adjacency[a] = list;
			}
			list.Add(b);
		}
		foreach ((int a, int b) in edges) {
			AddAdjacency(a, b);
			AddAdjacency(b, a);
		}

		var colored = new bool[vertices.Count];
		foreach (PenroseVertex start in vertices) {
			if (colored[start.Id])
				continue;
			start.ColorA = true;
			colored[start.Id] = true;
			var queue = new Queue<int>();
			queue.Enqueue(start.Id);
			while (queue.Count > 0) {
				int current = queue.Dequeue();
				bool nextColor = !vertices[current].ColorA;
				if (!adjacency.TryGetValue(current, out List<int> neighbors))
					continue;
				foreach (int neighbor in neighbors) {
					if (colored[neighbor])
						continue;
					vertices[neighbor].ColorA = nextColor;
					colored[neighbor] = true;
					queue.Enqueue(neighbor);
				}
			}
		}
	}

	// The real interior angle at Corners[ci] of a tile, measured directly from
	// the actual corner geometry (the angle between the rays to its two
	// neighboring corners) rather than assumed from IsThin - a tile can
	// present either its acute or its obtuse corner at a given vertex, so an
	// assumption-based sum can never actually detect a gap or overlap (see
	// the comment on the old version of this method, replaced below).
	private static double CornerAngleDegrees(PenroseTile tile, int cornerIndex) {
		Vector2 c = tile.Corners[cornerIndex];
		Vector2 prev = tile.Corners[(cornerIndex + 3) % 4];
		Vector2 next = tile.Corners[(cornerIndex + 1) % 4];
		Vector2 u = prev - c;
		Vector2 w = next - c;
		double dot = u.X * w.X + u.Y * w.Y;
		double lu = Math.Sqrt(u.X * u.X + u.Y * u.Y);
		double lw = Math.Sqrt(w.X * w.X + w.Y * w.Y);
		double cos = Math.Max(-1.0, Math.Min(1.0, dot / (lu * lw)));
		return Math.Acos(cos) * 180.0 / Math.PI;
	}

	// Debug-only sanity check on a generated patch: for every vertex, is it a
	// genuinely closed interior fan (every edge reaching it shared by exactly
	// 2 tiles) or a partial boundary wedge (at least one open edge) - and does
	// its real surrounding angle sum match what that implies (exactly 360 for
	// a closed fan, strictly less for a boundary wedge)? A mismatch means a
	// real gap or overlap in the tiling at that point. Also flags any closed
	// vertex with more than 7 tiles in its ring - a genuine Penrose vertex
	// never exceeds that (its 8 legal configurations top out there); a bigger
	// ring means the pentagrid's phase offsets have a hidden coincidence
	// forcing 3+ lines through one point (see PenroseGenerator.Gamma for a
	// real example of this).
	//
	// Returns the number of problems found (0 = clean); logs each one via
	// GD.PushWarning rather than throwing, since this is a startup diagnostic,
	// not a runtime guard.
	public static int Validate(List<PenroseTile> tiles, List<PenroseVertex> vertices) {
		int problems = 0;

		// Edge ownership keyed by the pair of vertex ids it connects - the
		// same keying BuildAdjacency used to derive EdgeNeighborIds, rebuilt
		// here so a specific edge (not just "some pair of tiles that happen to
		// be neighbors somewhere") can be checked at each vertex.
		var edgeOwners = new Dictionary<(int, int), int>();
		foreach (PenroseTile tile in tiles) {
			for (int i = 0; i < 4; i++) {
				int a = tile.CornerVertexIds[i];
				int b = tile.CornerVertexIds[(i + 1) % 4];
				(int, int) key = a < b ? (a, b) : (b, a);
				edgeOwners.TryGetValue(key, out int count);
				edgeOwners[key] = count + 1;
			}
		}
		foreach (KeyValuePair<(int, int), int> entry in edgeOwners) {
			if (entry.Value > 2) {
				problems++;
				GD.PushWarning($"PenroseTiling.Validate: edge between vertices {entry.Key.Item1}/{entry.Key.Item2} is shared by {entry.Value} tiles (expected at most 2) - tiles overlap.");
			}
			// A tile edge should never join two same-colored vertices - see
			// PenroseVertex.ColorA. A contradiction here means the tiling
			// doesn't actually admit the real Penrose vertex bipartition, a
			// deeper defect than mere geometric gaps/overlaps.
			if (vertices[entry.Key.Item1].ColorA == vertices[entry.Key.Item2].ColorA) {
				problems++;
				GD.PushWarning($"PenroseTiling.Validate: vertices {entry.Key.Item1}/{entry.Key.Item2} share a tile edge but have the same color - not a valid Penrose vertex bipartition.");
			}
		}

		foreach (PenroseVertex vertex in vertices) {
			bool closed = true;
			double sum = 0.0;
			foreach (int tileId in vertex.Ring) {
				PenroseTile tile = tiles[tileId];
				int ci = Array.IndexOf(tile.CornerVertexIds, vertex.Id);
				if (ci < 0) {
					problems++;
					GD.PushWarning($"PenroseTiling.Validate: vertex {vertex.Id}'s ring lists tile {tileId}, but that tile has no corner at this vertex.");
					continue;
				}
				sum += CornerAngleDegrees(tile, ci);
				int prevVertex = tile.CornerVertexIds[(ci + 3) % 4];
				int nextVertex = tile.CornerVertexIds[(ci + 1) % 4];
				foreach (int neighborVertex in new[] { prevVertex, nextVertex }) {
					(int, int) key = neighborVertex < vertex.Id ? (neighborVertex, vertex.Id) : (vertex.Id, neighborVertex);
					if (!edgeOwners.TryGetValue(key, out int count) || count != 2)
						closed = false;
				}
			}
			if (closed && Math.Abs(sum - 360.0) > 0.5) {
				problems++;
				GD.PushWarning($"PenroseTiling.Validate: closed vertex at ({vertex.Position.X:F1},{vertex.Position.Y:F1}) has {vertex.Ring.Count} tiles summing to {sum:F1} degrees (expected 360) - a real gap or overlap.");
			} else if (!closed && sum > 360.5) {
				problems++;
				GD.PushWarning($"PenroseTiling.Validate: boundary vertex at ({vertex.Position.X:F1},{vertex.Position.Y:F1}) is not fully edge-closed but its tiles already sum to {sum:F1} degrees - tiles overlap without tiling correctly.");
			} else if (closed && vertex.Ring.Count > 7) {
				problems++;
				GD.PushWarning($"PenroseTiling.Validate: closed vertex at ({vertex.Position.X:F1},{vertex.Position.Y:F1}) has {vertex.Ring.Count} tiles - a genuine Penrose vertex never exceeds 7.");
			}
		}

		if (problems > 0)
			GD.PushWarning($"PenroseTiling.Validate: {problems} problem(s) found in generated patch.");
		return problems;
	}
}
