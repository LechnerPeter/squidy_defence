using Godot;

public static partial class GameData {
	// edgeLength: world-space length of one Penrose tile edge. radius: only tiles
	// within this distance of the patch's cutout center are kept - see
	// PenroseGrid/PenroseTiling.Generate (the cutout itself is picked off-center,
	// at a random angle, from the broader pattern - not from the pentagrid's own
	// symmetric middle - but the returned patch is still translated back to sit
	// at the world origin, so there's no GridOrigin offset to track).
	public const float PenroseEdgeLength = 44.0f;
	public const float PenroseRadius = 600.0f;

	public const int StartingGold = 300;
	public const int StartingLives = 20;

	public const int RockCount = 12;
	public const int WaterCount = 8;
}
