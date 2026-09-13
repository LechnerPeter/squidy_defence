using Godot;

public partial class OutpostTower : BaseTower {
	public OutpostTower() {
		tower_type = "outpost";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Outpost";
	protected override int BaseCost => 25;
	protected override int BaseCostStep => 5;
	protected override Color BaseColor => new Color(0.65f, 0.55f, 0.3f);
	protected override string BaseFlavor => "3 walls fused where they meet at one junction, with a raised platform at its center — build one tower there, stronger behind cover.";
	protected override string BaseAbbreviation => "OUT";

	public override void _Draw() {
		Color c = TowerColor;
		foreach (TileShape shape in CellShapes) {
			DrawTileAt(shape, c, new Color(0, 0, 0, 0.4f), 2.0f);
			DrawTowerIcon(shape);
		}
		foreach (Vector2 platformOffset in platform_offsets) {
			DrawArc(platformOffset, CellSize * 0.4f, 0.0f, Mathf.Tau, 20, new Color(1.0f, 0.85f, 0.3f, 0.75f), 2.0f);
		}
		DrawTileBuffIcon();
	}
}
