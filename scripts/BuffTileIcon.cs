using Godot;

// The wiki's stand-in for a buff tile: the same diamond badge frame the tower icons
// use (see gen_icons.py/BaseTower.DrawTowerIcon), with the buff's own glyph (see
// TileBuff.Draw) drawn on top - not just a flat color swatch, so it actually reads
// as "a tile" rather than an arbitrary color chip.
public partial class BuffTileIcon : Control {
	private string _buffKey = "";

	public void Setup(string buffKey) {
		_buffKey = buffKey;
		QueueRedraw();
	}

	public override void _Draw() {
		TileBuff buff = GameData.GetTileBuff(_buffKey);
		if (buff == null || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;
		Vector2 center = Size / 2.0f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.46f;

		var pts = new Vector2[4];
		for (int i = 0; i < 4; i++)
			pts[i] = center + new Vector2(r, 0.0f).Rotated(Mathf.DegToRad(90.0f * i - 90.0f));
		DrawColoredPolygon(pts, new Color(0.16f, 0.18f, 0.15f, 1.0f));
		DrawColoredPolygon(pts, new Color(buff.Color.R, buff.Color.G, buff.Color.B, 0.25f));
		for (int i = 0; i < 4; i++)
			DrawLine(pts[i], pts[(i + 1) % 4], new Color(0, 0, 0, 0.6f), 1.5f);

		buff.Draw(this, center, r);
	}
}
