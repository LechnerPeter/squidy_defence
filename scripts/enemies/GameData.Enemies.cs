using Godot;
using System.Collections.Generic;

public static partial class GameData {
	public static readonly Dictionary<string, PackedScene> EnemyScenes = new Dictionary<string, PackedScene> {
		{ "grunt", GD.Load<PackedScene>("res://scenes/enemies/GruntEnemy.tscn") },
		{ "runner", GD.Load<PackedScene>("res://scenes/enemies/RunnerEnemy.tscn") },
		{ "brute", GD.Load<PackedScene>("res://scenes/enemies/BruteEnemy.tscn") },
		{ "eel", GD.Load<PackedScene>("res://scenes/enemies/EelEnemy.tscn") },
		{ "golem", GD.Load<PackedScene>("res://scenes/enemies/GolemEnemy.tscn") },
		{ "wisp", GD.Load<PackedScene>("res://scenes/enemies/WispEnemy.tscn") },
		{ "boss", GD.Load<PackedScene>("res://scenes/enemies/BossEnemy.tscn") },
		{ "boss_juggernaut", GD.Load<PackedScene>("res://scenes/enemies/JuggernautBoss.tscn") },
		{ "boss_titan", GD.Load<PackedScene>("res://scenes/enemies/TitanBoss.tscn") },
	};

	// Wave-spawnable non-boss types, in wiki display order (see UI.BuildWikiPanel).
	public static readonly string[] EnemyTypeKeys = { "grunt", "runner", "brute", "eel", "golem", "wisp" };

	// Which boss shows up on each boss wave - cycled by boss_index rather than picked
	// randomly, so repeat playthroughs see the same boss on the same wave number.
	public static readonly string[] BossTypes = { "boss", "boss_juggernaut", "boss_titan" };

	public static string BossTypeFor(int bossIndex) => BossTypes[bossIndex % BossTypes.Length];

	// Static reference info/color for a type, built from a throwaway instance that's
	// never added to the tree or setup() - mirrors GetTowerData's temp-instantiate-
	// and-Free pattern. Used by the in-game wiki so it can list every enemy type
	// without spawning any of them onto the field.
	public static TowerInfo GetEnemyPreviewInfo(string type) {
		if (!EnemyScenes.TryGetValue(type, out PackedScene scene) || scene == null)
			return new TowerInfo(type, "", new List<string>(), new List<string>());
		BaseEnemy temp = (BaseEnemy)scene.Instantiate();
		TowerInfo info = temp.GetPreviewInfo();
		temp.Free();
		return info;
	}

	public static Color GetEnemyPreviewColor(string type) {
		if (!EnemyScenes.TryGetValue(type, out PackedScene scene) || scene == null)
			return Colors.White;
		BaseEnemy temp = (BaseEnemy)scene.Instantiate();
		Color color = temp.PreviewColor;
		temp.Free();
		return color;
	}
}
