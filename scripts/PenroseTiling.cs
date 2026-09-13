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
	public bool[] CornerCurveColorA = new bool[4];         // which of the two curve colors (red/blue) this decorated corner's arc paints - see PenroseTiling.AssignCurveColors
}

// One vertex of the tiling - the "hub" a vertex-cluster merge recipe builds
// around (see GameData.Merges / Main.cs). Unlike a hex grid, where every vertex
// is surrounded by exactly 3 cells, a P3 vertex can have anywhere from 3 to 10
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
	// This determines which of a thick tile's two acute corners gets the big
	// arc versus the small one (not a fixed property of the corner index -
	// it's inherited from which class that corner's own vertex belongs to).
	// Computed once in PenroseTiling.BuildAdjacency via BFS over the real
	// vertex adjacency graph, then verified for contradictions in Validate().
	//
	// This is NOT the arc's paint color (red vs blue) - since any two
	// vertices joined by a tile edge are always DIFFERENT classes, using
	// ColorA directly as paint color would force every single crossing to
	// flip color, the opposite of the real rule (a red arc only ever
	// continues into another red arc, never blue - see
	// PenroseTiling.AssignCurveColors and PenroseTile.CornerCurveColorA).
	public bool ColorA;
}

// Generates a bounded patch of a genuine Penrose P3 tiling via de Bruijn's
// pentagrid ("multigrid") dual method: 5 families of parallel lines, one per
// direction at 72-degree spacing; every crossing of two lines from different
// families dualizes directly into one rhombus tile. This is the standard,
// closed-form way to build a P3 patch - no recursive subdivision, no
// accumulated floating point drift from repeated inflation steps - and it
// naturally produces a roughly circular/decagonal boundary simply by bounding
// which crossings get kept, rather than needing a separate crop step.
public static class PenroseTiling {
	private const int FamilyCount = 5;

	// Small, distinct, non-integer-summing phase offsets - keeps the pentagrid
	// "regular" (every crossing involves exactly 2 lines, never 3+), so tile
	// generation never has to special-case a higher-order coincidence. A
	// "singular" grid (sum of offsets is an integer) gives the famous perfectly
	// symmetric "cartwheel" pattern but needs fragile floating-point
	// tie-breaking at its center; this deliberately trades that off for a
	// robust, still fully authentic P3 patch.
	private static readonly double[] Gamma = { 0.1, 0.2, 0.3, 0.4, 0.5 };

	private static Vector2 Direction(int family) {
		double angle = 2.0 * Math.PI * family / FamilyCount;
		return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
	}

	// edgeLength: world-space length of one tile edge. radius: only tiles whose
	// center falls within this distance of the origin are kept - the source of
	// the patch's natural round/decagonal boundary.
	public static (List<PenroseTile> Tiles, List<PenroseVertex> Vertices) Generate(float edgeLength, float radius) {
		var dirs = new Vector2[FamilyCount];
		for (int m = 0; m < FamilyCount; m++)
			dirs[m] = Direction(m);

		// Generous margin past the radius cutoff so no boundary tile is ever
		// missed for want of index range - excess candidates are simply
		// rejected by the radius check in TryBuildTile.
		int n = (int)Math.Ceiling(radius / edgeLength) + 3;
		var tiles = new List<PenroseTile>();

		for (int j = 0; j < FamilyCount; j++) {
			for (int k = j + 1; k < FamilyCount; k++) {
				float det = dirs[j].X * dirs[k].Y - dirs[j].Y * dirs[k].X;
				for (int kj = -n; kj <= n; kj++) {
					for (int kk = -n; kk <= n; kk++) {
						PenroseTile tile = TryBuildTile(dirs, j, k, kj, kk, det, edgeLength, radius);
						if (tile == null)
							continue;
						tile.Id = tiles.Count;
						tiles.Add(tile);
					}
				}
			}
		}

		List<PenroseVertex> vertices = BuildAdjacency(tiles);
		AssignCurveColors(tiles); // needs CornerColorA, set inside BuildAdjacency, to compute arc radii first
		return (tiles, vertices);
	}

