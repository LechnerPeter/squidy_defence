using Godot;
using System;
using System.Collections.Generic;

// Which of the two P3 rhombus shapes a tile is.
public enum TileKind {
	Thin,   // 36/144 degree rhombus
	Thick,  // 72/108 degree rhombus
}

// Every corner of every P3 tile is one of exactly two kinds, and both tile
// kinds alternate ThinAngle/ThickAngle/ThinAngle/ThickAngle around their 4
// corners in the same rotational order. This is deliberately independent of
// the tile's own interior angle in degrees (a thick tile's corners are never
// literally 36 or 144 degrees) - it is a separate, purely combinatorial label
// that says which of the two matching-rule vertex classes a corner belongs
// to, the same bipartition PenroseTiling.PenroseVertex.ColorA computes
// geometrically. Concretely here it lines up with the acute/obtuse split
// (corner 0 and 2 are ThinAngle/acute, corner 1 and 3 are ThickAngle/obtuse),
// which is what makes the alternation hold for both tile kinds at once.
public enum VertexKind {
	ThinAngle,
	ThickAngle,
}

// One tile side, named by which tile kind it belongs to and its position
// (A/B/C/D) going around that tile starting from corner 0. This is the unit
// the matching rule is actually stated in terms of - not raw geometry.
public enum SideLabel {
	ThinA, ThinB, ThinC, ThinD,
	ThickA, ThickB, ThickC, ThickD,
}

// A generated tile instance: a fixed local shape (see PenroseTileRules.Canonical
// Corners) moved into world space by rotation and translation ONLY. There is
// no way to construct one with a reflection - Create() only exposes a
// rotation angle - so any instance built through this type is guaranteed
// non-mirrored by construction. Code that receives corners from elsewhere
// (a future generator) should still run IsMirrored/ValidateConnections below,
// since nothing stops that code from handing in reflected corners.
public sealed class PenroseTileInstance {
	public TileKind Kind;
	public Vector2[] Corners = new Vector2[4]; // world-space, corner order 0..3 matches PenroseTileRules.CornerVertexKinds

	public SideLabel SideAt(int edgeIndex) => PenroseTileRules.SideLabels(Kind)[edgeIndex];
}

// Which of the two visual arc colors a decorated corner's matching-rule arc
// is drawn in - see the reference "Penrose rhombs matching rules" diagram.
public enum ArcColor {
	Red,
	Blue,
}

// One decorated corner: which corner index it sits at, how big a radius (as
// a fraction of the tile's edge length - every P3 edge is the same length,
// so this is the tile's own edge length either way), and which color.
public readonly struct ArcDecoration {
	public readonly int CornerIndex;
	public readonly float RadiusFraction;
	public readonly ArcColor Color;

	public ArcDecoration(int cornerIndex, float radiusFraction, ArcColor color) {
		CornerIndex = cornerIndex;
		RadiusFraction = radiusFraction;
		Color = color;
	}
}

// A decorated corner's arc, resolved to world space for one tile instance -
// enough for a caller to draw it (DrawArc(Center, Radius, angle-to-Endpoint
// Prev, angle-to-EndpointNext, ...)) and for ValidateArcColors below to check
// whether two tiles' arcs actually meet cleanly.
public readonly struct ArcGeometry {
	public readonly Vector2 Center;
	public readonly float Radius;
	public readonly Vector2 EndpointTowardPrev; // point on the edge shared with corner-1
	public readonly Vector2 EndpointTowardNext; // point on the edge shared with corner+1
	public readonly ArcColor Color;

	public ArcGeometry(Vector2 center, float radius, Vector2 endpointTowardPrev, Vector2 endpointTowardNext, ArcColor color) {
		Center = center;
		Radius = radius;
		EndpointTowardPrev = endpointTowardPrev;
		EndpointTowardNext = endpointTowardNext;
		Color = color;
	}
}

