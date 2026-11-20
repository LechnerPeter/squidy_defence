using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Which aperiodic tiling a run is played on - picked on the title screen and kept
// on GameManager.Tiling (a loaded grid brings its own - see GridFile).
public enum TilingKind {
	Penrose, // Penrose P3: thin and thick rhombi (see PenroseTiling)
	Hat,     // the "hat" aperiodic monotile, or "einstein" (see HatTiling)
}

public static class TilingKindExtensions {
	// What the game calls each mode, on the title screen and in game.
	public static string DisplayName(this TilingKind tiling) => tiling == TilingKind.Hat ? "Einstein" : "Penrose";
}

// One tile of the board - a Penrose rhombus or a hat - identified by a stable Id (its
// index in BoardGrid.Tiles), not a lattice coordinate, since neither tiling sits on a
// simple 2-integer grid the way hex cells do.
public sealed class BoardTile {
	public int Id;
	public bool IsThin;                                   // Penrose only: thin (36/144) vs thick (72/108) rhombus
	public bool IsMirrored;                               // Hat only: a reflected hat - about 1 in 8, and never two side by side
	public HatLabel HatLabel;                             // Hat only: which metatile placed it - the paper's colour key (see HatGenerator)
	public Vector2 Center;
	public Vector2[] Corners;                             // world-space vertices, boundary order, positive winding: 4 for a rhombus, 14 for a hat
	public List<int> EdgeNeighborIds = new List<int>();   // tiles sharing at least one full edge (fewer at the patch rim)
	public List<int> VertexIds = new List<int>();         // which BoardVertex ids this tile touches
	public int[] CornerVertexIds = new int[4];            // CornerVertexIds[i] is the vertex id of Corners[i] - lets Validate() walk a specific corner's two edges precisely
	public bool[] CornerColorA = new bool[4];             // Penrose only: CornerColorA[i] mirrors Corners[i]'s vertex's ColorA - see BoardVertex.ColorA
	public int OpenEdgeCount;                             // edges no other tile shares - nonzero exactly along the patch rim
}

// One vertex of the tiling - the "hub" a vertex-cluster merge recipe builds
// around (see GameData.Merges / Main.cs). Unlike a hex grid, where every vertex
// is surrounded by exactly 3 cells, a P3 vertex can have anywhere from 3 to 7
// tiles around it depending on local configuration, and a hat vertex 2 to 4 -
// Ring.Count IS the shape's size now, not a fixed constant.
public sealed class BoardVertex {
	public int Id;
	public Vector2 Position;
	public List<int> Ring = new List<int>();                // tile ids, sorted into cyclic order around this vertex

