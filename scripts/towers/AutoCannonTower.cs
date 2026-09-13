using Godot;

public partial class AutoCannonTower : AttackBase {
	public AutoCannonTower() {
		tower_type = "auto_cannon";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Auto Cannon";
	protected override int BaseCost => 45;
	protected override int BaseCostStep => 9;
	protected override float BaseRange => 130.0f;
	protected override float BaseFireRate => 1.6f;
	protected override float BaseDamage => 16.0f;
	protected override float BaseSplashRadius => 55.0f;
	protected override Color BaseColor => new Color(0.9f, 0.55f, 0.15f);
	protected override Color BaseProjectileColor => new Color(1.0f, 0.6f, 0.1f);
	protected override float BaseProjectileSpeed => 380.0f;
	protected override string BaseFlavor => "2 Turret + 1 Cannon in a triangle. Rapid-fire shells that still splash on impact.";
	protected override string BaseAbbreviation => "A-CAN";
}
