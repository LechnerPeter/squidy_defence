using Godot;
using System.Collections.Generic;

public partial class Main : Node2D {
	// Lets a tower with no meaningful world-space parent reach the grid for things
	// like a line-of-sight check (see SniperTower) without every tower needing its
	// own reference wired through setup().
	public static Main Instance { get; private set; }

	private const float MaxSnapDistMult = 1.5f;
	private const float MaxSnapDistMultBig = 2.75f;
	// Max unordered neighbor-pair count for a single Penrose tile (up to 4 edges ->
	// C(4,2) = 6 possible (A,C) flanking pairs) - the cycle length for Crusher's Q/E
	// orientation (see RotateCrusher/FindPartialLine).
	private const int CrusherOrientationSteps = 6;

	private class Platform {
		public Vector2 WorldPos;
		public List<int> WallCells = new List<int>();
		public bool Occupied;
		public BaseTower Tower;
		public BaseTower Parent;
		public List<string> Buffs = new List<string>();
		public int OutpostStacks;
		public int BigOutpostStacks;
	}

	private class SpawnEntry {
		public string Type;
		public float Interval;
		public int? BossIndex;
	}

	private class SearchResult {
		public bool Valid;
		public List<int> Cells;
		public Dictionary<string, int> Missing = new Dictionary<string, int>();
	}

	private const float MinZoom = 0.5f;
	private const float MaxZoom = 3.0f;
	private const float ZoomStep = 1.1f;

	private Node2D _enemyContainer;
	private Node2D _towerContainer;
	private Node2D _projectileContainer;
	private Line2D _pathLine;
	private Node2D _hoverOverlay;
	private UI _ui;
	private bool _penroseViewOnly = false;
	private Camera2D _camera;
	private Rect2 _cameraPanBounds;
	private bool _isPanning;

	private PenroseGrid _grid;
	private List<Vector2> _currentPath = new List<Vector2>();
	private List<Vector2> _currentSwimPath = new List<Vector2>();
	private readonly HashSet<int> _waterCells = new HashSet<int>();
	private readonly Dictionary<int, BaseTower> _towerAtCell = new Dictionary<int, BaseTower>();
	private readonly Dictionary<int, BaseTower> _structureAtCell = new Dictionary<int, BaseTower>();
	private readonly List<Platform> _outpostPlatforms = new List<Platform>();
	private readonly Dictionary<int, string> _buffTiles = new Dictionary<int, string>();

	private string _selectedTowerType = "";
	private int _hoverCell = -1;
	private List<int> _hoverExtraCells = new List<int>();
	private List<Vector2> _hoverPreviewPath;
	private bool _hoverValid;
	private int _hoverPlatformIndex = -1;
	private bool _hoverPlatformPreviewActive;
	private List<Vector2> _hoverPlatformPreviewPositions = new List<Vector2>();
	private List<Vector2> _hoverPlatformMergePositions = new List<Vector2>();
	private bool _hoverRangePreviewActive;
	private Vector2 _hoverRangePreviewPos = Vector2.Zero;
	private float _hoverRangeValue;
	private HashSet<int> _hoverLosIgnoreCells;
	private bool _inspectedRangeActive;
	private Vector2 _inspectedRangePos = Vector2.Zero;
	private float _inspectedRangeValue;
	private HashSet<int> _inspectedLosIgnoreCells;
	private string _inspectedTowerType = "";
	private int _crusherRotation = -1;
	private BaseEnemy _focusedEnemy;

	private int _currentWaveIndex = -1;
	private readonly Queue<SpawnEntry> _spawnQueue = new Queue<SpawnEntry>();
	private float _spawnTimer;
	private bool _spawning;
	private int _enemiesAlive;
	private bool _gameActive = true;

	private bool _pendingWaveReward;
	private string _rewardStep = "";
	private string _placingBuffTileType = "";

	public override void _Ready() {
		Instance = this;
		_enemyContainer = GetNode<Node2D>("EnemyContainer");
		_towerContainer = GetNode<Node2D>("TowerContainer");
		_projectileContainer = GetNode<Node2D>("ProjectileContainer");
		_pathLine = GetNode<Line2D>("PathLine");
		_hoverOverlay = GetNode<Node2D>("HoverOverlay");
		_ui = GetNode<UI>("%UI");
		_camera = GetNode<Camera2D>("Camera2D");

		Engine.TimeScale = 1.0;
		_grid = new PenroseGrid(GameData.PenroseEdgeLength, GameData.PenroseRadius);
		if (OS.IsDebugBuild())
			GD.Print($"Penrose grid: {_grid.Tiles.Count} tiles, {_grid.Vertices.Count} vertices.");
		SetupCamera();
		SpawnRocks();
		SpawnWater();
		SpawnBuffTiles();
		RecomputePath();
		GameManager.Instance.TotalWaves = GameData.Waves.Count;
		GameManager.Instance.Reset();
		GameManager.Instance.GameOver += OnGameOver;
		_ui.TowerSelected += SelectTowerType;
		_ui.StartWavePressed += StartNextWave;
		_ui.RetryPressed += OnRetry;
		_ui.SpeedSelected += OnSpeedSelected;
		_ui.ChoiceMade += OnUiChoiceMade;
		_ui.PenroseViewToggled += OnPenroseViewToggled;
		RefreshTowerPrices();
		_hoverOverlay.Draw += DrawHoverOverlay;
	}

	// Centers the camera on the board's own bounding box - the pentagrid is
	// generated centered at the world origin, unlike the old rectangular grid's
	// corner-anchored layout, so framing the view around its actual bounds (rather
	// than anchoring world origin at the screen's top-left corner, like the old
	// formula did) is what keeps the whole patch on screen here. Pan is then bounded
	// the same way as before: a fixed margin past the board's own extents so the
	// player can't drag off into empty space.
	private void SetupCamera() {
		float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
		float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
		foreach (PenroseTile tile in _grid.Tiles) {
			foreach (Vector2 corner in tile.Corners) {
				minX = Mathf.Min(minX, corner.X);
				minY = Mathf.Min(minY, corner.Y);
				maxX = Mathf.Max(maxX, corner.X);
				maxY = Mathf.Max(maxY, corner.Y);
			}
		}
		Vector2 boardOrigin = new Vector2(minX, minY);
		Vector2 boardSize = new Vector2(maxX - minX, maxY - minY);
		_camera.Position = boardOrigin + boardSize / 2.0f;
		Vector2 margin = new Vector2(500.0f, 500.0f);
		_cameraPanBounds = new Rect2(boardOrigin - margin, boardSize + margin * 2.0f);
	}

	// Zooms around the cursor (the world point under it stays fixed on screen) rather
	// than around the board center, matching standard pan/zoom map controls.
	private void ZoomCamera(float factor) {
		Vector2 worldBefore = GetGlobalMousePosition();
		float newZoom = Mathf.Clamp(_camera.Zoom.X * factor, MinZoom, MaxZoom);
		_camera.Zoom = new Vector2(newZoom, newZoom);
		Vector2 worldAfter = GetGlobalMousePosition();
		_camera.Position += worldBefore - worldAfter;
		ClampCameraPosition();
	}

	private void ClampCameraPosition() {
		Vector2 pos = _camera.Position;
		pos.X = Mathf.Clamp(pos.X, _cameraPanBounds.Position.X, _cameraPanBounds.End.X);
		pos.Y = Mathf.Clamp(pos.Y, _cameraPanBounds.Position.Y, _cameraPanBounds.End.Y);
		_camera.Position = pos;
	}

	public override void _ExitTree() {
		if (GameManager.Instance != null)
			GameManager.Instance.GameOver -= OnGameOver;
		if (Instance == this)
			Instance = null;
	}

