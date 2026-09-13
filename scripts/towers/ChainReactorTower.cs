using Godot;
using Godot.Collections;

public partial class ChainReactorTower : RicochetTower {
	private static readonly PackedScene ExplodingBouncingProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/ExplodingBouncingProjectile.tscn");
	protected override PackedScene ProjectileScene => ExplodingBouncingProjectileScene;

	protected float ExplosionRadius;

	protected virtual float BaseExplosionRadius => 50.0f;

	public ChainReactorTower() {
		tower_type = "chain_reactor";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Chain Reactor";
	protected override int BaseCost => 65;
	protected override int BaseCostStep => 13;
	protected override float BaseRange => 140.0f;
	protected override float BaseFireRate => 0.8f;
	protected override float BaseDamage => 8.0f;
	protected override int BaseMaxBounces => 4;
	protected override float BaseBounceRange => 100.0f;
	protected override float BaseBounceDamageFalloff => 1.3f;
	protected override Color BaseColor => new Color(0.85f, 0.35f, 0.55f);
	protected override Color BaseProjectileColor => new Color(1.0f, 0.75f, 0.3f);
	protected override string BaseFlavor => "1 Cannon + 1 Lightning + 1 Ricochet in a triangle. Each chained hit deals more damage than the last, and when the chain finally ends it detonates.";
	protected override string BaseAbbreviation => "CHAIN";

	protected override void InitializeStats() {
		base.InitializeStats();
		ExplosionRadius = BaseExplosionRadius;
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["explosion_radius"] = BaseExplosionRadius;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["explosion_radius"] = ExplosionRadius;
	}

	protected override ProjectileStats BuildProjectileStats() => base.BuildProjectileStats() with { ExplosionRadius = ExplosionRadius };
}
