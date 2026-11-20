using System;
using System.Collections.Generic;

// Generates patches of the "hat" - the aperiodic monotile ("einstein") found by
// Smith, Myers, Kaplan and Goodman-Strauss in 2023 - through the H/T/P/F metatile
// substitution system from their paper "An aperiodic monotile". This file is a
// straight port of the geometry in Craig S. Kaplan's reference implementation,
// hatviz (https://github.com/isohedral/hatviz - geometry.js and hat.js), kept close
// to the original's structure and names (constructPatch's rule table above all) so
// the two can be compared line by line. It is pure double-precision math with no
// Godot types; HatTiling turns its output into a cut, world-space game board.
//
// hatviz is distributed under the following license, reproduced here as it requires:
//
//   BSD 3-Clause License
//
//   Copyright (c) 2023, Craig S. Kaplan
//
//   Redistribution and use in source and binary forms, with or without
//   modification, are permitted provided that the following conditions are met:
//
//   1. Redistributions of source code must retain the above copyright notice, this
//      list of conditions and the following disclaimer.
//
//   2. Redistributions in binary form must reproduce the above copyright notice,
//      this list of conditions and the following disclaimer in the documentation
//      and/or other materials provided with the distribution.
//
//   3. Neither the name of the copyright holder nor the names of its
//      contributors may be used to endorse or promote products derived from
//      this software without specific prior written permission.
//
//   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
//   AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//   IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//   DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
//   FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
//   DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
//   SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
//   CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
//   OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
//   OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

public readonly struct HatPoint {
	public readonly double X;
	public readonly double Y;

	public HatPoint(double x, double y) {
		X = x;
		Y = y;
	}

	public static HatPoint operator +(HatPoint a, HatPoint b) => new HatPoint(a.X + b.X, a.Y + b.Y);
	public static HatPoint operator -(HatPoint a, HatPoint b) => new HatPoint(a.X - b.X, a.Y - b.Y);
	public static HatPoint operator *(HatPoint a, double s) => new HatPoint(a.X * s, a.Y * s);

	public double Length => Math.Sqrt(X * X + Y * Y);

	// hatviz's hexPt: (x, y) in the triangular lattice spanned by (1, 0) and
	// (1/2, sqrt(3)/2) - the lattice every hat corner sits on.
	public static HatPoint Hex(double x, double y) => new HatPoint(x + 0.5 * y, HatGenerator.HalfRoot3 * y);
}

// hatviz's 6-element affine matrix [A B C; D E F] - a similarity (rotation,
// uniform scale, translation), with a negative determinant for a reflection.
public readonly struct HatTransform {
	public readonly double A, B, C, D, E, F;

	public HatTransform(double a, double b, double c, double d, double e, double f) {
		A = a;
		B = b;
		C = c;
		D = d;
		E = e;
		F = f;
	}

	public static readonly HatTransform Identity = new HatTransform(1, 0, 0, 0, 1, 0);

	public double Determinant => A * E - B * D;

	// Uniform scale factor - every transform here is a similarity.
	public double Scale => Math.Sqrt(Math.Abs(Determinant));

	public HatTransform Inverse() {
		double det = Determinant;
		return new HatTransform(E / det, -B / det, (B * F - C * E) / det, -D / det, A / det, (C * D - A * F) / det);
	}

	public static HatTransform operator *(HatTransform x, HatTransform y) => new HatTransform(
		x.A * y.A + x.B * y.D,
		x.A * y.B + x.B * y.E,
		x.A * y.C + x.B * y.F + x.C,
		x.D * y.A + x.E * y.D,
		x.D * y.B + x.E * y.E,
		x.D * y.C + x.E * y.F + x.F);

	public HatPoint Apply(HatPoint p) => new HatPoint(A * p.X + B * p.Y + C, D * p.X + E * p.Y + F);

	public static HatTransform Translation(double tx, double ty) => new HatTransform(1, 0, tx, 0, 1, ty);

	public static HatTransform Rotation(double angle) {
		double c = Math.Cos(angle);
		double s = Math.Sin(angle);
		return new HatTransform(c, -s, 0, s, c, 0);
	}

	public static HatTransform RotationAbout(HatPoint p, double angle) =>
		Translation(p.X, p.Y) * (Rotation(angle) * Translation(-p.X, -p.Y));

	// Maps the unit interval onto the segment p->q.
	private static HatTransform MatchSegment(HatPoint p, HatPoint q) =>
		new HatTransform(q.X - p.X, p.Y - q.Y, p.X, q.Y - p.Y, q.X - p.X, p.Y);

	// Maps segment p1->q1 onto segment p2->q2.
	public static HatTransform MatchTwo(HatPoint p1, HatPoint q1, HatPoint p2, HatPoint q2) =>
		MatchSegment(p2, q2) * MatchSegment(p1, q1).Inverse();
}

