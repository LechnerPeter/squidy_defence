using Godot;
using Godot.Collections;

public partial class LightningFrostTower : LightningTower {
	protected float BaseSlow;
	protected float SlowRampPerSecond;
	protected float MaxSlow;

	public LightningFrostTower() {
		tower_type = "lightning_frost";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Lightning Frost";
	protected override int BaseCost => 55;
	protected override int BaseCostStep => 11;
	protected override float BaseRange => 150.0f;
	protected override float BaseDamage => 9.0f;
	protected virtual float BaseSlowStart => 0.15f;
	protected virtual float BaseSlowRampPerSecond => 0.08f;
	protected virtual float BaseMaxSlow => 0.6f;
	protected override Color BaseColor => new Color(0.45f, 0.65f, 0.9f);
	protected override string BaseFlavor => "2 Lightning + 1 Frost in a triangle. Its beam chills the target as it ramps up, slowing it more the longer the lock holds.";
	protected override string BaseAbbreviation => "L-FRO";

	protected override void InitializeStats() {
		base.InitializeStats();
		BaseSlow = BaseSlowStart;
		SlowRampPerSecond = BaseSlowRampPerSecond;
		MaxSlow = BaseMaxSlow;
	}

	private float CurrentSlowMagnitude(float timeOnTarget) => Mathf.Min(BaseSlow + SlowRampPerSecond * timeOnTarget, MaxSlow);

	protected override void OnBeamTick(BaseEnemy target, float timeOnTarget, float delta) {
		base.OnBeamTick(target, timeOnTarget, delta);
		target.apply_status("slow", CurrentSlowMagnitude(timeOnTarget), 0.5f);
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["base_slow_magnitude"] = BaseSlowStart;
		data["slow_ramp_per_second"] = BaseSlowRampPerSecond;
		data["max_slow_magnitude"] = BaseMaxSlow;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["base_slow_magnitude"] = BaseSlow;
		data["slow_ramp_per_second"] = SlowRampPerSecond;
		data["max_slow_magnitude"] = MaxSlow;
	}
}
