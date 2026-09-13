using Godot;

public partial class BossEnemy : BaseEnemy {
	public int boss_index = 0;

	public BossEnemy() {
		enemy_type = "boss";
	}

	protected override float BaseHealth => 900.0f * (1.0f + 0.5f * boss_index);
	protected override float BaseSpeed => 45.0f;
	protected override int BaseReward => (int)(80 * (1.0f + 0.35f * boss_index));
	protected override int BaseDamageToLives => 5;
	protected override Color BaseColor => new Color(0.75f, 0.1f, 0.55f);
	protected override float BaseRadius => 27.0f;
	protected override string DisplayName => "Boss";
	protected override string Flavor => "A straightforward heavyweight - no tricks, just a lot of health.";

	private const float MaxSlowMagnitude = 0.3f;

	protected override float ClampSlowMagnitude(float magnitude) => Mathf.Min(magnitude, MaxSlowMagnitude);

	protected override void DrawBody() {
		DrawCircle(Vector2.Zero, radius, color);
		DrawBossCrown();
	}

	// Protected so other boss variants (JuggernautBoss, TitanBoss) can layer their own
	// body art underneath and still get the same crown on top.
	protected void DrawBossCrown() {
		int spikes = 5;
		float baseR = radius * 0.75f;
		float tipR = radius * 1.25f;
		Vector2[] pts = new Vector2[spikes * 2];
		for (int i = 0; i < spikes * 2; i++) {
			float angle = Mathf.DegToRad(360.0f / (spikes * 2) * i - 90.0f);
			float r = (i % 2 == 0) ? tipR : baseR;
			pts[i] = new Vector2(r, 0.0f).Rotated(angle);
		}
		DrawColoredPolygon(pts, new Color(1.0f, 0.85f, 0.2f, 0.9f));
		Vector2[] outline = new Vector2[pts.Length + 1];
		pts.CopyTo(outline, 0);
		outline[pts.Length] = pts[0];
		DrawPolyline(outline, new Color(0.55f, 0.35f, 0.0f, 0.9f), 1.5f);
	}
}
