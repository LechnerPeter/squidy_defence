using Godot;

public partial class FlipperTower : BaseTower {
	public FlipperTower() {
		tower_type = "flipper";
	}

	protected override string BaseTowerName => "Flipper";
	protected override int BaseCost => 30;
	protected override int BaseCostStep => 10;
	protected override Color BaseColor => new Color(0.75f, 0.3f, 0.75f);
	protected override string BaseFlavor => "No attack. The instant it's built, any buff tile in one of its edge-adjacent tiles swaps to the tile directly opposite it.";
	protected override string BaseAbbreviation => "FLIP";
}
