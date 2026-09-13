using Godot;

public partial class WallTower : BaseTower {
	public WallTower() {
		tower_type = "wall";
	}

	protected override string BaseTowerName => "Wall";
	protected override int BaseCost => 5;
	protected override int BaseCostStep => 5;
	protected override Color BaseColor => new Color(0.55f, 0.52f, 0.48f);
	protected override string BaseFlavor => "No attack — just blocks this tile to help shape the maze.";
	protected override string BaseAbbreviation => "WALL";

	public override void _Draw() {
		DrawTileAt(CellShapes[0], TowerColor, new Color(0, 0, 0, 0.4f), 2.0f);
		DrawTowerIcon(CellShapes[0]);
		DrawTileBuffIcon();
	}
}
