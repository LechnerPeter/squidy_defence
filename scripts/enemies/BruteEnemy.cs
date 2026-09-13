using Godot;

public partial class BruteEnemy : BaseEnemy {
	public BruteEnemy() {
		enemy_type = "brute";
	}

	protected override float BaseHealth => 130.0f;
	protected override float BaseSpeed => 42.0f;
	protected override int BaseReward => 18;
	protected override int BaseDamageToLives => 2;
	protected override Color BaseColor => new Color(0.5f, 0.15f, 0.6f);
	protected override float BaseRadius => 19.0f;
	protected override string DisplayName => "Brute";
	protected override string Flavor => "Slow but heavy - soaks up more hits than it looks like it should.";

	protected override void DrawBody() {
		Vector2[] pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
			pts[i] = new Vector2(radius, 0.0f).Rotated(Mathf.DegToRad(60.0f * i + 30.0f));
		DrawColoredPolygon(pts, color);
	}
}