	public override void _Process(double delta) {
		if (!_gameActive)
			return;
		UpdateHover();
		UpdateFocusedEnemy();
		if (_spawning) {
			_spawnTimer -= (float)delta;
			if (_spawnTimer <= 0.0f)
				SpawnNextEnemy();
		}
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (!_gameActive)
			return;
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Space) {
			StartNextWave();
			return;
		}
		if (@event is InputEventKey rotateEvent && rotateEvent.Pressed && !rotateEvent.Echo && _selectedTowerType == GameData.TripleMergeResult) {
			if (rotateEvent.Keycode == Key.Q) {
				RotateCrusher(-1);
				return;
			}
			else if (rotateEvent.Keycode == Key.E) {
				RotateCrusher(1);
				return;
			}
		}
		if (@event is InputEventMouseButton mouseEvent) {
			if (mouseEvent.Pressed) {
				if (mouseEvent.ButtonIndex == MouseButton.Left) {
					if (_placingBuffTileType != "")
						TryPlaceBuffTileAt(GetGlobalMousePosition());
					else if (_selectedTowerType == "")
						TryInspectAt(GetGlobalMousePosition());
					else
						TryPlaceTowerAt(GetGlobalMousePosition());
				}
				else if (mouseEvent.ButtonIndex == MouseButton.Right) {
					_ui.ClearSelection();
				}
				else if (mouseEvent.ButtonIndex == MouseButton.Middle) {
					_isPanning = true;
				}
				else if (mouseEvent.ButtonIndex == MouseButton.WheelUp) {
					ZoomCamera(ZoomStep);
				}
				else if (mouseEvent.ButtonIndex == MouseButton.WheelDown) {
					ZoomCamera(1.0f / ZoomStep);
				}
			}
			else if (mouseEvent.ButtonIndex == MouseButton.Middle) {
				_isPanning = false;
			}
		}
		else if (@event is InputEventMouseMotion motionEvent && _isPanning) {
			_camera.Position -= motionEvent.Relative / _camera.Zoom;
			ClampCameraPosition();
		}
	}

	private void SelectTowerType(string type) {
		_selectedTowerType = type;
		_placingBuffTileType = "";
		_hoverCell = -1;
		_inspectedRangeActive = false;
		_crusherRotation = -1;
		ClearEnemyFocus();
		_hoverOverlay.QueueRedraw();
		if (type == "") {
			_ui.ClearTowerInfo();
		}
		else {
			Godot.Collections.Dictionary stats = GameData.GetTowerData(type);
			_ui.ShowTowerInfo(GameData.DescribeStats(type, stats, 0, 0, new Godot.Collections.Array(), GameData.PenroseEdgeLength));
		}
	}

	private void RotateCrusher(int deltaSteps) {
		if (_crusherRotation == -1)
			_crusherRotation = deltaSteps > 0 ? 0 : CrusherOrientationSteps - 1;
		else
			_crusherRotation = (_crusherRotation + deltaSteps + CrusherOrientationSteps) % CrusherOrientationSteps;
		_hoverCell = -1;
		_ui.ShowStatus($"Crusher orientation {_crusherRotation + 1}/{CrusherOrientationSteps}");
	}

	private void TryInspectAt(Vector2 pos) {
		BaseEnemy clickedEnemy = FindEnemyAt(pos);
		if (clickedEnemy != null) {
			FocusEnemy(clickedEnemy);
			return;
		}
		ClearEnemyFocus();
		foreach (Platform platform in _outpostPlatforms) {
			if (platform.Occupied && pos.DistanceTo(platform.WorldPos) <= GameData.PenroseEdgeLength * 0.85f) {
				BaseTower buffedTower = platform.Tower;
				_ui.ShowTowerInfo(GameData.DescribeStats(buffedTower.tower_type, buffedTower.get_live_stats_dict(), buffedTower.outpost_stacks, buffedTower.big_outpost_stacks, ToVariantArray(buffedTower.buff_tile_types), GameData.PenroseEdgeLength));
				ShowInspectedRange(buffedTower);
				return;
			}
		}
		int cell = _grid.WorldToCell(pos);
		if (_structureAtCell.TryGetValue(cell, out BaseTower tower)) {
			_ui.ShowTowerInfo(GameData.DescribeStats(tower.tower_type, tower.get_live_stats_dict(), tower.outpost_stacks, tower.big_outpost_stacks, ToVariantArray(tower.buff_tile_types), GameData.PenroseEdgeLength));
			ShowInspectedRange(tower);
		}
		else if (_buffTiles.TryGetValue(cell, out string buffType)) {
			string buffLabel = GameData.GetTileBuff(buffType)?.Label ?? buffType;
			int pct = (int)((GameData.TileBuffMult - 1.0f) * 100.0f);
			_ui.ShowTowerInfo(new TowerInfo(
				$"{buffLabel} Buff Tile",
				"Empty ground with a hidden bonus.",
				new List<string> { $"Build here to grant +{pct}% {buffLabel} to whatever you place." },
				new List<string>()));
			_inspectedRangeActive = false;
			_hoverOverlay.QueueRedraw();
		}
		else {
			_ui.ClearTowerInfo();
			_inspectedRangeActive = false;
			_hoverOverlay.QueueRedraw();
		}
	}

	// Closest enemy whose body the click actually landed on, rather than a
	// grid-cell lookup (see _structureAtCell) - enemies move continuously between
	// cells, so only a distance check against their live position makes sense. The
	// small tolerance forgives a click just outside a small enemy's circle.
	private BaseEnemy FindEnemyAt(Vector2 pos) {
		BaseEnemy closest = null;
		float closestDist = float.PositiveInfinity;
		foreach (Node node in _enemyContainer.GetChildren()) {
			if (node is BaseEnemy enemy && IsInstanceValid(enemy)) {
				float dist = enemy.GlobalPosition.DistanceTo(pos);
				if (dist <= enemy.radius + 4.0f && dist < closestDist) {
					closest = enemy;
					closestDist = dist;
				}
			}
		}
		return closest;
	}

	// Selects an enemy for the live info panel + highlight ring (see
	// UpdateFocusedEnemy/DrawHoverOverlay) - called both on a direct click (see
	// TryInspectAt) and automatically the instant a boss spawns (see SpawnNextEnemy).
	private void FocusEnemy(BaseEnemy enemy) {
		_focusedEnemy = enemy;
		_inspectedRangeActive = false;
		_ui.ShowTowerInfo(enemy.GetInfo());
		_hoverOverlay.QueueRedraw();
	}

	private void ClearEnemyFocus() => _focusedEnemy = null;

	// Refreshes the info panel every frame while an enemy is focused, since its
	// health/speed keep changing after the click that selected it - and drops focus
	// the moment it dies or reaches the end (both free the node - see OnEnemyDied/
	// OnEnemyReachedEnd), rather than needing a separate hook on each of those.
	private void UpdateFocusedEnemy() {
		if (_focusedEnemy == null)
			return;
		if (!IsInstanceValid(_focusedEnemy)) {
			_focusedEnemy = null;
			_ui.ClearTowerInfo();
			_hoverOverlay.QueueRedraw();
			return;
		}
		_ui.ShowTowerInfo(_focusedEnemy.GetInfo());
		_hoverOverlay.QueueRedraw();
	}

	private static Godot.Collections.Array ToVariantArray(Godot.Collections.Array source) => source;

	private static Godot.Collections.Array ToVariantArray(List<string> source) {
		var arr = new Godot.Collections.Array();
		foreach (string s in source)
			arr.Add(s);
		return arr;
	}

	private void ShowInspectedRange(BaseTower tower) {
		float rangeVal = GetPreviewRange(tower.tower_type, tower.buff_stacks, tower.buff_tile_types);
		bool isGlobalRange = IsGlobalRangeType(tower.tower_type);
		_inspectedRangeActive = rangeVal > 0.0f || isGlobalRange;
		_inspectedRangePos = tower.GlobalPosition;
		_inspectedRangeValue = rangeVal;
		_inspectedTowerType = tower.tower_type;
		_inspectedLosIgnoreCells = isGlobalRange ? GetOwnStructureCells(tower) : null;
		_hoverOverlay.QueueRedraw();
	}

	private bool IsGlobalRangeType(string type) {
		Godot.Collections.Dictionary data = GameData.GetTowerData(type);
		return data.ContainsKey("global_range") && data["global_range"].AsBool();
	}

	private void UpdateHover() {
		Vector2 mousePos = GetGlobalMousePosition();
		int cell = _grid.WorldToCell(mousePos);
		if (_placingBuffTileType != "") {
			_hoverCell = cell;
			_hoverPlatformIndex = -1;
			_hoverExtraCells = new List<int>();
			_hoverPlatformPreviewActive = false;
			_hoverPlatformMergePositions = new List<Vector2>();
			_hoverRangePreviewActive = false;
			_hoverPreviewPath = null;
			_hoverValid = _grid.CanPlace(cell) && !_buffTiles.ContainsKey(cell) && !_structureAtCell.ContainsKey(cell);
			_hoverOverlay.QueueRedraw();
			return;
		}
		int platformIndex = -1;
		bool isT1 = _selectedTowerType != "" && !GameData.IsT2(_selectedTowerType) && _selectedTowerType != GameData.TripleMergeResult;
		if (isT1)
			platformIndex = FindOpenPlatformNear(mousePos);
		_hoverCell = cell;
		_hoverPlatformIndex = platformIndex;
		_hoverExtraCells = new List<int>();
		_hoverPlatformPreviewActive = false;
		_hoverPlatformMergePositions = new List<Vector2>();
		_hoverRangePreviewActive = false;
		_hoverLosIgnoreCells = null;
		_hoverPreviewPath = null;
		if (platformIndex != -1) {
			_hoverValid = true;
			_hoverRangePreviewActive = true;
			Platform hoverPlatform = _outpostPlatforms[platformIndex];
			_hoverRangePreviewPos = hoverPlatform.WorldPos;
			int hoverTotalStacks = hoverPlatform.OutpostStacks + hoverPlatform.BigOutpostStacks;
			_hoverRangeValue = GetPreviewRange(_selectedTowerType, hoverTotalStacks, ToVariantArray(hoverPlatform.Buffs));
		}
		else if (_selectedTowerType == "") {
			_hoverValid = false;
		}
		else if (_selectedTowerType == GameData.TripleMergeResult) {
			SearchResult result = FindPartialLine(cell, RecipeRequirements(_selectedTowerType), mousePos, _crusherRotation);
			_hoverValid = result.Valid;
			if (_hoverValid)
				SetMultiHover(result.Cells, _grid.CellToWorld(result.Cells[1]), cell);
		}
		else if (_selectedTowerType == "big_outpost") {
			SearchResult result = FindBigOutpost(cell, mousePos);
			_hoverValid = result.Valid;
			if (_hoverValid) {
				foreach (int c in result.Cells) {
					if (c != cell)
						_hoverExtraCells.Add(c);
				}
				_hoverPlatformPreviewPositions = new List<Vector2>();
				(List<int[]> platformGroups, _) = ComputeBigOutpostPlatforms(result.Cells.Count);
				foreach (int[] group in platformGroups) {
					var groupCells = new List<int> { result.Cells[group[0]], result.Cells[group[1]], result.Cells[group[2]] };
					_hoverPlatformPreviewPositions.Add(CentroidOf(groupCells));
				}
				_hoverPlatformPreviewActive = true;
				_hoverPreviewPath = ComputeHoverPreviewPath(GameData.GetBlockingCells("big_outpost", result.Cells));
			}
		}
		else if (GameData.IsT2(_selectedTowerType)) {
			Dictionary<string, int> required = RecipeRequirements(_selectedTowerType);
			int platformIdx = FindOpenPlatformNear(mousePos, true);
			List<int> indices = platformIdx != -1 ? PlatformsSharingParent(platformIdx) : new List<int>();
			SearchResult classification = (platformIdx != -1 && required.Count > 0) ? ClassifyPartialPlatforms(indices, required) : new SearchResult { Valid = false };
			if (platformIdx != -1 && classification.Valid) {
				_hoverValid = true;
				foreach (int i in indices)
					_hoverPlatformMergePositions.Add(_outpostPlatforms[i].WorldPos);
				// Matches TryPlatformMergeAt's own placement logic - the merged tower ends
				// up at its platform group's parent structure, not at any one platform.
				BaseTower mergeParent = _outpostPlatforms[platformIdx].Parent;
				_hoverRangePreviewActive = true;
				_hoverRangePreviewPos = mergeParent != null ? mergeParent.GlobalPosition : _outpostPlatforms[platformIdx].WorldPos;
				_hoverLosIgnoreCells = mergeParent != null ? CellsOwnedBy(mergeParent) : null;
				var mergedBuffs = new List<string>();
				foreach (int i in indices)
					mergedBuffs.AddRange(_outpostPlatforms[i].Buffs);
				_hoverRangeValue = GetPreviewRange(_selectedTowerType, 0, ToVariantArray(mergedBuffs));
			}
			else {
				SearchResult result = FindPartialTriangle(cell, required, mousePos);
				_hoverValid = result.Valid;
				if (_hoverValid) {
					SetMultiHover(result.Cells, CentroidOf(result.Cells), cell);
					if (_selectedTowerType == "outpost") {
						_hoverRangePreviewActive = false;
						_hoverPlatformPreviewPositions = new List<Vector2> { CentroidOf(result.Cells) };
						_hoverPlatformPreviewActive = true;
					}
				}
			}
		}
		else {
			SingleCellHover(cell);
		}
		_hoverOverlay.QueueRedraw();
	}

	private void SetMultiHover(List<int> cells, Vector2 center, int cell) {
		foreach (int c in cells) {
			if (c != cell)
				_hoverExtraCells.Add(c);
		}
		_hoverRangePreviewActive = true;
		_hoverRangePreviewPos = center;
		_hoverRangeValue = GetPreviewRange(_selectedTowerType, 0, ToVariantArray(TileBuffsForCells(cells)));
		_hoverPreviewPath = ComputeHoverPreviewPath(GameData.GetBlockingCells(_selectedTowerType, cells));
	}

	private void SingleCellHover(int cell) {
		var info = ClassifySinglePlacement(cell, _selectedTowerType);
		if (!info.Allowed) {
			_hoverValid = false;
		}
		else if (!TileShapeAllowed(_selectedTowerType, cell)) {
			_hoverValid = false;
		}
		else if (info.Existing == null && !_grid.CanPlace(cell)) {
			_hoverValid = false;
		}
		else {
			Godot.Collections.Dictionary data = GameData.GetTowerData(_selectedTowerType);
			bool nonBlocking = data.ContainsKey("non_blocking") && data["non_blocking"].AsBool();
			if (nonBlocking || info.Existing != null) {
				_hoverValid = true;
			}
			else {
				var enemyCells = new List<int>();
				foreach (Node node in _enemyContainer.GetChildren()) {
					if (node is BaseEnemy enemy)
						enemyCells.Add(_grid.WorldToCell(enemy.GlobalPosition));
				}
				_hoverValid = !_grid.WouldBlockPath(cell, enemyCells);
			}
			_hoverRangePreviewActive = true;
			_hoverRangePreviewPos = _grid.CellToWorld(cell);
			_hoverRangeValue = GetPreviewRange(_selectedTowerType, 0, ToVariantArray(TileBuffsForCells(new List<int> { cell })));
			if (_hoverValid && !nonBlocking)
				_hoverPreviewPath = ComputeHoverPreviewPath(new List<int> { cell });
		}
	}

	private float GetPreviewRange(string type, int buffStacks, Godot.Collections.Array tileBuffs) {
		Godot.Collections.Dictionary data = GameData.GetTowerData(type);
		float rangeVal = data.ContainsKey("range") ? data["range"].AsSingle() : 0.0f;
		if (rangeVal <= 0.0f)
			rangeVal = data.ContainsKey("splash_radius") ? data["splash_radius"].AsSingle() : 0.0f;
		if (buffStacks > 0 && rangeVal > 0.0f)
			rangeVal *= Mathf.Pow(GameData.OutpostBuffRangeMult, buffStacks);
		if (rangeVal > 0.0f) {
			foreach (Variant tileBuff in tileBuffs) {
				if (tileBuff.AsString() == "range")
					rangeVal *= GameData.TileBuffMult;
			}
		}
		return rangeVal;
	}

	private void TryPlaceTowerAt(Vector2 pos) {
		if (_selectedTowerType == "")
			return;
		if (_selectedTowerType == GameData.TripleMergeResult) {
			TryLinePlacementAt(pos, _selectedTowerType);
		}
		else if (_selectedTowerType == "big_outpost") {
			TryPlaceBigOutpostAt(pos);
		}
		else if (GameData.IsT2(_selectedTowerType)) {
			Dictionary<string, int> required = RecipeRequirements(_selectedTowerType);
			int platformIdx = FindOpenPlatformNear(pos, true);
			List<int> indices = platformIdx != -1 ? PlatformsSharingParent(platformIdx) : new List<int>();
			SearchResult classification = (platformIdx != -1 && required.Count > 0) ? ClassifyPartialPlatforms(indices, required) : new SearchResult { Valid = false };
			if (platformIdx != -1 && classification.Valid)
				TryPlatformMergeAt(platformIdx, _selectedTowerType);
			else
				TryTrianglePlacementAt(pos, _selectedTowerType);
		}
		else {
			int platformIndex = FindOpenPlatformNear(pos);
			if (platformIndex != -1)
				TryPlaceOnPlatform(platformIndex);
			else
				TryPlaceSingleAt(pos);
		}
	}

	private int FindOpenPlatformNear(Vector2 pos, bool includeOccupied = false) {
		for (int i = 0; i < _outpostPlatforms.Count; i++) {
			Platform platform = _outpostPlatforms[i];
			if (platform.Occupied && !includeOccupied)
				continue;
			if (pos.DistanceTo(platform.WorldPos) <= GameData.PenroseEdgeLength * 0.85f)
				return i;
		}
		return -1;
	}

	private void TryPlaceOnPlatform(int platformIndex) {
		Platform platform = _outpostPlatforms[platformIndex];
		Godot.Collections.Dictionary data = GameData.GetTowerData(_selectedTowerType);
		int cost = GameData.GetScaledCost(data, CountTowers(_selectedTowerType));
		if (!GameManager.Instance.SpendGold(cost)) {
			_ui.ShowStatus("Not enough gold!");
			return;
		}
		int outpostStacks = platform.OutpostStacks;
		int bigOutpostStacks = platform.BigOutpostStacks;
		BaseTower tower = InstantiateTower(_selectedTowerType);
		_towerContainer.AddChild(tower);
		tower.GlobalPosition = platform.WorldPos;
		bool? needsThin = RequiredTileIsThin(_selectedTowerType);
		TileShape shape = needsThin.HasValue
			? TileShape.Rhombus(Vector2.Zero, GameData.PenroseEdgeLength, needsThin.Value)
			: TileShape.Generic(Vector2.Zero, GameData.PenroseEdgeLength);
		tower.setup(_projectileContainer, GameData.PenroseEdgeLength, new List<TileShape> { shape });
		tower.apply_buffs(outpostStacks, bigOutpostStacks, ToVariantArray(platform.Buffs));
		platform.Occupied = true;
		platform.Tower = tower;
		RefreshTowerPrices();
		_ui.ShowStatus("Outpost bonus applied!");
		_ui.ShowTowerInfo(GameData.DescribeStats(_selectedTowerType, tower.get_live_stats_dict(), outpostStacks, bigOutpostStacks, ToVariantArray(tower.buff_tile_types), GameData.PenroseEdgeLength));
		_hoverCell = -1;
		_hoverPlatformIndex = -1;
		_hoverOverlay.QueueRedraw();
	}

	private Vector2 CentroidOf(List<int> cells) {
		Vector2 sum = Vector2.Zero;
		foreach (int c in cells)
			sum += _grid.CellToWorld(c);
		return sum / cells.Count;
	}

	// Builds the real per-tile shape (see TileShape) for a tower occupying tileId,
	// offset into local drawing space relative to towerWorldPos (which is usually
	// but not always that same tile's own center - e.g. a multi-tile merge's tower
	// sits at the group's centroid instead).
	private TileShape TileShapeAt(int tileId, Vector2 towerWorldPos) {
		PenroseTile tile = _grid.Tiles[tileId];
		var corners = new Vector2[tile.Corners.Length];
		for (int i = 0; i < corners.Length; i++)
			corners[i] = tile.Corners[i] - towerWorldPos;
		return new TileShape { CenterOffset = tile.Center - towerWorldPos, CornerOffsets = corners };
	}

	private List<TileShape> TileShapesAt(List<int> tileIds, Vector2 towerWorldPos) {
		var shapes = new List<TileShape>();
		foreach (int id in tileIds)
			shapes.Add(TileShapeAt(id, towerWorldPos));
		return shapes;
	}

	// Which Penrose rhombus shape `type` requires, if any - null for a tower with no
	// fixed shape (utility towers, every T2/merge tower). Base (T1) attacking towers
	// are keyed off their own damage type (see DamagingBase.TowerDamageType):
	// Physical needs a thick tile, Elemental needs a thin one. Utility towers never
	// populate "damage_type" at all, and every T2/merge tower always builds through
	// a multi-cell recipe rather than sitting on one single tile, so "has a
	// damage_type key" here is exactly "is one of the base attacking towers" - no
	// separate type list to keep in sync.
	private bool? RequiredTileIsThin(string type) {
		Godot.Collections.Dictionary data = GameData.GetTowerData(type);
		if (!data.ContainsKey("damage_type"))
			return null;
		return data["damage_type"].AsString() == "Elemental";
	}

	private bool TileShapeAllowed(string type, int cell) {
		bool? needsThin = RequiredTileIsThin(type);
		return needsThin == null || _grid.Tiles[cell].IsThin == needsThin.Value;
	}

	private readonly record struct PlacementInfo(BaseTower Existing, bool ReplacingRock, bool ReplacingWall, bool Allowed);

	private PlacementInfo ClassifySinglePlacement(int cell, string type) {
		_structureAtCell.TryGetValue(cell, out BaseTower existing);
		bool replacingRock = existing != null && existing.tower_type == "rock" && type == "wall";
		bool replacingWall = existing != null && existing.tower_type == "wall" && type != "wall";
		bool allowed = existing == null || replacingRock || replacingWall;
		return new PlacementInfo(existing, replacingRock, replacingWall, allowed);
	}

	private void TryPlaceSingleAt(Vector2 pos) {
		int cell = _grid.WorldToCell(pos);
		PlacementInfo info = ClassifySinglePlacement(cell, _selectedTowerType);
		if (!info.Allowed)
			return;
		if (!TileShapeAllowed(_selectedTowerType, cell))
			return;
		BaseTower existing = info.Existing;
		if (existing == null && !_grid.CanPlace(cell))
			return;
		Godot.Collections.Dictionary data = GameData.GetTowerData(_selectedTowerType);
		bool blocksPath = !(data.ContainsKey("non_blocking") && data["non_blocking"].AsBool());
		bool wasBlocking = existing != null;
		if (blocksPath && !wasBlocking) {
			var enemyCells = new List<int>();
			foreach (Node node in _enemyContainer.GetChildren()) {
				if (node is BaseEnemy enemy)
					enemyCells.Add(_grid.WorldToCell(enemy.GlobalPosition));
			}
			if (_grid.WouldBlockPath(cell, enemyCells)) {
				_ui.ShowStatus("That would trap an enemy with no way out!");
				return;
			}
		}
		int cost = GameData.GetScaledCost(data, CountTowers(_selectedTowerType));
		if (!GameManager.Instance.SpendGold(cost)) {
			_ui.ShowStatus("Not enough gold!");
			return;
		}
		if (info.ReplacingWall)
			_towerAtCell.Remove(cell);
		if (existing != null) {
			_towerContainer.RemoveChild(existing);
			existing.QueueFree();
		}
		if (blocksPath)
			_grid.Place(cell);
		else if (wasBlocking)
			_grid.Unblock(cell);
		BaseTower tower = InstantiateTower(_selectedTowerType);
		_towerContainer.AddChild(tower);
		tower.GlobalPosition = _grid.CellToWorld(cell);
		tower.setup(_projectileContainer, GameData.PenroseEdgeLength, TileShapesAt(new List<int> { cell }, _grid.CellToWorld(cell)));
		tower.apply_buffs(0, 0, ToVariantArray(TileBuffsForCells(new List<int> { cell })));
		_structureAtCell[cell] = tower;
		if (_selectedTowerType != "wall")
			_towerAtCell[cell] = tower;
		RefreshTowerPrices();
		if (blocksPath != wasBlocking) {
			RecomputePath();
			RerouteAllEnemies();
		}
		if (info.ReplacingWall)
			_ui.ShowStatus("Tower built over wall — no refund.");
		else if (info.ReplacingRock)
			_ui.ShowStatus("Wall built over rock.");
		if (_selectedTowerType == "flipper")
			FlipBuffTiles(cell);
		_hoverCell = -1;
	}

	// The edge-neighbor of flipperTile whose direction from it is closest to exactly
	// opposite candidate's - the Penrose analog of a hex tile's exact antipodal
	// neighbor. Penrose tiles rarely have a perfectly opposite neighbor the way a
	// hex tile's 6 fixed directions guaranteed, so this picks the closest match
	// among the flipper's own edge-neighbors instead of requiring an exact one; if
	// there is no other neighbor to compare against, it returns candidate itself
	// (the caller then treats that as "no valid opposite" and leaves the tile put).
	private int OppositeNeighbor(int flipperTile, int candidate) {
		Vector2 center = _grid.CellToWorld(flipperTile);
		Vector2 dir = (_grid.CellToWorld(candidate) - center).Normalized();
		int best = candidate;
		float bestDot = float.PositiveInfinity;
		foreach (int neighbor in _grid.Tiles[flipperTile].EdgeNeighborIds) {
			if (neighbor == candidate)
				continue;
			Vector2 otherDir = (_grid.CellToWorld(neighbor) - center).Normalized();
			float dot = dir.Dot(otherDir);
			if (dot < bestDot) {
				bestDot = dot;
				best = neighbor;
			}
		}
		return best;
	}

	// Point-reflects each of flipperTile's edge-adjacent buff tiles through it (see
	// OppositeNeighbor - the Penrose stand-in for a hex tile's exact antipodal
	// neighbor) - only buff tiles immediately adjacent to the flipper move, nothing
	// further out. A tile whose mirrored destination is invalid (off-grid, entry/
	// exit, blocked, occupied, no true opposite, or already claimed by another
	// neighbor this pass) stays at its original cell instead.
	private void FlipBuffTiles(int flipperTile) {
		var neighborTiles = new List<(int Cell, string Type)>();
		foreach (int neighbor in _grid.Tiles[flipperTile].EdgeNeighborIds) {
			if (_buffTiles.TryGetValue(neighbor, out string buffType))
				neighborTiles.Add((neighbor, buffType));
		}
		if (neighborTiles.Count == 0)
			return;
		foreach ((int cell, _) in neighborTiles)
			_buffTiles.Remove(cell);
		var claimed = new HashSet<int>();
		foreach ((int cell, string type) in neighborTiles) {
			int mirrored = OppositeNeighbor(flipperTile, cell);
			bool mirrorValid = mirrored != cell
				&& _grid.CanPlace(mirrored)
				&& !_structureAtCell.ContainsKey(mirrored)
				&& !_buffTiles.ContainsKey(mirrored)
				&& !claimed.Contains(mirrored);
			int destination = mirrorValid ? mirrored : cell;
			if (!_buffTiles.ContainsKey(destination) && !claimed.Contains(destination)) {
				_buffTiles[destination] = type;
				claimed.Add(destination);
			}
		}
		QueueRedraw();
		_ui.ShowStatus("Flipped the neighboring buff tiles to the opposite side!");
	}

	private void SpawnRocks() {
		SpawnBorderRocks();
		int placed = 0;
		int attempts = 0;
		while (placed < GameData.RockCount && attempts < GameData.RockCount * 25) {
			attempts++;
			int cell = GD.RandRange(0, _grid.Tiles.Count - 1);
			if (!_grid.CanPlace(cell) || _structureAtCell.ContainsKey(cell))
				continue;
			if (_grid.WouldBlockPath(cell))
				continue;
			PlaceRockAt(cell);
			placed++;
		}
	}

	// A ring of rocks along the whole patch's own boundary tiles, framing the
	// playable area - placed before the scattered random rocks so those can't land
	// on (or be double-counted against) a boundary tile.
	private void SpawnBorderRocks() {
		foreach (int cell in _grid.BoundaryTileIds()) {
			if (!_grid.CanPlace(cell) || _structureAtCell.ContainsKey(cell))
				continue;
			if (_grid.WouldBlockPath(cell))
				continue;
			PlaceRockAt(cell);
		}
	}

	private void PlaceRockAt(int cell) {
		_grid.Place(cell);
		BaseTower rock = InstantiateTower("rock");
		_towerContainer.AddChild(rock);
		rock.GlobalPosition = _grid.CellToWorld(cell);
		rock.setup(_projectileContainer, GameData.PenroseEdgeLength, TileShapesAt(new List<int> { cell }, _grid.CellToWorld(cell)));
		_structureAtCell[cell] = rock;
	}

	private void SpawnWater() {
		int placed = 0;
		int attempts = 0;
		while (placed < GameData.WaterCount && attempts < GameData.WaterCount * 25) {
			attempts++;
			int cell = GD.RandRange(0, _grid.Tiles.Count - 1);
			if (!_grid.CanPlace(cell) || _structureAtCell.ContainsKey(cell))
				continue;
			if (_grid.WouldBlockPath(cell))
				continue;
			_grid.Place(cell);
			_waterCells.Add(cell);
			BaseTower water = InstantiateTower("water");
			_towerContainer.AddChild(water);
			water.GlobalPosition = _grid.CellToWorld(cell);
			water.setup(_projectileContainer, GameData.PenroseEdgeLength, TileShapesAt(new List<int> { cell }, _grid.CellToWorld(cell)));
			_structureAtCell[cell] = water;
			placed++;
		}
	}

	private void SpawnBuffTiles() {
		var pool = new List<string>();
		foreach (TileBuff buff in GameData.TileBuffs) {
			for (int i = 0; i < buff.SpawnCount; i++)
				pool.Add(buff.Key);
		}
		Shuffle(pool);
		int placed = 0;
		int attempts = 0;
		while (placed < pool.Count && attempts < pool.Count * 25) {
			attempts++;
			int cell = GD.RandRange(0, _grid.Tiles.Count - 1);
			if (!_grid.IsInBounds(cell) || cell == _grid.Entry || cell == _grid.Exit)
				continue;
			if (_grid.IsBlocked(cell) || _buffTiles.ContainsKey(cell))
				continue;
			_buffTiles[cell] = pool[placed];
			placed++;
		}
	}

	private List<string> TileBuffsForCells(List<int> cells) {
		var found = new List<string>();
		foreach (int c in cells) {
			if (_buffTiles.TryGetValue(c, out string buffType))
				found.Add(buffType);
		}
		return found;
	}

	private BaseTower InstantiateTower(string type) => (BaseTower)GameData.TowerScenes[type].Instantiate();

	private int CountTowers(string type) {
		int count = 0;
		foreach (Node child in _towerContainer.GetChildren()) {
			if (child is BaseTower tower && tower.tower_type == type)
				count++;
		}
		return count;
	}

	private void RefreshTowerPrices() {
		foreach (string type in GameData.TowerTypeKeys)
			UpdatePriceButton(type, GameData.GetTowerData(type));
	}

	private void UpdatePriceButton(string type, Godot.Collections.Dictionary data) {
		int cost = GameData.GetScaledCost(data, CountTowers(type)) + MissingT1Cost(RecipeRequirements(type));
		_ui.UpdateTowerPrice(type, cost);
	}

	private Dictionary<string, int> RecipeRequirements(string resultType) {
		if (GameData.TriangleMergeRecipes.TryGetValue(resultType, out Dictionary<string, int> recipe))
			return recipe;
		if (resultType == "big_outpost")
			return GameData.BigOutpostRequired;
		if (resultType == GameData.TripleMergeResult) {
			var counts = new Dictionary<string, int>();
			foreach (string t1Type in GameData.TripleMergePattern)
				counts[t1Type] = counts.GetValueOrDefault(t1Type, 0) + 1;
			return counts;
		}
		return new Dictionary<string, int>();
	}

	private int MissingT1Cost(Dictionary<string, int> missing) {
		int total = 0;
		foreach (KeyValuePair<string, int> entry in missing) {
			Godot.Collections.Dictionary t1Data = GameData.GetTowerData(entry.Key);
			total += GameData.GetScaledCost(t1Data, CountTowers(entry.Key)) * entry.Value;
		}
		return total;
	}

	private BaseTower ConsumeAndBuild(List<int> cells, string resultType, Vector2 worldPos) {
		List<int> blockingCells = GameData.GetBlockingCells(resultType, cells);
		foreach (int c in cells) {
			if (blockingCells.Contains(c))
				_grid.Place(c);
			else if (_grid.IsBlocked(c))
				_grid.Unblock(c);
		}
		List<TileShape> shapes = TileShapesAt(cells, worldPos);
		foreach (int c in cells) {
			_towerAtCell.Remove(c);
			if (_structureAtCell.TryGetValue(c, out BaseTower old)) {
				_structureAtCell.Remove(c);
				_towerContainer.RemoveChild(old);
				old.QueueFree();
			}
		}
		BaseTower tower = InstantiateTower(resultType);
		_towerContainer.AddChild(tower);
		tower.GlobalPosition = worldPos;
		tower.setup(_projectileContainer, GameData.PenroseEdgeLength, shapes);
		tower.apply_buffs(0, 0, ToVariantArray(TileBuffsForCells(cells)));
		foreach (int c in cells) {
			_towerAtCell[c] = tower;
			_structureAtCell[c] = tower;
		}
		RefreshTowerPrices();
		RecomputePath();
		RerouteAllEnemies();
		return tower;
	}

	private SearchResult ClassifyPartialCells(List<int> cells, Dictionary<string, int> required) {
		var existingCounts = new Dictionary<string, int>();
		foreach (int c in cells) {
			if (_structureAtCell.TryGetValue(c, out BaseTower structure)) {
				string t = structure.tower_type;
				if (!required.ContainsKey(t))
					return new SearchResult { Valid = false };
				existingCounts[t] = existingCounts.GetValueOrDefault(t, 0) + 1;
				if (existingCounts[t] > required[t])
					return new SearchResult { Valid = false };
			}
			else if (!_grid.CanPlace(c)) {
				return new SearchResult { Valid = false };
			}
		}
		var missing = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in required) {
			int need = entry.Value - existingCounts.GetValueOrDefault(entry.Key, 0);
			if (need > 0)
				missing[entry.Key] = need;
		}
		if (!ShapesSatisfiable(cells, missing))
			return new SearchResult { Valid = false };
		return new SearchResult { Valid = true, Missing = missing };
	}

	// A merge recipe's still-missing ingredients each need to be freshly placed on an
	// empty tile of their own required shape (see RequiredTileIsThin/TileShapeAllowed),
	// same as if they were bought and placed alone - an already-occupied cell in
	// `cells` was necessarily placed correctly when it was built, so only the empty
	// ones need checking here. A wedge that's the right ingredient type/count but the
	// wrong mix of thick/thin tiles (e.g. all-thick where the recipe still needs a
	// thin ingredient) can't actually complete the recipe, even though the aggregate
	// counts in `missing` alone would suggest it can.
	private bool ShapesSatisfiable(List<int> cells, Dictionary<string, int> missing) {
		int missingThick = 0, missingThin = 0, missingEither = 0;
		foreach (KeyValuePair<string, int> entry in missing) {
			bool? needsThin = RequiredTileIsThin(entry.Key);
			if (needsThin == null)
				missingEither += entry.Value;
			else if (needsThin.Value)
				missingThin += entry.Value;
			else
				missingThick += entry.Value;
		}
		int emptyThick = 0, emptyThin = 0;
		foreach (int c in cells) {
			if (_structureAtCell.ContainsKey(c))
				continue;
			if (_grid.Tiles[c].IsThin)
				emptyThin++;
			else
				emptyThick++;
		}
		if (emptyThick < missingThick || emptyThin < missingThin)
			return false;
		return (emptyThick - missingThick) + (emptyThin - missingThin) >= missingEither;
	}

	private List<int> PlatformsSharingParent(int platformIdx) {
		BaseTower parent = _outpostPlatforms[platformIdx].Parent;
		if (parent == null)
			return new List<int> { platformIdx };
		var indices = new List<int>();
		for (int i = 0; i < _outpostPlatforms.Count; i++) {
			if (_outpostPlatforms[i].Parent == parent)
				indices.Add(i);
		}
		return indices;
	}

	private SearchResult ClassifyPartialPlatforms(List<int> indices, Dictionary<string, int> required) {
		int totalRequired = 0;
		foreach (int v in required.Values)
			totalRequired += v;
		if (indices.Count != totalRequired)
			return new SearchResult { Valid = false };
		var existingCounts = new Dictionary<string, int>();
		foreach (int i in indices) {
			Platform platform = _outpostPlatforms[i];
			if (platform.Occupied) {
				string t = platform.Tower.tower_type;
				if (!required.ContainsKey(t))
					return new SearchResult { Valid = false };
				existingCounts[t] = existingCounts.GetValueOrDefault(t, 0) + 1;
				if (existingCounts[t] > required[t])
					return new SearchResult { Valid = false };
			}
		}
		var missing = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> entry in required) {
			int need = entry.Value - existingCounts.GetValueOrDefault(entry.Key, 0);
			if (need > 0)
				missing[entry.Key] = need;
		}
		return new SearchResult { Valid = true, Missing = missing };
	}

	// clickedTile plus its own edge-neighbors - the search radius every shape search
	// below scans from, so a click near (not just exactly on) a valid shape still
	// finds it.
	private List<int> SearchAnchors(int clickedTile) {
		var anchors = new List<int> { clickedTile };
		anchors.AddRange(_grid.Tiles[clickedTile].EdgeNeighborIds);
		return anchors;
	}

	// Every tiling vertex clickedTile touches, plus every vertex its edge-neighbors
	// touch - the vertex-search analog of SearchAnchors, used by the two merge
	// shapes that hunt for a shared-corner cluster (T2 triangles, Big Outpost).
	private List<int> SearchVertices(int clickedTile) {
		var vertices = new List<int>(_grid.Tiles[clickedTile].VertexIds);
		foreach (int neighborId in _grid.Tiles[clickedTile].EdgeNeighborIds) {
			foreach (int v in _grid.Tiles[neighborId].VertexIds) {
				if (!vertices.Contains(v))
					vertices.Add(v);
			}
		}
		return vertices;
	}

	// T2 triangle recipes (long_range_frost, sniper, outpost, ...) look for 3 tiles
	// that are ALL mutually neighboring - the Penrose analog of the old "anchor + 2
	// consecutive 60-degree neighbors" hex search, where any 3 mutually-adjacent
	// hexes always converged on one shared corner. PenroseVertex.Ring is already
	// sorted into cyclic order, so 3 consecutive tiles in it are guaranteed adjacent
	// pairwise-in-sequence (ring[i]-ring[i+1], ring[i+1]-ring[i+2]) for free - but
	// unlike a hex vertex (always exactly 3 tiles), a Penrose vertex's ring can have
	// more than 3, so the first and last of a 3-window aren't automatically
	// neighbors too unless the ring only has 3 tiles total. The explicit check below
	// requires that regardless of ring size, so the result is always a tight
	// 3-way-mutual cluster, never just a chain through the middle tile.
	private SearchResult FindPartialTriangle(int clickedTile, Dictionary<string, int> required, Vector2 nearPos) {
		var best = new SearchResult { Valid = false };
		float bestDist = float.PositiveInfinity;
		float maxDist = GameData.PenroseEdgeLength * MaxSnapDistMult;
		foreach (int vertexId in SearchVertices(clickedTile)) {
			List<int> ring = _grid.Vertices[vertexId].Ring;
			if (ring.Count < 3)
				continue;
			for (int i = 0; i < ring.Count; i++) {
				int first = ring[i];
				int last = ring[(i + 2) % ring.Count];
				if (!_grid.Tiles[first].EdgeNeighborIds.Contains(last))
					continue;
				var cells = new List<int> { first, ring[(i + 1) % ring.Count], last };
				SearchResult result = ClassifyPartialCells(cells, required);
				if (result.Valid) {
					float dist = nearPos.DistanceTo(CentroidOf(cells));
					if (dist < bestDist && dist <= maxDist) {
						bestDist = dist;
						result.Cells = cells;
						best = result;
					}
				}
			}
		}
		return best;
	}

	// Every unordered pair of tile's own edge-neighbors that AREN'T themselves
	// mutually adjacent - the candidate (A,C) ends for a Crusher chain A-B-C
	// centered on tile. Excluding an adjacent A-C pair is what keeps this a genuine
	// 3-tile line instead of occasionally closing into the same kind of tight
	// mutual triangle the other T2 recipes now require (see FindPartialTriangle) -
	// Crusher is specifically the shape that ISN'T that.
	private List<(int A, int C)> NeighborPairs(PenroseTile tile) {
		var pairs = new List<(int, int)>();
		for (int i = 0; i < tile.EdgeNeighborIds.Count; i++) {
			for (int j = i + 1; j < tile.EdgeNeighborIds.Count; j++) {
				int a = tile.EdgeNeighborIds[i];
				int c = tile.EdgeNeighborIds[j];
				if (!_grid.Tiles[a].EdgeNeighborIds.Contains(c))
					pairs.Add((a, c));
			}
		}
		return pairs;
	}

	// Crusher's shape is a 3-tile connected chain A-B-C (B in the middle, edge-
	// adjacent to both A and C, but A and C themselves NOT adjacent - see
	// NeighborPairs) rather than the old fixed-direction 3-in-a-row - "which pair of
	// B's edge-neighbors to use as (A,C)" is exactly what _crusherRotation's Q/E
	// cycle now selects (see RotateCrusher), generalizing the old 3-compass-direction
	// cycle to however many qualifying neighbor pairs B actually has.
	private SearchResult FindPartialLine(int clickedTile, Dictionary<string, int> required, Vector2 nearPos, int pairIndex = -1) {
		var best = new SearchResult { Valid = false };
		float bestDist = float.PositiveInfinity;
		float maxDist = GameData.PenroseEdgeLength * MaxSnapDistMult;
		foreach (int b in SearchAnchors(clickedTile)) {
			List<(int A, int C)> pairs = NeighborPairs(_grid.Tiles[b]);
			if (pairs.Count == 0)
				continue;
			IEnumerable<(int A, int C)> candidates = pairIndex >= 0 ? new[] { pairs[pairIndex % pairs.Count] } : pairs;
			foreach ((int a, int c) in candidates) {
				var cells = new List<int> { a, b, c };
				SearchResult result = ClassifyPartialCells(cells, required);
				if (result.Valid) {
					float dist = nearPos.DistanceTo(_grid.CellToWorld(b));
					if (dist < bestDist && dist <= maxDist) {
						bestDist = dist;
						result.Cells = cells;
						best = result;
					}
				}
			}
		}
		return best;
	}

	private void ExecutePartialBuild(List<int> cells, Dictionary<string, int> missing, string resultType, Vector2 worldPos, List<int[]> platformGroups = null, List<int[]> buffGroups = null) {
		platformGroups ??= new List<int[]>();
		buffGroups ??= new List<int[]>();
		Godot.Collections.Dictionary data = GameData.GetTowerData(resultType);
		List<int> blockingCells = GameData.GetBlockingCells(resultType, cells);
		if (blockingCells.Count > 0) {
			var newlyBlocked = new List<int>();
			foreach (int c in blockingCells) {
				if (!_grid.IsBlocked(c))
					newlyBlocked.Add(c);
			}
			if (newlyBlocked.Count > 0) {
				var enemyCells = new List<int>();
				foreach (Node node in _enemyContainer.GetChildren()) {
					if (node is BaseEnemy enemy)
						enemyCells.Add(_grid.WorldToCell(enemy.GlobalPosition));
				}
				if (_grid.WouldBlockPathMulti(newlyBlocked, enemyCells)) {
					_ui.ShowStatus("That would trap an enemy with no way out!");
					return;
				}
			}
		}
		int cost = GameData.GetScaledCost(data, CountTowers(resultType)) + MissingT1Cost(missing);
		if (!GameManager.Instance.SpendGold(cost)) {
			_ui.ShowStatus("Not enough gold!");
			return;
		}
		BaseTower tower = ConsumeAndBuild(cells, resultType, worldPos);
		if (platformGroups.Count > 0) {
			List<int[]> effectiveBuffGroups = buffGroups.Count > 0 ? buffGroups : platformGroups;
			var platformOffsets = new Godot.Collections.Array();
			int initialOutpostStacks = resultType == "outpost" ? 1 : 0;
			int initialBigOutpostStacks = resultType == "big_outpost" ? 1 : 0;
			for (int i = 0; i < platformGroups.Count; i++) {
				var groupCells = new List<int>();
				foreach (int idx in platformGroups[i])
					groupCells.Add(cells[idx]);
				var buffCells = new List<int>();
				foreach (int idx in effectiveBuffGroups[i])
					buffCells.Add(cells[idx]);
				Vector2 platformPos = CentroidOf(groupCells);
				_outpostPlatforms.Add(new Platform {
					WorldPos = platformPos,
					WallCells = groupCells,
					Occupied = false,
					Tower = null,
					Parent = tower,
					Buffs = TileBuffsForCells(buffCells),
					OutpostStacks = initialOutpostStacks,
					BigOutpostStacks = initialBigOutpostStacks,
				});
				platformOffsets.Add(platformPos - worldPos);
			}
			tower.platform_offsets = platformOffsets;
		}
		_ui.ShowStatus($"Built {data["name"].AsString()}!");
		_ui.ShowTowerInfo(GameData.DescribeStats(resultType, tower.get_live_stats_dict(), 0, 0, ToVariantArray(tower.buff_tile_types), GameData.PenroseEdgeLength));
		_hoverCell = -1;
	}

	private void TryTrianglePlacementAt(Vector2 pos, string resultType) {
		int cell = _grid.WorldToCell(pos);
		SearchResult result = FindPartialTriangle(cell, RecipeRequirements(resultType), pos);
		if (!result.Valid) {
			_ui.ShowStatus("Needs a triangle of matching or empty tiles.");
			return;
		}
		List<int[]> groups = resultType == "outpost" ? new List<int[]> { new[] { 0, 1, 2 } } : new List<int[]>();
		ExecutePartialBuild(result.Cells, result.Missing, resultType, CentroidOf(result.Cells), groups);
	}

	private void TryPlatformMergeAt(int platformIdx, string resultType) {
		List<int> indices = PlatformsSharingParent(platformIdx);
		Dictionary<string, int> required = RecipeRequirements(resultType);
		SearchResult classification = required.Count > 0 ? ClassifyPartialPlatforms(indices, required) : new SearchResult { Valid = false };
		if (!classification.Valid) {
			_ui.ShowStatus($"These platforms don't match {GameData.GetTowerData(resultType)["name"].AsString()}'s recipe.");
			return;
		}
		Dictionary<string, int> missing = classification.Missing;
		Godot.Collections.Dictionary data = GameData.GetTowerData(resultType);
		int cost = GameData.GetScaledCost(data, CountTowers(resultType)) + MissingT1Cost(missing);
		if (!GameManager.Instance.SpendGold(cost)) {
			_ui.ShowStatus("Not enough gold!");
			return;
		}
		BaseTower parent = _outpostPlatforms[platformIdx].Parent;
		Vector2 worldPos = parent != null ? parent.GlobalPosition : _outpostPlatforms[platformIdx].WorldPos;
		int prevOutpostStacks = _outpostPlatforms[platformIdx].OutpostStacks;
		int prevBigOutpostStacks = _outpostPlatforms[platformIdx].BigOutpostStacks;
		var shapes = new List<TileShape>();
		var mergedBuffs = new List<string>();
		foreach (int i in indices) {
			shapes.Add(TileShape.Generic(_outpostPlatforms[i].WorldPos - worldPos, GameData.PenroseEdgeLength));
			mergedBuffs.AddRange(_outpostPlatforms[i].Buffs);
		}
		foreach (int i in indices) {
			BaseTower oldTower = _outpostPlatforms[i].Tower;
			if (oldTower != null) {
				_towerContainer.RemoveChild(oldTower);
				oldTower.QueueFree();
			}
		}
		BaseTower tower = InstantiateTower(resultType);
		_towerContainer.AddChild(tower);
		tower.GlobalPosition = worldPos;
		tower.setup(_projectileContainer, GameData.PenroseEdgeLength, shapes);
		tower.apply_buffs(prevOutpostStacks, prevBigOutpostStacks, ToVariantArray(mergedBuffs));
		foreach (int i in indices) {
			_outpostPlatforms[i].Occupied = true;
			_outpostPlatforms[i].Tower = tower;
		}
		if (resultType == "outpost") {
			_outpostPlatforms.Add(new Platform {
				WorldPos = worldPos,
				WallCells = new List<int>(),
				Occupied = false,
				Tower = null,
				Parent = tower,
				Buffs = mergedBuffs,
				OutpostStacks = prevOutpostStacks + 1,
				BigOutpostStacks = prevBigOutpostStacks,
			});
			tower.platform_offsets = new Godot.Collections.Array { Vector2.Zero };
		}
		RefreshTowerPrices();
		_ui.ShowStatus($"Built {data["name"].AsString()}!");
		_ui.ShowTowerInfo(GameData.DescribeStats(resultType, tower.get_live_stats_dict(), prevOutpostStacks, prevBigOutpostStacks, ToVariantArray(tower.buff_tile_types), GameData.PenroseEdgeLength));
		_hoverCell = -1;
		_hoverOverlay.QueueRedraw();
	}

	// Big Outpost's platforms generalize the old fixed 3-corner arrangement to "one
	// platform per ring tile": a sliding, wrapping window of 3 consecutive ring
	// tiles per platform, buffed by its 2 outer tiles (the window's middle tile is
	// the one shared with both neighboring windows, so it's excluded from the buff
	// pool - mirroring the old system's "corners split pairwise between neighboring
	// platforms" idea, generalized to however many tiles the found junction has).
	private static (List<int[]> Platforms, List<int[]> Buffs) ComputeBigOutpostPlatforms(int ringSize) {
		var platforms = new List<int[]>();
		var buffs = new List<int[]>();
		for (int i = 0; i < ringSize; i++) {
			int next = (i + 1) % ringSize;
			int nextNext = (i + 2) % ringSize;
			platforms.Add(new[] { i, next, nextNext });
			buffs.Add(new[] { i, nextNext });
		}
		return (platforms, buffs);
	}

	private void TryPlaceBigOutpostAt(Vector2 pos) {
		int cell = _grid.WorldToCell(pos);
		SearchResult result = FindBigOutpost(cell, pos);
		if (!result.Valid) {
			_ui.ShowStatus($"Needs {GameData.BigOutpostMinRingSize}+ Walls (built or empty) meeting at one junction.");
			return;
		}
		(List<int[]> platforms, List<int[]> buffs) = ComputeBigOutpostPlatforms(result.Cells.Count);
		ExecutePartialBuild(result.Cells, result.Missing, "big_outpost", CentroidOf(result.Cells), platforms, buffs);
	}

	// Big Outpost (today: a hard-coded 6-cell shape) becomes a full tiling-vertex
	// ring: any vertex with at least BigOutpostMinRingSize tiles around it (a
	// Sun-like junction) is a candidate, and the whole ring - not a sub-window -
	// becomes the required wall set, however big that particular junction actually
	// is. Ties break the same way every other shape search here does: whichever
	// valid candidate sits closest to the click.
	private SearchResult FindBigOutpost(int clickedTile, Vector2 nearPos) {
		var best = new SearchResult { Valid = false };
		float bestDist = float.PositiveInfinity;
		float maxDist = GameData.PenroseEdgeLength * MaxSnapDistMultBig;
		foreach (int vertexId in SearchVertices(clickedTile)) {
			List<int> ring = _grid.Vertices[vertexId].Ring;
			if (ring.Count < GameData.BigOutpostMinRingSize)
				continue;
			var required = new Dictionary<string, int> { { "wall", ring.Count } };
			SearchResult result = ClassifyPartialCells(ring, required);
			if (result.Valid) {
				float dist = nearPos.DistanceTo(CentroidOf(ring));
				if (dist < bestDist && dist <= maxDist) {
					bestDist = dist;
					result.Cells = new List<int>(ring);
					best = result;
				}
			}
		}
		return best;
	}

	private void TryLinePlacementAt(Vector2 pos, string resultType) {
		int cell = _grid.WorldToCell(pos);
		SearchResult result = FindPartialLine(cell, RecipeRequirements(resultType), pos, _crusherRotation);
		if (!result.Valid) {
			_ui.ShowStatus("Needs Cannon-Spikes-Cannon (built or empty) in a connected chain.");
			return;
		}
		ExecutePartialBuild(result.Cells, result.Missing, resultType, _grid.CellToWorld(result.Cells[1]));
	}

	// Samples the straight line between two world points at a fraction of a tile
	// width and checks every sampled tile against the grid's blocked set - except
	// the source and destination tiles themselves, since the tower's own tile (which
	// it blocks, like any tower) and the target's tile are never themselves the
	// obstruction. Skipping by index alone isn't enough: with a fine-grained
	// sampling step, several consecutive samples near either end can still land
	// inside that same origin/destination tile, not just the exact first/last one.
	// `ignoreCells` additionally excludes tiles that are part of the looking tower's
	// own multi-tile structure (see GetOwnStructureCells) - relevant for a tower
	// merged onto an outpost's platform, whose supporting walls occupy tiles well
	// beyond the tower's own single anchor tile. Water is never an obstruction
	// either - it blocks walking, not seeing, so a sniper can shoot clean over it.
	// Good enough for this board - it doesn't need to be a precise geometric
	// tile/line intersection to reliably catch anything actually in the way.
	public bool HasLineOfSight(Vector2 from, Vector2 to, HashSet<int> ignoreCells = null) {
		float dist = from.DistanceTo(to);
		if (dist <= 0.01f)
			return true;
		int fromCell = _grid.WorldToCell(from);
		int toCell = _grid.WorldToCell(to);
		int steps = Mathf.Max(1, (int)(dist / (GameData.PenroseEdgeLength * 0.4f)));
		for (int i = 1; i < steps; i++) {
			Vector2 sample = from.Lerp(to, (float)i / steps);
			int cell = _grid.WorldToCell(sample);
			if (cell == fromCell || cell == toCell || _waterCells.Contains(cell) || (ignoreCells != null && ignoreCells.Contains(cell)))
				continue;
			if (_grid.IsBlocked(cell))
				return false;
		}
		return true;
	}

	private HashSet<int> CellsOwnedBy(BaseTower tower) {
		var cells = new HashSet<int>();
		foreach (KeyValuePair<int, BaseTower> entry in _structureAtCell) {
			if (entry.Value == tower)
				cells.Add(entry.Key);
		}
		return cells;
	}

	// Cells that shouldn't block a tower's own line of sight: its own footprint,
	// plus - if it's the result of merging onto an outpost's platform(s) - that
	// outpost's whole wall structure too. TryPlatformMergeAt never repoints those
	// wall cells at the new tower (the walls still belong to the outpost, which
	// keeps standing), so without this a tower built onto a platform would have its
	// own supporting structure block its view in most directions.
	public HashSet<int> GetOwnStructureCells(BaseTower tower) {
		HashSet<int> cells = CellsOwnedBy(tower);
		foreach (Platform platform in _outpostPlatforms) {
			if (platform.Tower == tower && platform.Parent != null)
				cells.UnionWith(CellsOwnedBy(platform.Parent));
		}
		return cells;
	}

	private void RecomputePath() {
		List<int> cells = _grid.FindPath();
		var pts = new List<Vector2> { _grid.CellToWorld(_grid.Entry) + new Vector2(-GameData.PenroseEdgeLength, 0.0f) };
		foreach (int cell in cells)
			pts.Add(_grid.CellToWorld(cell));
		pts.Add(_grid.CellToWorld(_grid.Exit) + new Vector2(GameData.PenroseEdgeLength, 0.0f));
		_currentPath = pts;
		_pathLine.Points = pts.ToArray();
		QueueRedraw();
		RecomputeSwimPath();
	}

	// A second path, computed for swimming enemies only, that treats water cells as
	// walkable - always at least as short as the normal path since it's a strict
	// superset of what the normal path is allowed to cross.
	private void RecomputeSwimPath() {
		List<int> cells = _grid.FindPath(_waterCells);
		var pts = new List<Vector2> { _grid.CellToWorld(_grid.Entry) + new Vector2(-GameData.PenroseEdgeLength, 0.0f) };
		foreach (int cell in cells)
			pts.Add(_grid.CellToWorld(cell));
		pts.Add(_grid.CellToWorld(_grid.Exit) + new Vector2(GameData.PenroseEdgeLength, 0.0f));
		_currentSwimPath = pts;
	}

	// Temporarily blocks the given cells, computes what the path would become, then
	// restores the grid exactly as it was - never mutates real placement state.
	// Returns null if nothing in blockingCells actually changes the grid's blocked
	// set, or if the hypothetical blockage leaves no path at all.
	private List<Vector2> ComputeHoverPreviewPath(List<int> blockingCells) {
		var newlyBlocked = new List<int>();
		foreach (int c in blockingCells) {
			if (!_grid.IsBlocked(c)) {
				_grid.Place(c);
				newlyBlocked.Add(c);
			}
		}
		if (newlyBlocked.Count == 0)
			return null;
		List<int> cells = _grid.FindPath();
		foreach (int c in newlyBlocked)
			_grid.Unblock(c);
		if (cells.Count == 0)
			return null;
		var pts = new List<Vector2> { _grid.CellToWorld(_grid.Entry) + new Vector2(-GameData.PenroseEdgeLength, 0.0f) };
		foreach (int c in cells)
			pts.Add(_grid.CellToWorld(c));
		pts.Add(_grid.CellToWorld(_grid.Exit) + new Vector2(GameData.PenroseEdgeLength, 0.0f));
		return pts;
	}

	private void RerouteAllEnemies() {
		foreach (Node node in _enemyContainer.GetChildren()) {
			if (node is not BaseEnemy enemy)
				continue;
			int cell = _grid.WorldToCell(enemy.GlobalPosition);
			List<int> cells = _grid.FindPathFrom(cell, enemy.CanSwim ? _waterCells : null);
			if (cells.Count == 0)
				continue;
			var pts = new Godot.Collections.Array<Vector2> { enemy.GlobalPosition };
			for (int i = 1; i < cells.Count; i++)
				pts.Add(_grid.CellToWorld(cells[i]));
			pts.Add(_grid.CellToWorld(_grid.Exit) + new Vector2(GameData.PenroseEdgeLength, 0.0f));
			enemy.reroute(pts);
		}
	}

	private void StartNextWave() {
		if (_spawning || !_gameActive || _rewardStep != "")
			return;
		if (_currentWaveIndex >= GameData.Waves.Count - 1)
			return;
		_currentWaveIndex++;
		GameManager.Instance.AdvanceWave();
		ResetRoundDamage();
		_spawnQueue.Clear();
		foreach (WaveGroup group in GameData.Waves[_currentWaveIndex]) {
			for (int i = 0; i < group.Count; i++)
				_spawnQueue.Enqueue(new SpawnEntry { Type = group.Type, Interval = group.Interval });
		}
		int waveNumber = _currentWaveIndex + 1;
		if (waveNumber % GameData.BossWaveInterval == 0) {
			int bossIndex = waveNumber / GameData.BossWaveInterval - 1;
			_spawnQueue.Enqueue(new SpawnEntry { Type = GameData.BossTypeFor(bossIndex), Interval = 1.5f, BossIndex = bossIndex });
			_pendingWaveReward = true;
		}
		_spawning = true;
		_spawnTimer = 0.0f;
		_ui.SetWaveButtonEnabled(false);
	}

	private void ResetRoundDamage() {
		foreach (Node node in _towerContainer.GetChildren()) {
			if (node is BaseTower tower)
				tower.reset_round_damage();
		}
	}

	private void SpawnNextEnemy() {
		SpawnEntry entry = _spawnQueue.Dequeue();
		PackedScene enemyScene = GameData.EnemyScenes[entry.Type];
		BaseEnemy enemy = (BaseEnemy)enemyScene.Instantiate();
		_enemyContainer.AddChild(enemy);
		if (entry.BossIndex.HasValue && enemy is BossEnemy bossEnemy)
			bossEnemy.boss_index = entry.BossIndex.Value;
		var waypoints = new Godot.Collections.Array<Vector2>(enemy.CanSwim ? _currentSwimPath : _currentPath);
		enemy.setup(waypoints);
		float typeHealthMult = GameManager.Instance.GetEnemyTypeHealthMult(entry.Type);
		if (typeHealthMult != 1.0f)
			enemy.apply_status("enemy_type_health_curse", typeHealthMult, float.PositiveInfinity);
		float typeSpeedMult = GameManager.Instance.GetEnemyTypeSpeedMult(entry.Type);
		if (typeSpeedMult != 1.0f)
			enemy.apply_status("enemy_type_speed_curse", typeSpeedMult, float.PositiveInfinity);
		if (GameManager.Instance.CursePhysicalResistanceBonus != 0.0f)
			enemy.apply_status("physical_resistance_curse", GameManager.Instance.CursePhysicalResistanceBonus, float.PositiveInfinity);
		if (GameManager.Instance.CurseElementalResistanceBonus != 0.0f)
			enemy.apply_status("elemental_resistance_curse", GameManager.Instance.CurseElementalResistanceBonus, float.PositiveInfinity);
		enemy.died += OnEnemyDied;
		enemy.reached_end += OnEnemyReachedEnd;
		if (entry.BossIndex.HasValue)
			FocusEnemy(enemy);
		_enemiesAlive++;
		if (_spawnQueue.Count == 0) {
			_spawning = false;
			if (_currentWaveIndex < GameData.Waves.Count - 1)
				_ui.SetWaveButtonEnabled(true);
		}
		else {
			_spawnTimer = entry.Interval;
		}
	}

	private void OnEnemyDied(Area2D enemy) {
		GameManager.Instance.AddGold(((BaseEnemy)enemy).reward);
		_enemiesAlive--;
		enemy.QueueFree();
		CheckVictory();
		CheckWaveReward();
	}

	private void OnEnemyReachedEnd(Area2D enemy) {
		GameManager.Instance.LoseLife(((BaseEnemy)enemy).damage_to_lives);
		_enemiesAlive--;
		enemy.QueueFree();
		CheckVictory();
		CheckWaveReward();
	}

	private void CheckVictory() {
		if (_gameActive && !_spawning && _enemiesAlive <= 0 && _currentWaveIndex >= GameData.Waves.Count - 1 && _currentWaveIndex >= 0) {
			_gameActive = false;
			GameManager.Instance.Victory();
		}
	}

	private void CheckWaveReward() {
		if (_pendingWaveReward && _gameActive && !_spawning && _enemiesAlive <= 0) {
			_pendingWaveReward = false;
			StartCurseChoice();
		}
	}

	private void StartCurseChoice() {
		_ui.SetWaveButtonEnabled(false);
		var pool = new List<Curse>(GameData.Curses);
		Shuffle(pool);
		List<Curse> picks = pool.GetRange(0, 3);
		var options = new List<ChoiceOption>();
		foreach (Curse curse in picks)
			options.Add(new ChoiceOption(curse.Key, curse.Label, curse.Description));
		_rewardStep = "curse";
		_ui.ShowChoice("Boss defeated — choose a curse", "Pick the one you can live with. It applies immediately and lasts the rest of the run.", options);
	}

	private void ApplyCurse(string curseType) {
		GameManager.Instance.ApplyCurse(curseType);
		Curse curse = GameData.GetCurse(curseType);
		_ui.ShowStatus($"Curse chosen: {curse?.Label ?? curseType}");
	}

	private void StartBuffTileChoice() {
		var pool = new List<string>();
		foreach (TileBuff buff in GameData.TileBuffs)
			pool.Add(buff.Key);
		Shuffle(pool);
		var options = new List<ChoiceOption>();
		int pct = (int)((GameData.TileBuffMult - 1.0f) * 100.0f);
		foreach (string buffType in pool) {
			string label = GameData.GetTileBuff(buffType)?.Label ?? buffType;
			string description = buffType == "multishot"
				? "Place a new buff tile granting +1 target per shot to whatever's built on it."
				: $"Place a new buff tile granting +{pct}% {label} to whatever's built on it.";
			options.Add(new ChoiceOption(buffType, label, description));
		}
		_rewardStep = "buff_tile";
		_ui.ShowChoice("Choose a buff tile", "Pick a type, then click an empty tile on the field to place it.", options);
	}

	private void OnUiChoiceMade(string optionId) {
		if (_rewardStep == "curse") {
			ApplyCurse(optionId);
			StartBuffTileChoice();
		}
		else if (_rewardStep == "buff_tile") {
			_rewardStep = "";
			_ui.ClearSelection();
			_placingBuffTileType = optionId;
			_ui.SetWaveButtonEnabled(_currentWaveIndex < GameData.Waves.Count - 1);
			_ui.ShowStatus($"Click an empty tile to place your {GameData.GetTileBuff(optionId)?.Label ?? optionId} buff tile.");
		}
	}

	private void TryPlaceBuffTileAt(Vector2 pos) {
		int cell = _grid.WorldToCell(pos);
		if (!_grid.CanPlace(cell) || _buffTiles.ContainsKey(cell) || _structureAtCell.ContainsKey(cell)) {
			_ui.ShowStatus("Pick an empty, buildable tile.");
			return;
		}
		_buffTiles[cell] = _placingBuffTileType;
		_placingBuffTileType = "";
		_hoverCell = -1;
		_hoverOverlay.QueueRedraw();
		QueueRedraw();
		_ui.ShowStatus("Buff tile placed!");
	}

	private void OnGameOver(bool won) {
		_gameActive = false;
		GetTree().Paused = true;
	}

	private void OnRetry() {
		GetTree().Paused = false;
		GetTree().ReloadCurrentScene();
	}

	private void OnSpeedSelected(float multiplier) {
		Engine.TimeScale = multiplier;
	}

	private static void Shuffle<T>(List<T> list) {
		for (int i = list.Count - 1; i > 0; i--) {
			int j = GD.RandRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	public override void _Draw() {
		DrawRect(_cameraPanBounds, new Color(0.08f, 0.1f, 0.14f, 1.0f));
		foreach (PenroseTile tile in _grid.Tiles)
			DrawTileOutline(tile.Id, new Color(1, 1, 1, 0.08f));
		foreach (PenroseTile tile in _grid.Tiles)
			DrawMatchingRuleArcs(tile);
		if (_penroseViewOnly)
			return; // just the tiling and its arcs - no entry/exit markers, buffs, or anything gameplay-related
		DrawTile(_grid.Entry, new Color(0.3f, 0.9f, 0.4f, 0.5f));
		DrawTile(_grid.Exit, new Color(0.9f, 0.3f, 0.3f, 0.5f));
		foreach (KeyValuePair<int, string> entry in _buffTiles) {
			if (!_structureAtCell.ContainsKey(entry.Key))
				DrawBuffTile(entry.Key, entry.Value);
		}
	}

	// "Penrose View" (see UI.OnPenroseViewToggled) - strips away everything
	// but the tiling itself: the towers, enemies, projectiles, path line and
	// hover previews all live on their own nodes, so hiding them is just
	// toggling Visible; the entry/exit tint and buff-tile overlays are drawn
	// inline in _Draw() above, so that's gated on _penroseViewOnly instead.
	private void OnPenroseViewToggled(bool enabled) {
		_penroseViewOnly = enabled;
		_enemyContainer.Visible = !enabled;
		_towerContainer.Visible = !enabled;
		_projectileContainer.Visible = !enabled;
		_pathLine.Visible = !enabled;
		_hoverOverlay.Visible = !enabled;
		QueueRedraw();
	}

	private void DrawBuffTile(int cell, string buffType) {
		TileBuff buff = GameData.GetTileBuff(buffType);
		Color color = buff?.Color ?? Colors.White;
		Vector2 center = _grid.CellToWorld(cell);
		DrawTile(cell, new Color(color.R, color.G, color.B, 0.22f));
		buff?.Draw(this, center, GameData.PenroseEdgeLength);
	}

	private void DrawHoverOverlay() {
		foreach (Platform platform in _outpostPlatforms) {
			if (!platform.Occupied)
				DrawPlatformMarker(platform.WorldPos, new Color(1.0f, 0.85f, 0.3f, 0.35f));
		}
		if (_hoverPlatformPreviewActive) {
			foreach (Vector2 p in _hoverPlatformPreviewPositions)
				DrawPlatformMarker(p, new Color(1.0f, 0.85f, 0.3f, 0.5f));
		}
		if (_hoverPlatformMergePositions.Count > 0) {
			Color mc = _hoverValid ? new Color(0.3f, 0.9f, 0.4f, 0.55f) : new Color(0.9f, 0.2f, 0.2f, 0.55f);
			foreach (Vector2 p in _hoverPlatformMergePositions)
				DrawPlatformMarker(p, mc);
		}
		if (_hoverPlatformIndex != -1) {
			Color pc = _hoverValid ? new Color(0.3f, 0.9f, 0.4f, 0.55f) : new Color(0.9f, 0.2f, 0.2f, 0.55f);
			DrawPlatformMarker(_outpostPlatforms[_hoverPlatformIndex].WorldPos, pc);
		}
		else if ((_selectedTowerType != "" || _placingBuffTileType != "") && _grid.IsInBounds(_hoverCell)) {
			Color c = _hoverValid ? new Color(0.3f, 0.9f, 0.4f, 0.35f) : new Color(0.9f, 0.2f, 0.2f, 0.35f);
			_hoverOverlay.DrawColoredPolygon(TilePoints(_hoverCell), c);
			if (_hoverValid) {
				foreach (int extra in _hoverExtraCells) {
					if (_grid.IsInBounds(extra))
						_hoverOverlay.DrawColoredPolygon(TilePoints(extra), c);
				}
			}
		}
		if (_hoverRangePreviewActive && _hoverRangeValue > 0.0f)
			DrawRangePreview(_hoverRangePreviewPos, _hoverRangeValue);
		else if (_hoverRangePreviewActive && IsGlobalRangeType(_selectedTowerType))
			DrawLineOfSightPreview(_hoverRangePreviewPos, _hoverLosIgnoreCells);
		if (_inspectedRangeActive) {
			if (IsGlobalRangeType(_inspectedTowerType))
				DrawLineOfSightPreview(_inspectedRangePos, _inspectedLosIgnoreCells);
			else
				DrawRangePreview(_inspectedRangePos, _inspectedRangeValue);
		}
		if (_hoverValid && _hoverPreviewPath != null)
			DrawDashedPath(_hoverPreviewPath, new Color(0.3f, 0.9f, 1.0f, 0.9f), 4.0f, 14.0f, 10.0f);
		if (_focusedEnemy != null && IsInstanceValid(_focusedEnemy))
			DrawFocusRing(_focusedEnemy.GlobalPosition, _focusedEnemy.radius);
	}

	private void DrawFocusRing(Vector2 center, float radius) {
		float ringRadius = radius + 6.0f;
		_hoverOverlay.DrawArc(center, ringRadius, 0.0f, Mathf.Tau, 32, new Color(1.0f, 0.9f, 0.2f, 0.9f), 2.5f);
		_hoverOverlay.DrawArc(center, ringRadius + 4.0f, 0.0f, Mathf.Tau, 32, new Color(1.0f, 0.9f, 0.2f, 0.35f), 1.5f);
	}

	private void DrawDashedPath(List<Vector2> points, Color color, float width, float dashLength, float gapLength) {
		for (int i = 0; i < points.Count - 1; i++) {
			Vector2 a = points[i];
			Vector2 b = points[i + 1];
			float segmentLength = a.DistanceTo(b);
			if (segmentLength <= 0.0001f)
				continue;
			Vector2 dir = (b - a) / segmentLength;
			float traveled = 0.0f;
			bool drawing = true;
			while (traveled < segmentLength) {
				float step = Mathf.Min(drawing ? dashLength : gapLength, segmentLength - traveled);
				if (drawing)
					_hoverOverlay.DrawLine(a + dir * traveled, a + dir * (traveled + step), color, width);
				traveled += step;
				drawing = !drawing;
			}
		}
	}

	private void DrawPlatformMarker(Vector2 center, Color color) {
		_hoverOverlay.DrawCircle(center, GameData.PenroseEdgeLength * 0.4f, color);
	}

	private void DrawRangePreview(Vector2 center, float radius) {
		_hoverOverlay.DrawCircle(center, radius, new Color(1, 1, 1, 0.06f));
		_hoverOverlay.DrawArc(center, radius, 0.0f, Mathf.Tau, 64, new Color(1, 1, 1, 0.55f), 2.0f);
	}

	// For a global-range tower (see SniperTower), there's no radius to draw - instead,
	// wash every currently-visible tile so the player can see exactly which parts of
	// the board it could actually hit from here, given the obstacles on the field
	// right now.
	private void DrawLineOfSightPreview(Vector2 fromPos, HashSet<int> ignoreCells = null) {
		Color visible = new Color(1.0f, 0.85f, 0.3f, 0.16f);
		foreach (PenroseTile tile in _grid.Tiles) {
			// Water still shows as a potential target tile - it blocks walking, not
			// seeing, and a swimming enemy can be standing right on it.
			if (_grid.IsBlocked(tile.Id) && !_waterCells.Contains(tile.Id))
				continue;
			if (HasLineOfSight(fromPos, tile.Center, ignoreCells))
				_hoverOverlay.DrawColoredPolygon(tile.Corners, visible);
		}
	}

	private Vector2[] TilePoints(int tileId) => _grid.Tiles[tileId].Corners;

	private void DrawTileOutline(int tileId, Color color) {
		Vector2[] pts = TilePoints(tileId);
		for (int i = 0; i < pts.Length; i++)
			DrawLine(pts[i], pts[(i + 1) % pts.Length], color, 1.0f);
	}

	private void DrawTile(int tileId, Color color) {
		DrawColoredPolygon(TilePoints(tileId), color);
	}

	// The colored-arc "matching rule" decoration from the classic Penrose P3
	// reference diagram (Wikipedia's "Penrose-tiles-bump-and-color-coded.svg")
	// - see PenroseTiling.ValidateMatchingRule/AssignCurveColors for the full
	// derivation. Each rhombus rounds off two opposite corners with an arc
	// centered ON that corner: a thin tile's two 144-degree corners (both the
	// same small radius), a thick tile's two 72-degree corners (one small,
	// one large - which one depends on PenroseVertex.ColorA, cached per-
	// corner as CornerColorA). PAINT color is a separate property
	// (CornerCurveColorA): every arc belonging to the same connected curve -
	// possibly spanning many tiles - shares one color, which is what
	// guarantees a red arc only ever continues into another red arc, never
	// blue, exactly the rule that keeps this tiling from ever closing up
	// into a repeating, periodic pattern.
	private const float ArcScale = GameData.PenroseEdgeLength / 30.0f; // reference SVG's tile edge was 30 units
	private const float ThinArcRadius = 6.0f * ArcScale;
	private const float ThickBigArcRadius = 24.0f * ArcScale;
	private const float ThickSmallArcRadius = 6.0f * ArcScale;
	private static readonly Color ArcRed = new Color(1.0f, 0.25f, 0.25f, 0.4f);
	private static readonly Color ArcBlue = new Color(0.35f, 0.45f, 1.0f, 0.4f);

	private void DrawMatchingRuleArcs(PenroseTile tile) {
		if (tile.IsThin) {
			DrawCornerArc(tile, 1, ThinArcRadius, tile.CornerCurveColorA[1] ? ArcRed : ArcBlue);
			DrawCornerArc(tile, 3, ThinArcRadius, tile.CornerCurveColorA[3] ? ArcRed : ArcBlue);
		} else {
			DrawCornerArc(tile, 0, tile.CornerColorA[0] ? ThickBigArcRadius : ThickSmallArcRadius, tile.CornerCurveColorA[0] ? ArcRed : ArcBlue);
			DrawCornerArc(tile, 2, tile.CornerColorA[2] ? ThickBigArcRadius : ThickSmallArcRadius, tile.CornerCurveColorA[2] ? ArcRed : ArcBlue);
		}
	}

	private void DrawCornerArc(PenroseTile tile, int cornerIndex, float radius, Color color) {
		Vector2 corner = tile.Corners[cornerIndex];
		Vector2 neighborA = tile.Corners[(cornerIndex + 3) % 4];
		Vector2 neighborB = tile.Corners[(cornerIndex + 1) % 4];
		float angleA = (neighborA - corner).Angle();
		float angleB = (neighborB - corner).Angle();
		float sweep = Mathf.Wrap(angleB - angleA, -Mathf.Pi, Mathf.Pi);
		DrawArc(corner, radius, angleA, angleA + sweep, 12, color, 2.0f, true);
	}
}
