public sealed class ElementalResistanceCurse : Curse {
	public override string Key => "elemental_resistance";
	public override string Label => "Storm Ward";
	public override string Description => "Every enemy gains +20% Elemental resistance, forever.";

	public override void Apply() => GameManager.Instance.CurseElementalResistanceBonus += GameData.CurseResistanceBonus;
}
