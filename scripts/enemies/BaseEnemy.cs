using Godot;
using Godot.Collections;
using System.Collections.Generic;

public abstract partial class BaseEnemy : Area2D {
	[Signal]
	public delegate void diedEventHandler(Area2D enemy);
	[Signal]
	public delegate void reached_endEventHandler(Area2D enemy);

	public float speed;
	public float base_speed;
	public float max_health;
	public float health;
	public int reward;
	public int damage_to_lives;
	public Color color;
	public float radius;
	public string enemy_type;

	protected Curve2D _curve = new Curve2D();
	protected float _distance = 0.0f;

	// How far this enemy has walked along its current path - lets something like
	// Ricochet's bounce-toward-the-start preference compare progress between enemies
	// without caring about the path's actual shape.
	public float DistanceTraveled => _distance;
	protected bool _is_dead = false;
	protected bool _finished = false;
	protected Vector2 _facing = Vector2.Right;

	private readonly System.Collections.Generic.Dictionary<string, StatusEffectState> _statuses = new();

	// Curse-granted resistance that applies on top of this type's own innate
	// resistance (see ResistanceTo) - set once at spawn from whatever the run's
	// accumulated ElementalResistanceCurse/PhysicalResistanceCurse picks add up to
	// (see Main.SpawnNextEnemy), never touched again after that.
	private float _physicalResistanceBonus = 0.0f;
	private float _elementalResistanceBonus = 0.0f;

	protected virtual float BaseHealth => 40.0f;
	protected virtual float BaseSpeed => 70.0f;
	protected virtual int BaseReward => 8;
	protected virtual int BaseDamageToLives => 1;
	protected virtual Color BaseColor => new Color(0.8f, 0.2f, 0.2f);
	protected virtual float BaseRadius => 14.0f;
	protected virtual string DisplayName => "Enemy";
	protected virtual string Flavor => "";

	// Whether this enemy can cross water tiles - everything else treats water as
	// impassable terrain, same as rock or a built tower.
	public virtual bool CanSwim => false;

	// Read by the in-game wiki (see UI.BuildWikiPanel) to draw a swatch for a type
	// it never actually spawns onto the field.
	public Color PreviewColor => BaseColor;

	public void setup(Array<Vector2> waypoints) {
		ZIndex = 1;
		max_health = BaseHealth;
		health = max_health;
		speed = BaseSpeed;
		base_speed = speed;
		reward = BaseReward;
		damage_to_lives = BaseDamageToLives;
		color = BaseColor;
		radius = BaseRadius;
		BuildCurve(waypoints);
		_distance = 0.0f;
		GlobalPosition = _curve.SampleBaked(0.0f);
		UpdateFacing();
		AddToGroup("enemies");
	}

	public void reroute(Array<Vector2> waypoints) {
		if (_finished || waypoints.Count < 2)
			return;
		BuildCurve(waypoints);
		_distance = 0.0f;
	}

	private void BuildCurve(Array<Vector2> waypoints) {
		_curve = new Curve2D();
		int n = waypoints.Count;
		for (int i = 0; i < n; i++) {
			Vector2 p = waypoints[i];
			Vector2 prev = waypoints[Mathf.Max(i - 1, 0)];
			Vector2 next = waypoints[Mathf.Min(i + 1, n - 1)];
			Vector2 tangent = (next - prev) * 0.2f;
			_curve.AddPoint(p, -tangent, tangent);
		}
	}

	public override void _Process(double delta) {
		if (_finished)
			return;
		UpdateStatuses((float)delta);
		Move((float)delta);
		QueueRedraw();
	}

	private void Move(float delta) {
		_distance += speed * delta;
		float total = _curve.GetBakedLength();
		if (_distance >= total) {
			GlobalPosition = _curve.SampleBaked(total);
			_finished = true;
			EmitSignal(SignalName.reached_end, this);
		}
		else {
			GlobalPosition = _curve.SampleBaked(_distance);
			UpdateFacing();
		}
	}

	private void UpdateFacing() {
		float total = _curve.GetBakedLength();
		Vector2 ahead = _curve.SampleBaked(Mathf.Min(_distance + 4.0f, total));
		Vector2 here = _curve.SampleBaked(_distance);
		if (ahead.DistanceTo(here) > 0.01f)
			_facing = (ahead - here).Normalized();
	}

	public void take_damage(float amount, DamageType type) {
		if (_is_dead)
			return;
		ApplyDamage(amount * (1.0f - ResistanceTo(type)), type);
		if (IsDepleted()) {
			_is_dead = true;
			EmitSignal(SignalName.died, this);
		}
	}

	// Where the (already resistance-adjusted) damage actually lands - the default
	// single shared pool for almost everything. TitanBoss overrides this to route it
	// into one of two independent pools instead, based on `type`.
	protected virtual void ApplyDamage(float amount, DamageType type) => health -= amount;

	// Default: dead once the single shared pool is empty. TitanBoss overrides this so
	// both its pools need to be drained, not just whichever this call happened to hit.
	protected virtual bool IsDepleted() => health <= 0.0f;

	// General status-effect entry point: applies `type` at `magnitude` for `duration`
	// seconds. Re-applying an already-active status refreshes it in place (its
	// magnitude is replaced, its remaining duration extends to at least the new
	// duration) rather than stacking a second, independent instance.
	public void apply_status(string type, float magnitude, float duration) {
		if (duration <= 0.0f)
			return;
		if (_statuses.TryGetValue(type, out StatusEffectState existing)) {
			existing.Magnitude = magnitude;
			existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
		}
		else {
			_statuses[type] = new StatusEffectState { Magnitude = magnitude, RemainingDuration = duration };
		}
		ApplyStatusEffect(type, magnitude);
	}

