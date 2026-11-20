using Godot;

public static partial class GameData {
	// edgeLength: world-space length of one Penrose tile edge. radius: only tiles
	// within this distance of the patch's cutout center are kept - see
	// BoardGrid/PenroseTiling.Generate (the cutout itself is picked off-center,
	// at a random angle, from the broader pattern - not from the pentagrid's own
	// symmetric middle - but the returned patch is still translated back to sit
	// at the world origin, so there's no GridOrigin offset to track).
	//
	// PenroseEdgeLength is also the size every tower scene's art is authored at,
	// and the "tile" that ranges are described in - on a hat board as well.
	public const float PenroseEdgeLength = 44.0f;
	public const float PenroseRadius = 600.0f;

	// Einstein mode's board (see HatTiling). hatUnit: world length of the hat's short
	// sides (the hat is 8 kites of a hexagonal kite grid - see HatGenerator.HatOutline;
	// its other sides are sqrt(3) times this, bar one twice as long). Set so a hat
	// covers the same area as the average Penrose tile (thick and thin, weighted by
	// how often each occurs), so a hat board of the same radius holds about as many
	// tiles - around 700 - and everything balanced against tile size carries over.
	public const float HatUnit = 10.65f;
	public const float HatRadius = 600.0f;

	// The tiling of the run in progress - see GameManager.Tiling.
	public static TilingKind CurrentTiling => GameManager.Instance?.Tiling ?? TilingKind.Penrose;

	public const int StartingGold = 300;
	public const int StartingLives = 20;

	public const int RockCount = 12;
	public const int WaterCount = 8;
}
