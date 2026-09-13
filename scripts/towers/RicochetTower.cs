using Godot;
using Godot.Collections;

public partial class RicochetTower : AttackBase {
	private static readonly PackedScene BouncingProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/BouncingProjectile.tscn");
	protected override PackedScene ProjectileScene => BouncingProjectileScene;

	protected int MaxBounces;
	protected float BounceRange;
	protected float BounceDamageFalloff;

	protected virtual int BaseMaxBounces => 3;
	protected virtual float BaseBounceRange => 90.0f;
	protected virtual float BaseBounceDamageFalloff => 0.75f;

	public RicochetTower() {
		tower_type = "ricochet";
	}

	protected override string BaseTowerName => "Ricochet Tower";
	protected override int BaseCost => 60;
	protected override int BaseCostStep => 12;
	protected override float BaseRange => 130.0f;
	protected override float BaseFireRate => 1.0f;
	protected override float BaseDamage => 10.0f;
	protected override Color BaseColor => new Color(0.3f, 0.85f, 0.55f);
	protected override Color BaseProjectileColor => new Color(0.6f, 1.0f, 0.4f);
	protected override float BaseProjectileSpeed => 360.0f;
	protected override string BaseFlavor => "Its shot ricochets from enemy to enemy, weakening slightly with each bounce, but never hitting the same enemy twice.";
	protected override string BaseAbbreviation => "RIC";

	protected override void InitializeStats() {
		base.InitializeStats();
		MaxBounces = BaseMaxBounces;
		BounceRange = BaseBounceRange;
		BounceDamageFalloff = BaseBounceDamageFalloff;
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["max_bounces"] = BaseMaxBounces;
		data["bounce_range"] = BaseBounceRange;
		data["bounce_damage_falloff"] = BaseBounceDamageFalloff;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["max_bounces"] = MaxBounces;
		data["bounce_range"] = BounceRange;
		data["bounce_damage_falloff"] = BounceDamageFalloff;
	}

	protected override ProjectileStats BuildProjectileStats() => base.BuildProjectileStats() with {
		MaxBounces = MaxBounces,
		BounceRange = BounceRange,
		BounceDamageFalloff = BounceDamageFalloff,
	};
}
