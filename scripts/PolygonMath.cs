using Godot;

// Plain polygon helpers for board tiles and tower bodies. A hat isn't convex - unlike
// a Penrose rhombus - so questions like "is this point on that tile" or "how much room
// is there around this point" need real answers instead of center-distance shortcuts.
// Kept in managed code (rather than Godot's Geometry2D) since BoardGrid.WorldToCell
// asks thousands of times per frame while a line-of-sight preview is up.
public static class PolygonMath {
	// Even-odd rule - fine for the simple polygons every tile and body is.
	public static bool Contains(Vector2[] polygon, Vector2 point) {
		bool inside = false;
		for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
			Vector2 a = polygon[j];
			Vector2 b = polygon[i];
			if ((b.Y > point.Y) != (a.Y > point.Y) && point.X < (a.X - b.X) * (point.Y - b.Y) / (a.Y - b.Y) + b.X)
				inside = !inside;
		}
		return inside;
	}

	// Distance from point to the polygon's nearest edge, inside or out.
	public static float DistanceToEdges(Vector2[] polygon, Vector2 point) {
		float best = float.PositiveInfinity;
		for (int i = 0; i < polygon.Length; i++) {
			Vector2 a = polygon[i];
			Vector2 ab = polygon[(i + 1) % polygon.Length] - a;
			float t = Mathf.Clamp((point - a).Dot(ab) / ab.LengthSquared(), 0.0f, 1.0f);
			best = Mathf.Min(best, point.DistanceTo(a + ab * t));
		}
		return best;
	}

	// Positive for the winding every board tile is normalized to (see
	// PenroseGenerator, HatTiling).
	public static float SignedArea(Vector2[] polygon) {
		float area = 0.0f;
		for (int i = 0; i < polygon.Length; i++) {
			Vector2 a = polygon[i];
			Vector2 b = polygon[(i + 1) % polygon.Length];
			area += a.X * b.Y - b.X * a.Y;
		}
		return area / 2.0f;
	}

	public static Vector2 VertexAverage(Vector2[] polygon) {
		Vector2 sum = Vector2.Zero;
		foreach (Vector2 p in polygon)
			sum += p;
		return sum / polygon.Length;
	}

	// Whether the straight walk from `from` (inside the polygon) to `to` (on or inside
	// its boundary) never leaves it: no edge crosses the walk outright, and a few points
	// along it - which catch a walk slipping out exactly through a corner, where no edge
	// is crossed outright - are all inside.
	public static bool SegmentStaysInside(Vector2[] polygon, Vector2 from, Vector2 to) {
		for (int i = 0; i < polygon.Length; i++) {
			if (SegmentsCrossProperly(from, to, polygon[i], polygon[(i + 1) % polygon.Length]))
				return false;
		}
		const int Samples = 8;
		for (int s = 1; s < Samples; s++) {
			if (!Contains(polygon, from.Lerp(to, (float)s / Samples)))
				return false;
		}
		return true;
	}

	// True only when each segment's endpoints lie strictly on opposite sides of the
	// other - touching at an endpoint or running along the same line doesn't count.
	private static bool SegmentsCrossProperly(Vector2 a, Vector2 b, Vector2 c, Vector2 d) {
		float d1 = Cross(c, d, a);
		float d2 = Cross(c, d, b);
		float d3 = Cross(a, b, c);
		float d4 = Cross(a, b, d);
		const float Epsilon = 1e-4f;
		return ((d1 > Epsilon && d2 < -Epsilon) || (d1 < -Epsilon && d2 > Epsilon))
			&& ((d3 > Epsilon && d4 < -Epsilon) || (d3 < -Epsilon && d4 > Epsilon));
	}

	private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b - a).Cross(c - a);
}
