public sealed class EelStrengthCurse : Curse {
	public override string Key => "eel_strength";
	public override string Label => "Swarming Tide";
	public override string Description => "Eels gain +15% health and +15% speed, forever.";

	public override void Apply() {
		GameManager.Instance.MultiplyEnemyTypeHealth("eel", GameData.CurseTypeStatMult);
		GameManager.Instance.MultiplyEnemyTypeSpeed("eel", GameData.CurseTypeStatMult);
	}
}
