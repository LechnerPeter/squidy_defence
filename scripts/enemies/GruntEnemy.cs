public partial class GruntEnemy : BaseEnemy {
	public GruntEnemy() {
		enemy_type = "grunt";
	}

	protected override string DisplayName => "Grunt";
	protected override string Flavor => "Basic infantry. No frills, no resistances.";
}
