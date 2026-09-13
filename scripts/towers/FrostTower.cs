using Godot;
using System.Collections.Generic;

public partial class FrostTower : AttackBase {
	public FrostTower() {
		tower_type = "frost";
	}

	protected override string BaseTowerName => "Frost Tower";
	protected override int BaseCost => 70;
	protected override int BaseCostStep => 14;
	protected override float BaseRange => 120.0f;
	protected override float BaseFireRate => 1.0f;
	protected override float BaseDamage => 4.0f;
	protected override DamageType TowerDamageType => DamageType.Elemental;
	protected override List<StatusEffectState> BaseOnHitStatuses => new() { new SlowStatusEffectState { Magnitude = 0.5f, RemainingDuration = 1.5f } };
	protected override Color BaseColor => new Color(0.4f, 0.85f, 0.9f);
	protected override Color BaseProjectileColor => new Color(0.6f, 0.95f, 1.0f);
	protected override float BaseProjectileSpeed => 380.0f;
	protected override string BaseFlavor => "Chills whatever it hits, slowing it down for others to finish off.";
	protected override string BaseAbbreviation => "FRO";
}