// The from-scratch replacement for the old radius-based matching-rule check:
// instead of computing Ammann-arc radii and testing whether they sum to the
// edge length, every tile side gets a fixed symbolic label (see SideLabel),
// and two tiles may share an edge only if their two sides' labels appear
// together in AllowedConnections. This is exactly equivalent to the real P3
// matching rule (it is the same rule, just read off the tile's edge instead
// of an arc's radius) and is far cheaper to check.
public static class PenroseTileRules {
	// Shared by both tile kinds: corner i's vertex kind, i = 0..3, in the same
	// rotational direction the corners are always wound in (see
	// CanonicalCorners) - never re-ordered per instance, since rotation moves
	// where corner 0 sits in world space but never changes this cyclic pattern.
	public static readonly VertexKind[] CornerVertexKinds = {
		VertexKind.ThinAngle, VertexKind.ThickAngle, VertexKind.ThinAngle, VertexKind.ThickAngle,
	};

	private static readonly SideLabel[] ThinSideLabels = { SideLabel.ThinA, SideLabel.ThinB, SideLabel.ThinC, SideLabel.ThinD };
	private static readonly SideLabel[] ThickSideLabels = { SideLabel.ThickA, SideLabel.ThickB, SideLabel.ThickC, SideLabel.ThickD };

	// SideLabels(kind)[i] is the label of the edge between corner i and corner
	// (i+1)%4.
	public static SideLabel[] SideLabels(TileKind kind) => kind == TileKind.Thin ? ThinSideLabels : ThickSideLabels;

	// The interior angle at corner i, in degrees - only used to build the
	// canonical unit shape below. Corners 0/2 (ThinAngle) are the acute pair,
	// corners 1/3 (ThickAngle) are the obtuse pair, matching CornerVertexKinds.
	private static float CornerAngleDegrees(TileKind kind, int cornerIndex) {
		bool acute = cornerIndex == 0 || cornerIndex == 2;
		if (kind == TileKind.Thin)
			return acute ? 36.0f : 144.0f;
		return acute ? 72.0f : 108.0f;
	}

	// The base 6 side-label pairings the matching rule permits, plus the 2
	// same-kind-wraparound pairings (ThinD<->ThinA, ThickD<->ThickA) that a
	// Star or Sun vertex (several copies of one tile kind fanned by rotation
	// around their own acute corner, each one's D side meeting the next one's
	// A side) forces to exist - confirmed by ValidateConnections itself
	// rejecting a hand-built Sun/Star until these were added - plus
	// ThickB<->ThickC.
	private static readonly HashSet<(SideLabel, SideLabel)> AllowedConnections = new HashSet<(SideLabel, SideLabel)> {
		(SideLabel.ThinA, SideLabel.ThickA),
		(SideLabel.ThinA, SideLabel.ThinB),
		(SideLabel.ThinB, SideLabel.ThickD),
		(SideLabel.ThinC, SideLabel.ThinD),
		(SideLabel.ThinC, SideLabel.ThickB),
		(SideLabel.ThinD, SideLabel.ThickC),
		(SideLabel.ThinD, SideLabel.ThinA),
		(SideLabel.ThickD, SideLabel.ThickA),
		(SideLabel.ThickB, SideLabel.ThickC),
	};

	public static bool IsValidConnection(SideLabel a, SideLabel b) {
		return AllowedConnections.Contains((a, b)) || AllowedConnections.Contains((b, a));
	}

	// Matching-rule arcs, per the reference diagram: a thin tile is decorated
	// at its two ThickAngle (obtuse) corners, a thick tile at its two
	// ThinAngle (acute) corners - one small (1/3 edge) and, for the thick
	// tile, one big (2/3 edge) arc.
	private static readonly ArcDecoration[] ThinArcDecorations = {
		new ArcDecoration(1, 1.0f / 3.0f, ArcColor.Blue), // between ThinA and ThinB
		new ArcDecoration(3, 1.0f / 3.0f, ArcColor.Red),  // between ThinC and ThinD
	};
	private static readonly ArcDecoration[] ThickArcDecorations = {
		new ArcDecoration(0, 1.0f / 3.0f, ArcColor.Blue), // between ThickD and ThickA
		new ArcDecoration(2, 2.0f / 3.0f, ArcColor.Red),  // between ThickB and ThickC
	};

	public static ArcDecoration[] ArcDecorationsFor(TileKind kind) => kind == TileKind.Thin ? ThinArcDecorations : ThickArcDecorations;

