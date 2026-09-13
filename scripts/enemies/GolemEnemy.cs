using Godot;

public partial class GolemEnemy : BaseEnemy {
	public GolemEnemy() {
		enemy_type = "golem";
	}

	protected override float BaseHealth => 160.0f;
	protected override float BaseSpeed => 35.0f;
	protected override int BaseReward => 20;
	protected override int BaseDamageToLives => 2;
	protected override Color BaseColor => new Color(0.5f, 0.45f, 0.38f);
	protected override float BaseRadius => 20.0f;
	protected override string DisplayName => "Golem";
	protected override string Flavor => "Stone-plated - shrugs off half of any physical hit.";

	// Its stone plating shrugs off half of any kinetic/ballistic hit - lightning and
	// frost still bite it at full strength, so elemental towers matter more against a
	// Golem than another Turret or Cannon would.
	protected override float BaseResistanceTo(DamageType type) => type == DamageType.Physical ? 0.5f : 0.0f;

	protected override void DrawBody() {
		Vector2[] pts = new Vector2[4];
		for (int i = 0; i < 4; i++)
			pts[i] = new Vector2(radius, 0.0f).Rotated(Mathf.DegToRad(90.0f * i + 45.0f));
		DrawColoredPolygon(pts, color);
		Vector2[] core = new Vector2[4];
		for (int i = 0; i < 4; i++)
			core[i] = new Vector2(radius * 0.55f, 0.0f).Rotated(Mathf.DegToRad(90.0f * i + 45.0f));
		DrawColoredPolygon(core, color.Darkened(0.3f));
	}
}