	// Penrose only. Every Penrose vertex belongs to one of exactly two classes
	// under the documented vertex-type bipartition ("Star-Queen-King-Ace" vs
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

// The tiling-independent half of building a board's adjacency - the part every
// gameplay consumer (pathfinding, merge recipes, rendering) depends on, whatever
// shape the tiles are. PenroseTiling.BuildAdjacency adds its Penrose-only vertex
// coloring on top.
public static class BoardTopology {
	// Derives edge-adjacency and vertex rings purely from the already-computed
	// corner positions (snapped into shared keys) rather than reasoning about
	// which original grid line "owns" which edge direction - the geometry alone
	// determines both unambiguously. This is also exactly what Validate()
	// checks: every edge should end up shared by precisely one neighbor (or
	// none, at the patch rim), and every interior vertex's ring should sum to
	// 360 degrees of tile angles.
	//
	// Only works on boards matched edge to edge - every point where one tile has
	// a corner is a corner of every tile touching it, never the middle of a side.
	// Penrose rhombi always are; a hat needs its 14th, straight-angle corner for it
	// (see HatGenerator.StraightCorner).
	public static List<BoardVertex> Build(List<BoardTile> tiles) {
		var vertexOf = new Dictionary<Vector2I, int>();
		var vertices = new List<BoardVertex>();

		int KeyOf(Vector2 p) {
			var key = new Vector2I(Mathf.RoundToInt(p.X * 100.0f), Mathf.RoundToInt(p.Y * 100.0f));
			if (!vertexOf.TryGetValue(key, out int id)) {
				id = vertices.Count;
				vertexOf[key] = id;
				vertices.Add(new BoardVertex { Id = id, Position = p });
			}
			return id;
		}

		// Tiles are reused across repeated calls (PenroseTiling.Generate calls this
		// again after RepairLocalCircles, and the interactive click handlers in
		// Main.cs call it again after every manual flip), but EdgeNeighborIds/
		// VertexIds only ever grew via Add - never cleared - so a second call
		// left every previous call's ids sitting alongside the fresh ones.
		// Since ids are just small list indices, a stale one from a superseded
		// vertex list could coincidentally collide with an unrelated real
		// vertex in the new list, and iterating a tile's VertexIds would then
		// silently reach into the wrong part of the board. Clearing first
		// makes this function idempotent no matter how many times it's called
		// on the same tiles.
		foreach (BoardTile tile in tiles) {
			tile.EdgeNeighborIds.Clear();
			tile.VertexIds.Clear();
			tile.OpenEdgeCount = 0;
		}

		var edgeOwners = new Dictionary<(int, int), List<int>>();
		foreach (BoardTile tile in tiles) {
			int n = tile.Corners.Length;
			var cornerVertexIds = new int[n];
			for (int i = 0; i < n; i++)
				cornerVertexIds[i] = KeyOf(tile.Corners[i]);
			tile.CornerVertexIds = cornerVertexIds;
			if (tile.CornerColorA.Length != n)
				tile.CornerColorA = new bool[n];
			foreach (int vId in cornerVertexIds) {
				if (!tile.VertexIds.Contains(vId))
					tile.VertexIds.Add(vId);
				BoardVertex vertex = vertices[vId];
				if (!vertex.Ring.Contains(tile.Id))
					vertex.Ring.Add(tile.Id);
			}
			for (int i = 0; i < n; i++) {
				int a = cornerVertexIds[i];
				int b = cornerVertexIds[(i + 1) % n];
				(int, int) edgeKey = a < b ? (a, b) : (b, a);
				if (!edgeOwners.TryGetValue(edgeKey, out List<int> owners)) {
					owners = new List<int>();
					edgeOwners[edgeKey] = owners;
				}
				owners.Add(tile.Id);
			}
		}

		// Two hats can share several edges in a row, so a neighbor is only listed
		// once however many edges it shares.
		foreach (List<int> owners in edgeOwners.Values) {
			if (owners.Count != 2) {
				foreach (int id in owners)
					tiles[id].OpenEdgeCount++;
				continue;
			}
			BoardTile a = tiles[owners[0]];
			BoardTile b = tiles[owners[1]];
			if (!a.EdgeNeighborIds.Contains(b.Id))
				a.EdgeNeighborIds.Add(b.Id);
			if (!b.EdgeNeighborIds.Contains(a.Id))
				b.EdgeNeighborIds.Add(a.Id);
		}

		foreach (BoardVertex vertex in vertices) {
			var angles = new Dictionary<int, float>();
			foreach (int id in vertex.Ring)
				angles[id] = WedgeAngle(tiles[id], vertex.Id);
			vertex.Ring.Sort((idA, idB) => angles[idA].CompareTo(angles[idB]));
		}

		return vertices;
	}

	// The direction, from one of its corners, into the wedge a tile fills there - the
	// bisector of its interior angle. Sorting a vertex's tiles by it puts them in
	// their real order around the vertex. The direction to a tile's center would do
	// for a rhombus, but not for a hat: a non-convex tile's center can lie outside its
	// own wedge at a corner, putting it out of order.
	private static float WedgeAngle(BoardTile tile, int vertexId) {
		int n = tile.Corners.Length;
		int ci = Array.IndexOf(tile.CornerVertexIds, vertexId);
		Vector2 corner = tile.Corners[ci];
		float toNext = (tile.Corners[(ci + 1) % n] - corner).Angle();
		return Mathf.Wrap(toNext + InteriorAngle(tile, ci) / 2.0f, -Mathf.Pi, Mathf.Pi);
	}