// Which kind of metatile a hat was placed by - the paper's colour key. H1 is the one
// reflected hat in every H metatile; every other hat is unreflected.
public enum HatLabel {
	H1,
	H,
	T,
	P,
	F,
}

public abstract class HatGeometry {
}

// The bottom level of the hierarchy: one hat, drawn as HatGenerator.HatOutline under
// whatever transform its parents compose.
public sealed class HatTile : HatGeometry {
	public readonly HatLabel Label;

	public HatTile(HatLabel label) {
		Label = label;
	}
}

// A group of transformed children (metatiles, or hats at the lowest level) plus the
// outline the substitution rules glue neighbouring metatiles along.
public sealed class HatMetaTile : HatGeometry {
	public sealed class Child {
		public HatTransform T;
		public HatGeometry Geom;
	}

	public readonly HatPoint[] Shape;
	public readonly List<Child> Children = new List<Child>();

	public HatMetaTile(HatPoint[] shape) {
		Shape = shape;
	}

	public void AddChild(HatTransform t, HatGeometry geom) => Children.Add(new Child { T = t, Geom = geom });

	public HatPoint EvalChild(int n, int i) => Children[n].T.Apply(((HatMetaTile)Children[n].Geom).Shape[i]);

	// Moves the outline's vertex average to the origin, carrying the children along.
	public void Recentre() {
		double cx = 0.0, cy = 0.0;
		foreach (HatPoint p in Shape) {
			cx += p.X;
			cy += p.Y;
		}
		cx /= Shape.Length;
		cy /= Shape.Length;
		var tr = new HatPoint(-cx, -cy);
		for (int idx = 0; idx < Shape.Length; idx++)
			Shape[idx] = Shape[idx] + tr;
		HatTransform m = HatTransform.Translation(-cx, -cy);
		foreach (Child ch in Children)
			ch.T = m * ch.T;
	}
}

// One hat of a flattened patch: its label, and the transform that takes
// HatGenerator.HatOutline to where it sits.
public readonly struct PlacedHat {
	public readonly HatLabel Label;
	public readonly HatTransform Transform;

	public PlacedHat(HatLabel label, HatTransform transform) {
		Label = label;
		Transform = transform;
	}

	public bool IsReflected => Transform.Determinant < 0.0;
}

public static class HatGenerator {
	public const double Root3 = 1.7320508075688772;
	public const double HalfRoot3 = 0.8660254037844386;

	// The hat itself, as 13 corners on the hexPt lattice: 8 kites of the
	// deltoidal trihexagonal grid. Its sides are 1 or sqrt(3) long, except
	// the one from corner 2 to corner 3, which is 2 - two short kite edges in
	// a straight line (see StraightCornerAfter).
	public static readonly HatPoint[] HatOutline = {
		HatPoint.Hex(0, 0), HatPoint.Hex(-1, -1), HatPoint.Hex(0, -2), HatPoint.Hex(2, -2),
		HatPoint.Hex(2, -1), HatPoint.Hex(4, -2), HatPoint.Hex(5, -1), HatPoint.Hex(4, 0),
		HatPoint.Hex(3, 0), HatPoint.Hex(2, 2), HatPoint.Hex(0, 3), HatPoint.Hex(0, 2),
		HatPoint.Hex(-1, 2),
	};

	// HatOutline's long side (corner 2 to 3) has a kite-grid vertex at its
	// midpoint. Neighbouring hats can have a real corner there, so a board that
	// matches tiles edge to edge needs that point as a corner of this hat too.
	public const int StraightCornerAfter = 2;
	public static readonly HatPoint StraightCorner = HatPoint.Hex(1, -2);

