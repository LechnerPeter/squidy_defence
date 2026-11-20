using Godot;

public partial class WallTower : BaseTower {
	public WallTower() {
		tower_type = "wall";
	}

	protected override string BaseTowerName => "Wall";
	protected override int BaseCost => 5;
	protected override int BaseCostStep => 5;
	protected override string BaseFlavor => "No attack — just blocks this tile to help shape the maze.";
	protected override string BaseAbbreviation => "WALL";
	protected override bool HasPlinth => false;

	public override void _Draw() {
		DrawTileBuffIcon();
	}
}
