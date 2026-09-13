using Godot;

public partial class EelEnemy : BaseEnemy {
	public EelEnemy() {
		enemy_type = "eel";
	}

	protected override float BaseHealth => 55.0f;
	protected override float BaseSpeed => 95.0f;
	protected override int BaseReward => 10;
	protected override Color BaseColor => new Color(0.2f, 0.75f, 0.65f);
	protected override float BaseRadius => 13.0f;

	public override bool CanSwim => true;
	protected override string DisplayName => "Eel";
	protected override string Flavor => "Swims - the only thing on the field that can cross water.";

	protected override void DrawBody() {
		float angle = _facing.Angle();
		Vector2[] pts = new Vector2[] {
			new Vector2(radius * 1.4f, 0.0f).Rotated(angle),
			new Vector2(radius * 0.3f, -radius * 0.55f).Rotated(angle),
			new Vector2(-radius * 1.1f, -radius * 0.35f).Rotated(angle),
			new Vector2(-radius * 1.4f, 0.0f).Rotated(angle),
			new Vector2(-radius * 1.1f, radius * 0.35f).Rotated(angle),
			new Vector2(radius * 0.3f, radius * 0.55f).Rotated(angle),
		};
		DrawColoredPolygon(pts, color);
	}
}
