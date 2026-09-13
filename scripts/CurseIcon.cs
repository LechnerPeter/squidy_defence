using Godot;
using System.Collections.Generic;

// A curse's own badge - a dark hex frame (matching the tower/enemy icon vocabulary
// elsewhere) with a small silhouette inside instead of a letter. The three enemy-type
// curses reuse that enemy's own body shape (see EelEnemy/BruteEnemy/RunnerEnemy
// DrawBody) so the icon reads as "this creature, empowered" at a glance; the two
// resistance curses get a shield and a lightning bolt instead, since they don't
// belong to one specific enemy.
public partial class CurseIcon : Control {
	private static readonly Dictionary<string, Color> ColorTable = new Dictionary<string, Color> {
		{ "eel_strength", new Color(0.2f, 0.75f, 0.65f) },
		{ "brute_toughness", new Color(0.5f, 0.15f, 0.6f) },
		{ "runner_frenzy", new Color(0.9f, 0.8f, 0.1f) },
		{ "physical_resistance", new Color(0.6f, 0.55f, 0.42f) },
		{ "elemental_resistance", new Color(0.3f, 0.7f, 0.95f) },
	};

	private string _curseKey = "";
	private int _stackCount = 1;

	public void Setup(string curseKey, int stackCount = 1) {
		_curseKey = curseKey;
		_stackCount = stackCount;
		Curse curse = GameData.GetCurse(curseKey);
		string stackSuffix = stackCount > 1 ? $" x{stackCount}" : "";
		TooltipText = curse != null ? $"{curse.Label}{stackSuffix}\n{curse.Description}" : curseKey;
		QueueRedraw();
	}

	public override void _Draw() {
		if (!ColorTable.TryGetValue(_curseKey, out Color fg) || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;
		Vector2 center = Size / 2.0f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.46f;

		var frame = new Vector2[6];
		for (int i = 0; i < 6; i++)
			frame[i] = center + new Vector2(r, 0.0f).Rotated(Mathf.DegToRad(60.0f * i - 30.0f));
		DrawColoredPolygon(frame, new Color(0.1f, 0.1f, 0.14f, 0.92f));
		for (int i = 0; i < 6; i++)
			DrawLine(frame[i], frame[(i + 1) % 6], new Color(0, 0, 0, 0.6f), 1.5f);

		DrawGlyph(_curseKey, center, r * 0.72f, fg);

		if (_stackCount > 1) {
			string stackText = $"x{_stackCount}";
			Font font = GetThemeDefaultFont();
			int stackFontSize = 10;
			Vector2 stackSize = font.GetStringSize(stackText, HorizontalAlignment.Left, -1, stackFontSize);
			Vector2 stackPos = center + new Vector2(r - stackSize.X * 0.4f, r - 1.0f);
			DrawString(font, stackPos, stackText, HorizontalAlignment.Left, -1, stackFontSize, new Color(1.0f, 0.85f, 0.3f, 1.0f));
		}
	}

	private void DrawGlyph(string key, Vector2 center, float r, Color fg) {
		switch (key) {
			case "eel_strength":
				DrawEelGlyph(center, r, fg);
				break;
			case "brute_toughness":
				DrawBruteGlyph(center, r, fg);
				break;
			case "runner_frenzy":
				DrawRunnerGlyph(center, r, fg);
				break;
			case "physical_resistance":
				DrawShieldGlyph(center, r, fg);
				break;
			case "elemental_resistance":
				DrawBoltGlyph(center, r, fg);
				break;
		}
	}

	// The same elongated fish silhouette as EelEnemy.DrawBody, facing right, scaled
	// down to icon size.
	private void DrawEelGlyph(Vector2 center, float r, Color fg) {
		Vector2[] pts = {
			center + new Vector2(r * 1.4f, 0.0f),
			center + new Vector2(r * 0.3f, -r * 0.55f),
			center + new Vector2(-r * 1.1f, -r * 0.35f),
			center + new Vector2(-r * 1.4f, 0.0f),
			center + new Vector2(-r * 1.1f, r * 0.35f),
			center + new Vector2(r * 0.3f, r * 0.55f),
		};
		DrawColoredPolygon(pts, fg);
	}

	// The same hexagon silhouette as BruteEnemy.DrawBody.
	private void DrawBruteGlyph(Vector2 center, float r, Color fg) {
		Vector2[] pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
			pts[i] = center + new Vector2(r, 0.0f).Rotated(Mathf.DegToRad(60.0f * i + 30.0f));
		DrawColoredPolygon(pts, fg);
	}

	// The same arrow/chevron silhouette as RunnerEnemy.DrawBody, facing right.
	private void DrawRunnerGlyph(Vector2 center, float r, Color fg) {
		Vector2[] pts = {
			center + new Vector2(r * 1.15f, 0.0f),
			center + new Vector2(-r * 0.75f, -r * 0.85f),
			center + new Vector2(-r * 0.3f, 0.0f),
			center + new Vector2(-r * 0.75f, r * 0.85f),
		};
		DrawColoredPolygon(pts, fg);
	}

	private void DrawShieldGlyph(Vector2 center, float r, Color fg) {
		Vector2[] pts = {
			center + new Vector2(-r * 0.55f, -r * 0.6f),
			center + new Vector2(r * 0.55f, -r * 0.6f),
			center + new Vector2(r * 0.55f, r * 0.05f),
			center + new Vector2(0.0f, r * 0.75f),
			center + new Vector2(-r * 0.55f, r * 0.05f),
		};
		DrawColoredPolygon(pts, fg);
		DrawLine(center + new Vector2(0.0f, -r * 0.6f), center + new Vector2(0.0f, r * 0.75f), new Color(0, 0, 0, 0.35f), 1.5f);
	}

	private void DrawBoltGlyph(Vector2 center, float r, Color fg) {
		Vector2[] pts = {
			center + new Vector2(r * 0.1f, -r * 0.9f),
			center + new Vector2(-r * 0.5f, r * 0.1f),
			center + new Vector2(-r * 0.05f, r * 0.1f),
			center + new Vector2(-r * 0.3f, r * 0.9f),
			center + new Vector2(r * 0.5f, -r * 0.15f),
			center + new Vector2(r * 0.05f, -r * 0.15f),
		};
		DrawColoredPolygon(pts, fg);
	}
}