	// Resolves every decorated corner of one tile instance to world space:
	// the arc's center (the corner itself), its radius, and the two points
	// where it meets the tile's own two edges at that corner.
	public static IEnumerable<ArcGeometry> GetArcs(PenroseTileInstance tile, float edgeLength) {
		foreach (ArcDecoration deco in ArcDecorationsFor(tile.Kind)) {
			Vector2 corner = tile.Corners[deco.CornerIndex];
			Vector2 prev = tile.Corners[(deco.CornerIndex + 3) % 4];
			Vector2 next = tile.Corners[(deco.CornerIndex + 1) % 4];
			float radius = deco.RadiusFraction * edgeLength;
			Vector2 endpointPrev = corner + (prev - corner).Normalized() * radius;
			Vector2 endpointNext = corner + (next - corner).Normalized() * radius;
			yield return new ArcGeometry(corner, radius, endpointPrev, endpointNext, deco.Color);
		}
	}

	// The canonical, always-CCW, unit-edge-length shape for a tile kind, in
	// local space with corner 0 at the origin. e1/e2 are the two edge
	// directions out of corner 0, separated by that corner's own angle;
	// walking corner0 -> corner0+e1 -> corner0+e1+e2 -> corner0+e2 traces the
	// rhombus boundary counter-clockwise with corner 0 and 2 acute, 1 and 3
	// obtuse - exactly the CornerVertexKinds pattern. Every PenroseTileInstance
	// is built by rotating and translating this fixed shape, never reflecting
	// it, which is what makes "never mirror" a guarantee rather than a rule to
	// remember to follow.
	public static Vector2[] CanonicalCorners(TileKind kind, float edgeLength) {
		double angle = CornerAngleDegrees(kind, 0) * Math.PI / 180.0;
		var e1 = new Vector2(edgeLength, 0.0f);
		var e2 = new Vector2((float)Math.Cos(angle) * edgeLength, (float)Math.Sin(angle) * edgeLength);
		return new[] { Vector2.Zero, e1, e1 + e2, e2 };
	}

	private static double SignedArea(Vector2[] corners) {
		double area = 0.0;
		for (int i = 0; i < corners.Length; i++) {
			Vector2 a = corners[i];
			Vector2 b = corners[(i + 1) % corners.Length];
			area += (double)a.X * b.Y - (double)b.X * a.Y;
		}
		return 0.5 * area;
	}

	// CanonicalCorners always winds CCW (positive signed area). Rotation and
	// translation both preserve that sign; only a reflection flips it - so
	// this is a complete, cheap test for "was this tile mirrored," regardless
	// of how its corners were produced.
	public static bool IsMirrored(Vector2[] corners) => SignedArea(corners) < 0.0;

	// Builds a tile purely by rotating (any angle) and translating the
	// canonical shape - there is no reflection parameter, so nothing built
	// through this method can ever come out mirrored.
	public static PenroseTileInstance Create(TileKind kind, float edgeLength, Vector2 position, float rotationDegrees) {
		Vector2[] local = CanonicalCorners(kind, edgeLength);
		double rotation = rotationDegrees * Math.PI / 180.0;
		float cos = (float)Math.Cos(rotation);
		float sin = (float)Math.Sin(rotation);
		var corners = new Vector2[4];
		for (int i = 0; i < 4; i++) {
			Vector2 p = local[i];
			var rotated = new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
			corners[i] = rotated + position;
		}
		return new PenroseTileInstance { Kind = kind, Corners = corners };
	}

	// Rounds a world position to a fixed grid so two tiles' shared corner
	// (computed independently, possibly with tiny floating point differences)
	// hash to the same key.
	private static (long, long) PositionKey(Vector2 p) {
		const float grid = 100.0f; // 0.01 world-unit buckets
		return ((long)Math.Round(p.X * grid), (long)Math.Round(p.Y * grid));
	}

