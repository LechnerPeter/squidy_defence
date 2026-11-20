using Godot;

public partial class RockTower : BaseTower {
	// A fixed per-instance jitter (rather than a shared constant array) since each
	// rock now shrinks its own real tile shape toward center rather than redrawing a
	// universal regular hexagon - a rock still needs to look distinct from its
	// neighbors despite thin/thick tiles being real, non-uniform shapes. Generated
	// once here and applied once at placement (see OnVisualPlaced).
	private readonly float[] _cornerJitter = { 0.0f, 0.0f, 0.0f, 0.0f };

	public RockTower() {
		tower_type = "rock";
		for (int i = 0; i < _cornerJitter.Length; i++)
			_cornerJitter[i] = 0.55f + GD.Randf() * 0.3f;
	}

	protected override string BaseTowerName => "Rock";
	protected override string BaseFlavor => "Scattered terrain, already there when you arrive. Can't be built on or removed.";

	protected override void OnVisualPlaced(Polygon2D body) {
		Vector2[] pts = body.Polygon;
		for (int i = 0; i < pts.Length; i++)
			pts[i] *= _cornerJitter[i];
		body.Polygon = pts;
		body.GetNode<Line2D>("Outline").Points = pts;
	}
}
