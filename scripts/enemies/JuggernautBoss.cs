using Godot;

public partial class JuggernautBoss : BossEnemy {
	public JuggernautBoss() {
		enemy_type = "boss_juggernaut";
	}

	protected override float BaseHealth => 1200.0f * (1.0f + 0.5f * boss_index);
	protected override float BaseSpeed => 32.0f;
	protected override int BaseReward => (int)(95 * (1.0f + 0.35f * boss_index));
	protected override Color BaseColor => new Color(0.45f, 0.4f, 0.32f);
	protected override float BaseRadius => 30.0f;
	protected override string DisplayName => "Juggernaut";
	protected override string Flavor => "An armored boss - physical damage is only half as effective against it.";

	// Same stone-plated resistance as a Golem, just wearing a crown - half of any
	// kinetic hit is wasted on it, so elemental firepower matters more here.
	protected override float BaseResistanceTo(DamageType type) => type == DamageType.Physical ? 0.5f : 0.0f;

	protected override void DrawBody() {
		DrawCircle(Vector2.Zero, radius, color);
		DrawArc(Vector2.Zero, radius * 0.8f, 0.0f, Mathf.Tau, 6, new Color(0.2f, 0.17f, 0.1f, 0.9f), 6.0f);
		DrawBossCrown();
	}
}
