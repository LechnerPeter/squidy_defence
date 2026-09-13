using Godot;

public static partial class GameData {
	// edgeLength: world-space length of one Penrose tile edge. radius: only tiles
	// whose center falls within this distance of the origin are kept - see
	// PenroseTiling.Generate. The pentagrid is naturally centered at the world
	// origin, so there's no GridOrigin offset to track any more.
	public const float PenroseEdgeLength = 44.0f;
	public const float PenroseRadius = 420.0f;

	public const int StartingGold = 300;
	public const int StartingLives = 20;

	public const int RockCount = 12;
	public const int WaterCount = 8;
}
