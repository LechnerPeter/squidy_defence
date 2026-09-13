using Godot;
using System.Collections.Generic;

public partial class BouncingProjectile : Projectile {
	private int _bouncesRemaining = 0;
	private float _bounceRange = 0.0f;
	private float _bounceDamageFalloff = 1.0f;
	private readonly HashSet<BaseEnemy> _hitEnemies = new HashSet<BaseEnemy>();

	protected override void InitializeFeatureStats(ProjectileStats stats) {
		base.InitializeFeatureStats(stats);
		_bouncesRemaining = stats.MaxBounces;
		_bounceRange = stats.BounceRange;
		_bounceDamageFalloff = stats.BounceDamageFalloff;
	}

	protected override bool TryContinueFlight(Vector2 hitPos) {
		if (_bouncesRemaining <= 0)
			return false;
		_hitEnemies.Add(Target);
		GlobalPosition = hitPos;
		BaseEnemy next = FindNextBounceTarget(hitPos);
		if (next == null)
			return false;
		_bouncesRemaining--;
		Damage *= _bounceDamageFalloff;
		Target = next;
		_hitEnemies.Add(next);
		Facing = (next.GlobalPosition - hitPos).Normalized();
		return true;
	}

	// Prefers the nearest un-hit enemy within _bounceRange that's closer to the start
	// of the path than the one just hit (i.e. bounces back toward the entrance);
	// falls back to the nearest un-hit enemy in range at all when none qualify, so
	// the chain doesn't just fizzle out at the front of the pack.
	private BaseEnemy FindNextBounceTarget(Vector2 fromPos) {
		float currentDistance = Target.DistanceTraveled;
		BaseEnemy bestBackward = null;
		float bestBackwardDist = _bounceRange;
		BaseEnemy bestAny = null;
		float bestAnyDist = _bounceRange;
		foreach (Node node in GetTree().GetNodesInGroup("enemies")) {
			if (node is not BaseEnemy enemy || !IsInstanceValid(enemy) || _hitEnemies.Contains(enemy))
				continue;
			float dist = enemy.GlobalPosition.DistanceTo(fromPos);
			if (dist > _bounceRange)
				continue;
			if (dist <= bestAnyDist) {
				bestAnyDist = dist;
				bestAny = enemy;
			}
			if (enemy.DistanceTraveled < currentDistance && dist <= bestBackwardDist) {
				bestBackwardDist = dist;
				bestBackward = enemy;
			}
		}
		return bestBackward ?? bestAny;
	}
}
