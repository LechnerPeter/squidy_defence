using Godot;
using System.Collections.Generic;

public readonly record struct ChoiceOption(string Id, string Label, string Description);

public partial class UI : CanvasLayer {
	private const float EffectIconSize = 26.0f;
	private const string T2HintText = "Place directly, or click a triangle/row of 3 tiles — any mix of empty ground and matching towers works. Existing matching towers are free (already paid for); empty tiles cost their tower's current price.";
	private const float IconWidth = 84.0f;
	private const float IconHeight = 108.0f;
	private const int IconColumns = 2;

	[Signal]
	public delegate void TowerSelectedEventHandler(string type);
	[Signal]
	public delegate void StartWavePressedEventHandler();
	[Signal]
	public delegate void RetryPressedEventHandler();
	[Signal]
	public delegate void SpeedSelectedEventHandler(float multiplier);
	[Signal]
	public delegate void ChoiceMadeEventHandler(string optionId);
	[Signal]
	public delegate void PenroseViewToggledEventHandler(bool enabled);

	private Label _goldLabel;
	private Label _livesLabel;
	private Label _waveLabel;
	private VBoxContainer _towerShopList;
	private Control _gameOverPanel;
	private Label _gameOverLabel;
	private Button _retryButton;
	private Label _statusLabel;
	private Label _towerInfoName;
	private Label _towerInfoFlavor;
	private Label _towerInfoStats;
	private HBoxContainer _towerInfoEffects;
	private Button _speed1XButton;
	private Button _speed2XButton;
	private Button _speed4XButton;
	private Button _waveButton;
	private Button _penroseViewButton;
	private ColorRect _topBarBg;
	private ColorRect _sidebarBg;
	private ColorRect _sidebarDivider;
	private ScrollContainer _towerInfoStatsScroll;
	private ScrollContainer _towerShopScroll;

	private readonly Dictionary<string, Button> _towerButtons = new Dictionary<string, Button>();
	private readonly Dictionary<string, Label> _towerCostLabels = new Dictionary<string, Label>();
	private readonly Dictionary<float, Button> _speedButtons = new Dictionary<float, Button>();

	private Control _choicePanel;
	private Label _choiceTitleLabel;
	private Label _choiceSubtitleLabel;
	private VBoxContainer _choiceOptionsBox;

	private Control _wikiPanel;
	private Button _wikiButton;
	private Label _wikiDetailName;
	private Label _wikiDetailFlavor;
	private Label _wikiDetailStats;
	private HBoxContainer _curseIconRow;

	public override void _Ready() {
		_goldLabel = GetNode<Label>("%GoldLabel");
		_livesLabel = GetNode<Label>("%LivesLabel");
		_waveLabel = GetNode<Label>("%WaveLabel");
		_towerShopList = GetNode<VBoxContainer>("%TowerShopList");
		_gameOverPanel = GetNode<Control>("%GameOverPanel");
		_gameOverLabel = GetNode<Label>("%GameOverLabel");
		_retryButton = GetNode<Button>("%RetryButton");
		_statusLabel = GetNode<Label>("%StatusLabel");
		_towerInfoName = GetNode<Label>("%TowerInfoName");
		_towerInfoFlavor = GetNode<Label>("%TowerInfoFlavor");
		_towerInfoStats = GetNode<Label>("%TowerInfoStats");
		_towerInfoEffects = GetNode<HBoxContainer>("%TowerInfoEffects");
		_speed1XButton = GetNode<Button>("%Speed1xButton");
		_speed2XButton = GetNode<Button>("%Speed2xButton");
		_speed4XButton = GetNode<Button>("%Speed4xButton");
		_waveButton = GetNode<Button>("%WaveButton");
		_wikiButton = GetNode<Button>("%WikiButton");
		_curseIconRow = GetNode<HBoxContainer>("%CurseIconRow");
		_penroseViewButton = GetNode<Button>("%PenroseViewButton");
		_topBarBg = GetNode<ColorRect>("%TopBarBg");
		_sidebarBg = GetNode<ColorRect>("%SidebarBg");
		_sidebarDivider = GetNode<ColorRect>("%SidebarDivider");
		_towerInfoStatsScroll = GetNode<ScrollContainer>("%TowerInfoStatsScroll");
		_towerShopScroll = GetNode<ScrollContainer>("%TowerShopScroll");

		_gameOverPanel.Visible = false;
		_statusLabel.Visible = false;
		ClearTowerInfo();
		BuildChoicePanel();
		BuildWikiPanel();
		_speedButtons[1.0f] = _speed1XButton;
		_speedButtons[2.0f] = _speed2XButton;
		_speedButtons[4.0f] = _speed4XButton;
		_speed1XButton.Pressed += () => SelectSpeed(1.0f);
		_speed2XButton.Pressed += () => SelectSpeed(2.0f);
		_speed4XButton.Pressed += () => SelectSpeed(4.0f);
		SelectSpeed(1.0f);
		_waveButton.Pressed += () => EmitSignal(SignalName.StartWavePressed);
		_penroseViewButton.Toggled += OnPenroseViewToggled;
		RebuildTowerShop();
		_retryButton.Pressed += () => EmitSignal(SignalName.RetryPressed);
		GameManager.Instance.GoldChanged += OnGoldChanged;
		GameManager.Instance.LivesChanged += OnLivesChanged;
		GameManager.Instance.WaveChanged += OnWaveChanged;
		GameManager.Instance.GameOver += OnGameOver;
		GameManager.Instance.CursesChanged += RefreshCurseIcons;
		OnGoldChanged(GameManager.Instance.Gold);
		OnLivesChanged(GameManager.Instance.Lives);
		OnWaveChanged(GameManager.Instance.Wave, GameManager.Instance.TotalWaves);
		RefreshCurseIcons();
	}

