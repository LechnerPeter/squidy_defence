public sealed class PhysicalResistanceCurse : Curse {
	public override string Key => "physical_resistance";
	public override string Label => "Iron Hides";
	public override string Description => "Every enemy gains +20% Physical resistance, forever.";

	public override void Apply() => GameManager.Instance.CursePhysicalResistanceBonus += GameData.CurseResistanceBonus;
}