	// The from-scratch "are these tiles connected correctly" check: finds
	// every pair of tile edges that occupy the same place in world space
	// (i.e. are actually glued together), then requires their side labels to
	// appear in AllowedConnections, and requires every tile to be
	// non-mirrored. Returns the number of problems found (0 = fully valid),
	// pushing a warning describing each one - same convention as
	// PenroseTiling.Validate.
	// logDetails: pushes one warning per problem plus a final summary line -
	// useful for an ad hoc check, but overwhelming when this runs on every
	// debug launch against a real, imperfectly-resolvable patch (see
	// PenroseTiling.Generate, which already prints its own one-line summary).
	// Pass false there to just get the count back.
	public static int ValidateConnections(List<PenroseTileInstance> tiles, bool logDetails = true) {
		int problems = 0;

		for (int t = 0; t < tiles.Count; t++) {
			if (IsMirrored(tiles[t].Corners)) {
				problems++;
				if (logDetails)
					GD.PushWarning($"PenroseTileRules.ValidateConnections: tile {t} ({tiles[t].Kind}) is mirrored - only translation and rotation are allowed.");
			}
		}

		// Undirected edge key (its two endpoints, order-independent) -> every
		// (tile, edgeIndex) whose edge sits at that position.
		var edgesByPosition = new Dictionary<((long, long), (long, long)), List<(int Tile, int Edge)>>();
		for (int t = 0; t < tiles.Count; t++) {
			Vector2[] corners = tiles[t].Corners;
			for (int e = 0; e < 4; e++) {
				(long, long) ka = PositionKey(corners[e]);
				(long, long) kb = PositionKey(corners[(e + 1) % 4]);
				var key = ka.CompareTo(kb) <= 0 ? (ka, kb) : (kb, ka);
				if (!edgesByPosition.TryGetValue(key, out List<(int, int)> list)) {
					list = new List<(int, int)>();
					edgesByPosition[key] = list;
				}
				list.Add((t, e));
			}
		}

		foreach (KeyValuePair<((long, long), (long, long)), List<(int Tile, int Edge)>> entry in edgesByPosition) {
			if (entry.Value.Count < 2)
				continue; // boundary edge - nothing to check it against
			if (entry.Value.Count > 2) {
				problems++;
				if (logDetails)
					GD.PushWarning($"PenroseTileRules.ValidateConnections: edge shared by {entry.Value.Count} tiles (expected at most 2) - tiles overlap.");
				continue;
			}

			(int Tile, int Edge) a = entry.Value[0];
			(int Tile, int Edge) b = entry.Value[1];
			SideLabel labelA = tiles[a.Tile].SideAt(a.Edge);
			SideLabel labelB = tiles[b.Tile].SideAt(b.Edge);
			if (!IsValidConnection(labelA, labelB)) {
				problems++;
				if (logDetails)
					GD.PushWarning($"PenroseTileRules.ValidateConnections: tile {a.Tile} side {labelA} is glued to tile {b.Tile} side {labelB} - not an allowed connection.");
			}
		}

		if (problems > 0 && logDetails)
			GD.PushWarning($"PenroseTileRules.ValidateConnections: {problems} problem(s) found.");

		return problems;
	}

	// The literal check for "every red arc endpoint only ever meets another
	// red arc endpoint, every blue only ever meets another blue": buckets
	// every decorated corner's two arc endpoints by world position, then
	// flags any position where a red and a blue endpoint land on top of each
	// other. This is independent of ValidateConnections - a tiling can pass
	// the side-label check yet still fail this one, if a same-kind
	// wraparound connection (ThinD<->ThinA, ThickD<->ThickA) happens to seat
	// two different-colored corners at the same point.
	//
	// Confirmed case in point: a 10-thin-tile Star vertex (fanned via
	// ThinD<->ThinA) passes ValidateConnections cleanly but fails this check
	// at every one of its 10 joins, since the thin tile's decorated corners
	// (1 and 3) sit away from the Star's shared hub corner (0). Per design,
	// that makes the Star illegal here - a generator must run BOTH
	// ValidateConnections and ValidateArcColors, since neither alone would
	// catch it (a 5-thick Sun, by contrast, passes both: its hub corner (0)
	// is one of the thick tile's own decorated corners, so every wedge draws
	// the same blue arc and the hub becomes one clean circle).
	// logDetails: see ValidateConnections above - same reasoning.
	public static int ValidateArcColors(List<PenroseTileInstance> tiles, float edgeLength, bool logDetails = true) {
		int problems = 0;
		var colorsByPosition = new Dictionary<(long, long), List<ArcColor>>();

		foreach (PenroseTileInstance tile in tiles) {
			foreach (ArcGeometry arc in GetArcs(tile, edgeLength)) {
				foreach (Vector2 endpoint in new[] { arc.EndpointTowardPrev, arc.EndpointTowardNext }) {
					(long, long) key = PositionKey(endpoint);
					if (!colorsByPosition.TryGetValue(key, out List<ArcColor> list)) {
						list = new List<ArcColor>();
						colorsByPosition[key] = list;
					}
					list.Add(arc.Color);
				}
			}
		}

		foreach (KeyValuePair<(long, long), List<ArcColor>> entry in colorsByPosition) {
			bool hasRed = entry.Value.Contains(ArcColor.Red);
			bool hasBlue = entry.Value.Contains(ArcColor.Blue);
			if (hasRed && hasBlue) {
				problems++;
				if (logDetails)
					GD.PushWarning("PenroseTileRules.ValidateArcColors: a red and a blue arc endpoint coincide at the same point - colors must never touch.");
			}
		}

		if (problems > 0 && logDetails)
			GD.PushWarning($"PenroseTileRules.ValidateArcColors: {problems} problem(s) found.");

		return problems;
	}