	// A tile's real interior angle at a corner, in radians - reflex ones included,
	// which a hat has. With every tile wound the same way (positive signed area),
	// the interior runs from the edge toward the next corner round to the edge toward
	// the previous one, increasing in angle.
	public static float InteriorAngle(BoardTile tile, int cornerIndex) {
		int n = tile.Corners.Length;
		Vector2 corner = tile.Corners[cornerIndex];
		float toNext = (tile.Corners[(cornerIndex + 1) % n] - corner).Angle();
		float toPrev = (tile.Corners[(cornerIndex + n - 1) % n] - corner).Angle();
		return Mathf.PosMod(toPrev - toNext, Mathf.Tau);
	}

	// Debug-only sanity check on a generated patch, the shape-agnostic part of
	// PenroseTiling.Validate: no edge is shared by more than 2 tiles, and every
	// vertex's real surrounding angle is exactly 360 degrees if it's a genuinely
	// closed interior fan (every edge reaching it shared by exactly 2 tiles), and
	// no more than that at a partial boundary wedge. A mismatch means a real gap
	// or overlap in the tiling at that point. Returns the number of problems found
	// (0 = clean), logging each via GD.PushWarning.
	public static int Validate(List<BoardTile> tiles, List<BoardVertex> vertices, string source) {
		int problems = 0;
		var edgeOwners = new Dictionary<(int, int), int>();
		foreach (BoardTile tile in tiles) {
			int n = tile.Corners.Length;
			for (int i = 0; i < n; i++) {
				int a = tile.CornerVertexIds[i];
				int b = tile.CornerVertexIds[(i + 1) % n];
				(int, int) key = a < b ? (a, b) : (b, a);
				edgeOwners.TryGetValue(key, out int count);
				edgeOwners[key] = count + 1;
			}
		}
		foreach (KeyValuePair<(int, int), int> entry in edgeOwners) {
			if (entry.Value > 2) {
				problems++;
				GD.PushWarning($"{source}.Validate: edge between vertices {entry.Key.Item1}/{entry.Key.Item2} is shared by {entry.Value} tiles (expected at most 2) - tiles overlap.");
			}
		}

		foreach (BoardVertex vertex in vertices) {
			bool closed = true;
			double sum = 0.0;
			foreach (int tileId in vertex.Ring) {
				BoardTile tile = tiles[tileId];
				int n = tile.Corners.Length;
				int ci = Array.IndexOf(tile.CornerVertexIds, vertex.Id);
				sum += Mathf.RadToDeg(InteriorAngle(tile, ci));
				foreach (int neighborVertex in new[] { tile.CornerVertexIds[(ci + n - 1) % n], tile.CornerVertexIds[(ci + 1) % n] }) {
					(int, int) key = neighborVertex < vertex.Id ? (neighborVertex, vertex.Id) : (vertex.Id, neighborVertex);
					if (!edgeOwners.TryGetValue(key, out int count) || count != 2)
						closed = false;
				}
			}
			if (closed && Math.Abs(sum - 360.0) > 0.5) {
				problems++;
				GD.PushWarning($"{source}.Validate: closed vertex at ({vertex.Position.X:F1},{vertex.Position.Y:F1}) has {vertex.Ring.Count} tiles summing to {sum:F1} degrees (expected 360) - a real gap or overlap.");
			}
			else if (!closed && sum > 360.5) {
				problems++;
				GD.PushWarning($"{source}.Validate: boundary vertex at ({vertex.Position.X:F1},{vertex.Position.Y:F1}) is not fully edge-closed but its tiles already sum to {sum:F1} degrees - tiles overlap without tiling correctly.");
			}
		}

		if (problems > 0)
			GD.PushWarning($"{source}.Validate: {problems} problem(s) found in generated patch.");
		return problems;
	}
}

// Gameplay-facing wrapper around a generated patch of either tiling (see
// TilingKind) - the tile-based replacement for the old hex MazeGrid. Tiles are
// identified by a stable integer Id (an index into Tiles), not a lattice
// coordinate. Keeps the same logical surface as MazeGrid (IsBlocked/CanPlace/
// Place/Unblock/WouldBlockPath/FindPath[From]/WorldToCell/CellToWorld/Entry/Exit)
// so callers change shape as little as possible - only the coordinate type
// (Vector2I -> int) is different.
public class BoardGrid {
	public TilingKind Tiling { get; }
	public List<BoardTile> Tiles { get; }
	public List<BoardVertex> Vertices { get; private set; }
	public int Entry { get; }
	public int Exit { get; }

