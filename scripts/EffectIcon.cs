using Godot;
using System.Collections.Generic;

public partial class EffectIcon : Control {
	private readonly struct EffectInfo {
		public readonly Color Color;
		public readonly string Letter;
		public readonly bool ArrowUp;
		public readonly string Label;
		public readonly string Desc;

		public EffectInfo(Color color, string letter, bool arrowUp, string label, string desc) {
			Color = color;
			Letter = letter;
			ArrowUp = arrowUp;
			Label = label;
			Desc = desc;
		}
	}

	private static readonly Dictionary<string, EffectInfo> EffectInfoTable = new Dictionary<string, EffectInfo> {
		{ "outpost_buff", new EffectInfo(new Color(1.0f, 0.85f, 0.3f), "O", true, "Outpost Buffed", "+50% damage, +35% range per stack.") },
		{ "big_outpost_buff", new EffectInfo(new Color(0.9f, 0.7f, 0.15f), "B", true, "Big Outpost Buffed", "+30% damage, +20% range per stack.") },
		{ "tile_range", new EffectInfo(new Color(0.3f, 0.7f, 1.0f), "", true, "Range Tile", "+30% range, stacks per tile.") },
		{ "tile_damage", new EffectInfo(new Color(1.0f, 0.35f, 0.25f), "", true, "Damage Tile", "+30% damage, stacks per tile.") },
		{ "tile_atk_speed", new EffectInfo(new Color(1.0f, 0.85f, 0.2f), "", true, "Attack Speed Tile", "+30% attack speed, stacks per tile.") },
		{ "tile_multishot", new EffectInfo(new Color(0.65f, 0.4f, 1.0f), "", true, "Multishot Tile", "+1 target per shot, stacks per tile.") },
	};

	private string _effectType = "";

	public void Setup(string type) {
		_effectType = type;
		TooltipText = EffectInfoTable.TryGetValue(type, out EffectInfo info) ? $"{info.Label}\n{info.Desc}" : type;
		QueueRedraw();
	}

	public override void _Draw() {
		if (!EffectInfoTable.TryGetValue(_effectType, out EffectInfo info) || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;
		Vector2 center = Size / 2.0f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.46f;
		Rect2 rect = new Rect2(center - new Vector2(r, r), new Vector2(r * 2.0f, r * 2.0f));
		DrawRect(rect, info.Color);
		DrawRect(rect, new Color(0, 0, 0, 0.55f), false, 1.5f);
		if (!string.IsNullOrEmpty(info.Letter)) {
			Font font = GetThemeDefaultFont();
			int fontSize = 10;
			Vector2 textSize = font.GetStringSize(info.Letter, HorizontalAlignment.Left, -1, fontSize);
			DrawString(font, center + new Vector2(-textSize.X / 2.0f, textSize.Y * 0.32f), info.Letter, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.95f));
		}
		else {
			float ar = r * 0.55f;
			Vector2[] pts;
			if (info.ArrowUp)
				pts = new Vector2[] { center + new Vector2(0.0f, -ar), center + new Vector2(ar * 0.9f, ar * 0.6f), center + new Vector2(-ar * 0.9f, ar * 0.6f) };
			else
				pts = new Vector2[] { center + new Vector2(0.0f, ar), center + new Vector2(ar * 0.9f, -ar * 0.6f), center + new Vector2(-ar * 0.9f, -ar * 0.6f) };
			DrawColoredPolygon(pts, new Color(1, 1, 1, 0.95f));
		}
	}
}
