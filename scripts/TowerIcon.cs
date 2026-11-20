using Godot;
using System.Linq;

// Shows a tower exactly as its scene draws it: the scene's own Visual node, scaled to
// fit, rather than a separate image that could disagree with it. A tower that can take
// several shapes shows whichever one its scene shows by default.
public partial class TowerIcon : Control {
	private const float OutlineMargin = 2.0f;

	private Node2D _visual;
	private Rect2 _bounds;

	public override void _Ready() {
		Resized += FitVisual;
		FitVisual();
	}

	public void Setup(string type) {
		Node tower = GameData.TowerScenes[type].Instantiate();
		_visual = tower.GetNode<Node2D>("Visual");
		tower.RemoveChild(_visual);
		tower.Free();
		_visual.ShowBehindParent = false;
		AddChild(_visual);
		_bounds = ShapeBounds(_visual);
		FitVisual();
	}

	// The default shape is the first visible child of Visual - a single-tile tower's
	// body, or a merged tower's first arrangement of tiles.
	private static Rect2 ShapeBounds(Node2D visual) {
		Node2D shape = visual.GetChildren().OfType<Node2D>().First(child => child.Visible);
		Rect2? bounds = null;
		foreach (Polygon2D polygon in shape.GetChildren().Prepend(shape).OfType<Polygon2D>()) {
			foreach (Vector2 corner in polygon.Polygon)
				bounds = bounds?.Expand(corner) ?? new Rect2(corner, Vector2.Zero);
		}
		return bounds.Value.Grow(OutlineMargin);
	}

	private void FitVisual() {
		if (_visual == null || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;
		float scale = Mathf.Min(Size.X / _bounds.Size.X, Size.Y / _bounds.Size.Y);
		_visual.Scale = new Vector2(scale, scale);
		_visual.Position = Size / 2.0f - _bounds.GetCenter() * scale;
	}
}