	private readonly HashSet<int> _blocked = new HashSet<int>();

	// Hat boards only - see WorldToCell and Portal.
	private TileLookup _lookup;
	private Dictionary<(int, int), Vector2> _portals;

	private BoardGrid(TilingKind tiling, List<BoardTile> tiles, List<BoardVertex> vertices, int entry, int exit) {
		Tiling = tiling;
		Tiles = tiles;
		Vertices = vertices;
		Entry = entry;
		Exit = exit;
		if (tiling == TilingKind.Hat)
			BuildHatHelpers();
	}

	public static BoardGrid GeneratePenrose(float edgeLength, float radius) {
		// Slice the circular patch off-center from the broader pentagrid
		// pattern instead of from its own perfectly symmetric middle -
		// a random direction each time, at a fixed fraction of the radius so
		// the shift is always clearly visible without ever cutting the
		// slice's far edge outside the pattern this generator can produce.
		float angle = GD.Randf() * Mathf.Tau;
		Vector2 cutoutCenter = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * 0.6f);
		(List<BoardTile> tiles, List<BoardVertex> vertices) = PenroseTiling.Generate(edgeLength, radius, cutoutCenter);
		if (OS.IsDebugBuild())
			PenroseTiling.Validate(tiles, vertices);
		(int entry, int exit) = PickEntryExit(tiles);
		return new BoardGrid(TilingKind.Penrose, tiles, vertices, entry, exit);
	}

	// A disc of hats cut from a random spot of a large supertile, at a random angle -
	// see HatTiling.Generate.
	public static BoardGrid GenerateHat(float hatUnit, float radius) {
		List<BoardTile> tiles = HatTiling.Generate(hatUnit, radius);
		List<BoardVertex> vertices = BoardTopology.Build(tiles);
		if (OS.IsDebugBuild())
			BoardTopology.Validate(tiles, vertices, nameof(HatTiling));
		(int entry, int exit) = PickEntryExit(tiles);
		return new BoardGrid(TilingKind.Hat, tiles, vertices, entry, exit);
	}

	// A previously exported patch (see GridFile.Load) instead of a freshly
	// generated one - the saved entry/exit are kept as-is rather than
	// re-picked, so the board plays exactly as it did when it was saved.
	public static BoardGrid Load(TilingKind tiling, List<BoardTile> tiles, int entry, int exit) {
		List<BoardVertex> vertices = BuildAdjacency(tiling, tiles);
		if (OS.IsDebugBuild()) {
			if (tiling == TilingKind.Penrose)
				PenroseTiling.Validate(tiles, vertices);
			else
				BoardTopology.Validate(tiles, vertices, nameof(HatTiling));
		}
		return new BoardGrid(tiling, tiles, vertices, entry, exit);
	}

