using Godot;
using System.Collections.Generic;

public partial class LongRangeFrostTower : AttackBase {
	public LongRangeFrostTower() {
		tower_type = "long_range_frost";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Long Range Frost";
	protected override int BaseCost => 40;
	protected override int BaseCostStep => 8;
	protected override float BaseRange => 220.0f;
	protected override float BaseFireRate => 1.0f;
	protected override float BaseDamage => 6.0f;
	protected override DamageType TowerDamageType => DamageType.Elemental;
	protected override float BaseSplashRadius => 40.0f;
	protected override List<StatusEffectState> BaseOnHitStatuses => new() { new SlowStatusEffectState { Magnitude = 0.55f, RemainingDuration = 2.0f } };
	protected override Color BaseColor => new Color(0.35f, 0.55f, 0.95f);
	protected override Color BaseProjectileColor => new Color(0.6f, 0.95f, 1.0f);
	protected override float BaseProjectileSpeed => 420.0f;
	protected override string BaseFlavor => "2 Cannon + 1 Frost in a triangle. Reaches deep into the maze with a splash of chill.";
	protected override string BaseAbbreviation => "LR-FRO";
}
