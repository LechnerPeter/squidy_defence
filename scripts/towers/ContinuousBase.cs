using Godot;
using Godot.Collections;
using System.Collections.Generic;

public abstract partial class ContinuousBase : DamageCapBase {
	protected float SplashRadius;
	protected bool Continuous;

	protected virtual float BaseSplashRadius => 0.0f;
	protected virtual bool BaseContinuous => false;

	protected override bool HasPlinth => false;

	protected override void InitializeStats() {
		base.InitializeStats();
		SplashRadius = BaseSplashRadius;
		Continuous = BaseContinuous;
	}

	protected override void ApplyToRange(float mult) {
		if (SplashRadius > 0.0f)
			SplashRadius *= mult;
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["splash_radius"] = BaseSplashRadius;
		data["continuous"] = BaseContinuous;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["splash_radius"] = SplashRadius;
		data["continuous"] = Continuous;
	}

	public override void _Process(double delta) {
		base._Process(delta);
		ApplyContinuousDamage((float)delta);
		UpdateBodyColor();
	}

	private void UpdateBodyColor() {
		bool depleted = DamageDealtThisRound >= MaxDamagePerRound;
		if (depleted)
			SetBodyColor(BodyColor.Darkened(0.55f));
		else if (ActiveGlow)
			SetBodyColor(BodyColor.Lightened(0.25f));
		else
			SetBodyColor(BodyColor);
	}

	private void ApplyContinuousDamage(float delta) {
		float remainingRound = MaxDamagePerRound - DamageDealtThisRound;
		if (remainingRound <= 0.0f) {
			ActiveGlow = false;
			return;
		}
		float radius = SplashRadius;
		float dps = Damage;
		var targets = new List<BaseEnemy>();
		foreach (Node node in GetTree().GetNodesInGroup("enemies")) {
			if (node is BaseEnemy enemy && IsInstanceValid(enemy) && enemy.GlobalPosition.DistanceTo(GlobalPosition) <= radius)
				targets.Add(enemy);
		}
		if (targets.Count == 0) {
			ActiveGlow = false;
			return;
		}
		float perTarget = (dps * delta) / targets.Count;
		bool hitAny = false;
		foreach (var enemy in targets) {
			if (remainingRound <= 0.0f)
				break;
			float amount = Mathf.Min(perTarget, remainingRound);
			enemy.take_damage(amount, TowerDamageType);
			DamageDealtThisRound += amount;
			remainingRound -= amount;
			hitAny = true;
		}
		ActiveGlow = hitAny;
	}
}