	private static readonly HatTile H1Hat = new HatTile(HatLabel.H1);
	private static readonly HatTile HHat = new HatTile(HatLabel.H);
	private static readonly HatTile THat = new HatTile(HatLabel.T);
	private static readonly HatTile PHat = new HatTile(HatLabel.P);
	private static readonly HatTile FHat = new HatTile(HatLabel.F);

	private static HatMetaTile InitH() {
		HatPoint[] outline = {
			new HatPoint(0, 0), new HatPoint(4, 0), new HatPoint(4.5, HalfRoot3),
			new HatPoint(2.5, 5 * HalfRoot3), new HatPoint(1.5, 5 * HalfRoot3), new HatPoint(-0.5, HalfRoot3),
		};
		var meta = new HatMetaTile(outline);
		meta.AddChild(HatTransform.MatchTwo(HatOutline[5], HatOutline[7], outline[5], outline[0]), HHat);
		meta.AddChild(HatTransform.MatchTwo(HatOutline[9], HatOutline[11], outline[1], outline[2]), HHat);
		meta.AddChild(HatTransform.MatchTwo(HatOutline[5], HatOutline[7], outline[3], outline[4]), HHat);
		meta.AddChild(
			HatTransform.Translation(2.5, HalfRoot3)
				* (new HatTransform(-0.5, -HalfRoot3, 0, HalfRoot3, -0.5, 0)
					* new HatTransform(0.5, 0, 0, 0, -0.5, 0)),
			H1Hat);
		return meta;
	}

	private static HatMetaTile InitT() {
		HatPoint[] outline = { new HatPoint(0, 0), new HatPoint(3, 0), new HatPoint(1.5, 3 * HalfRoot3) };
		var meta = new HatMetaTile(outline);
		meta.AddChild(new HatTransform(0.5, 0, 0.5, 0, 0.5, HalfRoot3), THat);
		return meta;
	}

	private static HatMetaTile InitP() {
		HatPoint[] outline = { new HatPoint(0, 0), new HatPoint(4, 0), new HatPoint(3, 2 * HalfRoot3), new HatPoint(-1, 2 * HalfRoot3) };
		var meta = new HatMetaTile(outline);
		meta.AddChild(new HatTransform(0.5, 0, 1.5, 0, 0.5, HalfRoot3), PHat);
		meta.AddChild(
			HatTransform.Translation(0, 2 * HalfRoot3)
				* (new HatTransform(0.5, HalfRoot3, 0, -HalfRoot3, 0.5, 0)
					* new HatTransform(0.5, 0.0, 0.0, 0.0, 0.5, 0.0)),
			PHat);
		return meta;
	}

	private static HatMetaTile InitF() {
		HatPoint[] outline = {
			new HatPoint(0, 0), new HatPoint(3, 0), new HatPoint(3.5, HalfRoot3),
			new HatPoint(3, 2 * HalfRoot3), new HatPoint(-1, 2 * HalfRoot3),
		};
		var meta = new HatMetaTile(outline);
		meta.AddChild(new HatTransform(0.5, 0, 1.5, 0, 0.5, HalfRoot3), FHat);
		meta.AddChild(
			HatTransform.Translation(0, 2 * HalfRoot3)
				* (new HatTransform(0.5, HalfRoot3, 0, -HalfRoot3, 0.5, 0)
					* new HatTransform(0.5, 0.0, 0.0, 0.0, 0.5, 0.0)),
			FHat);
		return meta;
	}