	private void UpdateStatuses(float delta) {
		if (_statuses.Count == 0)
			return;
		List<string> expired = null;
		foreach (KeyValuePair<string, StatusEffectState> entry in _statuses) {
			entry.Value.RemainingDuration -= delta;
			if (entry.Value.RemainingDuration <= 0.0f)
				(expired ??= new List<string>()).Add(entry.Key);
		}
		if (expired == null)
			return;
		foreach (string type in expired) {
			_statuses.Remove(type);
			RevertStatusEffect(type);
		}
	}

	// Per-status "on applied/refreshed" hook. Add a case here for each new status type.
	private void ApplyStatusEffect(string type, float magnitude) {
		switch (type) {
			case "slow":
				speed = base_speed * (1.0f - ClampSlowMagnitude(magnitude));
				break;
			case "enemy_type_health_curse":
				max_health *= magnitude;
				health = max_health;
				break;
			case "enemy_type_speed_curse":
				base_speed *= magnitude;
				speed = base_speed;
				break;
			case "physical_resistance_curse":
				_physicalResistanceBonus = magnitude;
				break;
			case "elemental_resistance_curse":
				_elementalResistanceBonus = magnitude;
				break;
		}
	}

	// Per-status "on expired" hook, undoing whatever ApplyStatusEffect changed.
	private void RevertStatusEffect(string type) {
		switch (type) {
			case "slow":
				speed = base_speed;
				break;
		}
	}

	// Lets a subclass resist slow effects (e.g. bosses shouldn't be brought to a
	// crawl by frost towers) without changing how slow itself is tracked or applied.
	protected virtual float ClampSlowMagnitude(float magnitude) => magnitude;

	// Innate resistance for this enemy TYPE, before any curse bonus - overridden by
	// GolemEnemy/WispEnemy/JuggernautBoss. 0 by default.
	protected virtual float BaseResistanceTo(DamageType type) => 0.0f;

	// Total resistance actually applied to incoming damage: this type's own innate
	// resistance plus any curse-granted bonus that applies to every enemy regardless
	// of type (see ElementalResistanceCurse/PhysicalResistanceCurse), capped so
	// nothing on the field is ever fully immune to a damage type.
	private float ResistanceTo(DamageType type) {
		float bonus = type == DamageType.Physical ? _physicalResistanceBonus : _elementalResistanceBonus;
		return Mathf.Min(0.9f, BaseResistanceTo(type) + bonus);
	}

	// Read by Main.cs when the player clicks this enemy (or, for a boss, the instant
	// it spawns) to show a live info panel - reuses TowerInfo (see GameData.TowerInfo)
	// since it's already just "name, flavor, a list of stat lines, a list of effect
	// icons" and nothing here needs effect icons. Virtual so TitanBoss can report its
	// two separate pools instead of the one shared health line below.
	public virtual TowerInfo GetInfo() {
		var lines = new List<string> {
			$"Health: {Mathf.CeilToInt(Mathf.Max(health, 0.0f))} / {(int)max_health}",
			$"Speed: {speed:F0}",
			$"Reward: {reward}g",
			$"Damage to lives if it escapes: {damage_to_lives}",
		};
		AppendResistanceLines(lines);
		return new TowerInfo(DisplayName, Flavor, lines, new List<string>());
	}

	// Static reference version of GetInfo() built from this type's Base* stats
	// instead of a spawned instance's current fields - used by the in-game wiki (see
	// UI.BuildWikiPanel) to list every enemy type without spawning one onto the
	// field. TitanBoss overrides this the same way it overrides GetInfo(), to show
	// its two pools instead of one shared health line.
	public virtual TowerInfo GetPreviewInfo() {
		var lines = new List<string> {
			$"Health: {(int)BaseHealth}",
			$"Speed: {BaseSpeed:F0}",
			$"Reward: {BaseReward}g",
			$"Damage to lives if it escapes: {BaseDamageToLives}",
		};
		AppendResistanceLines(lines);
		if (CanSwim)
			lines.Add("Can swim across water tiles.");
		return new TowerInfo(DisplayName, Flavor, lines, new List<string>());
	}

	private void AppendResistanceLines(List<string> lines) {
		float physicalResist = ResistanceTo(DamageType.Physical);
		float elementalResist = ResistanceTo(DamageType.Elemental);
		if (physicalResist > 0.0f)
			lines.Add($"Resists Physical damage by {(int)(physicalResist * 100.0f)}%");
		if (elementalResist > 0.0f)
			lines.Add($"Resists Elemental damage by {(int)(elementalResist * 100.0f)}%");
	}

	public override void _Draw() {
		DrawBody();
		DrawArc(Vector2.Zero, radius, 0.0f, Mathf.Tau, 24, Colors.Black, 1.5f);
		float barW = radius * 2.2f;
		float barH = 5.0f;
		Vector2 barPos = new Vector2(-barW / 2.0f, -radius - 12.0f);
		DrawRect(new Rect2(barPos, new Vector2(barW, barH)), new Color(0, 0, 0, 0.6f));
		float ratio = Mathf.Clamp(health / max_health, 0.0f, 1.0f);
		DrawRect(new Rect2(barPos, new Vector2(barW * ratio, barH)), new Color(0.2f, 0.9f, 0.3f));
	}

	protected virtual void DrawBody() {
		DrawCircle(Vector2.Zero, radius, color);
	}
}
