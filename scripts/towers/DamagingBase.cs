using Godot.Collections;

public abstract partial class DamagingBase : BaseTower {
	protected float Damage;

	protected virtual float BaseDamage => 0.0f;

	// Which of the two resistance buckets this tower's damage falls into (see
	// DamageType) - fixed per tower type, not something a buff/curse ever changes.
	protected virtual DamageType TowerDamageType => DamageType.Physical;

	protected override void InitializeStats() {
		base.InitializeStats();
		Damage = BaseDamage;
	}

	protected override void ApplyToDamage(float mult) => Damage *= mult;

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["damage"] = BaseDamage;
		data["damage_type"] = TowerDamageType.ToString();
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["damage"] = Damage;
		data["damage_type"] = TowerDamageType.ToString();
	}
}
