using Godot;

public partial class ExplodingBouncingProjectile : BouncingProjectile {
	private float _explosionRadius = 0.0f;

	protected override void InitializeFeatureStats(ProjectileStats stats) {
		base.InitializeFeatureStats(stats);
		_explosionRadius = stats.ExplosionRadius;
	}

	// Fires when the chain naturally ends (out of bounces or nothing left to bounce to),
	// bursting the current (possibly bounce-escalated) damage onto everything nearby,
	// including whatever was just hit directly.
	protected override void OnFlightConcluded(Vector2 finalHitPos) {
		base.OnFlightConcluded(finalHitPos);
		if (_explosionRadius <= 0.0f)
			return;
		SpawnAoeEffect(finalHitPos, _explosionRadius);
		foreach (Node node in GetTree().GetNodesInGroup("enemies")) {
			if (node is BaseEnemy enemy && IsInstanceValid(enemy) && enemy.GlobalPosition.DistanceTo(finalHitPos) <= _explosionRadius)
				enemy.take_damage(Damage, ProjectileDamageType);
		}
	}
}
