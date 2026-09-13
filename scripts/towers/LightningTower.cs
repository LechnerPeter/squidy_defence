using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class LightningTower : TargetingBase {
	private class Beam {
		public BaseEnemy Target;
		public float TimeOnTarget;
	}

	protected int MultishotStacks;
	private readonly List<Beam> _beams = new List<Beam> { new Beam() };

	protected float RampPerSecond;
	protected float MaxRampMultiplier;

	public LightningTower() {
		tower_type = "lightning";
	}

	protected override string BaseTowerName => "Lightning Tower";
	protected override int BaseCost => 75;
	protected override int BaseCostStep => 15;
	protected override float BaseRange => 140.0f;
	protected override float BaseDamage => 8.0f;
	protected override DamageType TowerDamageType => DamageType.Elemental;
	protected virtual float BaseRampPerSecond => 0.4f;
	protected virtual float BaseMaxRampMultiplier => 4.0f;
	protected override Color BaseColor => new Color(0.45f, 0.4f, 0.85f);
	protected override string BaseFlavor => "Locks onto one target and arcs a beam into it that gets stronger the longer it stays locked. Switching targets resets the ramp.";
	protected override string BaseAbbreviation => "LTN";

	protected override void InitializeStats() {
		base.InitializeStats();
		RampPerSecond = BaseRampPerSecond;
		MaxRampMultiplier = BaseMaxRampMultiplier;
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["ramp_per_second"] = BaseRampPerSecond;
		data["max_ramp_multiplier"] = BaseMaxRampMultiplier;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["ramp_per_second"] = RampPerSecond;
		data["max_ramp_multiplier"] = MaxRampMultiplier;
	}

	// Each multishot stack locks on one more simultaneous target, each with its own
	// independent ramp - a second beam doesn't share the first's focus time.
	protected override void ApplyToMultishot(int stackDelta) {
		MultishotStacks = Mathf.Max(0, MultishotStacks + stackDelta);
		int desired = 1 + MultishotStacks;
		while (_beams.Count < desired)
			_beams.Add(new Beam());
		while (_beams.Count > desired)
			_beams.RemoveAt(_beams.Count - 1);
	}

	public override void _Process(double delta) {
		base._Process(delta);
		UpdateBeams((float)delta);
		QueueRedraw();
	}

	protected float CurrentRampMultiplier(float timeOnTarget) => Mathf.Min(1.0f + RampPerSecond * timeOnTarget, MaxRampMultiplier);

	private void UpdateBeams(float delta) {
		List<BaseEnemy> targets = GetTargets(_beams.Count);
		// Beams keep whichever target they already had (so their ramp doesn't reset
		// every frame); only beams that lost their target get reassigned from
		// whatever's left in `targets`, so two beams never lock the same enemy.
		var stillTargeted = new HashSet<BaseEnemy>();
		foreach (Beam beam in _beams) {
			if (beam.Target != null && IsInstanceValid(beam.Target) && targets.Contains(beam.Target))
				stillTargeted.Add(beam.Target);
			else
				beam.Target = null;
		}
		var freeTargets = new List<BaseEnemy>();
		foreach (BaseEnemy t in targets) {
			if (!stillTargeted.Contains(t))
				freeTargets.Add(t);
		}
		int freeIndex = 0;
		foreach (Beam beam in _beams) {
			if (beam.Target == null && freeIndex < freeTargets.Count)
				beam.Target = freeTargets[freeIndex++];
		}
		foreach (Beam beam in _beams) {
			if (beam.Target == null) {
				beam.TimeOnTarget = 0.0f;
				continue;
			}
			beam.TimeOnTarget += delta;
			beam.Target.take_damage(Damage * CurrentRampMultiplier(beam.TimeOnTarget) * delta, TowerDamageType);
			OnBeamTick(beam.Target, beam.TimeOnTarget, delta);
		}
	}

	// Extension point for subclasses that add more ramping effects to the beam
	// besides damage (e.g. LightningFrostTower's ramping slow) - timeOnTarget is
	// this specific beam's own focus duration, not shared across beams.
	protected virtual void OnBeamTick(BaseEnemy target, float timeOnTarget, float delta) {
	}

	public override void _Draw() {
		base._Draw();
		foreach (Beam beam in _beams) {
			if (beam.Target == null || !IsInstanceValid(beam.Target))
				continue;
			float intensity = MaxRampMultiplier > 1.0f ? (CurrentRampMultiplier(beam.TimeOnTarget) - 1.0f) / (MaxRampMultiplier - 1.0f) : 0.0f;
			DrawBeam(ToLocal(beam.Target.GlobalPosition), intensity);
		}
	}

	private void DrawBeam(Vector2 localEnd, float intensity) {
		Vector2 dir = localEnd.Normalized();
		Vector2 perp = new Vector2(-dir.Y, dir.X);
		const int segments = 6;
		var pts = new List<Vector2> { Vector2.Zero };
		for (int i = 1; i < segments; i++) {
			Vector2 basePoint = localEnd * ((float)i / segments);
			float jitter = (GD.Randf() - 0.5f) * 10.0f * (0.4f + 0.6f * intensity);
			pts.Add(basePoint + perp * jitter);
		}
		pts.Add(localEnd);
		Color beamColor = new Color(0.75f, 0.85f, 1.0f, 0.5f + 0.5f * intensity);
		float width = 1.5f + 2.5f * intensity;
		for (int i = 0; i < pts.Count - 1; i++)
			DrawLine(pts[i], pts[i + 1], beamColor, width);
	}
}