	public override void _ExitTree() {
		if (GameManager.Instance == null)
			return;
		GameManager.Instance.GoldChanged -= OnGoldChanged;
		GameManager.Instance.LivesChanged -= OnLivesChanged;
		GameManager.Instance.WaveChanged -= OnWaveChanged;
		GameManager.Instance.GameOver -= OnGameOver;
	}

	private void RebuildTowerShop() {
		foreach (Node child in _towerShopList.GetChildren())
			child.QueueFree();
		_towerButtons.Clear();
		_towerCostLabels.Clear();
		var tabs = new TabContainer();
		_towerShopList.AddChild(tabs);

		var t1Tab = new VBoxContainer { Name = "T1" };
		tabs.AddChild(t1Tab);
		GridContainer t1Grid = MakeIconGrid();
		t1Tab.AddChild(t1Grid);

		var t2Tab = new VBoxContainer { Name = "T2" };
		tabs.AddChild(t2Tab);
		AddShopLabel(t2Tab, T2HintText, 11, new Color(0.6f, 0.6f, 0.68f, 1.0f));
		GridContainer t2Grid = MakeIconGrid();
		t2Tab.AddChild(t2Grid);

		var utilityTab = new VBoxContainer { Name = "Utility" };
		tabs.AddChild(utilityTab);
		GridContainer utilityGrid = MakeIconGrid();
		utilityTab.AddChild(utilityGrid);

		foreach (string type in GameData.TowerTypeKeys) {
			Godot.Collections.Dictionary data = GameData.GetTowerData(type);
			int tier = GameData.GetTier(type);
			Button button = MakeShopIconButton(type, data);
			if (GameData.UtilityTowerTypes.Contains(type))
				utilityGrid.AddChild(button);
			else if (tier == 2)
				t2Grid.AddChild(button);
			else
				t1Grid.AddChild(button);
			_towerButtons[type] = button;
		}
	}