	private static List<BoardVertex> BuildAdjacency(TilingKind tiling, List<BoardTile> tiles) =>
		tiling == TilingKind.Penrose ? PenroseTiling.BuildAdjacency(tiles) : BoardTopology.Build(tiles);

	// Opposite ends of the patch by X, mirroring how the old rectangular
	// grid's entry/exit sat at opposite ends of one row.
	private static (int Entry, int Exit) PickEntryExit(List<BoardTile> tiles) {
		List<int> boundary = BoundaryTileIds(tiles).ToList();
		int entry = boundary.OrderBy(id => tiles[id].Center.X).First();
		int exit = boundary.OrderByDescending(id => tiles[id].Center.X).First();
		return (entry, exit);
	}

	// Recomputes EdgeNeighborIds/VertexIds/Ring from the tiles' current corner
	// positions - needed after any interactive edit that moves tile corners
	// or reassigns which vertex a corner belongs to (Main.RotatePenroseTripletAt),
	// since that edit's own incremental bookkeeping only ever needs to be
	// locally consistent for the one vertex it touched, not globally correct.
	// Cheap enough (a few hundred tiles) to just do in full rather than trust
	// partial patching - see PenroseTiling.BuildAdjacency.
	public void RefreshAdjacency() {
		Vertices = BuildAdjacency(Tiling, Tiles);
		if (Tiling == TilingKind.Hat)
			BuildHatHelpers();
	}

	// Tiles with at least one open (missing-neighbor) edge - the patch's rim,
	// analogous to the old hex grid's top/bottom border rows.
	public IEnumerable<int> BoundaryTileIds() => BoundaryTileIds(Tiles);

	private static IEnumerable<int> BoundaryTileIds(List<BoardTile> tiles) {
		for (int i = 0; i < tiles.Count; i++) {
			if (tiles[i].OpenEdgeCount > 0)
				yield return i;
		}
	}

	public bool IsInBounds(int tileId) => tileId >= 0 && tileId < Tiles.Count;

	public bool IsBlocked(int tileId) => _blocked.Contains(tileId);

	public bool CanPlace(int tileId) {
		if (!IsInBounds(tileId))
			return false;
		if (tileId == Entry || tileId == Exit)
			return false;
		return !IsBlocked(tileId);
	}

	public void Place(int tileId) => _blocked.Add(tileId);

	public void Unblock(int tileId) => _blocked.Remove(tileId);

	public bool WouldBlockPath(int tileId, List<int> extraStarts = null) =>
		WouldBlockPathMulti(new List<int> { tileId }, extraStarts);

	public bool WouldBlockPathMulti(List<int> tileIds, List<int> extraStarts = null) {
		foreach (int id in tileIds)
			_blocked.Add(id);
		bool blocks = FindPath().Count == 0;
		if (!blocks && extraStarts != null) {
			foreach (int start in extraStarts) {
				if (FindPathFrom(start).Count == 0) {
					blocks = true;
					break;
				}
			}
		}
		foreach (int id in tileIds)
			_blocked.Remove(id);
		return blocks;
	}

	public List<int> FindPath(HashSet<int> extraPassable = null) => FindPathFrom(Entry, extraPassable);

	// extraPassable lets a caller treat specific normally-blocked tiles (e.g.
	// water, for a swimming enemy) as walkable for this search only - the
	// grid's real blocked set is untouched. Plain unweighted BFS over each
	// tile's real edge-neighbors, identical in shape to MazeGrid's - only the
	// neighbor source changed (a per-tile list instead of 6 fixed directions).
	public List<int> FindPathFrom(int start, HashSet<int> extraPassable = null) {
		var frontier = new List<int> { start };
		var cameFrom = new Dictionary<int, int> { { start, start } };
		while (frontier.Count > 0) {
			int current = frontier[0];
			frontier.RemoveAt(0);
			if (current == Exit)
				break;
			foreach (int next in Tiles[current].EdgeNeighborIds) {
				bool passable = !IsBlocked(next) || (extraPassable != null && extraPassable.Contains(next));
				if (!passable || cameFrom.ContainsKey(next))
					continue;
				cameFrom[next] = current;
				frontier.Add(next);
			}
		}
		if (!cameFrom.ContainsKey(Exit))
			return new List<int>();
		var result = new List<int>();
		int step = Exit;
		while (step != start) {
			result.Add(step);
			step = cameFrom[step];
		}
		result.Add(start);
		result.Reverse();
		return result;
	}

