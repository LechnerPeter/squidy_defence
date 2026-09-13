using Godot;

public partial class CannonTower : AttackBase {
	public CannonTower() {
		tower_type = "cannon";
	}

	protected override string BaseTowerName => "Fire Cannon";
	protected override int BaseCost => 90;
	protected override int BaseCostStep => 18;
	protected override float BaseRange => 110.0f;
	protected override float BaseFireRate => 0.6f;
	protected override float BaseDamage => 18.0f;
	protected override float BaseSplashRadius => 55.0f;
	protected override Color BaseColor => new Color(0.85f, 0.4f, 0.2f);
	protected override Color BaseProjectileColor => new Color(1.0f, 0.5f, 0.1f);
	protected override float BaseProjectileSpeed => 320.0f;
	protected override DamageType TowerDamageType => DamageType.Elemental;
	protected override string BaseFlavor => "Slow to fire, but the fireball scorches everything near the impact.";
	protected override string BaseAbbreviation => "CAN";
}
