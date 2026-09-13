using Godot;

public partial class RockTower : BaseTower {
	// A fixed per-instance jitter (rather than a shared constant array) since each
	// rock now shrinks its own real tile shape toward center rather than redrawing a
	// universal regular hexagon - a rock still needs to look distinct from its
	// neighbors despite thin/thick tiles being real, non-uniform shapes. Generated
	// once here rather than in _Draw() so repeated redraws don't reshuffle it.
	private readonly float[] _cornerJitter = { 0.0f, 0.0f, 0.0f, 0.0f };

	public RockTower() {
		tower_type = "rock";
		for (int i = 0; i < _cornerJitter.Length; i++)
			_cornerJitter[i] = 0.55f + GD.Randf() * 0.3f;
	}

	protected override string BaseTowerName => "Rock";
	protected override Color BaseColor => new Color(0.42f, 0.4f, 0.38f);
	protected override string BaseFlavor => "Scattered terrain, already there when you arrive. Can't be built on or removed.";

	public override void _Draw() {
		Color c = TowerColor;
		Vector2[] corners = CellShapes[0].CornerOffsets;
		var pts = new Vector2[corners.Length];
		for (int i = 0; i < pts.Length; i++)
			pts[i] = corners[i] * _cornerJitter[i];
		DrawColoredPolygon(pts, c);
		for (int i = 0; i < pts.Length; i++)
			DrawLine(pts[i], pts[(i + 1) % pts.Length], new Color(0, 0, 0, 0.35f), 1.5f);
		float bodyRadius = CellSize * 0.7f;
		DrawCircle(new Vector2(-bodyRadius * 0.18f, -bodyRadius * 0.12f), bodyRadius * 0.2f, c.Lightened(0.2f));
	}
}
