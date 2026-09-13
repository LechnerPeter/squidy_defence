using Godot;

public partial class CrusherTower : ContinuousBase {
	public CrusherTower() {
		tower_type = "crusher";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Crusher";
	protected override int BaseCost => 70;
	protected override int BaseCostStep => 14;
	protected override float BaseDamage => 45.0f;
	protected override float BaseSplashRadius => 46.0f;
	protected override Color BaseColor => new Color(0.55f, 0.25f, 0.2f);
	protected override bool BaseContinuous => true;
	protected override string BaseFlavor => "Cannon + Spikes + Cannon, in a row. A brutal 3-tile stretch of spikes reinforced by twin cannons.";
	protected override string BaseAbbreviation => "CRUSH";
}
