using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// First screen on launch: start on a freshly generated grid, or pick one of
// the grids saved by Main's "Export Grid" button (see GridFile). The choice
// reaches Main through GameManager.GridFilePath. A saved grid is test-loaded
// here first, so a broken or outdated file shows why it can't be used instead
// of quietly falling back to a generated grid once the game starts.
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

		var newButton = new Button {
			Text = "Generate New Grid",
			CustomMinimumSize = new Vector2(0, 40),
		};
		newButton.AddThemeFontSizeOverride("font_size", 16);
		newButton.Pressed += () => StartGame("");
		box.AddChild(newButton);

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
			_messageLabel.Text = "No saved grids yet - in a game, turn on Penrose View and click Export Grid.";
	}

	private void OnLoadPressed() {
		int[] selected = _savedList.GetSelectedItems();
		if (selected.Length > 0)
			LoadSaved(selected[0]);
	}

	private void LoadSaved(int index) {
		string path = _savedPaths[index];
		if (GridFile.Load(path, out string error) == null) {
			_messageLabel.Text = $"Can't load this grid: {error}.";
			_messageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.45f, 1.0f));
			return;
		}
		StartGame(path);
	}

	private void StartGame(string gridFilePath) {
		GameManager.Instance.GridFilePath = gridFilePath;
		GetTree().ChangeSceneToFile(MainScenePath);
	}

	// "grid_20261027_231500.json" -> "2026-10-27 23:15:00"; a file not named by
	// the exporter just shows its own name.
	private static string DisplayName(string path) {
		string name = path.GetFile().GetBaseName();
		if (name.StartsWith("grid_") && DateTime.TryParseExact(name.Substring(5), "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
			return time.ToString("yyyy-MM-dd HH:mm:ss");
		return name;
	}
}
