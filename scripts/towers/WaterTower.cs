using Godot;

public partial class WaterTower : BaseTower {
	public WaterTower() {
		tower_type = "water";
	}

	protected override string BaseTowerName => "Water";
	protected override string BaseFlavor => "A pool blocking the tile, already there when you arrive. Can't be built on or removed — not even with a wall.";
}
