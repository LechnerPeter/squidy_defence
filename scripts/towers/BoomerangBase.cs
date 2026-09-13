using Godot;
using Godot.Collections;
using System.Collections.Generic;

public abstract partial class BoomerangBase : TargetingBase {
	private static readonly PackedScene BoomerangProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/BoomerangProjectile.tscn");
	protected override PackedScene ProjectileScene => BoomerangProjectileScene;

	private List<Dictionary> _arms = new List<Dictionary>();
	// Parallel to _arms (same index) - which enemy each arm is currently chasing,
	// or null while it's free. Kept outside the Godot Dictionary to avoid Variant
	// round-tripping just to read it back one frame later.
	private List<BaseEnemy> _armTargets = new List<BaseEnemy>();

	protected bool Boomerang;
	protected Array BoomerangProjectiles = new Array();
	protected float OrbitDuration;
	protected float OrbitRadius;

	protected virtual bool BaseBoomerang => false;
	protected virtual Array BaseBoomerangProjectiles => new Array();
	protected virtual float BaseOrbitDuration => 0.0f;
	protected virtual float BaseOrbitRadius => 0.0f;

	// How many extra arms one multishot stack adds - a plain Boomerang gets one more
	// arm per stack, but TwinBoomerangTower (already built around multiple arms)
	// doubles that.
	protected virtual int MultishotArmsPerStack => 1;
	private int _multishotArmCount;

	protected override void InitializeStats() {
		base.InitializeStats();
		Boomerang = BaseBoomerang;
		OrbitDuration = BaseOrbitDuration;
		OrbitRadius = BaseOrbitRadius;

		BoomerangProjectiles = new Array();
		foreach (Dictionary profile in BaseBoomerangProjectiles)
			BoomerangProjectiles.Add(profile.Duplicate(true));
	}

	protected override void ConfigureCombat() {
		base.ConfigureCombat();
		if (Range <= 0.0f)
			return;
		var profiles = new List<Dictionary>();
		foreach (Dictionary p in BoomerangProjectiles)
			profiles.Add(p);
		if (profiles.Count == 0)
			profiles.Add(new Dictionary());
		foreach (var profile in profiles)
			AddArm(profile);
	}

	private void AddArm(Dictionary profile) {
		var arm = new Dictionary();
		arm["busy"] = false;
		arm["profile"] = profile;
		_arms.Add(arm);
		_armTargets.Add(null);
	}

	// A multishot arm clones one of the tower's own named profiles (cycling through
	// them if there's more than one, e.g. Twin Boomerang alternates fire/frost) so it
	// gets a real orbit_duration/orbit_radius. A tower with no named profiles (plain
	// Boomerang) has no profile to clone, so it falls back to its own base stats
	// instead - the same thing ApplyArmProfile already does for a missing key.
	protected override void ApplyToMultishot(int stackDelta) {
		int armDelta = stackDelta * MultishotArmsPerStack;
		if (armDelta > 0) {
			for (int i = 0; i < armDelta; i++) {
				Dictionary profile = BoomerangProjectiles.Count > 0
					? (Dictionary)BoomerangProjectiles[_multishotArmCount % BoomerangProjectiles.Count]
					: new Dictionary();
				AddArm(profile);
				_multishotArmCount++;
			}
		}
		else if (armDelta < 0) {
			int removeCount = Mathf.Min(-armDelta, _multishotArmCount);
			for (int i = 0; i < removeCount; i++) {
				_arms.RemoveAt(_arms.Count - 1);
				_armTargets.RemoveAt(_armTargets.Count - 1);
			}
			_multishotArmCount -= removeCount;
		}
	}

	protected override void ApplyToDamage(float mult) {
		base.ApplyToDamage(mult);
		foreach (Dictionary profile in BoomerangProjectiles) {
			float current = profile.ContainsKey("damage") ? (float)profile["damage"] : 0.0f;
			profile["damage"] = current * mult;
		}
	}

	protected override void ApplyToRange(float mult) {
		base.ApplyToRange(mult);
		foreach (Dictionary profile in BoomerangProjectiles) {
			if (profile.ContainsKey("splash_radius")) {
				float v = (float)profile["splash_radius"];
				if (v > 0.0f) profile["splash_radius"] = v * mult;
			}
		}
	}

