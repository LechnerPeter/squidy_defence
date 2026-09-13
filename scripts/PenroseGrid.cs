using Godot;
using System.Collections.Generic;
using System.Linq;

// Gameplay-facing wrapper around a generated Penrose P3 patch (see
// PenroseTiling.Generate) - the tile-based replacement for the old hex
// MazeGrid. Tiles are identified by a stable integer Id (an index into Tiles),
// not a lattice coordinate, since Penrose tiles don't sit on a simple
// 2-integer grid the way hex cells do. Keeps the same logical surface as
// MazeGrid (IsBlocked/CanPlace/Place/Unblock/WouldBlockPath/FindPath[From]/
// WorldToCell/CellToWorld/Entry/Exit) so callers change shape as little as
// possible - only the coordinate type (Vector2I -> int) is different.
public class PenroseGrid {
	public List<PenroseTile> Tiles { get; }
	public List<PenroseVertex> Vertices { get; }
	public int Entry { get; }
	public int Exit { get; }

	private readonly HashSet<int> _blocked = new HashSet<int>();

	public PenroseGrid(float edgeLength, float radius) {
		(Tiles, Vertices) = PenroseTiling.Generate(edgeLength, radius);
		if (OS.IsDebugBuild()) {
			PenroseTiling.Validate(Tiles, Vertices);
			PenroseTiling.ValidateMatchingRule(Tiles);
		}
		List<int> boundary = BoundaryTileIds().ToList();
		// Opposite ends of the patch by X, mirroring how the old rectangular
		// grid's entry/exit sat at opposite ends of one row.
		Entry = boundary.OrderBy(id => Tiles[id].Center.X).First();
		Exit = boundary.OrderByDescending(id => Tiles[id].Center.X).First();
	}

	// Tiles with at least one open (missing-neighbor) edge - the patch's rim,
	// analogous to the old hex grid's top/bottom border rows.
	public IEnumerable<int> BoundaryTileIds() {
		for (int i = 0; i < Tiles.Count; i++) {
			if (Tiles[i].EdgeNeighborIds.Count < 4)
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

	// Nearest tile by centroid distance - a plain linear scan, more than fast
	// enough at this tile count (a few hundred) for a per-frame mouse-hover
	// lookup. Penrose tiles have no closed-form inverse the way hex's cube
	// rounding gave one. Always returns the closest tile, even for a point
	// outside the patch entirely (never -1) - callers that care whether a point
	// is actually inside the board use CanPlace/IsInBounds on the result.
	public int WorldToCell(Vector2 pos) {
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
}