	// hatviz's constructPatch rules, verbatim. Each rule adds one metatile to the
	// patch: { shape } places the first one; { a, e, shape, f } glues edge f of the
	// new metatile to edge e of child a; { a, i, b, j, shape, f } glues edge f of
	// the new one between corner i of child a and corner j of child b.
	private static readonly object[][] PatchRules = {
		new object[] { 'H' },
		new object[] { 0, 0, 'P', 2 },
		new object[] { 1, 0, 'H', 2 },
		new object[] { 2, 0, 'P', 2 },
		new object[] { 3, 0, 'H', 2 },
		new object[] { 4, 4, 'P', 2 },
		new object[] { 0, 4, 'F', 3 },
		new object[] { 2, 4, 'F', 3 },
		new object[] { 4, 1, 3, 2, 'F', 0 },
		new object[] { 8, 3, 'H', 0 },
		new object[] { 9, 2, 'P', 0 },
		new object[] { 10, 2, 'H', 0 },
		new object[] { 11, 4, 'P', 2 },
		new object[] { 12, 0, 'H', 2 },
		new object[] { 13, 0, 'F', 3 },
		new object[] { 14, 2, 'F', 1 },
		new object[] { 15, 3, 'H', 4 },
		new object[] { 8, 2, 'F', 1 },
		new object[] { 17, 3, 'H', 0 },
		new object[] { 18, 2, 'P', 0 },
		new object[] { 19, 2, 'H', 2 },
		new object[] { 20, 4, 'F', 3 },
		new object[] { 20, 0, 'P', 2 },
		new object[] { 22, 0, 'H', 2 },
		new object[] { 23, 4, 'F', 3 },
		new object[] { 23, 0, 'F', 3 },
		new object[] { 16, 0, 'P', 2 },
		new object[] { 9, 4, 0, 2, 'T', 2 },
		new object[] { 4, 0, 'F', 3 },
	};

	private static HatMetaTile ConstructPatch(HatMetaTile h, HatMetaTile t, HatMetaTile p, HatMetaTile f) {
		var ret = new HatMetaTile(Array.Empty<HatPoint>());
		var shapes = new Dictionary<char, HatMetaTile> { { 'H', h }, { 'T', t }, { 'P', p }, { 'F', f } };

		foreach (object[] r in PatchRules) {
			if (r.Length == 1) {
				ret.AddChild(HatTransform.Identity, shapes[(char)r[0]]);
			}
			else if (r.Length == 4) {
				HatMetaTile.Child parent = ret.Children[(int)r[0]];
				HatPoint[] poly = ((HatMetaTile)parent.Geom).Shape;
				int edge = (int)r[1];
				HatPoint pp = parent.T.Apply(poly[(edge + 1) % poly.Length]);
				HatPoint qq = parent.T.Apply(poly[edge]);
				HatMetaTile nshp = shapes[(char)r[2]];
				HatPoint[] npoly = nshp.Shape;
				int nedge = (int)r[3];
				ret.AddChild(HatTransform.MatchTwo(npoly[nedge], npoly[(nedge + 1) % npoly.Length], pp, qq), nshp);
			}
			else {
				HatMetaTile.Child chP = ret.Children[(int)r[0]];
				HatMetaTile.Child chQ = ret.Children[(int)r[2]];
				HatPoint pp = chQ.T.Apply(((HatMetaTile)chQ.Geom).Shape[(int)r[3]]);
				HatPoint qq = chP.T.Apply(((HatMetaTile)chP.Geom).Shape[(int)r[1]]);
				HatMetaTile nshp = shapes[(char)r[4]];
				HatPoint[] npoly = nshp.Shape;
				int nedge = (int)r[5];
				ret.AddChild(HatTransform.MatchTwo(npoly[nedge], npoly[(nedge + 1) % npoly.Length], pp, qq), nshp);
			}
		}
		return ret;
	}

	private static HatPoint Intersect(HatPoint p1, HatPoint q1, HatPoint p2, HatPoint q2) {
		double d = (q2.Y - p2.Y) * (q1.X - p1.X) - (q2.X - p2.X) * (q1.Y - p1.Y);
		double uA = ((q2.X - p2.X) * (p1.Y - p2.Y) - (q2.Y - p2.Y) * (p1.X - p2.X)) / d;
		return new HatPoint(p1.X + uA * (q1.X - p1.X), p1.Y + uA * (q1.Y - p1.Y));
	}