	// Same checks as ValidateConnections/ValidateArcColors, but returns WHICH
	// tiles are involved in a violation instead of just a count - lets a
	// caller (an interactive debug view, say) highlight exactly the tiles a
	// manual fix should target, and re-check after each change.
	public static HashSet<int> FindProblemTiles(List<PenroseTileInstance> tiles, float edgeLength) {
		var problems = new HashSet<int>();

		for (int t = 0; t < tiles.Count; t++) {
			if (IsMirrored(tiles[t].Corners))
				problems.Add(t);
		}

		var edgesByPosition = new Dictionary<((long, long), (long, long)), List<(int Tile, int Edge)>>();
		for (int t = 0; t < tiles.Count; t++) {
			Vector2[] corners = tiles[t].Corners;
			for (int e = 0; e < 4; e++) {
				(long, long) ka = PositionKey(corners[e]);
				(long, long) kb = PositionKey(corners[(e + 1) % 4]);
				var key = ka.CompareTo(kb) <= 0 ? (ka, kb) : (kb, ka);
				if (!edgesByPosition.TryGetValue(key, out List<(int, int)> list)) {
					list = new List<(int, int)>();
					edgesByPosition[key] = list;
				}
				list.Add((t, e));
			}
		}
		foreach (List<(int Tile, int Edge)> entry in edgesByPosition.Values) {
			if (entry.Count < 2)
				continue;
			if (entry.Count > 2) {
				foreach ((int Tile, int Edge) e in entry)
					problems.Add(e.Tile);
				continue;
			}
			(int Tile, int Edge) a = entry[0];
			(int Tile, int Edge) b = entry[1];
			SideLabel labelA = tiles[a.Tile].SideAt(a.Edge);
			SideLabel labelB = tiles[b.Tile].SideAt(b.Edge);
			if (!IsValidConnection(labelA, labelB)) {
				problems.Add(a.Tile);
				problems.Add(b.Tile);
			}
		}

		var colorsByPosition = new Dictionary<(long, long), List<(int Tile, ArcColor Color)>>();
		for (int t = 0; t < tiles.Count; t++) {
			foreach (ArcGeometry arc in GetArcs(tiles[t], edgeLength)) {
				foreach (Vector2 endpoint in new[] { arc.EndpointTowardPrev, arc.EndpointTowardNext }) {
					(long, long) key = PositionKey(endpoint);
					if (!colorsByPosition.TryGetValue(key, out List<(int, ArcColor)> list)) {
						list = new List<(int, ArcColor)>();
						colorsByPosition[key] = list;
					}
					list.Add((t, arc.Color));
				}
			}
		}
		foreach (List<(int Tile, ArcColor Color)> entry in colorsByPosition.Values) {
			bool hasRed = entry.Exists(e => e.Color == ArcColor.Red);
			bool hasBlue = entry.Exists(e => e.Color == ArcColor.Blue);
			if (hasRed && hasBlue) {
				foreach ((int Tile, ArcColor Color) e in entry)
					problems.Add(e.Tile);
			}
		}

		return problems;
	}
}

internal static class PositionKeyComparerExtensions {
	// (long,long) doesn't implement IComparable by itself - this gives
	// ValidateConnections a stable, order-independent way to build an
	// undirected edge key from two endpoint keys.
	public static int CompareTo(this (long, long) a, (long, long) b) {
		int cmp = a.Item1.CompareTo(b.Item1);
		return cmp != 0 ? cmp : a.Item2.CompareTo(b.Item2);
	}
}
