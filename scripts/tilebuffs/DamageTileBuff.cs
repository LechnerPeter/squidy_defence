using Godot;

public sealed class DamageTileBuff : TileBuff {
	public override string Key => "damage";
	public override string Label => "Damage";
	public override Color Color => new Color(1.0f, 0.35f, 0.25f);
	public override string StatusType => "tile_damage";

	public override void Draw(CanvasItem canvas, Vector2 center, float cellSize) {
		Vector2[] pts = {
			center + new Vector2(0.0f, -cellSize * 0.4f),
			center + new Vector2(-cellSize * 0.35f, cellSize * 0.25f),
			center + new Vector2(cellSize * 0.35f, cellSize * 0.25f),
		};
		canvas.DrawColoredPolygon(pts, Color);
	}
}