	// hatviz's constructMetatiles: the next level's H/T/P/F outlines are read off
	// specific points of the patch, and each takes a fixed subset of its children.
	private static HatMetaTile[] ConstructMetaTiles(HatMetaTile patch) {
		HatPoint bps1 = patch.EvalChild(8, 2);
		HatPoint bps2 = patch.EvalChild(21, 2);
		HatPoint rbps = HatTransform.RotationAbout(bps1, -2.0 * Math.PI / 3.0).Apply(bps2);

		HatPoint p72 = patch.EvalChild(7, 2);
		HatPoint p252 = patch.EvalChild(25, 2);

		HatPoint llc = Intersect(bps1, rbps, patch.EvalChild(6, 2), p72);
		HatPoint w = patch.EvalChild(6, 2) - llc;

		var newHOutline = new List<HatPoint> { llc, bps1 };
		w = HatTransform.Rotation(-Math.PI / 3.0).Apply(w);
		newHOutline.Add(newHOutline[1] + w);
		newHOutline.Add(patch.EvalChild(14, 2));
		w = HatTransform.Rotation(-Math.PI / 3.0).Apply(w);
		newHOutline.Add(newHOutline[3] - w);
		newHOutline.Add(patch.EvalChild(6, 2));

		var newH = new HatMetaTile(newHOutline.ToArray());
		foreach (int ch in new[] { 0, 9, 16, 27, 26, 6, 1, 8, 10, 15 })
			newH.AddChild(patch.Children[ch].T, patch.Children[ch].Geom);

		HatPoint[] newPOutline = { p72, p72 + (bps1 - llc), bps1, llc };
		var newP = new HatMetaTile(newPOutline);
		foreach (int ch in new[] { 7, 2, 3, 4, 28 })
			newP.AddChild(patch.Children[ch].T, patch.Children[ch].Geom);

		HatPoint[] newFOutline = {
			bps2, patch.EvalChild(24, 2), patch.EvalChild(25, 0),
			p252, p252 + (llc - bps1),
		};
		var newF = new HatMetaTile(newFOutline);
		foreach (int ch in new[] { 21, 20, 22, 23, 24, 25 })
			newF.AddChild(patch.Children[ch].T, patch.Children[ch].Geom);

		HatPoint aaa = newHOutline[2];
		HatPoint bbb = newHOutline[1] + (newHOutline[4] - newHOutline[5]);
		HatPoint ccc = HatTransform.RotationAbout(bbb, -Math.PI / 3.0).Apply(aaa);
		HatPoint[] newTOutline = { bbb, ccc, aaa };
		var newT = new HatMetaTile(newTOutline);
		newT.AddChild(patch.Children[11].T, patch.Children[11].Geom);

		newH.Recentre();
		newP.Recentre();
		newF.Recentre();
		newT.Recentre();

		return new[] { newH, newT, newP, newF };
	}

	// The H supertile after `substitutions` rounds of constructPatch +
	// constructMetatiles, centred on its outline's vertex average. Each round
	// multiplies the hat count by about 6.85 (phi^4): 4, 25, 169, 1156, 7921, ...
	public static HatMetaTile BuildSupertile(int substitutions) {
		HatMetaTile[] tiles = { InitH(), InitT(), InitP(), InitF() };
		for (int i = 0; i < substitutions; i++)
			tiles = ConstructMetaTiles(ConstructPatch(tiles[0], tiles[1], tiles[2], tiles[3]));
		return tiles[0];
	}

	// Every hat of `root` (under `rootTransform`) that could reach within
	// `radius` of `center`, without walking into any metatile that can't -
	// a deep supertile holds tens of thousands of hats, but only the few hundred
	// near the cut are ever needed.
	public static List<PlacedHat> Flatten(HatMetaTile root, HatTransform rootTransform, HatPoint center, double radius) {
		var bounds = new Dictionary<HatGeometry, (HatPoint Center, double Radius)>();
		var result = new List<PlacedHat>();
		Visit(root, rootTransform);
		return result;

		void Visit(HatGeometry geom, HatTransform m) {
			(HatPoint localCenter, double localRadius) = BoundOf(geom, bounds);
			HatPoint worldCenter = m.Apply(localCenter);
			if ((worldCenter - center).Length > radius + localRadius * m.Scale)
				return;
			if (geom is HatTile tile) {
				result.Add(new PlacedHat(tile.Label, m));
				return;
			}
			foreach (HatMetaTile.Child child in ((HatMetaTile)geom).Children)
				Visit(child.Geom, m * child.T);
		}
	}

