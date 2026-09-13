using Godot;
using System.Collections.Generic;

public partial class GameManager : Node {
	public static GameManager Instance { get; private set; }

	[Signal]
	public delegate void GoldChangedEventHandler(int gold);
	[Signal]
	public delegate void LivesChangedEventHandler(int lives);
	[Signal]
	public delegate void WaveChangedEventHandler(int wave, int total);
	[Signal]
	public delegate void GameOverEventHandler(bool won);
	[Signal]
	public delegate void CursesChangedEventHandler();

	public int Gold { get; private set; } = GameData.StartingGold;
	public int Lives { get; private set; } = GameData.StartingLives;
	public int Wave { get; private set; } = 0;
	public int TotalWaves = 0;

	public float CursePhysicalResistanceBonus = 0.0f;
	public float CurseElementalResistanceBonus = 0.0f;

	// Per-enemy-type health/speed multipliers (see EelStrengthCurse/BruteToughnessCurse/
	// RunnerFrenzyCurse) - keyed by the same type string used in GameData.EnemyScenes/
	// SpawnEntry.Type, so a curse can single out one enemy type without touching any other.
	public readonly Dictionary<string, float> EnemyTypeHealthMult = new Dictionary<string, float>();
	public readonly Dictionary<string, float> EnemyTypeSpeedMult = new Dictionary<string, float>();

	public void MultiplyEnemyTypeHealth(string type, float mult) =>
		EnemyTypeHealthMult[type] = EnemyTypeHealthMult.GetValueOrDefault(type, 1.0f) * mult;

	public void MultiplyEnemyTypeSpeed(string type, float mult) =>
		EnemyTypeSpeedMult[type] = EnemyTypeSpeedMult.GetValueOrDefault(type, 1.0f) * mult;

	public float GetEnemyTypeHealthMult(string type) => EnemyTypeHealthMult.GetValueOrDefault(type, 1.0f);
	public float GetEnemyTypeSpeedMult(string type) => EnemyTypeSpeedMult.GetValueOrDefault(type, 1.0f);

	// How many times each curse has been picked this run (a curse can be offered and
	// chosen again later - see Main.StartCurseChoice) - read by UI.RefreshCurseIcons
	// to show a running strip of every curse active so far, with a stack count.
	public readonly Dictionary<string, int> CurseCounts = new Dictionary<string, int>();

	public override void _Ready() {
		Instance = this;
	}

	public void Reset() {
		Gold = GameData.StartingGold;
		Lives = GameData.StartingLives;
		Wave = 0;
		CursePhysicalResistanceBonus = 0.0f;
		CurseElementalResistanceBonus = 0.0f;
		EnemyTypeHealthMult.Clear();
		EnemyTypeSpeedMult.Clear();
		CurseCounts.Clear();
		EmitSignal(SignalName.GoldChanged, Gold);
		EmitSignal(SignalName.LivesChanged, Lives);
		EmitSignal(SignalName.WaveChanged, Wave, TotalWaves);
		EmitSignal(SignalName.CursesChanged);
	}

	public void ApplyCurse(string curseType) {
		GameData.GetCurse(curseType)?.Apply();
		CurseCounts[curseType] = CurseCounts.GetValueOrDefault(curseType, 0) + 1;
		EmitSignal(SignalName.CursesChanged);
	}

	public void AddGold(int amount) {
		Gold += amount;
		EmitSignal(SignalName.GoldChanged, Gold);
	}

	public bool SpendGold(int amount) {
		if (Gold < amount)
			return false;
		Gold -= amount;
		EmitSignal(SignalName.GoldChanged, Gold);
		return true;
	}

	public void LoseLife(int amount = 1) {
		Lives = Mathf.Max(0, Lives - amount);
		EmitSignal(SignalName.LivesChanged, Lives);
		if (Lives == 0)
			EmitSignal(SignalName.GameOver, false);
	}

	public void AdvanceWave() {
		Wave += 1;
		EmitSignal(SignalName.WaveChanged, Wave, TotalWaves);
	}

	public void Victory() {
		EmitSignal(SignalName.GameOver, true);
	}
}
