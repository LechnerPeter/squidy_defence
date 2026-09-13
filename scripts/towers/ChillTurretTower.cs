using Godot;
using System.Collections.Generic;

public partial class ChillTurretTower : AttackBase {
	public ChillTurretTower() {
		tower_type = "chill_turret";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Chill Turret";
	protected override int BaseCost => 35;
	protected override int BaseCostStep => 7;
	protected override float BaseRange => 140.0f;
	protected override float BaseFireRate => 2.2f;
	protected override float BaseDamage => 6.0f;
	protected override DamageType TowerDamageType => DamageType.Elemental;
	protected override List<StatusEffectState> BaseOnHitStatuses => new() { new SlowStatusEffectState { Magnitude = 0.35f, RemainingDuration = 1.2f } };
	protected override Color BaseColor => new Color(0.5f, 0.75f, 1.0f);
	protected override Color BaseProjectileColor => new Color(0.8f, 0.95f, 1.0f);
	protected override float BaseProjectileSpeed => 460.0f;
	protected override string BaseFlavor => "2 Turret + 1 Frost in a triangle. Fires fast enough to keep enemies slowed almost permanently.";
	protected override string BaseAbbreviation => "CHILL";
}
