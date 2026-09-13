using Godot;
using Godot.Collections;

// What a tower hands a Projectile when firing - built by BaseTower.BuildProjectileStats()
// (each tier's override adding only the fields its own combat style needs, via `with`
// expressions), so Projectile.setup() reads typed fields directly instead of doing a
// ContainsKey/cast dance against a loosely-typed dictionary. OnHitStatuses stays as a
// Godot.Collections.Array of {type, magnitude, duration} dicts rather than a typed
// List<StatusEffectState>: boomerang arm profiles (see BoomerangBase.BaseBoomerangProjectiles)
// are themselves authored as raw dictionary literals, so that's the one place a Variant-shaped
// value is the more honest representation rather than an extra round-trip.
public readonly record struct ProjectileStats {
	public float Damage { get; init; }
	public DamageType DamageType { get; init; }
	public float ProjectileSpeed { get; init; } = 400.0f;
	public Color ProjectileColor { get; init; } = Colors.Yellow;
	public Array OnHitStatuses { get; init; } = new Array();
	public float SplashRadius { get; init; }
	public int MaxBounces { get; init; }
	public float BounceRange { get; init; }
	public float BounceDamageFalloff { get; init; } = 1.0f;
	public float ExplosionRadius { get; init; }
	public float OrbitDuration { get; init; }
	public float OrbitRadius { get; init; } = 16.0f;

	public ProjectileStats() {
	}
}
