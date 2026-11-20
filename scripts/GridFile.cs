using Godot;
using System;
using System.Collections.Generic;

// What a grid exported by Main's "Export Grid" button brings back - only the
// parts needed to rebuild the same board. The tiles' shapes and corner order
// are enough for the tiling itself: neighbors, vertex rings and vertex colors
// are all derived from those by BoardGrid.Load, exactly as for a freshly
// generated patch, so the export's copies of them are ignored.
public sealed class SavedGrid {
	public TilingKind Tiling = TilingKind.Penrose;
	public List<BoardTile> Tiles = new List<BoardTile>();
	public int Entry;
	public int Exit;
	public Dictionary<int, string> Structures = new Dictionary<int, string>();   // tile id -> structure type (rock/water/wall)
	public Dictionary<int, string> Buffs = new Dictionary<int, string>();        // tile id -> buff tile type
}

public static class GridFile {
	public const string ExportDir = "user://grid_exports";

	// The export's "tiling" value - an export without one predates Einstein mode, so
	// it's a Penrose grid.
	public static string TilingName(TilingKind tiling) => tiling == TilingKind.Hat ? "hat" : "penrose";

	// "grid_20261027_231500.json" for a Penrose grid, "grid_hat_20261027_231500.json"
	// for a hat grid - so the title screen can tell them apart without opening them.
	public static string NewExportPath(TilingKind tiling) =>
		$"{ExportDir}/grid_{(tiling == TilingKind.Hat ? "hat_" : "")}{DateTime.Now:yyyyMMdd_HHmmss}.json";

	public static TilingKind TilingFromFileName(string path) =>
		path.GetFile().StartsWith("grid_hat_") ? TilingKind.Hat : TilingKind.Penrose;

	// Newest first - export file names are timestamped, so name order is time order.
	// (Sorted by timestamp alone, so both tilings' exports interleave correctly.)
	public static List<string> ListSaved() {
		var paths = new List<string>();
		if (!DirAccess.DirExistsAbsolute(ExportDir))
			return paths;
		foreach (string name in DirAccess.GetFilesAt(ExportDir)) {
			if (name.EndsWith(".json"))
				paths.Add($"{ExportDir}/{name}");
		}
		paths.Sort((a, b) => string.CompareOrdinal(SortKey(a), SortKey(b)));
		paths.Reverse();
		return paths;
	}

	private static string SortKey(string path) {
		string name = path.GetFile();
		return name.StartsWith("grid_hat_") ? "grid_" + name.Substring("grid_hat_".Length) : name;
	}

	// Returns null (with the reason in `error`) for anything that isn't a
	// usable export - including one made with a different PenroseEdgeLength (or
	// HatUnit, for a hat grid), since its corners would no longer match the size
	// towers, ranges and tile shapes are built at.
	public static SavedGrid Load(string path, out string error) {
		string text = FileAccess.GetFileAsString(path);
		if (text == "") {
			error = $"couldn't read the file ({FileAccess.GetOpenError()})";
			return null;
		}
		var json = new Json();
		if (json.Parse(text) != Error.Ok) {
			error = $"invalid JSON on line {json.GetErrorLine()}: {json.GetErrorMessage()}";
			return null;
		}
		if (json.Data.VariantType != Variant.Type.Dictionary) {
			error = "not a grid export";
			return null;
		}
		Godot.Collections.Dictionary data = json.Data.AsGodotDictionary();
		foreach (string key in new[] { "edge_length", "entry", "exit", "tiles" }) {
			if (!data.ContainsKey(key)) {
				error = $"not a grid export (no \"{key}\")";
				return null;
			}
		}

		var saved = new SavedGrid();
		string tilingName = data.ContainsKey("tiling") ? data["tiling"].AsString() : TilingName(TilingKind.Penrose);
		if (tilingName == TilingName(TilingKind.Hat))
			saved.Tiling = TilingKind.Hat;
		else if (tilingName != TilingName(TilingKind.Penrose)) {
			error = $"it's a \"{tilingName}\" grid, which this version doesn't know";
			return null;
		}
		bool hat = saved.Tiling == TilingKind.Hat;

		float edgeLength = data["edge_length"].AsSingle();
		float expectedEdgeLength = hat ? GameData.HatUnit : GameData.PenroseEdgeLength;
		if (!Mathf.IsEqualApprox(edgeLength, expectedEdgeLength)) {
			error = $"it was saved with tile edge length {edgeLength}, but the game now uses {expectedEdgeLength}";
			return null;
		}

		int cornerCount = hat ? HatGenerator.HatOutline.Length + 1 : 4;
		foreach (Variant tileVariant in data["tiles"].AsGodotArray()) {
			Godot.Collections.Dictionary tileData = tileVariant.AsGodotDictionary();
			int id = saved.Tiles.Count;
			Godot.Collections.Array cornerData = tileData["corners"].AsGodotArray();
			if (cornerData.Count != cornerCount) {
				error = $"tile {id} doesn't have {cornerCount} corners";
				return null;
			}
			// Kept in the saved order, not re-derived - which corner comes
			// first is the tile's orientation under PenroseTileRules (see
			// Main.FlipPenroseTileAt), including any manual flips made before
			// the export.
			var corners = new Vector2[cornerCount];
			for (int i = 0; i < cornerCount; i++) {
				Godot.Collections.Array point = cornerData[i].AsGodotArray();
				corners[i] = new Vector2(point[0].AsSingle(), point[1].AsSingle());
			}
			var tile = new BoardTile { Id = id, Corners = corners };
			if (hat) {
				// A hat's center is its roomiest interior point (see HatTiling),
				// which nothing simpler than the generator's own geometry gives -
				// so it's read back as saved rather than recomputed.
				if (!tileData.ContainsKey("center")) {
					error = $"tile {id} has no center";
					return null;
				}
				Godot.Collections.Array center = tileData["center"].AsGodotArray();
				tile.Center = new Vector2(center[0].AsSingle(), center[1].AsSingle());
				tile.IsMirrored = tileData.ContainsKey("mirrored") && tileData["mirrored"].AsBool();
				if (tileData.ContainsKey("hat_label") && Enum.TryParse(tileData["hat_label"].AsString(), out HatLabel label))
					tile.HatLabel = label;
			}
			else {
				tile.IsThin = tileData["thin"].AsBool();
				tile.Center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4.0f;
			}
			saved.Tiles.Add(tile);
			if (tileData.ContainsKey("structure"))
				saved.Structures[id] = tileData["structure"].AsString();
			if (tileData.ContainsKey("buff"))
				saved.Buffs[id] = tileData["buff"].AsString();
		}

		saved.Entry = data["entry"].AsInt32();
		saved.Exit = data["exit"].AsInt32();
		if (saved.Entry < 0 || saved.Entry >= saved.Tiles.Count || saved.Exit < 0 || saved.Exit >= saved.Tiles.Count) {
			error = "its entry/exit tile isn't on the grid";
			return null;
		}
		error = "";
		return saved;
	}
}
