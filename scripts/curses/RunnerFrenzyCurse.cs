public sealed class RunnerFrenzyCurse : Curse {
	public override string Key => "runner_frenzy";
	public override string Label => "Sprinting Pack";
	public override string Description => "Runners gain +15% speed, forever.";

	public override void Apply() => GameManager.Instance.MultiplyEnemyTypeSpeed("runner", GameData.CurseTypeStatMult);
}
