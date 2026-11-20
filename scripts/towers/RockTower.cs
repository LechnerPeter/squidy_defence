using Godot;

public partial class RockTower : BaseTower {
	public RockTower() {
		tower_type = "rock";
	}

	protected override string BaseTowerName => "Rock";
	protected override string BaseFlavor => "Scattered terrain, already there when you arrive. Can't be built on or removed.";

	// A fresh per-instance jitter (rather than a shared constant array) since each
	// rock shrinks its own real tile shape toward center rather than redrawing a
	// universal regular hexagon - a rock still needs to look distinct from its
	// neighbors despite the tiles being real, non-uniform shapes. One factor per
	// corner, however many the tile has: 4 for a rhombus, 14 for a hat. A hat isn't
	// star-shaped around its center, but corners pulled in by factors this close
	// together still never make its outline cross itself.
	protected override void OnVisualPlaced(Polygon2D body) {
		Vector2[] pts = body.Polygon;
		for (int i = 0; i < pts.Length; i++)
			pts[i] *= 0.55f + GD.Randf() * 0.3f;
		body.Polygon = pts;
		body.GetNode<Line2D>("Outline").Points = pts;
	}
}
