using Godot;

public partial class RunnerEnemy : BaseEnemy {
	public RunnerEnemy() {
		enemy_type = "runner";
	}

	protected override float BaseHealth => 22.0f;
	protected override float BaseSpeed => 130.0f;
	protected override int BaseReward => 6;
	protected override Color BaseColor => new Color(0.9f, 0.8f, 0.1f);
	protected override float BaseRadius => 11.0f;
	protected override string DisplayName => "Runner";
	protected override string Flavor => "Fast and fragile - gets there quick unless something drops it first.";

	protected override void DrawBody() {
		float angle = _facing.Angle();
		Vector2[] pts = new Vector2[] {
			new Vector2(radius * 1.15f, 0.0f).Rotated(angle),
			new Vector2(-radius * 0.75f, -radius * 0.85f).Rotated(angle),
			new Vector2(-radius * 0.3f, 0.0f).Rotated(angle),
			new Vector2(-radius * 0.75f, radius * 0.85f).Rotated(angle),
		};
		DrawColoredPolygon(pts, color);
	}
}
