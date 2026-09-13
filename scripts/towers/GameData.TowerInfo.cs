using Godot;
using System.Collections.Generic;

public readonly record struct TowerInfo(string Name, string Flavor, List<string> Lines, List<string> Effects);

public static partial class GameData {
	public static TowerInfo DescribeStats(string type, Godot.Collections.Dictionary s, int outpostStacks, int bigOutpostStacks, Godot.Collections.Array tileBuffs, float cellSize) {
		int buffStacks = outpostStacks + bigOutpostStacks;
		var lines = new List<string>();
		float Get(string key, float def = 0.0f) => s.ContainsKey(key) ? s[key].AsSingle() : def;
		bool GetBool(string key, bool def = false) => s.ContainsKey(key) ? s[key].AsBool() : def;
		bool TryGetSlow(Godot.Collections.Dictionary source, out float magnitude, out float slowDuration) {
			magnitude = 0.0f;
			slowDuration = 0.0f;
			if (!source.ContainsKey("on_hit_statuses"))
				return false;
			foreach (Variant v in source["on_hit_statuses"].AsGodotArray()) {
				Godot.Collections.Dictionary status = v.AsGodotDictionary();
				if (status.ContainsKey("type") && status["type"].AsString() == "slow") {
					magnitude = status["magnitude"].AsSingle();
					slowDuration = status["duration"].AsSingle();
					return true;
				}
			}
			return false;
		}

		if (type == "wall") {
			lines.Add("Blocks this tile. No attack.");
			lines.Add("Build a tower on it to upgrade — no refund for the wall.");
		}
		else if (type == "rock") {
			lines.Add("Natural obstacle. Blocks this tile permanently.");
			lines.Add("Build a wall on it to make use of the tile.");
		}
		else if (type == "water") {
			lines.Add("Natural obstacle. Blocks this tile permanently.");
			lines.Add("Unlike rock, a wall can't be built over it.");
		}
		else if (type == "outpost") {
			lines.Add("3 walls that touch each other. No attack.");
			lines.Add("Their shared corner opens an offset platform for one buffed tower.");
		}
		else if (type == "big_outpost") {
			lines.Add("5 or more walls fused at one big junction. No attack.");
			lines.Add("Opens one raised platform per wall, each for one buffed tower.");
		}
		else if (type == "flipper") {
			lines.Add("No attack. The instant it's built, any buff tile in one of its edge-adjacent tiles swaps to the tile directly opposite it.");
			lines.Add("A tile that would land somewhere invalid (blocked, occupied, off the grid) stays put instead.");
		}
		else if (type == "lightning" || type == "lightning_frost") {
			bool hasSlowRamp = s.ContainsKey("max_slow_magnitude");
			lines.Add($"Base beam DPS: {Get("damage"):F1}, ramping up the longer it stays locked on one target");
			lines.Add($"Ramps to {Get("max_ramp_multiplier"):F1}x damage (+{(int)(Get("ramp_per_second") * 100.0f)}%/sec of sustained focus)");
			lines.Add($"Range: {Get("range") / cellSize:F1} tiles");
			if (hasSlowRamp) {
				lines.Add($"Also chills the target: starts at {(int)(Get("base_slow_magnitude") * 100.0f)}% slow, ramping to {(int)(Get("max_slow_magnitude") * 100.0f)}% (+{(int)(Get("slow_ramp_per_second") * 100.0f)}%/sec)");
			}
			lines.Add($"Switching targets resets the ramp back to base damage{(hasSlowRamp ? " and slow" : "")}.");
		}
		else if (GetBool("boomerang") && s.ContainsKey("boomerang_projectiles") && s["boomerang_projectiles"].AsGodotArray().Count > 0) {
			lines.Add($"Range: {Get("range") / cellSize:F1} tiles");
			foreach (Variant profileVariant in s["boomerang_projectiles"].AsGodotArray()) {
				Godot.Collections.Dictionary profile = profileVariant.AsGodotDictionary();
				string key = profile.ContainsKey("key") ? profile["key"].AsString() : "shot";
				key = char.ToUpperInvariant(key[0]) + key.Substring(1);
				float profileDamage = profile.ContainsKey("damage") ? profile["damage"].AsSingle() : 0.0f;
				float profileSplash = profile.ContainsKey("splash_radius") ? profile["splash_radius"].AsSingle() : 0.0f;
				bool profileHasSlow = TryGetSlow(profile, out float profileSlow, out float profileSlowDuration);
				if (profileSplash > 0.0f)
					lines.Add($"{key} shot: {profileDamage:F1} dmg, splash {profileSplash / cellSize:F1} tiles");
				else if (profileHasSlow)
					lines.Add($"{key} shot: {profileDamage:F1} dmg, slows {(int)(profileSlow * 100.0f)}% for {profileSlowDuration:F1}s");
				else
					lines.Add($"{key} shot: {profileDamage:F1} dmg");
			}
			lines.Add("Both shots fly out and back independently at different speeds, so they never stay in sync.");
			lines.Add("No fixed attack speed for either shot — closer enemies mean faster hits.");
		}
		else if (GetBool("boomerang")) {
			lines.Add($"Damage: {Get("damage"):F1} per hit");
			lines.Add($"Range: {Get("range") / cellSize:F1} tiles");
			lines.Add("No fixed attack speed — its shot flies to the target and all the way back before firing again.");
			lines.Add("Whirls briefly around the target, then again around the tower, before it's ready to fire.");
			lines.Add("Closer enemies mean a shorter round trip, so it hits far faster up close than at max range.");
			lines.Add("Attack speed buffs make its shot fly faster instead of firing on a timer.");
		}
		else if (GetBool("continuous")) {
			lines.Add($"Total DPS: {Get("damage"):F1} (continuous)");
			lines.Add("Splits evenly across enemies hit");
			lines.Add($"Damage radius: {Get("splash_radius") / cellSize:F1} tiles");
			float maxDamagePerRound = Get("max_damage_per_round", float.PositiveInfinity);
			if (maxDamagePerRound < float.PositiveInfinity)
				lines.Add($"Max damage/round: {maxDamagePerRound:F0}");
			if (type == "crusher")
				lines.Add("Only the center tile is walkable — the flanking tiles block the path.");
			else if (GetBool("non_blocking"))
				lines.Add("Does not block the path.");
		}
		else {
			float damage = Get("damage");
			float fireRate = Get("fire_rate");
			lines.Add($"Damage: {damage:F1} per shot");
			lines.Add($"Fire rate: {fireRate:F1} / sec");
			lines.Add($"DPS: {damage * fireRate:F1}");
			if (GetBool("global_range"))
				lines.Add("Range: unlimited, but only at an enemy it has a clear line of sight to");
			else
				lines.Add($"Range: {Get("range") / cellSize:F1} tiles");
			float splash = Get("splash_radius");
			if (splash > 0.0f) {
				lines.Add($"Splash radius: {splash / cellSize:F1} tiles");
				lines.Add($"Total hit damage capped at {damage:F1}");
			}
			if (TryGetSlow(s, out float slow, out float slowDuration))
				lines.Add($"Slows by {(int)(slow * 100.0f)}% for {slowDuration:F1}s");
			if (s.ContainsKey("max_bounces") && Get("max_bounces") > 0.0f) {
				int bounces = (int)Get("max_bounces");
				float bounceMult = Get("bounce_damage_falloff");
				lines.Add($"Ricochets up to {bounces} more time{(bounces == 1 ? "" : "s")} within {Get("bounce_range") / cellSize:F1} tiles, preferring enemies closer to the start, never hitting the same enemy twice");
				if (bounceMult >= 1.0f)
					lines.Add($"Each bounce deals {(int)((bounceMult - 1.0f) * 100.0f)}% more damage than the last");
				else
					lines.Add($"Each bounce deals {(int)(bounceMult * 100.0f)}% of the previous hit's damage");
				if (s.ContainsKey("explosion_radius") && Get("explosion_radius") > 0.0f)
					lines.Add($"When the chain ends, it detonates for its current damage within {Get("explosion_radius") / cellSize:F1} tiles");
			}
		}

		bool dealsDamage = Get("damage") > 0.0f || (s.ContainsKey("boomerang_projectiles") && s["boomerang_projectiles"].AsGodotArray().Count > 0);
		if (dealsDamage) {
			string damageType = s.ContainsKey("damage_type") ? s["damage_type"].AsString() : "Physical";
			lines.Add($"Damage type: {damageType}");
			// Only base (T1) towers are shape-restricted (see Main.TileShapeAllowed) -
			// a T2 tower always builds through a multi-cell recipe instead of sitting on
			// one single tile, so the restriction never applies to it.
			if (GetTier(type) == 1)
				lines.Add($"Buildable only on {(damageType == "Elemental" ? "thin" : "thick")} tiles.");
		}

		bool holdsBuffsForPlatform = type == "outpost" || type == "big_outpost";
		if (buffStacks > 0 && !holdsBuffsForPlatform) {
			int dmgPct = (int)((Mathf.Pow(OutpostBuffDamageMult, buffStacks) - 1.0f) * 100.0f);
			int rngPct = (int)((Mathf.Pow(OutpostBuffRangeMult, buffStacks) - 1.0f) * 100.0f);
			string stackSuffix = buffStacks > 1 ? $" (stacked x{buffStacks})" : "";
			lines.Add($"Outpost buffed: +{dmgPct}% DMG, +{rngPct}% RNG{stackSuffix}");
		}

		var tileBuffTypes = new List<string>();
		foreach (Variant t in tileBuffs)
			tileBuffTypes.Add(t.AsString());

		if (tileBuffTypes.Count > 0) {
			var counts = new Dictionary<string, int>();
			foreach (string t in tileBuffTypes)
				counts[t] = counts.GetValueOrDefault(t, 0) + 1;
			foreach (KeyValuePair<string, int> entry in counts) {
				string buffLabel = GetTileBuff(entry.Key)?.Label ?? entry.Key;
				if (entry.Key == "multishot") {
					string targetWord = entry.Value == 1 ? "target" : "targets";
					lines.Add(holdsBuffsForPlatform
						? $"Passes +{entry.Value} {targetWord} per shot to the tower on its platform"
						: $"Buff tile: +{entry.Value} {targetWord} per shot");
					continue;
				}
				int pct = (int)((Mathf.Pow(TileBuffMult, entry.Value) - 1.0f) * 100.0f);
				string suffix = entry.Value > 1 ? $" (stacked x{entry.Value})" : "";
				lines.Add(holdsBuffsForPlatform
					? $"Passes +{pct}% {buffLabel}{suffix} to the tower on its platform"
					: $"Buff tile: +{pct}% {buffLabel}{suffix}");
			}
		}

		var effects = new List<string>();
		if (outpostStacks > 0 && !holdsBuffsForPlatform)
			effects.Add("outpost_buff");
		if (bigOutpostStacks > 0 && !holdsBuffsForPlatform)
			effects.Add("big_outpost_buff");
		foreach (string t in new List<string>(new HashSet<string>(tileBuffTypes))) {
			switch (t) {
				case "range": effects.Add("tile_range"); break;
				case "damage": effects.Add("tile_damage"); break;
				case "atk_speed": effects.Add("tile_atk_speed"); break;
				case "multishot": effects.Add("tile_multishot"); break;
			}
		}

		string towerName = s.ContainsKey("name") ? s["name"].AsString() : "Tower";
		return new TowerInfo(towerName, GetTowerFlavor(type), lines, effects);
	}
}
