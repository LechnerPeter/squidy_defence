public class StatusEffectState {
	public float Magnitude;
	public float RemainingDuration;

	// Non-null only on leaf subclasses used as on-hit status definitions (see
	// BaseTower.OnHitStatuses) - it's the key BaseEnemy/BaseTower's status dictionaries
	// use to track and dispatch it. Statuses applied directly by curses/buffs don't need
	// a leaf subclass since their type key is already known at the call site.
	public virtual string Type => null;
}
