using Godot;
using System.Collections.Generic;

public static partial class GameData {
	// A regular Outpost only ever buffs one tower (its single platform), so its
	// per-tower buff is stronger than Big Outpost's - which trades that strength for
	// spreading buffs across many platforms instead.
	public const float OutpostBuffDamageMult = 1.5f;
	public const float OutpostBuffRangeMult = 1.35f;
	public const float BigOutpostBuffDamageMult = 1.3f;
	public const float BigOutpostBuffRangeMult = 1.2f;

	public static readonly string[] TripleMergePattern = { "cannon", "spikes", "cannon" };
	public const string TripleMergeResult = "crusher";

	public static readonly Dictionary<string, Dictionary<string, int>> TriangleMergeRecipes = new Dictionary<string, Dictionary<string, int>> {
		{ "long_range_frost", new Dictionary<string, int> { { "cannon", 2 }, { "frost", 1 } } },
		{ "auto_cannon", new Dictionary<string, int> { { "basic", 2 }, { "cannon", 1 } } },
		{ "chill_turret", new Dictionary<string, int> { { "basic", 2 }, { "frost", 1 } } },
		{ "lightning_frost", new Dictionary<string, int> { { "lightning", 2 }, { "frost", 1 } } },
		{ "twin_boomerang", new Dictionary<string, int> { { "boomerang", 1 }, { "cannon", 1 }, { "frost", 1 } } },
		{ "chain_reactor", new Dictionary<string, int> { { "cannon", 1 }, { "lightning", 1 }, { "ricochet", 1 } } },
		{ "sniper", new Dictionary<string, int> { { "basic", 2 }, { "lightning", 1 } } },
		{ "outpost", new Dictionary<string, int> { { "wall", 3 } } },
	};

	// A Big Outpost forms at a Penrose vertex where at least this many tiles meet
	// (see Main.FindBigOutpost) - unlike the old fixed 6-cell shape, the real
	// required wall count is however many tiles that specific vertex's ring
	// actually has, computed at search time. This constant is only the minimum
	// floor and the shop's rough cost estimate (see Main.RecipeRequirements /
	// MissingT1Cost, and TowerT2Hints's matching hint text).
	public const int BigOutpostMinRingSize = 5;
	public static readonly Dictionary<string, int> BigOutpostRequired = new Dictionary<string, int> { { "wall", BigOutpostMinRingSize } };
}
