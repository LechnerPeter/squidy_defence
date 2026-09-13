using Godot;

public partial class WispEnemy : BaseEnemy {
	public WispEnemy() {
		enemy_type = "wisp";
	}

	protected override float BaseHealth => 30.0f;
	protected override float BaseSpeed => 110.0f;
	protected override int BaseReward => 9;
	protected override Color BaseColor => new Color(0.72f, 0.62f, 0.95f);
	protected override float BaseRadius => 12.0f;
	protected override string DisplayName => "Wisp";
	protected override string Flavor => "A body of loose energy - half of any elemental hit passes right through it.";

	// A body of loose, half-there energy - a bolt or a chill bites into it just like
	// anything else, but a solid, physical hit passes half its force straight through.
	protected override float BaseResistanceTo(DamageType type) => type == DamageType.Elemental ? 0.5f : 0.0f;

	protected override void DrawBody() {
		DrawCircle(Vector2.Zero, radius, new Color(color.R, color.G, color.B, 0.35f));
		DrawCircle(Vector2.Zero, radius * 0.65f, color);
		DrawCircle(Vector2.Zero, radius * 0.28f, new Color(1.0f, 1.0f, 1.0f, 0.85f));
	}
}
