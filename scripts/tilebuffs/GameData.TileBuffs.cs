using System.Collections.Generic;

public static partial class GameData {
	public const float TileBuffMult = 1.3f;

	public static readonly List<TileBuff> TileBuffs = new List<TileBuff> {
		new RangeTileBuff(),
		new DamageTileBuff(),
		new AtkSpeedTileBuff(),
		new MultishotTileBuff(),
	};

	public static TileBuff GetTileBuff(string key) {
		foreach (TileBuff buff in TileBuffs) {
			if (buff.Key == key)
				return buff;
		}
		return null;
	}
}