	protected override void ApplyToProjectileSpeed(float mult) {
		base.ApplyToProjectileSpeed(mult);
		foreach (Dictionary profile in BoomerangProjectiles) {
			float current = profile.ContainsKey("projectile_speed") ? (float)profile["projectile_speed"] : 0.0f;
			profile["projectile_speed"] = current * mult;
		}
	}

	protected override void ApplyToAttackSpeed(float mult) => ApplyToProjectileSpeed(mult);

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["boomerang"] = BaseBoomerang;
		data["boomerang_projectiles"] = BaseBoomerangProjectiles;
		data["orbit_duration"] = BaseOrbitDuration;
		data["orbit_radius"] = BaseOrbitRadius;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["boomerang"] = Boomerang;
		data["boomerang_projectiles"] = BoomerangProjectiles;
		data["orbit_duration"] = OrbitDuration;
		data["orbit_radius"] = OrbitRadius;
	}

	protected override ProjectileStats BuildProjectileStats() => base.BuildProjectileStats() with {
		OrbitDuration = OrbitDuration,
		OrbitRadius = OrbitRadius,
	};

	// Applies a boomerang arm's profile overrides (a raw dict literal - see e.g.
	// TwinBoomerangTower.BaseBoomerangProjectiles) on top of this tower's own current stats,
	// producing the final per-shot values for that specific arm.
	private static ProjectileStats ApplyArmProfile(ProjectileStats stats, Dictionary profile) {
		return stats with {
			Damage = profile.ContainsKey("damage") ? profile["damage"].AsSingle() : stats.Damage,
			SplashRadius = profile.ContainsKey("splash_radius") ? profile["splash_radius"].AsSingle() : stats.SplashRadius,
			ProjectileSpeed = profile.ContainsKey("projectile_speed") ? profile["projectile_speed"].AsSingle() : stats.ProjectileSpeed,
			ProjectileColor = profile.ContainsKey("projectile_color") ? profile["projectile_color"].AsColor() : stats.ProjectileColor,
			OnHitStatuses = profile.ContainsKey("on_hit_statuses") ? profile["on_hit_statuses"].AsGodotArray() : stats.OnHitStatuses,
			OrbitDuration = profile.ContainsKey("orbit_duration") ? profile["orbit_duration"].AsSingle() : stats.OrbitDuration,
			OrbitRadius = profile.ContainsKey("orbit_radius") ? profile["orbit_radius"].AsSingle() : stats.OrbitRadius,
		};
	}

	public override void _Process(double delta) {
		base._Process(delta);
		for (int i = 0; i < _arms.Count; i++)
			TryFireArm(i);
	}

	private void TryFireArm(int index) {
		Dictionary arm = _arms[index];
		if ((bool)arm["busy"])
			return;
		// Only ever chase an enemy no other currently-flying arm already has - if
		// every enemy in range is claimed, this arm just waits rather than piling
		// onto the same target as an identical, perfectly-overlapping shot.
		var busyTargets = new HashSet<BaseEnemy>();
		for (int i = 0; i < _arms.Count; i++) {
			if (i != index && (bool)_arms[i]["busy"] && _armTargets[i] != null && IsInstanceValid(_armTargets[i]))
				busyTargets.Add(_armTargets[i]);
		}
		List<BaseEnemy> candidates = GetTargets(1, busyTargets);
		if (candidates.Count == 0)
			return;
		BaseEnemy target = candidates[0];
		arm["busy"] = true;
		_armTargets[index] = target;
		BoomerangProjectile projectile = (BoomerangProjectile)ProjectileScene.Instantiate();
		ProjectileContainer.AddChild(projectile);
		ProjectileStats stats = ApplyArmProfile(BuildProjectileStats(), (Dictionary)arm["profile"]);
		projectile.setup(GlobalPosition, target, stats);
		projectile.SetupBoomerang(this, arm, stats.OrbitDuration, stats.OrbitRadius);
	}

	public void _on_boomerang_returned(Dictionary arm) {
		arm["busy"] = false;
		int index = _arms.IndexOf(arm);
		if (index >= 0)
			_armTargets[index] = null;
	}
}
