public sealed class BruteToughnessCurse : Curse {
	public override string Key => "brute_toughness";
	public override string Label => "Brutish Reinforcement";
	public override string Description => "Brutes gain +15% health, forever.";

	public override void Apply() => GameManager.Instance.MultiplyEnemyTypeHealth("brute", GameData.CurseTypeStatMult);
}
