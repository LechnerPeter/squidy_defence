using Godot;
using System.Collections.Generic;

// What a grid exported by Main's "Export Grid" button brings back - only the
// parts needed to rebuild the same board. The tiles' shapes and corner order
// are enough for the tiling itself: neighbors, vertex rings and vertex colors
// are all derived from those by PenroseTiling.BuildAdjacency, exactly as for
// a freshly generated patch, so the export's copies of them are ignored.
public sealed class SavedGrid {
	public List<PenroseTile> Tiles = new List<PenroseTile>();
	public int Entry;
	public int Exit;
	public Dictionary<int, string> Structures = new Dictionary<int, string>();   // tile id -> structure type (rock/water/wall)
	public Dictionary<int, string> Buffs = new Dictionary<int, string>();        // tile id -> buff tile type
}

public static class GridFile {
	public const string ExportDir = "user://grid_exports";

	// Newest first - export file names are timestamped, so name order is time order.
	public static List<string> ListSaved() {
		var paths = new List<string>();
		if (!DirAccess.DirExistsAbsolute(ExportDir))
			return paths;
		foreach (string name in DirAccess.GetFilesAt(ExportDir)) {
			if (name.EndsWith(".json"))
				paths.Add($"{ExportDir}/{name}");
		}
		paths.Sort();
		paths.Reverse();
		return paths;
	}

	// Returns null (with the reason in `error`) for anything that isn't a
	// usable export - including one made with a different PenroseEdgeLength,
	// since its corners would no longer match the size towers, ranges and
	// tile shapes are built at.
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
		float edgeLength = data["edge_length"].AsSingle();
		if (!Mathf.IsEqualApprox(edgeLength, GameData.PenroseEdgeLength)) {
			error = $"it was saved with tile edge length {edgeLength}, but the game now uses {GameData.PenroseEdgeLength}";
			return null;
		}

		var saved = new SavedGrid();
		foreach (Variant tileVariant in data["tiles"].AsGodotArray()) {
			Godot.Collections.Dictionary tileData = tileVariant.AsGodotDictionary();
			int id = saved.Tiles.Count;
			Godot.Collections.Array cornerData = tileData["corners"].AsGodotArray();
			if (cornerData.Count != 4) {
				error = $"tile {id} doesn't have 4 corners";
				return null;
			}
			// Kept in the saved order, not re-derived - which corner comes
			// first is the tile's orientation under PenroseTileRules (see
			// Main.FlipPenroseTileAt), including any manual flips made before
			// the export.
			var corners = new Vector2[4];
			for (int i = 0; i < 4; i++) {
				Godot.Collections.Array point = cornerData[i].AsGodotArray();
				corners[i] = new Vector2(point[0].AsSingle(), point[1].AsSingle());
			}
			saved.Tiles.Add(new PenroseTile {
				Id = id,
				IsThin = tileData["thin"].AsBool(),
				Corners = corners,
				Center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4.0f,
			});
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
