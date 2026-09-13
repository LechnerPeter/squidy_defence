using Godot;
using Godot.Collections;

public partial class TwinBoomerangTower : BoomerangBase {
	public TwinBoomerangTower() {
		tower_type = "twin_boomerang";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Twin Boomerang";
	protected override int BaseCost => 60;
	protected override int BaseCostStep => 12;
	protected override float BaseRange => 150.0f;
	protected override Color BaseColor => new Color(0.75f, 0.35f, 0.55f);
	protected override Color BaseProjectileColor => new Color(0.85f, 0.55f, 1.0f);
	protected override bool BaseBoomerang => true;
	protected override int MultishotArmsPerStack => 2;

	protected override Array BaseBoomerangProjectiles => new Array {
		new Dictionary {
			{ "key", "fire" },
			{ "damage", 20.0f },
			{ "splash_radius", 40.0f },
			{ "projectile_speed", 230.0f },
			{ "projectile_color", new Color(1.0f, 0.55f, 0.15f) },
			{ "orbit_duration", 0.3f },
			{ "orbit_radius", 14.0f },
		},
		new Dictionary {
			{ "key", "frost" },
			{ "damage", 8.0f },
			{ "on_hit_statuses", new Array { new Dictionary { { "type", "slow" }, { "magnitude", 0.45f }, { "duration", 1.5f } } } },
			{ "projectile_speed", 300.0f },
			{ "projectile_color", new Color(0.6f, 0.95f, 1.0f) },
			{ "orbit_duration", 0.3f },
			{ "orbit_radius", 22.0f },
		},
	};

	protected override string BaseFlavor => "1 Boomerang + 1 Cannon + 1 Frost in a triangle. Fires two independent boomerang shots at different speeds — a splashy firebomb and a chilling frost bolt, always out of sync with each other.";
	protected override string BaseAbbreviation => "TWIN";
}
