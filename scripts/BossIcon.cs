using Godot;

// The wiki's stand-in for a boss: the same shapes each boss actually draws on the
// field (see BossEnemy/JuggernautBoss/TitanBoss.DrawBody and DrawBossCrown), redrawn
// here relative to an arbitrary center/radius instead of the enemy's own instance
// fields, since a Control can't just call a spawned Node2D's protected _Draw() logic
// directly.
public partial class BossIcon : Control {
	private static readonly Color TitanPhysicalColor = new Color(0.8f, 0.7f, 0.25f);
	private static readonly Color TitanElementalColor = new Color(0.3f, 0.75f, 0.95f);
	private static readonly Color CrownColor = new Color(1.0f, 0.85f, 0.2f, 0.9f);
	private static readonly Color CrownOutlineColor = new Color(0.55f, 0.35f, 0.0f, 0.9f);

	private string _bossType = "";
	private Color _baseColor = Colors.White;

	public void Setup(string bossType) {
		_bossType = bossType;
		_baseColor = GameData.GetEnemyPreviewColor(bossType);
		QueueRedraw();
	}

	public override void _Draw() {
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
			return;
		Vector2 center = Size / 2.0f;
		// Sized so the crown's spike tips (1.25x this radius - see DrawCrown) still
		// land inside the control's bounds instead of clipping.
		float r = Mathf.Min(Size.X, Size.Y) * 0.36f;

		DrawCircle(center, r, _baseColor);
		switch (_bossType) {
			case "boss_juggernaut":
				DrawArc(center, r * 0.8f, 0.0f, Mathf.Tau, 6, new Color(0.2f, 0.17f, 0.1f, 0.9f), r * 0.2f);
				break;
			case "boss_titan":
				DrawArc(center, r * 0.75f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 16, TitanPhysicalColor, r * 0.23f);
				DrawArc(center, r * 0.75f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 16, TitanElementalColor, r * 0.23f);
				break;
		}
		DrawCrown(center, r);
		DrawArc(center, r, 0.0f, Mathf.Tau, 24, Colors.Black, 1.5f);
	}

	private void DrawCrown(Vector2 center, float r) {
		int spikes = 5;
		float baseR = r * 0.75f;
		float tipR = r * 1.25f;
		var pts = new Vector2[spikes * 2];
		for (int i = 0; i < spikes * 2; i++) {
			float angle = Mathf.DegToRad(360.0f / (spikes * 2) * i - 90.0f);
			float spikeR = (i % 2 == 0) ? tipR : baseR;
			pts[i] = center + new Vector2(spikeR, 0.0f).Rotated(angle);
		}
		DrawColoredPolygon(pts, CrownColor);
		var outline = new Vector2[pts.Length + 1];
		pts.CopyTo(outline, 0);
		outline[pts.Length] = pts[0];
		DrawPolyline(outline, CrownOutlineColor, 1.5f);
	}
}