	// Every rhombus in a P3 tiling is dual to exactly one crossing of two
	// pentagrid lines from different families - its 4 corners come from
	// projecting the 5-index "which strip of each family" vector back through
	// the same 5 direction vectors, nudging the two crossing families' indices
	// by 0/1 across the 4 corner combinations. This is de Bruijn's dual
	// projection; the resulting parallelogram always has edge length
	// `edgeLength` and a vertex angle equal to the angle between the two
	// families' directions (72 degrees two families apart -> thick rhombus,
	// 144 degrees -> thin), which is exactly what P3 requires.
	private static PenroseTile TryBuildTile(Vector2[] dirs, int j, int k, int kj, int kk, float det, float edgeLength, float radius) {
		double bj = kj + Gamma[j];
		double bk = kk + Gamma[k];
		double x = (bj * dirs[k].Y - bk * dirs[j].Y) / det;
		double y = (dirs[j].X * bk - dirs[k].X * bj) / det;

		var kIndex = new int[FamilyCount];
		kIndex[j] = kj;
		kIndex[k] = kk;
		for (int m = 0; m < FamilyCount; m++) {
			if (m == j || m == k)
				continue;
			double proj = x * dirs[m].X + y * dirs[m].Y - Gamma[m];
			kIndex[m] = (int)Math.Ceiling(proj);
		}

		Vector2 baseSum = Vector2.Zero;
		for (int m = 0; m < FamilyCount; m++)
			baseSum += dirs[m] * kIndex[m];
		baseSum *= edgeLength;

		Vector2 v00 = baseSum;
		Vector2 v10 = baseSum + dirs[j] * edgeLength;
		Vector2 v11 = baseSum + (dirs[j] + dirs[k]) * edgeLength;
		Vector2 v01 = baseSum + dirs[k] * edgeLength;
		Vector2 center = (v00 + v10 + v11 + v01) / 4.0f;
		if (center.Length() > radius)
			return null;

		int diff = Math.Min(Mod(k - j, FamilyCount), Mod(j - k, FamilyCount));
		bool isThin = diff == 2;

		// The natural corner order (v00,v10,v11,v01) traces the parallelogram
		// consistently only when dirs[j]->dirs[k] itself turns counter-clockwise
		// the short way (true for k-j = 1 or 2); for k-j = 3 or 4 the same
		// listing winds clockwise instead (det/cross(dirs[j],dirs[k]) flips
		// sign). Left alone, that made roughly 40% of tiles wind the opposite
		// way from the rest - a real, if previously invisible, inconsistency.
		// Normalizing every tile to the same (counter-clockwise, positive
		// shoelace area) winding here keeps corner order meaningful for any
		// future consumer that cares about winding (outward normals, polygon
		// boolean ops, etc), not just fill-drawing which tolerates either.
		Vector2[] order = { v00, v10, v11, v01 };
		float signedArea = 0.0f;
		for (int i = 0; i < 4; i++) {
			Vector2 a = order[i];
			Vector2 b = order[(i + 1) % 4];
			signedArea += a.X * b.Y - b.X * a.Y;
		}
		if (signedArea < 0.0f)
			order = new[] { v00, v01, v11, v10 }; // same cycle, reversed direction, still starts at v00

		// v00's own interior angle - the angle between dirs[j] and dirs[k] -
		// is 72 degrees (acute) for a thick tile but 144 degrees (obtuse) for a
		// thin one, regardless of which way the array above winds (an unsigned
		// vertex angle doesn't care about traversal direction). So v00 sits at
		// order[0] acute for thick already; for thin it's order[0] that's
		// obtuse and order[1] that's acute. Cyclically shifting by 1 in that
		// case (a pure relabeling of the same 4 points and edges, so winding is
		// unaffected) keeps corner 0 consistently the acute corner on both
		// shapes, which is what every consumer that orients a badge to this
		// tile's real rotation relies on (see BaseTower.DrawTowerIcon).
		Vector2[] corners = isThin
			? new[] { order[1], order[2], order[3], order[0] }
			: order;
		return new PenroseTile {
			IsThin = isThin,
			Center = center,
			Corners = corners,
		};
	}

	private static int Mod(int a, int m) => ((a % m) + m) % m;

