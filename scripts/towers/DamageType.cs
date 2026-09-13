// Every damage-dealing tower is one or the other (see DamagingBase.TowerDamageType) -
// an enemy can then resist one or the other by half (see BaseEnemy.ResistanceTo)
// without needing a whole new stacking-resistance system for what's still just two
// buckets. Physical is 0 so a tower that never overrides TowerDamageType, or a
// ProjectileStats built without setting DamageType, defaults to it.
public enum DamageType {
	Physical,
	Elemental,
}
