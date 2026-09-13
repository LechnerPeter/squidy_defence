using Godot;

public partial class BigOutpostTower : OutpostTower {
	public BigOutpostTower() {
		tower_type = "big_outpost";
	}

	protected override string BaseTowerName => "Big Outpost";
	protected override int BaseCost => 60;
	protected override int BaseCostStep => 12;
	protected override Color BaseColor => new Color(0.7f, 0.6f, 0.32f);
	protected override string BaseFlavor => "5 or more walls fused at one big junction — a raised platform opens for each wall, one buffed tower each.";
	protected override string BaseAbbreviation => "BIG-OUT";
}