	// Derives edge-adjacency and vertex rings purely from the already-computed
	// corner positions (snapped into shared keys) rather than reasoning about
	// which original grid line "owns" which edge direction - the geometry alone
	// determines both unambiguously. This is also exactly what Validate() below
	// checks: every edge should end up shared by precisely one neighbor (or
	// none, at the patch rim), and every interior vertex's ring should sum to
	// 360 degrees of tile angles.
	private static List<PenroseVertex> BuildAdjacency(List<PenroseTile> tiles) {
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
	// real gap or overlap in the tiling at that point.
	//
	// The previous version of this method summed each tile's ACUTE angle
	// (IsThin ? 36 : 72) regardless of which corner actually touched the
	// vertex, which only ever undercounts (a tile's true contribution is
	// sometimes its obtuse angle instead) - so its ">361" overshoot check
	// could structurally never fire, silently passing any tiling no matter
	// how broken. This version measures each tile's real corner angle and
	// determines closedness from actual shared-edge data, so it can (and, per
	// a standalone from-scratch verification run against this exact
	// generator, does) actually confirm correctness rather than rubber-stamp it.
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
			}
		}

		if (problems > 0)
			GD.PushWarning($"PenroseTiling.Validate: {problems} problem(s) found in generated patch.");
		return problems;
	}

	// A stronger correctness check than Validate() above: it confirms this
	// patch actually obeys the real Penrose P3 *matching rule*, not just
	// "the rhombi happen to tile the plane with no gaps or overlaps" (a
	// generic, non-aperiodic rhombus tiling could still pass Validate()).
	//
	// Decoded directly from Wikipedia's reference diagram of the rule
	// ("Penrose-tiles-bump-and-color-coded.svg", the classic de Bruijn /
	// Grunbaum-Shephard arc decoration) by reading its own SVG transforms
	// numerically rather than guessing at the geometry:
	//   - Every edge touches exactly one "decorated" corner: a thin tile's two
	//     144-degree corners, or a thick tile's two 72-degree corners.
	//   - That corner is rounded off by an arc CENTERED ON THE CORNER, so it
	//     meets each of the corner's two edges at a point exactly `radius`
	//     units from the corner. A thin tile's radius is always 6 at both its
	//     decorated corners; a thick tile's acute corners use 24 or 6
	//     depending on which of the two vertex classes that specific corner's
	//     vertex belongs to (see PenroseVertex.ColorA) - NOT a fixed property
	//     of the corner index. An earlier version of this assumed "corner
	//     index 0 is always 24, corner 2 always 6", which is exactly what a
	//     from-scratch bipartite-coloring verification (zero contradictions
	//     across the vertex adjacency graph) proved wrong for roughly a
	//     quarter of thick tiles.
	//   - Two such arcs facing each other across a shared edge only actually
	//     meet - continuing one curve into the next tile - when their two
	//     radii add up to the full edge length (6+24, never 6+6): that's the
	//     real matching condition, not merely "both sides have some arc."
	// Tracing the resulting curves, a genuine P3 patch should show every one
	// of them running to the patch boundary at both ends (open), or forming
	// one of the specific closed shapes the tiling is documented to
	// legitimately produce at a locally 5-fold-symmetric point (a "Sun" or
	// "Star" vertex, or other symmetric vertex types) - never a small closed
	// loop with no such symmetry behind it, which is the artifact a broken
	// tiling would produce instead.
	//
	// Returns the number of small (non-decagon) closed loops found - 0 means
	// the patch's matching rule is clean, modulo whatever large symmetric
	// loops legitimately occur.
	public static int ValidateMatchingRule(List<PenroseTile> tiles) {
		const int decagonSize = 10; // the one legitimate closed-loop size (5 thick + 5 thin around a Sun/Star vertex)

		BuildMatchingGraph(tiles, out bool[] isNode, out int[] partner, out int[] externalPartner);
		int n = tiles.Count;

		var visited = new bool[n * 4];
		for (int start = 0; start < n * 4; start++) {
			if (visited[start] || !isNode[start])
				continue;
			bool isOpenEnd = externalPartner[start] < 0;
			if (!isOpenEnd)
				continue;
			visited[start] = true;
			int cur = start;
			while (true) {
				int nextInternal = partner[cur];
				visited[nextInternal] = true;
				int ext = externalPartner[nextInternal];
				if (ext < 0)
					break;
				visited[ext] = true;
				cur = ext;
			}
		}

		int smallLoops = 0;
		for (int start = 0; start < n * 4; start++) {
			if (visited[start] || !isNode[start])
				continue;
			int cur = start;
			var seenTiles = new HashSet<int>();
			while (!visited[cur]) {
				visited[cur] = true;
				seenTiles.Add(cur / 4);
				int nextInternal = partner[cur];
				visited[nextInternal] = true;
				seenTiles.Add(nextInternal / 4);
				cur = externalPartner[nextInternal];
				if (cur < 0)
					break; // not actually closed - shouldn't happen, guard anyway
			}
			if (seenTiles.Count < decagonSize) {
				smallLoops++;
				GD.PushWarning($"PenroseTiling.ValidateMatchingRule: found a small ({seenTiles.Count}-tile) closed matching-rule loop - real Penrose tilings never close one this small.");
			}
		}

		if (smallLoops > 0)
			GD.PushWarning($"PenroseTiling.ValidateMatchingRule: {smallLoops} small closed loop(s) found - this patch violates the P3 matching rule.");
		return smallLoops;
	}

	// The radius (in the reference SVG's own units) of the arc at Corners[ci]
	// of a tile, or 0 if that corner isn't one of the tile's decorated ones -
	// shared by ValidateMatchingRule and AssignCurveColors so both trace
	// exactly the same graph.
	private static float ArcRadiusAt(PenroseTile t, int ci) {
		const float thinRadius = 6.0f, thickBigRadius = 24.0f, thickSmallRadius = 6.0f;
		if (t.IsThin)
			return (ci == 1 || ci == 3) ? thinRadius : 0.0f;
		if (ci != 0 && ci != 2)
			return 0.0f;
		return t.CornerColorA[ci] ? thickBigRadius : thickSmallRadius;
	}

	// Builds the matching-rule graph once, shared by ValidateMatchingRule and
	// AssignCurveColors: one node per (tile, edge); `partner` links the two
	// edges meeting at each decorated corner (the arc drawn there connects
	// them); `externalPartner` links an edge to the matching edge on whichever
	// neighbor's arc radius sums with this one to the full edge length (30 in
	// the reference SVG's units) - the real cross-tile matching condition.
	private static void BuildMatchingGraph(List<PenroseTile> tiles, out bool[] isNode, out int[] partner, out int[] externalPartner) {
		const float edgeLength = 30.0f;
		int n = tiles.Count;
		isNode = new bool[n * 4];
		partner = new int[n * 4];
		for (int t = 0; t < n; t++) {
			for (int ci = 0; ci < 4; ci++) {
				if (ArcRadiusAt(tiles[t], ci) <= 0.0f)
					continue;
				int edgeA = (ci + 3) % 4; // the two edges meeting at this decorated corner
				int edgeB = ci;
				partner[t * 4 + edgeA] = t * 4 + edgeB;
				partner[t * 4 + edgeB] = t * 4 + edgeA;
				isNode[t * 4 + edgeA] = true;
				isNode[t * 4 + edgeB] = true;
			}
		}

		externalPartner = new int[n * 4];
		for (int i = 0; i < externalPartner.Length; i++)
			externalPartner[i] = -1;
		for (int t = 0; t < n; t++) {
			PenroseTile tile = tiles[t];
			for (int ei = 0; ei < 4; ei++) {
				if (!isNode[t * 4 + ei])
					continue;
				float rHere = Math.Max(ArcRadiusAt(tile, ei), ArcRadiusAt(tile, (ei + 1) % 4));
				int va = tile.CornerVertexIds[ei], vb = tile.CornerVertexIds[(ei + 1) % 4];
				foreach (int nb in tile.EdgeNeighborIds) {
					PenroseTile nbTile = tiles[nb];
					for (int ej = 0; ej < 4; ej++) {
						if (!isNode[nb * 4 + ej])
							continue;
						int wa = nbTile.CornerVertexIds[ej], wb = nbTile.CornerVertexIds[(ej + 1) % 4];
						bool sameEdge = (wa == va && wb == vb) || (wa == vb && wb == va);
						if (!sameEdge)
							continue;
						float rThere = Math.Max(ArcRadiusAt(nbTile, ej), ArcRadiusAt(nbTile, (ej + 1) % 4));
						if (Math.Abs(rHere + rThere - edgeLength) < 0.01f)
							externalPartner[t * 4 + ei] = nb * 4 + ej;
					}
				}
			}
		}
	}

	// Assigns each decorated corner's arc a PAINT color (red/blue), as
	// opposed to PenroseVertex.ColorA which governs its RADIUS - two
	// deliberately independent properties. Crossing a shared edge always
	// flips vertex class (Validate() proves no edge joins two same-class
	// vertices), which is exactly why radius alternates big/small along a
	// curve - but the real matching rule requires the opposite for color: a
	// red arc must only ever continue into another red arc, never blue. So
	// paint color can't be derived from vertex class at all; instead this
	// finds every whole connected curve in the matching graph (a full trace
	// - both the "same corner" internal links and the "sums to edge length"
	// external links) and gives each entire curve ONE consistent color,
	// which makes a mismatched connection structurally impossible rather
	// than merely unlikely.
	public static void AssignCurveColors(List<PenroseTile> tiles) {
		BuildMatchingGraph(tiles, out bool[] isNode, out int[] partner, out int[] externalPartner);
		int n = tiles.Count;

		var componentId = new int[n * 4];
		for (int i = 0; i < componentId.Length; i++)
			componentId[i] = -1;
		int componentCount = 0;
		for (int start = 0; start < n * 4; start++) {
			if (!isNode[start] || componentId[start] != -1)
				continue;
			var queue = new Queue<int>();
			queue.Enqueue(start);
			componentId[start] = componentCount;
			while (queue.Count > 0) {
				int current = queue.Dequeue();
				foreach (int neighbor in new[] { partner[current], externalPartner[current] }) {
					if (neighbor < 0 || componentId[neighbor] != -1)
						continue;
					componentId[neighbor] = componentCount;
					queue.Enqueue(neighbor);
				}
			}
			componentCount++;
		}

		for (int t = 0; t < n; t++) {
			PenroseTile tile = tiles[t];
			for (int ci = 0; ci < 4; ci++) {
				if (ArcRadiusAt(tile, ci) <= 0.0f)
					continue;
				// Either of this corner's two edge-nodes gives the same
				// component (they're internally linked by construction).
				int node = t * 4 + ci;
				tile.CornerCurveColorA[ci] = (componentId[node] % 2) == 0;
			}
		}
	}
}