	public Vector2 CellToWorld(int tileId) => Tiles[tileId].Center;

	// On a hat board, where a step between two neighbors' centers should cross:
	// a point on their shared boundary that the straight walk from either center
	// reaches without leaving that tile. Hats aren't convex, so the direct line
	// between two neighbors' centers cuts across a third tile about 1 step in 9;
	// through the portal, never. Null for tiles that aren't neighbors, and on a
	// Penrose board, whose convex rhombi don't need one.
	public Vector2? Portal(int fromTile, int toTile) {
		if (_portals == null)
			return null;
		return _portals.TryGetValue(fromTile < toTile ? (fromTile, toTile) : (toTile, fromTile), out Vector2 portal) ? portal : null;
	}

	// The tile containing pos - by exact point-in-polygon on a hat board, where
	// the nearest center is often a neighbor's (a hat's brim reaches far from its
	// own center). Otherwise, and for a point on no tile at all, the nearest tile
	// by centroid distance - a plain linear scan, more than fast enough at this
	// tile count (a few hundred) for a per-frame mouse-hover lookup. Neither
	// tiling has a closed-form inverse the way hex's cube rounding gave one.
	// Always returns a tile, even for a point outside the patch entirely (never
	// -1) - callers that care whether a point is actually inside the board use
	// CanPlace/IsInBounds on the result.
	public int WorldToCell(Vector2 pos) {
		if (_lookup != null) {
			int hit = _lookup.TileContaining(pos);
			if (hit >= 0)
				return hit;
		}
		int best = 0;
		float bestDist = float.PositiveInfinity;
		for (int i = 0; i < Tiles.Count; i++) {
			float d = Tiles[i].Center.DistanceSquaredTo(pos);
			if (d < bestDist) {
				bestDist = d;
				best = i;
			}
		}
		return best;
	}

	private void BuildHatHelpers() {
		_lookup = new TileLookup(Tiles);
		_portals = BuildPortals(Tiles);
	}

	// For every pair of edge-neighbors, the candidate point on their shared
	// boundary (each shared edge's midpoint and quarter points) with the shortest
	// walk center-portal-center that stays inside both tiles - which on a real hat
	// board exists for every pair. Should one ever not, the longest shared edge's
	// midpoint stands in.
	private static Dictionary<(int, int), Vector2> BuildPortals(List<BoardTile> tiles) {
		var sharedEdges = new Dictionary<(int, int), List<(Vector2 A, Vector2 B)>>();
		var edgeOwner = new Dictionary<(int, int), int>();
		foreach (BoardTile tile in tiles) {
			int n = tile.Corners.Length;
			for (int i = 0; i < n; i++) {
				int va = tile.CornerVertexIds[i];
				int vb = tile.CornerVertexIds[(i + 1) % n];
				(int, int) edgeKey = va < vb ? (va, vb) : (vb, va);
				if (!edgeOwner.TryGetValue(edgeKey, out int other)) {
					edgeOwner[edgeKey] = tile.Id;
					continue;
				}
				(int, int) pair = other < tile.Id ? (other, tile.Id) : (tile.Id, other);
				if (!sharedEdges.TryGetValue(pair, out List<(Vector2, Vector2)> edges)) {
					edges = new List<(Vector2, Vector2)>();
					sharedEdges[pair] = edges;
				}
				edges.Add((tile.Corners[i], tile.Corners[(i + 1) % n]));
			}
		}

		var portals = new Dictionary<(int, int), Vector2>();
		foreach (KeyValuePair<(int, int), List<(Vector2 A, Vector2 B)>> entry in sharedEdges) {
			BoardTile a = tiles[entry.Key.Item1];
			BoardTile b = tiles[entry.Key.Item2];
			Vector2? best = null;
			float bestLength = float.PositiveInfinity;
			foreach ((Vector2 p, Vector2 q) in entry.Value) {
				foreach (float t in new[] { 0.5f, 0.25f, 0.75f }) {
					Vector2 candidate = p.Lerp(q, t);
					float length = a.Center.DistanceTo(candidate) + candidate.DistanceTo(b.Center);
					if (length >= bestLength)
						continue;
					if (PolygonMath.SegmentStaysInside(a.Corners, a.Center, candidate) && PolygonMath.SegmentStaysInside(b.Corners, b.Center, candidate)) {
						best = candidate;
						bestLength = length;
					}
				}
			}
			if (best == null) {
				(Vector2 p, Vector2 q) = entry.Value.OrderByDescending(e => e.A.DistanceSquaredTo(e.B)).First();
				best = p.Lerp(q, 0.5f);
			}
			portals[entry.Key] = best.Value;
		}
		return portals;
	}

