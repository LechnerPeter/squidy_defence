using Godot;

public abstract class TileBuff {
	public abstract string Key { get; }
	public abstract string Label { get; }
	public abstract Color Color { get; }

	// Status type applied to a tower built on a tile with this buff (see
	// BaseTower.apply_buffs / ApplyStatusEffect).
	public abstract string StatusType { get; }

	// How many of this buff tile the map generates (see Main.SpawnBuffTiles).
	public virtual int SpawnCount => 3;

	// Icon drawn on the map tile itself. Called from within the target canvas's own
	// active _draw() (Main._Draw() draws its own map tiles directly, and the wiki's
	// BuffTileIcon - a Control, not a Node2D - draws a standalone reference copy), so
	// it's safe for this to live outside that class. CanvasItem (rather than Node2D)
	// is the common base both callers actually need.
	public abstract void Draw(CanvasItem canvas, Vector2 center, float cellSize);
}
