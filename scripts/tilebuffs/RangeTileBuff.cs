using Godot;

public sealed class RangeTileBuff : TileBuff {
	public override string Key => "range";
	public override string Label => "Range";
	public override Color Color => new Color(0.3f, 0.7f, 1.0f);
	public override string StatusType => "tile_range";

	public override void Draw(CanvasItem canvas, Vector2 center, float cellSize) {
		canvas.DrawArc(center, cellSize * 0.5f, 0.0f, Mathf.Tau, 24, Color, 2.0f);
	}
}
