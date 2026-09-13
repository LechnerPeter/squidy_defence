using Godot;

public partial class BoomerangTower : BoomerangBase {
	public BoomerangTower() {
		tower_type = "boomerang";
	}

	protected override string BaseTowerName => "Boomerang";
	protected override int BaseCost => 65;
	protected override int BaseCostStep => 13;
	protected override float BaseRange => 150.0f;
	protected override float BaseDamage => 26.0f;
	protected override Color BaseColor => new Color(0.65f, 0.35f, 0.85f);
	protected override Color BaseProjectileColor => new Color(0.85f, 0.55f, 1.0f);
	protected override float BaseProjectileSpeed => 260.0f;
	protected override bool BaseBoomerang => true;
	protected override float BaseOrbitDuration => 0.35f;
	protected override float BaseOrbitRadius => 16.0f;
	protected override string BaseFlavor => "Its shot flies out to the target, then flies all the way back before it can fire again — no fixed attack speed, just faster hits the closer the enemy is.";
	protected override string BaseAbbreviation => "BOOM";
}
