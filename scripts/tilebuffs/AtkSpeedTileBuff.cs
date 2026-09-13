using Godot;

public sealed class AtkSpeedTileBuff : TileBuff {
	public override string Key => "atk_speed";
	public override string Label => "Attack Speed";
	public override Color Color => new Color(1.0f, 0.85f, 0.2f);
	public override string StatusType => "tile_atk_speed";

	public override void Draw(CanvasItem canvas, Vector2 center, float cellSize) {
		Vector2[] pts = {
			center + new Vector2(0.0f, -cellSize * 0.35f),
			center + new Vector2(cellSize * 0.3f, 0.0f),
			center + new Vector2(0.0f, cellSize * 0.35f),
			center + new Vector2(-cellSize * 0.3f, 0.0f),
		};
		canvas.DrawColoredPolygon(pts, Color);
	}
}