	// A uniform bucket grid over the board, so a point-in-tile lookup only tests
	// the handful of tiles whose bounding box covers the point's bucket.
	private sealed class TileLookup {
		private readonly List<BoardTile> _tiles;
		private readonly Vector2 _origin;
		private readonly float _bucketSize;
		private readonly int _columns;
		private readonly int _rows;
		private readonly List<int>[] _buckets;

		public TileLookup(List<BoardTile> tiles) {
			_tiles = tiles;
			Rect2 board = BoundsOf(tiles[0].Corners);
			float largest = 0.0f;
			foreach (BoardTile tile in tiles) {
				Rect2 bounds = BoundsOf(tile.Corners);
				board = board.Merge(bounds);
				largest = Mathf.Max(largest, Mathf.Max(bounds.Size.X, bounds.Size.Y));
			}
			_origin = board.Position;
			_bucketSize = Mathf.Max(largest, 1.0f);
			_columns = Mathf.Max(1, Mathf.CeilToInt(board.Size.X / _bucketSize));
			_rows = Mathf.Max(1, Mathf.CeilToInt(board.Size.Y / _bucketSize));
			_buckets = new List<int>[_columns * _rows];
			foreach (BoardTile tile in tiles) {
				Rect2 bounds = BoundsOf(tile.Corners);
				(int x0, int y0) = BucketOf(bounds.Position);
				(int x1, int y1) = BucketOf(bounds.End);
				for (int y = y0; y <= y1; y++) {
					for (int x = x0; x <= x1; x++)
						(_buckets[y * _columns + x] ??= new List<int>()).Add(tile.Id);
				}
			}
		}

		public int TileContaining(Vector2 pos) {
			Vector2 local = (pos - _origin) / _bucketSize;
			if (local.X < 0.0f || local.Y < 0.0f || local.X >= _columns || local.Y >= _rows)
				return -1;
			List<int> bucket = _buckets[(int)local.Y * _columns + (int)local.X];
			if (bucket == null)
				return -1;
			foreach (int id in bucket) {
				if (PolygonMath.Contains(_tiles[id].Corners, pos))
					return id;
			}
			return -1;
		}

		private (int X, int Y) BucketOf(Vector2 pos) {
			Vector2 local = (pos - _origin) / _bucketSize;
			return (Mathf.Clamp((int)local.X, 0, _columns - 1), Mathf.Clamp((int)local.Y, 0, _rows - 1));
		}

		private static Rect2 BoundsOf(Vector2[] corners) {
			var bounds = new Rect2(corners[0], Vector2.Zero);
			foreach (Vector2 corner in corners)
				bounds = bounds.Expand(corner);
			return bounds;
		}
	}
}
