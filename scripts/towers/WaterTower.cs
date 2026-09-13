using Godot;

public partial class WaterTower : BaseTower {
	public WaterTower() {
		tower_type = "water";
	}

	protected override string BaseTowerName => "Water";
	protected override Color BaseColor => new Color(0.22f, 0.42f, 0.62f);
	protected override string BaseFlavor => "A pool blocking the tile, already there when you arrive. Can't be built on or removed — not even with a wall.";

	public override void _Draw() {
		DrawTileAt(CellShapes[0], TowerColor, new Color(0, 0, 0, 0.35f), 1.5f);
		float bodyRadius = CellSize * 0.72f;
		Color rippleColor = new Color(1.0f, 1.0f, 1.0f, 0.3f);
		for (int i = -1; i <= 1; i++) {
			float y = i * bodyRadius * 0.32f;
			DrawArc(new Vector2(-bodyRadius * 0.15f, y), bodyRadius * 0.38f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 8, rippleColor, 1.5f);
		}
	}
}
