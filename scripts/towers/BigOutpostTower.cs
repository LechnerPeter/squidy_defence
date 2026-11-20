using Godot;

public partial class BigOutpostTower : OutpostTower {
	public BigOutpostTower() {
		tower_type = "big_outpost";
	}

	protected override string BaseTowerName => "Big Outpost";
	protected override int BaseCost => 60;
	protected override int BaseCostStep => 12;
	protected override string BaseFlavor => $"{GameData.BigOutpostMinRingSize} or more walls fused at one big junction — a raised platform opens for each wall, one buffed tower each.";
	protected override string BaseAbbreviation => "BIG-OUT";
}
