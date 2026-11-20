using Godot.Collections;

public abstract partial class DamagingBase : BaseTower {
	protected float Damage;

	protected virtual float BaseDamage => 0.0f;

	// Which of the two resistance buckets this tower's damage falls into (see
	// DamageType) - fixed per tower type, not something a buff/curse ever changes.
	protected virtual DamageType TowerDamageType => DamageType.Physical;

	// Which tile shape a base tower must stand on (see Main.TileShapeAllowed). Follows
	// damage type by default, but it's a separate choice - Fire Cannon deals Elemental
	// damage from a thick tile.
	protected virtual bool NeedsThinTile => TowerDamageType == DamageType.Elemental;

	protected override void InitializeStats() {
		base.InitializeStats();
		Damage = BaseDamage;
	}

	protected override void ApplyToDamage(float mult) => Damage *= mult;

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["damage"] = BaseDamage;
		data["damage_type"] = TowerDamageType.ToString();
		data["needs_thin_tile"] = NeedsThinTile;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["damage"] = Damage;
		data["damage_type"] = TowerDamageType.ToString();
		data["needs_thin_tile"] = NeedsThinTile;
	}
}