	// A circle (in geom's own coordinates) containing every hat under geom -
	// built bottom-up from the hat's own bounding circle, since a metatile's
	// hats poke out past its outline.
	private static (HatPoint Center, double Radius) BoundOf(HatGeometry geom, Dictionary<HatGeometry, (HatPoint, double)> cache) {
		if (cache.TryGetValue(geom, out (HatPoint, double) known))
			return known;
		(HatPoint, double) bound;
		if (geom is HatTile) {
			HatPoint c = Average(HatOutline);
			double r = 0.0;
			foreach (HatPoint p in HatOutline)
				r = Math.Max(r, (p - c).Length);
			bound = (c, r);
		}
		else {
			var meta = (HatMetaTile)geom;
			var childCenters = new HatPoint[meta.Children.Count];
			var childRadii = new double[meta.Children.Count];
			for (int i = 0; i < meta.Children.Count; i++) {
				HatMetaTile.Child child = meta.Children[i];
				(HatPoint cc, double cr) = BoundOf(child.Geom, cache);
				childCenters[i] = child.T.Apply(cc);
				childRadii[i] = cr * child.T.Scale;
			}
			HatPoint c = Average(childCenters);
			double r = 0.0;
			for (int i = 0; i < childCenters.Length; i++)
				r = Math.Max(r, (childCenters[i] - c).Length + childRadii[i]);
			bound = (c, r);
		}
		cache[geom] = bound;
		return bound;
	}

	private static HatPoint Average(IReadOnlyList<HatPoint> points) {
		double x = 0.0, y = 0.0;
		foreach (HatPoint p in points) {
			x += p.X;
			y += p.Y;
		}
		return new HatPoint(x / points.Count, y / points.Count);
	}

	// HatOutline with StraightCorner inserted - the 14 corners a hat has on a board
	// matched edge to edge (see StraightCornerAfter), in HatOutline's own order.
	public static HatPoint[] OutlineWithStraightCorner() {
		var corners = new HatPoint[HatOutline.Length + 1];
		for (int i = 0, j = 0; i < HatOutline.Length; i++) {
			corners[j++] = HatOutline[i];
			if (i == StraightCornerAfter)
				corners[j++] = StraightCorner;
		}
		return corners;
	}

	// The outline's inside point farthest from its edges (its "pole of
	// inaccessibility") and that distance - the roomiest spot for a tower's
	// glyph or an enemy's path to pass through, which a non-convex hat's vertex
	// average or area centroid doesn't guarantee. Found by a coarse grid search
	// refined around the best sample; it only ever runs on HatOutline, once.
	public static (HatPoint Center, double Clearance) VisualCenter(IReadOnlyList<HatPoint> outline) {
		double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
		double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
		foreach (HatPoint p in outline) {
			minX = Math.Min(minX, p.X);
			minY = Math.Min(minY, p.Y);
			maxX = Math.Max(maxX, p.X);
			maxY = Math.Max(maxY, p.Y);
		}
		var best = new HatPoint((minX + maxX) / 2.0, (minY + maxY) / 2.0);
		double bestClearance = double.NegativeInfinity;
		double step = Math.Max(maxX - minX, maxY - minY) / 64.0;
		for (double y = minY; y <= maxY; y += step) {
			for (double x = minX; x <= maxX; x += step) {
				var p = new HatPoint(x, y);
				double d = SignedClearance(outline, p);
				if (d > bestClearance) {
					bestClearance = d;
					best = p;
				}
			}
		}
		for (int round = 0; round < 24; round++) {
			step /= 2.0;
			HatPoint roundBest = best;
			for (int dy = -2; dy <= 2; dy++) {
				for (int dx = -2; dx <= 2; dx++) {
					var p = new HatPoint(best.X + dx * step, best.Y + dy * step);
					double d = SignedClearance(outline, p);
					if (d > bestClearance) {
						bestClearance = d;
						roundBest = p;
					}
				}
			}
			best = roundBest;
		}
		return (best, bestClearance);
	}

	// Distance from p to the outline's nearest edge - negative if p is outside.
	private static double SignedClearance(IReadOnlyList<HatPoint> outline, HatPoint p) {
		double minDist = double.PositiveInfinity;
		bool inside = false;
		for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++) {
			HatPoint a = outline[j];
			HatPoint b = outline[i];
			if ((b.Y > p.Y) != (a.Y > p.Y) && p.X < (a.X - b.X) * (p.Y - b.Y) / (a.Y - b.Y) + b.X)
				inside = !inside;
			HatPoint ab = b - a;
			double t = Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / (ab.X * ab.X + ab.Y * ab.Y), 0.0, 1.0);
			minDist = Math.Min(minDist, (p - (a + ab * t)).Length);
		}
		return inside ? minDist : -minDist;
	}
}
