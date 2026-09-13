using Godot;

public partial class TowerIcon : Control {
	private Texture2D _texture;

	public override void _Ready() {
		Resized += QueueRedraw;
	}

	public void Setup(string type) {
		_texture = GD.Load<Texture2D>($"res://icons/towers/{type}.png");
		QueueRedraw();
	}

	public override void _Draw() {
		if (_texture == null || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;
		float side = Mathf.Min(Size.X, Size.Y);
		Vector2 origin = (Size - new Vector2(side, side)) / 2.0f;
		DrawTextureRect(_texture, new Rect2(origin, new Vector2(side, side)), false);
	}
}
