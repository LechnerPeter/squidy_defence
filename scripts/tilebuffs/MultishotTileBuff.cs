using Godot;

public sealed class MultishotTileBuff : TileBuff {
	public override string Key => "multishot";
	public override string Label => "Multishot";
	public override Color Color => new Color(0.65f, 0.4f, 1.0f);
	public override string StatusType => "tile_multishot";
	public override int SpawnCount => 1;

	public override void Draw(CanvasItem canvas, Vector2 center, float cellSize) {
		float top = -cellSize * 0.35f;
		float bottom = cellSize * 0.2f;
		Vector2[] left = {
			center + new Vector2(-cellSize * 0.24f, top),
			center + new Vector2(-cellSize * 0.42f, bottom),
			center + new Vector2(-cellSize * 0.06f, bottom),
		};
		Vector2[] right = {
			center + new Vector2(cellSize * 0.24f, top),
			center + new Vector2(cellSize * 0.06f, bottom),
			center + new Vector2(cellSize * 0.42f, bottom),
		};
		canvas.DrawColoredPolygon(left, Color);
		canvas.DrawColoredPolygon(right, Color);
	}
}