	private GridContainer MakeIconGrid() {
		var grid = new GridContainer {
			Columns = IconColumns,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		return grid;
	}

	private Button MakeShopIconButton(string type, Godot.Collections.Dictionary data) {
		var button = new Button {
			ToggleMode = true,
			CustomMinimumSize = new Vector2(IconWidth, IconHeight),
			ClipText = true,
		};
		button.Pressed += () => Select(type);
		var icon = new TowerIcon {
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		icon.OffsetTop = 18.0f;
		icon.OffsetBottom = -20.0f;
		button.AddChild(icon);
		icon.Setup(type);

		string abbreviation = GameData.GetTowerAbbreviation(type);
		if (string.IsNullOrEmpty(abbreviation)) {
			string name = data.ContainsKey("name") ? data["name"].AsString() : type;
			abbreviation = name.Substring(0, System.Math.Min(4, name.Length)).ToUpperInvariant();
		}
		var nameLabel = new Label {
			Text = abbreviation,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			ClipText = true,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 9);
		nameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 0.9f));
		nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
		nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		nameLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		nameLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		nameLabel.OffsetTop = 3.0f;
		nameLabel.OffsetBottom = 17.0f;
		button.AddChild(nameLabel);

		var costLabel = new Label {
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		costLabel.AddThemeFontSizeOverride("font_size", 12);
		costLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f, 1.0f));
		costLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
		costLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		costLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		costLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		costLabel.OffsetTop = -20.0f;
		costLabel.OffsetBottom = -3.0f;
		button.AddChild(costLabel);
		_towerCostLabels[type] = costLabel;
		return button;
	}

	private void AddShopLabel(Node parent, string text, int fontSize, Color color) {
		var label = new Label {
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		parent.AddChild(label);
	}

	private void Select(string type) {
		foreach (KeyValuePair<string, Button> entry in _towerButtons)
			entry.Value.ButtonPressed = entry.Key == type;
		EmitSignal(SignalName.TowerSelected, type);
	}

	private void SelectSpeed(float multiplier) {
		foreach (KeyValuePair<float, Button> entry in _speedButtons)
			entry.Value.ButtonPressed = entry.Key == multiplier;
		EmitSignal(SignalName.SpeedSelected, multiplier);
	}

	// "Penrose View" hides every bit of chrome and gameplay dressing (the top
	// bar, sidebar, and - via the PenroseViewToggled signal - Main's own
	// towers/enemies/projectiles/path/hover layers) so only the tiling itself
	// (outlines + the matching-rule arcs) remains, letting the pattern be
	// appreciated on its own. The toggle button is deliberately left out of
	// its own hide-list so it stays clickable to switch back. StatusLabel is
	// only ever force-hidden, never force-shown, since its visibility is
	// otherwise driven by ShowStatus's own timer - restoring it blindly could
	// resurrect a stale message.
	private void OnPenroseViewToggled(bool enabled) {
		bool chromeVisible = !enabled;
		_topBarBg.Visible = chromeVisible;
		_wikiButton.Visible = chromeVisible;
		_curseIconRow.Visible = chromeVisible;
		_goldLabel.Visible = chromeVisible;
		_livesLabel.Visible = chromeVisible;
		_waveLabel.Visible = chromeVisible;
		_waveButton.Visible = chromeVisible;
		_speed1XButton.Visible = chromeVisible;
		_speed2XButton.Visible = chromeVisible;
		_speed4XButton.Visible = chromeVisible;
		_sidebarBg.Visible = chromeVisible;
		_sidebarDivider.Visible = chromeVisible;
		_towerInfoName.Visible = chromeVisible;
		_towerInfoFlavor.Visible = chromeVisible;
		_towerInfoEffects.Visible = chromeVisible;
		_towerInfoStatsScroll.Visible = chromeVisible;
		_towerShopScroll.Visible = chromeVisible;
		if (enabled)
			_statusLabel.Visible = false;
		EmitSignal(SignalName.PenroseViewToggled, enabled);
	}

	public void ClearSelection() {
		foreach (Button button in _towerButtons.Values)
			button.ButtonPressed = false;
		EmitSignal(SignalName.TowerSelected, "");
	}

	public void UpdateTowerPrice(string type, int cost) {
		if (!_towerButtons.ContainsKey(type))
			return;
		Godot.Collections.Dictionary data = GameData.GetTowerData(type);
		string name = data.ContainsKey("name") ? data["name"].AsString() : type;
		string tooltip = $"{name} — {cost}g";
		if (GameData.TowerT2Hints.TryGetValue(type, out string hint))
			tooltip += $"\n{hint}";
		_towerButtons[type].TooltipText = tooltip;
		if (_towerCostLabels.ContainsKey(type))
			_towerCostLabels[type].Text = $"{cost}g";
	}

	public void ShowTowerInfo(TowerInfo info) {
		_towerInfoName.Text = info.Name;
		_towerInfoFlavor.Text = info.Flavor;
		_towerInfoStats.Text = string.Join("\n", info.Lines);
		RebuildEffectIcons(info.Effects);
	}

	private void RebuildEffectIcons(List<string> effects) {
		foreach (Node child in _towerInfoEffects.GetChildren())
			child.QueueFree();
		foreach (string effectType in effects) {
			var icon = new EffectIcon {
				CustomMinimumSize = new Vector2(EffectIconSize, EffectIconSize),
			};
			_towerInfoEffects.AddChild(icon);
			icon.Setup(effectType);
		}
	}

	public void ClearTowerInfo() {
		_towerInfoName.Text = "No Tower Selected";
		_towerInfoFlavor.Text = "Click a tower on the field to inspect it, or pick one below to build.";
		_towerInfoStats.Text = "";
		RebuildEffectIcons(new List<string>());
	}

	public void ShowStatus(string text) {
		_statusLabel.Text = text;
		_statusLabel.Visible = true;
		SceneTreeTimer statusTimer = GetTree().CreateTimer(1.5);
		statusTimer.Timeout += () => _statusLabel.Visible = false;
	}

	private void OnGoldChanged(int gold) => _goldLabel.Text = $"Gold: {gold}";

	private void OnLivesChanged(int lives) => _livesLabel.Text = $"Lives: {lives}";

	private void OnWaveChanged(int wave, int total) => _waveLabel.Text = $"Wave: {wave} / {total}";

	// Rebuilt from scratch (rather than appended to) each time a curse is chosen -
	// one icon per distinct curse type actually picked this run, with a stack count
	// badge if it's been picked more than once, instead of one icon per pick.
	private void RefreshCurseIcons() {
		foreach (Node child in _curseIconRow.GetChildren())
			child.QueueFree();
		foreach (Curse curse in GameData.Curses) {
			if (!GameManager.Instance.CurseCounts.TryGetValue(curse.Key, out int count) || count <= 0)
				continue;
			var icon = new CurseIcon { CustomMinimumSize = new Vector2(36, 36) };
			_curseIconRow.AddChild(icon);
			icon.Setup(curse.Key, count);
		}
	}

	public void SetWaveButtonEnabled(bool enabled) => _waveButton.Disabled = !enabled;

	private void OnGameOver(bool won) {
		_gameOverPanel.Visible = true;
		_gameOverLabel.Text = won ? "Victory!" : "Game Over";
		SetWaveButtonEnabled(false);
	}

	private void BuildChoicePanel() {
		_choicePanel = new Control {
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false,
		};
		_choicePanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_choicePanel);

		var dim = new ColorRect {
			Color = new Color(0, 0, 0, 0.65f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_choicePanel.AddChild(dim);

		var center = new CenterContainer {
			MouseFilter = Control.MouseFilterEnum.Pass,
		};
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_choicePanel.AddChild(center);

		var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
		box.AddThemeConstantOverride("separation", 12);
		center.AddChild(box);

		_choiceTitleLabel = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_choiceTitleLabel.AddThemeFontSizeOverride("font_size", 22);
		box.AddChild(_choiceTitleLabel);

		_choiceSubtitleLabel = new Label {
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_choiceSubtitleLabel.AddThemeFontSizeOverride("font_size", 13);
		_choiceSubtitleLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.82f, 1.0f));
		box.AddChild(_choiceSubtitleLabel);

		_choiceOptionsBox = new VBoxContainer();
		_choiceOptionsBox.AddThemeConstantOverride("separation", 10);
		box.AddChild(_choiceOptionsBox);
	}

	public void ShowChoice(string title, string subtitle, List<ChoiceOption> options) {
		_choiceTitleLabel.Text = title;
		_choiceSubtitleLabel.Text = subtitle;
		foreach (Node child in _choiceOptionsBox.GetChildren())
			child.QueueFree();
		foreach (ChoiceOption opt in options) {
			var optionBox = new VBoxContainer();
			optionBox.AddThemeConstantOverride("separation", 2);
			var button = new Button {
				CustomMinimumSize = new Vector2(0, 32),
				Text = opt.Label,
			};
			string id = opt.Id;
			button.Pressed += () => OnChoicePressed(id);
			optionBox.AddChild(button);
			var descLabel = new Label {
				Text = opt.Description,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			descLabel.AddThemeFontSizeOverride("font_size", 11);
			descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.78f, 1.0f));
			optionBox.AddChild(descLabel);
			_choiceOptionsBox.AddChild(optionBox);
		}
		_choicePanel.Visible = true;
	}

	private void OnChoicePressed(string optionId) {
		_choicePanel.Visible = false;
		EmitSignal(SignalName.ChoiceMade, optionId);
	}

	// A reference panel listing every implemented tower, enemy, boss, and buff tile as
	// a grid of icons - clicking one shows its full info in the detail pane on the
	// right (see ShowWikiDetail). Built once from GameData (see GameData.Towers/
	// GameData.Enemies/GameData.TileBuffs) so it can never drift out of sync with
	// what's actually in the game, the same way RebuildTowerShop reads the shop's
	// contents from GameData rather than hardcoding them.
	private void BuildWikiPanel() {
		_wikiPanel = new Control {
			MouseFilter = Control.MouseFilterEnum.Stop,
			Visible = false,
		};
		_wikiPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_wikiPanel);

		var dim = new ColorRect {
			Color = new Color(0, 0, 0, 0.65f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_wikiPanel.AddChild(dim);

		var center = new CenterContainer {
			MouseFilter = Control.MouseFilterEnum.Pass,
		};
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_wikiPanel.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(980, 620) };
		center.AddChild(panel);

		var mainBox = new VBoxContainer();
		mainBox.AddThemeConstantOverride("separation", 8);
		panel.AddChild(mainBox);

		var header = new HBoxContainer();
		mainBox.AddChild(header);
		var titleLabel = new Label { Text = "Wiki", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		titleLabel.AddThemeFontSizeOverride("font_size", 20);
		header.AddChild(titleLabel);
		var closeButton = new Button { Text = "Close" };
		closeButton.Pressed += () => _wikiPanel.Visible = false;
		header.AddChild(closeButton);

		var bodyBox = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		bodyBox.AddThemeConstantOverride("separation", 12);
		mainBox.AddChild(bodyBox);

		var tabs = new TabContainer {
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(660, 560),
		};
		bodyBox.AddChild(tabs);
		tabs.AddChild(BuildWikiTowersTab());
		tabs.AddChild(BuildWikiEnemiesTab());
		tabs.AddChild(BuildWikiBossesTab());
		tabs.AddChild(BuildWikiBuffTilesTab());
		tabs.AddChild(BuildWikiCursesTab());

		bodyBox.AddChild(BuildWikiDetailPane());

		_wikiButton.Pressed += () => _wikiPanel.Visible = true;
	}

	private Control BuildWikiDetailPane() {
		var detailScroll = new ScrollContainer {
			CustomMinimumSize = new Vector2(280, 0),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		var detailBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		detailBox.AddThemeConstantOverride("separation", 6);
		detailScroll.AddChild(detailBox);

		_wikiDetailName = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_wikiDetailName.AddThemeFontSizeOverride("font_size", 18);
		_wikiDetailName.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.6f));
		detailBox.AddChild(_wikiDetailName);

		_wikiDetailFlavor = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_wikiDetailFlavor.AddThemeFontSizeOverride("font_size", 12);
		_wikiDetailFlavor.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.78f, 1.0f));
		detailBox.AddChild(_wikiDetailFlavor);

		detailBox.AddChild(new HSeparator());

		_wikiDetailStats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
		_wikiDetailStats.AddThemeFontSizeOverride("font_size", 13);
		detailBox.AddChild(_wikiDetailStats);

		ShowWikiDetail(new TowerInfo("", "Click an icon to see its details.", new List<string>(), new List<string>()));
		return detailScroll;
	}

	private void ShowWikiDetail(TowerInfo info) {
		_wikiDetailName.Text = info.Name;
		_wikiDetailFlavor.Text = info.Flavor;
		_wikiDetailStats.Text = string.Join("\n", info.Lines);
	}

	private ScrollContainer BuildWikiTab(string tabName, GridContainer content) {
		var scroll = new ScrollContainer { Name = tabName, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		scroll.AddChild(content);
		return scroll;
	}

	private GridContainer MakeWikiIconGrid() {
		var grid = new GridContainer { Columns = 6 };
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 8);
		return grid;
	}

	private ScrollContainer BuildWikiTowersTab() {
		GridContainer grid = MakeWikiIconGrid();
		foreach (string type in GameData.TowerTypeKeys) {
			Godot.Collections.Dictionary data = GameData.GetTowerData(type);
			TowerInfo info = GameData.DescribeStats(type, data, 0, 0, new Godot.Collections.Array(), GameData.PenroseEdgeLength);
			if (GameData.TowerT2Hints.TryGetValue(type, out string hint))
				info.Lines.Add($"Merge recipe: {hint}");
			var icon = new TowerIcon();
			icon.Setup(type);
			grid.AddChild(MakeWikiIconButton(icon, info.Name, info));
		}
		return BuildWikiTab("Towers", grid);
	}

	private ScrollContainer BuildWikiEnemiesTab() {
		GridContainer grid = MakeWikiIconGrid();
		foreach (string type in GameData.EnemyTypeKeys) {
			TowerInfo info = GameData.GetEnemyPreviewInfo(type);
			grid.AddChild(MakeWikiIconButton(MakeColorSwatch(GameData.GetEnemyPreviewColor(type)), info.Name, info));
		}
		return BuildWikiTab("Enemies", grid);
	}

	private ScrollContainer BuildWikiBossesTab() {
		GridContainer grid = MakeWikiIconGrid();
		foreach (string type in GameData.BossTypes) {
			TowerInfo info = GameData.GetEnemyPreviewInfo(type);
			var icon = new BossIcon();
			icon.Setup(type);
			grid.AddChild(MakeWikiIconButton(icon, info.Name, info));
		}
		return BuildWikiTab("Bosses", grid);
	}

	private ScrollContainer BuildWikiBuffTilesTab() {
		GridContainer grid = MakeWikiIconGrid();
		foreach (TileBuff buff in GameData.TileBuffs) {
			var lines = new List<string>();
			if (buff.Key == "multishot")
				lines.Add($"+{buff.SpawnCount} target{(buff.SpawnCount == 1 ? "" : "s")} per shot, stacks per tile.");
			else
				lines.Add($"+{(int)((GameData.TileBuffMult - 1.0f) * 100.0f)}% {buff.Label}, stacks per tile.");
			lines.Add($"{buff.SpawnCount} spawn on the map each run.");
			lines.Add("Built on an outpost/big outpost platform, it passes its bonus to the tower on that platform instead of the tile itself.");
			var info = new TowerInfo($"{buff.Label} Tile", "Empty ground with a hidden bonus.", lines, new List<string>());
			var icon = new BuffTileIcon();
			icon.Setup(buff.Key);
			grid.AddChild(MakeWikiIconButton(icon, info.Name, info));
		}
		return BuildWikiTab("Buff Tiles", grid);
	}

	private ScrollContainer BuildWikiCursesTab() {
		GridContainer grid = MakeWikiIconGrid();
		foreach (Curse curse in GameData.Curses) {
			var lines = new List<string> {
				"Offered as 1 of 3 random curses after each boss wave.",
				"Can be offered and picked again later - its effect applies again each time.",
			};
			var info = new TowerInfo(curse.Label, curse.Description, lines, new List<string>());
			var icon = new CurseIcon();
			icon.Setup(curse.Key);
			grid.AddChild(MakeWikiIconButton(icon, curse.Label, info));
		}
		return BuildWikiTab("Curses", grid);
	}

	private ColorRect MakeColorSwatch(Color color) => new ColorRect { Color = color };

	// Icon-only button for the wiki grids - pressing it doesn't select anything (unlike
	// the shop's identical-looking buttons), it just routes `info` to the shared detail
	// pane (see ShowWikiDetail) so every tab can reuse the same result area.
	private Button MakeWikiIconButton(Control icon, string label, TowerInfo info) {
		var button = new Button {
			CustomMinimumSize = new Vector2(78, 96),
			ClipText = true,
			TooltipText = label,
		};
		button.Pressed += () => ShowWikiDetail(info);

		icon.MouseFilter = Control.MouseFilterEnum.Ignore;
		icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		icon.OffsetLeft = 6.0f;
		icon.OffsetRight = -6.0f;
		icon.OffsetTop = 6.0f;
		icon.OffsetBottom = -22.0f;
		button.AddChild(icon);

		var nameLabel = new Label {
			Text = label,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
			ClipText = true,
		};
		nameLabel.AddThemeFontSizeOverride("font_size", 10);
		nameLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		nameLabel.OffsetTop = -20.0f;
		nameLabel.OffsetBottom = -4.0f;
		button.AddChild(nameLabel);

		return button;
	}
}
