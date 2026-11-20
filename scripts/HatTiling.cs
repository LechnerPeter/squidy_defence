using Godot;
using System;
using System.Collections.Generic;

// Builds the gameplay-ready hat patch for Einstein mode. HatGenerator supplies the
// raw substitution geometry; this cuts a round board out of it and turns each hat
// into a BoardTile. Unlike PenroseTiling there is nothing to resolve or repair
// afterwards: the substitution system places every hat exactly, with no matching
// rule left to satisfy - a hat's shape alone forces the tiling to be aperiodic.
public static class HatTiling {
	// How much room, in metatile units, a hat can stick out past its own
	// supertile's outline - kept clear between the cut and that outline, so the cut
	// never reaches where the supertile's hats run out.
	private const double OutlineMargin = 4.0;

	// The deepest supertile considered. Its H holds about 370,000 hats - far more
	// room than any board needs - and is only ever walked near the cut (see
	// HatGenerator.Flatten).
	private const int MaxSubstitutions = 6;

	// The hat's roomiest interior point (see HatGenerator.VisualCenter), in
	// HatOutline units. Every hat is a rotated and possibly mirrored copy of the
	// same shape, so this one point, carried along by each hat's transform, is
	// every tile's Center.
	private static readonly (HatPoint Center, double Clearance) Visual = HatGenerator.VisualCenter(HatGenerator.HatOutline);

	// World-space distance from a hat's Center to its nearest side - how much room a
	// tower's glyph has.
	public static float Clearance(float hatUnit) => (float)Visual.Clearance * hatUnit;

	// hatUnit: world length of the hat's short sides (see GameData.HatUnit). radius:
	// only hats whose Center falls within this distance of the cut's center are
	// kept, which gives the board its round outline. The cut is taken at a random
	// spot of a supertile big enough to hold it with room to spare, at a random
	// angle, so every run gets a different board. Returned centered on the world
	// origin, like a Penrose board.
	public static List<BoardTile> Generate(float hatUnit, float radius) {
		// HatGenerator's metatiles place hats at half the scale of HatOutline.
		double worldPerUnit = 2.0 * hatUnit;
		double cutRadius = radius / worldPerUnit;

		// Deep enough that the cut can sit anywhere within one cut radius of the
		// supertile's middle and still clear its outline - so the spot really varies.
		HatMetaTile supertile = null;
		double room = 0.0;
		for (int substitutions = 1; substitutions <= MaxSubstitutions; substitutions++) {
			supertile = HatGenerator.BuildSupertile(substitutions);
			room = Inradius(supertile.Shape) - cutRadius - OutlineMargin;
			if (room >= cutRadius)
				break;
		}
		float spotAngle = GD.Randf() * Mathf.Tau;
		double spotDistance = Math.Sqrt(GD.Randf()) * Math.Max(room, 0.0);
		var cutCenter = new HatPoint(Math.Cos(spotAngle) * spotDistance, Math.Sin(spotAngle) * spotDistance);
		double turn = GD.Randf() * Math.PI * 2.0;
		double cos = Math.Cos(turn) * worldPerUnit;
		double sin = Math.Sin(turn) * worldPerUnit;

		// Godot's y axis points down, so y is negated as well - otherwise the
		// board would show the paper's figures mirrored, with its rare reflected
		// hats looking like the common kind and vice versa.
		Vector2 ToWorld(HatPoint p) {
			double x = p.X - cutCenter.X;
			double y = -(p.Y - cutCenter.Y);
			return new Vector2((float)(x * cos - y * sin), (float)(x * sin + y * cos));
		}

		List<PlacedHat> nearby = HatGenerator.Flatten(supertile, HatTransform.Identity, cutCenter, cutRadius + OutlineMargin);
		HatPoint[] outline = HatGenerator.OutlineWithStraightCorner();
		HatPoint? latticeOrigin = null;
		var tiles = new List<BoardTile>();
		foreach (PlacedHat hat in nearby) {
			HatPoint center = hat.Transform.Apply(Visual.Center);
			if ((center - cutCenter).Length > cutRadius)
				continue;
			latticeOrigin ??= hat.Transform.Apply(outline[0]);
			var corners = new Vector2[outline.Length];
			for (int i = 0; i < outline.Length; i++)
				corners[i] = ToWorld(SnapToLattice(hat.Transform.Apply(outline[i]), latticeOrigin.Value));
			if (PolygonMath.SignedArea(corners) < 0.0f)
				Array.Reverse(corners);
			tiles.Add(new BoardTile {
				Id = tiles.Count,
				IsMirrored = hat.IsReflected,
				HatLabel = hat.Label,
				Center = ToWorld(center),
				Corners = corners,
			});
		}
		return tiles;
	}

	// Every hat corner lies on one shared triangular lattice of half-unit spacing
	// (every transform between hats is a turn by a multiple of 60 degrees, a
	// mirroring, or a lattice shift), so it's snapped exactly onto it. Each tile
	// computes its own copy of a shared corner, through its own chain of transforms;
	// snapped, every copy comes out bit-identical, so BoardTopology always
	// recognizes it as one point.
	private static HatPoint SnapToLattice(HatPoint p, HatPoint origin) {
		HatPoint q = p - origin;
		double b = Math.Round(2.0 * q.Y / HatGenerator.HalfRoot3) / 2.0;
		double a = Math.Round(2.0 * (q.X - 0.5 * b)) / 2.0;
		return origin + HatPoint.Hex(a, b);
	}

	private static double Inradius(HatPoint[] outline) {
		HatPoint center = new HatPoint(0.0, 0.0);
		foreach (HatPoint p in outline)
			center += p;
		center = center * (1.0 / outline.Length);
		double best = double.PositiveInfinity;
		for (int i = 0; i < outline.Length; i++) {
			HatPoint a = outline[i];
			HatPoint ab = outline[(i + 1) % outline.Length] - a;
			best = Math.Min(best, Math.Abs(ab.X * (center.Y - a.Y) - ab.Y * (center.X - a.X)) / ab.Length);
		}
		return best;
	}

	// One hat at board scale - corners relative to its Center, oriented as an
	// unrotated hat would be on the board - for a tower built on an outpost
	// platform, which stands on no real tile of its own (see TileShape.Hat).
	public static Vector2[] PlatformHatCorners(float hatUnit) {
		HatPoint[] outline = HatGenerator.OutlineWithStraightCorner();
		var corners = new Vector2[outline.Length];
		for (int i = 0; i < outline.Length; i++) {
			HatPoint p = outline[i] - Visual.Center;
			corners[i] = new Vector2((float)p.X, (float)-p.Y) * hatUnit;
		}
		if (PolygonMath.SignedArea(corners) < 0.0f)
			Array.Reverse(corners);
		return corners;
	}
}
