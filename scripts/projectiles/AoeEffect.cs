using Godot;

public partial class AoeEffect : Node2D {
	private float _radius;
	private Color _color;
	private float _duration = 0.35f;
	private float _elapsed = 0.0f;

	public void Setup(float radius, Color color, float duration = 0.35f) {
		_radius = radius;
		_color = color;
		_duration = duration;
		ZIndex = 1;
	}

	public override void _Process(double delta) {
		_elapsed += (float)delta;
		if (_elapsed >= _duration) {
			QueueFree();
			return;
		}
		QueueRedraw();
	}

	public override void _Draw() {
		float t = Mathf.Clamp(_elapsed / _duration, 0.0f, 1.0f);
		float currentRadius = _radius * (0.3f + 0.7f * t);
		float alpha = _color.A * (1.0f - t);
		DrawCircle(Vector2.Zero, currentRadius, new Color(_color.R, _color.G, _color.B, alpha * 0.25f));
		DrawArc(Vector2.Zero, currentRadius, 0.0f, Mathf.Tau, 32, new Color(_color.R, _color.G, _color.B, alpha), 3.0f);
	}
}
