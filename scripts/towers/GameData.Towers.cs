using Godot;
using System.Collections.Generic;

public static partial class GameData {
	public static readonly string[] TowerTypeKeys = {
		"basic", "cannon", "frost", "wall", "spikes", "boomerang", "lightning", "flipper", "ricochet",
		"twin_boomerang", "long_range_frost", "auto_cannon", "chill_turret", "lightning_frost", "chain_reactor",
		"crusher", "outpost", "big_outpost", "sniper",
	};

	// No attack of their own - shape the maze / buff whatever's built on or near them.
	public static readonly HashSet<string> UtilityTowerTypes = new HashSet<string> { "wall", "flipper", "outpost", "big_outpost" };

	public static readonly Dictionary<string, PackedScene> TowerScenes = new Dictionary<string, PackedScene> {
		{ "basic", GD.Load<PackedScene>("res://scenes/towers/TurretTower.tscn") },
		{ "cannon", GD.Load<PackedScene>("res://scenes/towers/CannonTower.tscn") },
		{ "frost", GD.Load<PackedScene>("res://scenes/towers/FrostTower.tscn") },
		{ "wall", GD.Load<PackedScene>("res://scenes/towers/WallTower.tscn") },
		{ "spikes", GD.Load<PackedScene>("res://scenes/towers/SpikesTower.tscn") },
		{ "boomerang", GD.Load<PackedScene>("res://scenes/towers/BoomerangTower.tscn") },
		{ "lightning", GD.Load<PackedScene>("res://scenes/towers/LightningTower.tscn") },
		{ "lightning_frost", GD.Load<PackedScene>("res://scenes/towers/LightningFrostTower.tscn") },
		{ "flipper", GD.Load<PackedScene>("res://scenes/towers/FlipperTower.tscn") },
		{ "ricochet", GD.Load<PackedScene>("res://scenes/towers/RicochetTower.tscn") },
		{ "chain_reactor", GD.Load<PackedScene>("res://scenes/towers/ChainReactorTower.tscn") },
		{ "twin_boomerang", GD.Load<PackedScene>("res://scenes/towers/TwinBoomerangTower.tscn") },
		{ "long_range_frost", GD.Load<PackedScene>("res://scenes/towers/LongRangeFrostTower.tscn") },
		{ "auto_cannon", GD.Load<PackedScene>("res://scenes/towers/AutoCannonTower.tscn") },
		{ "chill_turret", GD.Load<PackedScene>("res://scenes/towers/ChillTurretTower.tscn") },
		{ "crusher", GD.Load<PackedScene>("res://scenes/towers/CrusherTower.tscn") },
		{ "outpost", GD.Load<PackedScene>("res://scenes/towers/OutpostTower.tscn") },
		{ "big_outpost", GD.Load<PackedScene>("res://scenes/towers/BigOutpostTower.tscn") },
		{ "rock", GD.Load<PackedScene>("res://scenes/towers/RockTower.tscn") },
		{ "water", GD.Load<PackedScene>("res://scenes/towers/WaterTower.tscn") },
		{ "sniper", GD.Load<PackedScene>("res://scenes/towers/SniperTower.tscn") },
	};

	public static readonly Dictionary<string, string> TowerT2Hints = new Dictionary<string, string> {
		{ "long_range_frost", "(place directly, or 2 Cannon + 1 Frost)" },
		{ "auto_cannon", "(place directly, or 2 Turret + 1 Cannon)" },
		{ "chill_turret", "(place directly, or 2 Turret + 1 Frost)" },
		{ "lightning_frost", "(place directly, or 2 Lightning + 1 Frost)" },
		{ "twin_boomerang", "(place directly, or 1 Boomerang + 1 Cannon + 1 Frost)" },
		{ "chain_reactor", "(place directly, or 1 Cannon + 1 Lightning + 1 Ricochet)" },
		{ "crusher", "(place directly, or Cannon-Spikes-Cannon)" },
		{ "outpost", "(place directly, or 3 Walls in a triangle)" },
		{ "big_outpost", "(place directly, or 5+ Walls meeting at one junction)" },
		{ "sniper", "(place directly, or 2 Turret + 1 Lightning)" },
	};

	public static int GetTier(string type) {
		Godot.Collections.Dictionary data = GetTowerData(type);
		return data.ContainsKey("tier") ? data["tier"].AsInt32() : 0;
	}

	public static bool IsT2(string type) => GetTier(type) == 2;

	public static Godot.Collections.Dictionary GetTowerData(string type) {
		if (!TowerScenes.TryGetValue(type, out PackedScene scene) || scene == null)
			return new Godot.Collections.Dictionary();
		BaseTower temp = (BaseTower)scene.Instantiate();
		Godot.Collections.Dictionary data = temp.get_preview_data();
		temp.Free();
		return data;
	}

	private static string GetTowerFlavor(string type) {
		Godot.Collections.Dictionary data = GetTowerData(type);
		return data.ContainsKey("flavor") ? data["flavor"].AsString() : "";
	}

	public static string GetTowerAbbreviation(string type) {
		Godot.Collections.Dictionary data = GetTowerData(type);
		return data.ContainsKey("abbreviation") ? data["abbreviation"].AsString() : "";
	}

	public static int GetScaledCost(Godot.Collections.Dictionary data, int existingCount) {
		int cost = data["cost"].AsInt32();
		int costStep = data.ContainsKey("cost_step") ? data["cost_step"].AsInt32() : 0;
		return cost + costStep * existingCount;
	}

	public static List<int> GetBlockingCells(string resultType, List<int> cells) {
		if (resultType == "crusher" && cells.Count == 3)
			return new List<int> { cells[0], cells[2] };
		Godot.Collections.Dictionary data = GetTowerData(resultType);
		if (data.ContainsKey("non_blocking") && data["non_blocking"].AsBool())
			return new List<int>();
		return new List<int>(cells);
	}
}
