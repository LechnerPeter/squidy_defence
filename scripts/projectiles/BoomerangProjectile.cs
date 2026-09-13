using Godot;
using Godot.Collections;

public partial class BoomerangProjectile : Projectile {
	private string _phase = "outbound";
	private float _orbitDuration = 0.0f;
	private float _orbitRadius = 16.0f;
	private Vector2 _orbitCenter = Vector2.Zero;
	private float _orbitTimer = 0.0f;
	private float _orbitAngle = 0.0f;
	private BoomerangBase _ownerTower;
	private Dictionary _ownerArm = new Dictionary();

	// Separate from setup() because ownerArm is a live mutable reference into
	// BoomerangBase._arms that must be mutated later (on return), not descriptive
	// stat data - and orbit duration/radius can vary per arm profile (see
	// BoomerangBase.TryFireArm), so they're passed explicitly here too rather than
	// re-read generically from a dict.
	public void SetupBoomerang(BoomerangBase ownerTower, Dictionary ownerArm, float orbitDuration, float orbitRadius) {
		_ownerTower = ownerTower;
		_ownerArm = ownerArm ?? new Dictionary();
		_orbitDuration = orbitDuration;
		_orbitRadius = orbitRadius;
	}

	// Overrides _Process entirely rather than calling base._Process(delta) first: the base
	// body (MoveOutbound) is only ONE branch of this leaf's phase switch, not an always-run
	// prerequisite, so the usual "call base first" convention doesn't apply here.
	public override void _Process(double delta) {
		switch (_phase) {
			case "outbound":
				MoveOutbound((float)delta);
				break;
			case "orbit_target":
			case "orbit_tower":
				MoveOrbit((float)delta);
				break;
			case "return":
				MoveReturn((float)delta);
				break;
		}
	}

	protected override void OnTargetLost() => StartReturn();

	protected override void EndFlight() {
		_orbitCenter = GlobalPosition;
		StartOrbit("orbit_target");
	}

	protected override Color GetDrawColor() {
		Color c = ProjectileColor;
		if (_phase == "return" || _phase == "orbit_tower")
			c.A *= 0.5f;
		return c;
	}

	private void StartOrbit(string phase) {
		_phase = phase;
		_orbitTimer = 0.0f;
		_orbitAngle = 0.0f;
		if (_orbitDuration <= 0.0f)
			FinishOrbit();
	}

	private void MoveOrbit(float delta) {
		_orbitTimer += delta;
		_orbitAngle += (Mathf.Tau / _orbitDuration) * delta;
		Vector2 radial = new Vector2(_orbitRadius, 0.0f).Rotated(_orbitAngle);
		GlobalPosition = _orbitCenter + radial;
		Facing = radial.Normalized().Rotated(Mathf.Pi / 2.0f);
		QueueRedraw();
		if (_orbitTimer >= _orbitDuration)
			FinishOrbit();
	}

	private void FinishOrbit() {
		if (_phase == "orbit_target")
			StartReturn();
		else
			CompleteReturn();
	}

	private void StartReturn() {
		_phase = "return";
		Target = null;
		QueueRedraw();
	}

	private void MoveReturn(float delta) {
		if (_ownerTower == null || !IsInstanceValid(_ownerTower)) {
			QueueFree();
			return;
		}
		Vector2 toOwner = _ownerTower.GlobalPosition - GlobalPosition;
		float dist = toOwner.Length();
		float step = Speed * delta;
		if (dist <= step) {
			_orbitCenter = _ownerTower.GlobalPosition;
			StartOrbit("orbit_tower");
		}
		else {
			Facing = toOwner.Normalized();
			GlobalPosition += Facing * step;
			QueueRedraw();
		}
	}

	private void CompleteReturn() {
		if (_ownerTower != null && IsInstanceValid(_ownerTower))
			_ownerTower._on_boomerang_returned(_ownerArm);
		QueueFree();
	}
}
