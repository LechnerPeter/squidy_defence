using System.Collections.Generic;

public static partial class GameData {
	public const float CurseTypeStatMult = 1.15f;
	public const float CurseResistanceBonus = 0.2f;

	public static readonly List<Curse> Curses = new List<Curse> {
		new EelStrengthCurse(),
		new BruteToughnessCurse(),
		new RunnerFrenzyCurse(),
		new PhysicalResistanceCurse(),
		new ElementalResistanceCurse(),
	};

	public static Curse GetCurse(string key) {
		foreach (Curse curse in Curses) {
			if (curse.Key == key)
				return curse;
		}
		return null;
	}
}
