using Godot;
using System.Collections.Generic;

// The one boss with two independent health pools instead of one shared bar - physical
// damage only ever drains PhysicalHealth, elemental damage only ever drains
// ElementalHealth (see ApplyDamage), and it isn't dead until BOTH hit zero (see
// IsDepleted). Lean entirely on one damage type and it stalls forever at half health.
public partial class TitanBoss : BossEnemy {
	private static readonly Color PhysicalPoolColor = new Color(0.8f, 0.7f, 0.25f);
	private static readonly Color ElementalPoolColor = new Color(0.3f, 0.75f, 0.95f);

	private float _physicalHealth = -1.0f;
	private float _elementalHealth = -1.0f;

	public TitanBoss() {
		enemy_type = "boss_titan";
	}

	protected override float BaseHealth => 1000.0f * (1.0f + 0.5f * boss_index);
	protected override float BaseSpeed => 40.0f;
	protected override int BaseReward => (int)(110 * (1.0f + 0.35f * boss_index));
	protected override Color BaseColor => new Color(0.5f, 0.2f, 0.7f);
	protected override float BaseRadius => 28.0f;
	protected override string DisplayName => "Titan";
	protected override string Flavor => "Two independent health pools, one physical, one elemental - both must be drained to kill it.";

	// Split lazily on first hit rather than in setup() so it still reflects whatever
	// enemy_health_curse already did to max_health by the time damage actually starts
	// landing, instead of a snapshot taken before that curse is applied.
	private void EnsurePools() {
		if (_physicalHealth >= 0.0f)
			return;
		_physicalHealth = max_health * 0.5f;
		_elementalHealth = max_health * 0.5f;
	}

	protected override void ApplyDamage(float amount, DamageType type) {
		EnsurePools();
		if (type == DamageType.Physical)
			_physicalHealth = Mathf.Max(0.0f, _physicalHealth - amount);
		else
			_elementalHealth = Mathf.Max(0.0f, _elementalHealth - amount);
	}

	protected override bool IsDepleted() {
		EnsurePools();
		return _physicalHealth <= 0.0f && _elementalHealth <= 0.0f;
	}

	protected override void DrawBody() {
		DrawCircle(Vector2.Zero, radius, color);
		DrawArc(Vector2.Zero, radius * 0.75f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 16, PhysicalPoolColor, 7.0f);
		DrawArc(Vector2.Zero, radius * 0.75f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 16, ElementalPoolColor, 7.0f);
		DrawBossCrown();
	}

	// Replaces the single shared health bar from BaseEnemy._Draw entirely (rather
	// than layering on top of it) since that bar reads the un-split `health` field,
	// which nothing here ever touches and would otherwise sit frozen at full width.
	public override void _Draw() {
		DrawBody();
		DrawArc(Vector2.Zero, radius, 0.0f, Mathf.Tau, 24, Colors.Black, 1.5f);
		EnsurePools();
		float halfMax = Mathf.Max(max_health * 0.5f, 0.01f);
		DrawPoolBar(-radius - 18.0f, PhysicalPoolColor, Mathf.Clamp(_physicalHealth / halfMax, 0.0f, 1.0f));
		DrawPoolBar(-radius - 12.0f, ElementalPoolColor, Mathf.Clamp(_elementalHealth / halfMax, 0.0f, 1.0f));
	}

	// Overrides the base single "Health: x / y" line with its own two pools, since
	// take_damage's shared `health` field is never touched here (see ApplyDamage).
	public override TowerInfo GetInfo() {
		EnsurePools();
		float halfMax = max_health * 0.5f;
		var lines = new List<string> {
			$"Physical HP: {Mathf.CeilToInt(Mathf.Max(_physicalHealth, 0.0f))} / {(int)halfMax}",
			$"Elemental HP: {Mathf.CeilToInt(Mathf.Max(_elementalHealth, 0.0f))} / {(int)halfMax}",
			$"Speed: {speed:F0}",
			$"Reward: {reward}g",
			$"Damage to lives if it escapes: {damage_to_lives}",
			"Immune to being finished off by one damage type alone - both pools must reach zero.",
		};
		return new TowerInfo(DisplayName, Flavor, lines, new List<string>());
	}

	// Static reference version for the wiki (see BaseEnemy.GetPreviewInfo) - same
	// even split, just off BaseHealth instead of a spawned instance's max_health.
	public override TowerInfo GetPreviewInfo() {
		float halfMax = BaseHealth * 0.5f;
		var lines = new List<string> {
			$"Physical HP: {(int)halfMax}",
			$"Elemental HP: {(int)halfMax}",
			$"Speed: {BaseSpeed:F0}",
			$"Reward: {BaseReward}g",
			$"Damage to lives if it escapes: {BaseDamageToLives}",
			"Immune to being finished off by one damage type alone - both pools must reach zero.",
		};
		return new TowerInfo(DisplayName, Flavor, lines, new List<string>());
	}

	private void DrawPoolBar(float yOffset, Color fillColor, float ratio) {
		float barW = radius * 2.2f;
		float barH = 4.0f;
		Vector2 barPos = new Vector2(-barW / 2.0f, yOffset);
		DrawRect(new Rect2(barPos, new Vector2(barW, barH)), new Color(0, 0, 0, 0.6f));
		DrawRect(new Rect2(barPos, new Vector2(barW * ratio, barH)), fillColor);
	}
}
