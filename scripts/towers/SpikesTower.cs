using Godot;

public partial class SpikesTower : ContinuousBase {
	public SpikesTower() {
		tower_type = "spikes";
	}

	protected override string BaseTowerName => "Spikes";
	protected override int BaseCost => 55;
	protected override int BaseCostStep => 11;
	protected override float BaseDamage => 14.0f;
	protected override float BaseSplashRadius => 26.0f;
	protected override Color BaseColor => new Color(0.5f, 0.42f, 0.35f);
	protected override bool BaseNonBlocking => true;
	protected override bool BaseContinuous => true;
	protected override float BaseMaxDamagePerRound => 140.0f;
	protected override string BaseFlavor => "Doesn't block the path — enemies walk right over it and take continuous damage.";
	protected override string BaseAbbreviation => "SPK";
}
