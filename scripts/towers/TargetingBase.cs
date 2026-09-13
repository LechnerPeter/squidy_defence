using Godot;
using Godot.Collections;
using System.Collections.Generic;

public abstract partial class TargetingBase : DamagingBase {
	protected Area2D RangeArea;
	protected CollisionShape2D RangeShape;
	protected List<BaseEnemy> EnemiesInRange = new List<BaseEnemy>();

	protected float Range;
	protected List<StatusEffectState> OnHitStatuses = new();
	protected Color ProjectileColor;
	protected float ProjectileSpeed;

	protected virtual float BaseRange => 0.0f;
	protected virtual List<StatusEffectState> BaseOnHitStatuses => new();
	protected virtual Color BaseProjectileColor => new Color(0, 0, 0, 0);
	protected virtual float BaseProjectileSpeed => 0.0f;

	public override void _Ready() {
		RangeArea = GetNodeOrNull<Area2D>("RangeArea");
		RangeShape = GetNodeOrNull<CollisionShape2D>("RangeArea/CollisionShape2D");
	}

	protected override void InitializeStats() {
		base.InitializeStats();
		Range = BaseRange;
		OnHitStatuses = BaseOnHitStatuses;
		ProjectileColor = BaseProjectileColor;
		ProjectileSpeed = BaseProjectileSpeed;
	}

	protected override void ConfigureCombat() {
		if (Range <= 0.0f)
			return;
		var shape = new CircleShape2D();
		shape.Radius = Range;
		RangeShape.Shape = shape;
		RangeArea.AreaEntered += OnAreaEntered;
		RangeArea.AreaExited += OnAreaExited;
	}

	protected override void ApplyToRange(float mult) {
		if (Range <= 0.0f)
			return;
		Range *= mult;
		if (RangeShape?.Shape != null)
			((CircleShape2D)RangeShape.Shape).Radius = Range;
	}

	// Not reached from BaseTower's dispatch directly - only via a subclass's own
	// ApplyToAttackSpeed override (e.g. BoomerangBase, whose attack speed IS its
	// projectile speed).
	protected virtual void ApplyToProjectileSpeed(float mult) => ProjectileSpeed *= mult;

	protected void OnAreaEntered(Area2D area) {
		if (area is BaseEnemy enemy && enemy.IsInGroup("enemies"))
			EnemiesInRange.Add(enemy);
	}

	protected void OnAreaExited(Area2D area) {
		if (area is BaseEnemy enemy)
			EnemiesInRange.Remove(enemy);
	}

	protected BaseEnemy GetTarget() {
		List<BaseEnemy> targets = GetTargets(1);
		return targets.Count > 0 ? targets[0] : null;
	}

	// Up to `count` distinct enemies currently in range, for towers that fire more
	// than one shot per trigger (see AttackBase.MultishotStacks) - each returned
	// enemy is different, so a multishot volley never double-targets the same one.
	// `excluding` additionally skips enemies already claimed by another still-active
	// shot fired earlier (see BoomerangBase, whose arms fire on independent timers
	// rather than all at once, so they can't just be handed one shared batch).
	protected virtual List<BaseEnemy> GetTargets(int count, HashSet<BaseEnemy> excluding = null) {
		while (EnemiesInRange.Count > 0 && !IsInstanceValid(EnemiesInRange[0]))
			EnemiesInRange.RemoveAt(0);
		var result = new List<BaseEnemy>();
		foreach (BaseEnemy enemy in EnemiesInRange) {
			if (!IsInstanceValid(enemy) || (excluding != null && excluding.Contains(enemy)))
				continue;
			result.Add(enemy);
			if (result.Count >= count)
				break;
		}
		return result;
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["range"] = BaseRange;
		data["on_hit_statuses"] = OnHitStatusesToVariant(BaseOnHitStatuses);
		data["projectile_color"] = BaseProjectileColor;
		data["projectile_speed"] = BaseProjectileSpeed;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["range"] = Range;
		data["on_hit_statuses"] = OnHitStatusesToVariant(OnHitStatuses);
		data["projectile_color"] = ProjectileColor;
		data["projectile_speed"] = ProjectileSpeed;
	}

	// What a fired shot needs from this tower right now - each tier below adds only the
	// fields its own combat style contributes, via a `with` expression on the base result.
	protected virtual ProjectileStats BuildProjectileStats() => new ProjectileStats {
		Damage = Damage,
		DamageType = TowerDamageType,
		ProjectileSpeed = ProjectileSpeed,
		ProjectileColor = ProjectileColor,
		OnHitStatuses = OnHitStatusesToVariant(OnHitStatuses),
	};
}
