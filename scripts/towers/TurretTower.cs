using Godot;

public partial class TurretTower : AttackBase {
	public TurretTower() {
		tower_type = "basic";
	}

	protected override string BaseTowerName => "Turret";
	protected override int BaseCost => 50;
	protected override int BaseCostStep => 10;
	protected override float BaseRange => 130.0f;
	protected override float BaseFireRate => 1.2f;
	protected override float BaseDamage => 12.0f;
	protected override Color BaseColor => new Color(0.3f, 0.6f, 1.0f);
	protected override Color BaseProjectileColor => new Color(0.9f, 0.9f, 0.2f);
	protected override float BaseProjectileSpeed => 420.0f;
	protected override string BaseFlavor => "Reliable single-target turret. Cheap and steady.";
	protected override string BaseAbbreviation => "TUR";
}
