using Godot.Collections;

public abstract partial class DamageCapBase : DamagingBase {
	protected float MaxDamagePerRound;
	protected float DamageDealtThisRound = 0.0f;
	protected bool ActiveGlow = false;

	protected virtual float BaseMaxDamagePerRound => float.PositiveInfinity;

	protected override void InitializeStats() {
		base.InitializeStats();
		MaxDamagePerRound = BaseMaxDamagePerRound;
	}

	public override void reset_round_damage() {
		DamageDealtThisRound = 0.0f;
		QueueRedraw();
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["max_damage_per_round"] = BaseMaxDamagePerRound;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["max_damage_per_round"] = MaxDamagePerRound;
	}
}
