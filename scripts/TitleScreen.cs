using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// First screen on launch: start on a freshly generated grid of either tiling
// (see TilingKind), or pick one of the grids saved by Main's "Export Grid" button
// (see GridFile). The choice reaches Main through GameManager.GridFilePath and
// GameManager.Tiling. A saved grid is test-loaded here first, so a broken or
// outdated file shows why it can't be used instead of quietly falling back to a
// generated grid once the game starts.
public partial class TitleScreen : Control {
	private const string MainScenePath = "res://scenes/Main.tscn";

	private ItemList _savedList;
	private Button _loadButton;
	private Label _messageLabel;
	private readonly List<string> _savedPaths = new List<string>();

	public override void _Ready() {
		var background = new ColorRect { Color = new Color(0.05f, 0.05f, 0.09f, 1.0f) };
		background.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
		box.AddThemeConstantOverride("separation", 12);
		center.AddChild(box);

		var titleLabel = new Label {
			Text = "Squidy Defence",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 40);
		box.AddChild(titleLabel);

		AddModeButton(box, TilingKind.Penrose, "Two tiles, thin and thick rhombi - each tower needs the right one.");
		AddModeButton(box, TilingKind.Hat, "One tile, the aperiodic \"hat\" - any tower fits anywhere.");

		box.AddChild(new HSeparator());

		var loadLabel = new Label { Text = "Load a Saved Grid" };
		loadLabel.AddThemeFontSizeOverride("font_size", 16);
		box.AddChild(loadLabel);

		_savedList = new ItemList { CustomMinimumSize = new Vector2(0, 240) };
		_savedList.ItemSelected += index => _loadButton.Disabled = false;
		_savedList.ItemActivated += index => LoadSaved((int)index);
		box.AddChild(_savedList);

		_loadButton = new Button {
			Text = "Load Selected Grid",
			CustomMinimumSize = new Vector2(0, 40),
			Disabled = true,
		};
		_loadButton.AddThemeFontSizeOverride("font_size", 16);
		_loadButton.Pressed += OnLoadPressed;
		box.AddChild(_loadButton);

		_messageLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_messageLabel.AddThemeFontSizeOverride("font_size", 12);
		_messageLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.78f, 1.0f));
		box.AddChild(_messageLabel);

		foreach (string path in GridFile.ListSaved()) {
			_savedPaths.Add(path);
			_savedList.AddItem(DisplayName(path));
		}
		if (_savedPaths.Count == 0)
			_messageLabel.Text = "No saved grids yet - in a game, turn on Penrose View (Einstein View on a hat grid) and click Export Grid.";
	}

	// A button that starts a freshly generated grid of this tiling, with a line under
	// it saying what sets the tiling apart.
	private void AddModeButton(VBoxContainer box, TilingKind tiling, string blurb) {
		var button = new Button {
			Text = $"New {tiling.DisplayName()} Grid",
			CustomMinimumSize = new Vector2(0, 40),
		};
		button.AddThemeFontSizeOverride("font_size", 16);
		button.Pressed += () => StartGame("", tiling);
		box.AddChild(button);

		var blurbLabel = new Label {
			Text = blurb,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		blurbLabel.AddThemeFontSizeOverride("font_size", 12);
		blurbLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.78f, 1.0f));
		box.AddChild(blurbLabel);
	}

	private void OnLoadPressed() {
		int[] selected = _savedList.GetSelectedItems();
		if (selected.Length > 0)
			LoadSaved(selected[0]);
	}

	private void LoadSaved(int index) {
		string path = _savedPaths[index];
		SavedGrid saved = GridFile.Load(path, out string error);
		if (saved == null) {
			_messageLabel.Text = $"Can't load this grid: {error}.";
			_messageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.45f, 1.0f));
			return;
		}
		StartGame(path, saved.Tiling);
	}

	private void StartGame(string gridFilePath, TilingKind tiling) {
		GameManager.Instance.GridFilePath = gridFilePath;
		GameManager.Instance.Tiling = tiling;
		GetTree().ChangeSceneToFile(MainScenePath);
	}

	// "grid_20261027_231500.json" -> "2026-10-27 23:15:00 - Penrose" (and
	// "grid_hat_..." -> "... - Einstein"); a file not named by the exporter just
	// shows its own name.
	private static string DisplayName(string path) {
		string name = path.GetFile().GetBaseName();
		TilingKind tiling = GridFile.TilingFromFileName(path);
		string prefix = tiling == TilingKind.Hat ? "grid_hat_" : "grid_";
		if (name.StartsWith(prefix) && DateTime.TryParseExact(name.Substring(prefix.Length), "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
			return $"{time:yyyy-MM-dd HH:mm:ss} - {tiling.DisplayName()}";
		return name;
	}
}
