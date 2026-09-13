using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class Projectile : Node2D {
	protected BaseEnemy Target;
	protected float Damage = 0.0f;
	protected DamageType ProjectileDamageType = DamageType.Physical;
	protected float Speed = 400.0f;
	protected Color ProjectileColor = Colors.Yellow;
	protected Vector2 Facing = Vector2.Right;

	private static readonly PackedScene AoeEffectScene = GD.Load<PackedScene>("res://scenes/projectiles/AoeEffect.tscn");

	private float _splashRadius = 0.0f;
	private Array _onHitStatuses = new Array();

	public void setup(Vector2 startPos, BaseEnemy targetEnemy, ProjectileStats stats) {
		ZIndex = 2;
		GlobalPosition = startPos;
		Target = targetEnemy;
		Damage = stats.Damage;
		ProjectileDamageType = stats.DamageType;
		Speed = stats.ProjectileSpeed;
		_splashRadius = stats.SplashRadius;
		_onHitStatuses = stats.OnHitStatuses;
		ProjectileColor = stats.ProjectileColor;
		InitializeFeatureStats(stats);
	}

	// Leaf-specific stat reads (bounce/explosion/orbit stats) - already present as typed
	// fields on the same stats struct, so no extra setup parameters are needed for those.
	// Boomerang's ownerTower/ownerArm are the one exception (see
	// BoomerangProjectile.SetupBoomerang), since ownerArm is a live mutable reference,
	// not descriptive stat data.
	protected virtual void InitializeFeatureStats(ProjectileStats stats) {
	}

	public override void _Process(double delta) {
		MoveOutbound((float)delta);
	}

	protected void MoveOutbound(float delta) {
		if (!IsInstanceValid(Target)) {
			OnTargetLost();
			return;
		}
		Vector2 toTarget = Target.GlobalPosition - GlobalPosition;
		float dist = toTarget.Length();
		float step = Speed * delta;
		if (dist <= step) {
			Hit();
		}
		else {
			Facing = toTarget.Normalized();
			GlobalPosition += Facing * step;
			QueueRedraw();
		}
	}

	// The target disappeared (died to something else, etc.) before this shot arrived.
	protected virtual void OnTargetLost() => QueueFree();

	// Only ever called from MoveOutbound right after it confirms Target is valid.
	private void Hit() {
		Vector2 hitPos = Target.GlobalPosition;
		ApplyOnHitStatuses();
		DealDamage(hitPos);
		if (TryContinueFlight(hitPos))
			return;
		OnFlightConcluded(hitPos);
		EndFlight();
	}

	private void ApplyOnHitStatuses() {
		foreach (Variant v in _onHitStatuses) {
			Dictionary status = v.AsGodotDictionary();
			Target.apply_status(status["type"].AsString(), status["magnitude"].AsSingle(), status["duration"].AsSingle());
		}
	}

	private void DealDamage(Vector2 hitPos) {
		var enemies = new List<BaseEnemy> { Target };
		var amounts = new List<float> { Damage };
		if (_splashRadius > 0.0f) {
			SpawnAoeEffect(hitPos, _splashRadius);
			foreach (Node node in GetTree().GetNodesInGroup("enemies")) {
				if (node is BaseEnemy enemy && enemy != Target && IsInstanceValid(enemy) && enemy.GlobalPosition.DistanceTo(hitPos) <= _splashRadius) {
					enemies.Add(enemy);
					amounts.Add(Damage * 0.6f);
				}
			}
		}
		float total = 0.0f;
		foreach (float amount in amounts)
			total += amount;
		float damageScale = 1.0f;
		if (Damage > 0.0f && total > Damage)
			damageScale = Damage / total;
		for (int i = 0; i < enemies.Count; i++)
			enemies[i].take_damage(amounts[i] * damageScale, ProjectileDamageType);
	}

	// Spawns a brief expanding/fading ring at position to visualize an AOE radius (splash,
	// explosion, ...). Added as a sibling of this projectile (same parent) so it survives
	// independently once the projectile itself frees.
	protected void SpawnAoeEffect(Vector2 position, float radius) {
		AoeEffect effect = (AoeEffect)AoeEffectScene.Instantiate();
		GetParent().AddChild(effect);
		effect.GlobalPosition = position;
		effect.Setup(radius, ProjectileColor);
	}

	// Return true to keep this shot alive and flying on to a new target (e.g. a bounce
	// chain continuing) instead of concluding here - matches the original code's early
	// `return` that skipped both OnFlightConcluded and EndFlight for that frame.
	protected virtual bool TryContinueFlight(Vector2 hitPos) => false;

	// One-shot side effect fired exactly once, when the shot's flight is truly over
	// (e.g. ExplodingBouncingProjectile's detonation) - called before EndFlight.
	protected virtual void OnFlightConcluded(Vector2 finalHitPos) {
	}

	// How the shot actually terminates once its flight has concluded.
	protected virtual void EndFlight() => QueueFree();

	protected virtual Color GetDrawColor() => ProjectileColor;

	public override void _Draw() {
		Color drawColor = GetDrawColor();
		Vector2 tail = -Facing * 10.0f;
		Color trailColor = new Color(drawColor.R, drawColor.G, drawColor.B, drawColor.A * 0.35f);
		DrawLine(Vector2.Zero, tail, trailColor, 3.0f);
		DrawCircle(Vector2.Zero, 5.0f, drawColor);
		DrawArc(Vector2.Zero, 5.0f, 0.0f, Mathf.Tau, 10, new Color(1.0f, 1.0f, 1.0f, drawColor.A * 0.5f), 1.0f);
	}
}
