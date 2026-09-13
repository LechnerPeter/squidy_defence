using Godot;
using Godot.Collections;
using System.Collections.Generic;

public abstract partial class AttackBase : TargetingBase {
	protected Timer FireTimer;

	protected float FireRate;
	protected float SplashRadius;
	protected int MultishotStacks;

	protected virtual float BaseFireRate => 0.0f;
	protected virtual float BaseSplashRadius => 0.0f;

	public override void _Ready() {
		base._Ready();
		FireTimer = GetNodeOrNull<Timer>("FireTimer");
	}

	protected override void InitializeStats() {
		base.InitializeStats();
		FireRate = BaseFireRate;
		SplashRadius = BaseSplashRadius;
	}

	protected override void ConfigureCombat() {
		if (Range <= 0.0f || FireRate <= 0.0f)
			return;
		base.ConfigureCombat();
		FireTimer.WaitTime = 1.0 / FireRate;
		FireTimer.Timeout += OnFireTimerTimeout;
		FireTimer.Start();
	}

	protected override void ApplyToFireRate(float mult) {
		if (FireRate <= 0.0f)
			return;
		FireRate *= mult;
		if (FireTimer != null)
			FireTimer.WaitTime = 1.0 / FireRate;
	}

	protected override void ApplyToRange(float mult) {
		base.ApplyToRange(mult);
		if (SplashRadius > 0.0f)
			SplashRadius *= mult;
	}

	protected override void ApplyToAttackSpeed(float mult) => ApplyToFireRate(mult);

	protected override void ApplyToMultishot(int stackDelta) => MultishotStacks = Mathf.Max(0, MultishotStacks + stackDelta);

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["fire_rate"] = BaseFireRate;
		data["splash_radius"] = BaseSplashRadius;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["fire_rate"] = FireRate;
		data["splash_radius"] = SplashRadius;
	}

	protected override ProjectileStats BuildProjectileStats() => base.BuildProjectileStats() with { SplashRadius = SplashRadius };

	// Protected (not private) so a subclass with a non-standard ConfigureCombat (see
	// SniperTower, which skips the range-gated setup this class normally does) can
	// still wire the timer to this same handler.
	protected virtual void OnFireTimerTimeout() {
		List<BaseEnemy> targets = GetTargets(1 + MultishotStacks);
		if (targets.Count == 0)
			return;
		ProjectileStats stats = BuildProjectileStats();
		foreach (BaseEnemy target in targets) {
			Projectile projectile = (Projectile)ProjectileScene.Instantiate();
			ProjectileContainer.AddChild(projectile);
			projectile.setup(GlobalPosition, target, stats);
		}
	}
}
