using Godot;
using System;
using System.Collections.Generic;

// Generates a bounded Penrose P3 patch via de Bruijn's pentagrid ("multigrid")
// dual method - the same algorithm used by patterncollider
// (github.com/aatishb/patterncollider): 5 families of parallel lines at
// 72-degree spacing; every crossing of two lines from different families
// dualizes into exactly one rhombus tile, with the crossing families' own
// two indices swept over their 0/1 combinations giving the tile's 4 corners
// directly (a closed-form projection, no recursive subdivision).
//
// Emits PenroseTileInstance (see PenroseTileRules.cs) directly, in exactly
// the corner convention PenroseTileRules expects (corner 0/2 acute, wound
// CCW, built by construction so it can never come out mirrored) - so the
// result can be checked immediately with PenroseTileRules.ValidateConnections
// and ValidateArcColors.
public static class PenroseGenerator {
	private const int FamilyCount = 5;

	// Phase offsets - keeps the pentagrid "regular" (every crossing involves
	// exactly 2 lines, never 3+), so a tile's 4 corners are always well
	// defined. A "singular" grid (offsets summing to an integer) gives the
	// famous perfectly symmetric "cartwheel" pattern but needs fragile
	// floating-point tie-breaking at its center; this trades that off for a
	// robust patch.
	//
	// These specific values matter more than that one property, though: an
	// evenly-spaced set like {0.1,0.2,0.3,0.4,0.5} is riddled with small
	// linear relations (0.1+0.4=0.5, 0.2+0.3=0.5, ...), and each one forces
	// 3+ pentagrid lines to cross at a shared point somewhere in the plane -
	// a real "singular" coincidence despite the offsets not summing to an
	// integer overall. In this generator that showed up as vertices with
	// far more tiles meeting them than a genuine Penrose tiling ever has
	// (confirmed against real generated patches: illegal 10-thin-rhombus
	// "Star" vertices, and even a few 8-tile ones - true Penrose vertices
	// never exceed 7). These values were verified, by direct ring-size
	// inspection of generated patches from radius 200 through 800, to never
	// produce a vertex with more than 7 tiles.
	private static readonly double[] Gamma = { 0.41662, 0.010169, 0.825207, 0.29864, 0.368412 };

	private static Vector2 Direction(int family) {
		double angle = 2.0 * Math.PI * family / FamilyCount;
		return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
	}

	private static int Mod(int a, int m) => ((a % m) + m) % m;

	// edgeLength: world-space length of one tile edge (uniform across the
	// whole patch - the "sidelength" ArcDecoration's RadiusFraction scales
	// against). radius: only tiles whose center falls within this distance
	// of cutoutCenter are kept, which is what gives the patch its naturally
	// round/decagonal boundary. The pentagrid itself is always indexed
	// relative to the true origin (that's baked into Gamma), so cutoutCenter
	// doesn't shift the underlying pattern - it just picks a different
	// circular slice of the same infinite tiling, off to one side instead of
	// centered on the pattern's own middle. The returned tiles are
	// translated back so that slice sits at the world origin regardless of
	// cutoutCenter, matching what every caller already expects.
	public static List<PenroseTileInstance> Generate(float edgeLength, float radius, Vector2 cutoutCenter = default) {
		var dirs = new Vector2[FamilyCount];
		for (int m = 0; m < FamilyCount; m++)
			dirs[m] = Direction(m);

		// Generous margin past the radius cutoff so no boundary tile is ever
		// missed for want of index range - excess candidates are simply
		// rejected by the radius check in TryBuildTile. Reach extends past
		// cutoutCenter itself, since indices are still measured from the
		// pattern's true origin, not from the slice we're keeping.
		float reach = cutoutCenter.Length() + radius;
		int n = (int)Math.Ceiling(reach / edgeLength) + 3;
		var tiles = new List<PenroseTileInstance>();

		for (int j = 0; j < FamilyCount; j++) {
			for (int k = j + 1; k < FamilyCount; k++) {
				float det = dirs[j].X * dirs[k].Y - dirs[j].Y * dirs[k].X;
				for (int kj = -n; kj <= n; kj++) {
					for (int kk = -n; kk <= n; kk++) {
						PenroseTileInstance tile = TryBuildTile(dirs, j, k, kj, kk, det, edgeLength, radius, cutoutCenter);
						if (tile != null)
							tiles.Add(tile);
					}
				}
			}
		}

		if (cutoutCenter != Vector2.Zero) {
			foreach (PenroseTileInstance tile in tiles)
				for (int i = 0; i < 4; i++)
					tile.Corners[i] -= cutoutCenter;
		}

		return tiles;
	}

	// Every rhombus in a P3 tiling is dual to exactly one crossing of two
	// pentagrid lines from different families. Its 4 corners come from
	// projecting the 5-index "which strip of each family" vector back
	// through the same 5 direction vectors, nudging the two crossing
	// families' indices by 0/1 across the 4 corner combinations - de
	// Bruijn's dual projection.
	private static PenroseTileInstance TryBuildTile(Vector2[] dirs, int j, int k, int kj, int kk, float det, float edgeLength, float radius, Vector2 cutoutCenter) {
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
		if ((center - cutoutCenter).Length() > radius)
			return null;

		int diff = Math.Min(Mod(k - j, FamilyCount), Mod(j - k, FamilyCount));
		bool isThin = diff == 2;

		// The natural corner order (v00,v10,v11,v01) traces the
		// parallelogram CCW only when dirs[j]->dirs[k] itself turns
		// counter-clockwise the short way (true for k-j = 1 or 2); for
		// k-j = 3 or 4 the same listing winds clockwise instead. Normalizing
		// every tile to the same CCW winding here is what makes
		// PenroseTileRules.IsMirrored a meaningful, never-false-positive
		// check on this generator's output.
		Vector2[] order = { v00, v10, v11, v01 };
		float signedArea = 0.0f;
		for (int i = 0; i < 4; i++) {
			Vector2 a = order[i];
			Vector2 b = order[(i + 1) % 4];
			signedArea += a.X * b.Y - b.X * a.Y;
		}
		if (signedArea < 0.0f)
			order = new[] { v00, v01, v11, v10 }; // same cycle, reversed direction, still starts at v00

		// v00's own interior angle (between dirs[j] and dirs[k]) is 72
		// degrees (acute) for a thick tile but 144 degrees (obtuse) for a
		// thin one. So order[0] is already the acute corner for a thick
		// tile; for a thin tile a one-step cyclic shift (a pure relabeling,
		// winding unaffected) puts the acute corner at index 0 instead -
		// exactly PenroseTileRules.CanonicalCorners's corner-0-is-acute
		// convention.
		Vector2[] corners = isThin
			? new[] { order[1], order[2], order[3], order[0] }
			: order;

		return new PenroseTileInstance {
			Kind = isThin ? TileKind.Thin : TileKind.Thick,
			Corners = corners,
		};
	}
}
