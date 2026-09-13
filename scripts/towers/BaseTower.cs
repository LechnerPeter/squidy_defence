using Godot;
using Godot.Collections;
using System.Collections.Generic;

// A tower's real footprint on the board: one entry per occupied tile, each with its
// own actual corners (Penrose tiles aren't uniform the way hex cells were, so a
// multi-cell tower's shape has to be built from real per-tile geometry rather than
// a single formula). Offsets are local to the tower's own world position.
public readonly struct TileShape {
	public Vector2 CenterOffset { get; init; }
	public Vector2[] CornerOffsets { get; init; }

	// Uniformly scales the shape from its own center - used for the outpost platform
	// plinth, which is drawn slightly larger than the tile it sits on.
	public TileShape Scaled(float factor) {
		var corners = new Vector2[CornerOffsets.Length];
		for (int i = 0; i < corners.Length; i++)
			corners[i] = CenterOffset + (CornerOffsets[i] - CenterOffset) * factor;
		return new TileShape { CenterOffset = CenterOffset, CornerOffsets = corners };
	}

	// A generic diamond shape for a "virtual" cell that isn't a real generated
	// Penrose tile - namely an Outpost/Big Outpost platform, which sits at the
	// centroid of its supporting walls rather than on any specific tile of its own.
	// Used only for a tower with no fixed shape of its own (Wall/Flipper, or a T2
	// tower spanning several platforms) - see Rhombus below for one that does.
	public static TileShape Generic(Vector2 centerOffset, float radius) {
		var corners = new Vector2[4];
		for (int i = 0; i < 4; i++)
			corners[i] = centerOffset + new Vector2(radius, 0.0f).Rotated(Mathf.DegToRad(90.0f * i - 90.0f));
		return new TileShape { CenterOffset = centerOffset, CornerOffsets = corners };
	}

	// A proper thin (36/144 degree) or thick (72/108 degree) Penrose rhombus at an
	// arbitrary "virtual" position, for a base tower dropped directly onto an outpost
	// platform (see Main.TryPlaceOnPlatform) - now that every base tower has one
	// fixed required shape (see Main.TileShapeAllowed), it should be drawn as that
	// shape even there, not the shapeless generic diamond.
	//
	// Built directly as a kite (acute corners on the vertical axis, obtuse corners on
	// the horizontal one) rather than by rotating two edge vectors by +-half the
	// interior angle: that rotation approach only keeps the acute corners on the
	// longer axis while the acute angle is under 90 degrees (true for thick's 72, false
	// for thin's 144-at-the-shared-vertex framing) - past that it silently flips which
	// axis is long. Deriving straight from the acute angle instead (p/q below are the
	// half-diagonals of a rhombus with edge `edgeLength` and acute angle `acute`) keeps
	// the acute corners vertical - and therefore the long axis vertical - for both
	// shapes alike. Corner 0 (Top) is always the acute one, matching the badge art's
	// own convention (see icons/towers/src/*.svg's background polygon, tips at
	// (32,3)/(32,61)) - that's the reference direction DrawTowerIcon rotates the badge
	// texture from, so a shape built here needs to agree with the art on which way "up" is.
	public static TileShape Rhombus(Vector2 centerOffset, float edgeLength, bool isThin) {
		float acute = Mathf.DegToRad(isThin ? 36.0f : 72.0f);
		float p = edgeLength * Mathf.Cos(acute / 2.0f);
		float q = edgeLength * Mathf.Sin(acute / 2.0f);
		Vector2 top = centerOffset + new Vector2(0.0f, -p);
		Vector2 right = centerOffset + new Vector2(q, 0.0f);
		Vector2 bottom = centerOffset + new Vector2(0.0f, p);
		Vector2 left = centerOffset + new Vector2(-q, 0.0f);
		return new TileShape { CenterOffset = centerOffset, CornerOffsets = new[] { top, right, bottom, left } };
	}
}

public abstract partial class BaseTower : Node2D {
	// Outpost and Big Outpost are tracked as separate, independently-stacking buff
	// types (see apply_buffs) even though they currently grant identical bonuses -
	// this keeps them free to diverge later without a structural change. Both mults
	// live in GameData rather than on OutpostTower/BigOutpostTower: they're the
	// magnitude of a status effect applied to whatever tower sits on the platform
	// (via the switch below), not a stat of the outpost tower itself, and BaseTower
	// can't depend on its own subclasses to read them.
	private static readonly Color OutpostPlinthColor = new Color(0.65f, 0.55f, 0.3f);

	private static readonly PackedScene DefaultProjectileScene = GD.Load<PackedScene>("res://scenes/projectiles/Projectile.tscn");

	// Overridden by tower leaves whose shots need a different Projectile subclass
	// (see BoomerangBase/RicochetTower/ChainReactorTower) - each loads its own scene
	// once via its own static field, same cost as this single shared default.
	protected virtual PackedScene ProjectileScene => DefaultProjectileScene;

	public string tower_type = "basic";
	public int buff_stacks = 0;
	public int outpost_stacks = 0;
	public int big_outpost_stacks = 0;
	public Array buff_tile_types = new Array();
	public Array platform_offsets = new Array { Vector2.Zero };

	protected Node2D ProjectileContainer;
	protected float CellSize = 40.0f;
	protected List<TileShape> CellShapes = new List<TileShape> { TileShape.Generic(Vector2.Zero, 40.0f) };
	protected bool IsBuffed = false;

	protected Color TowerColor;
	protected bool NonBlocking;

	private Texture2D _towerIconTexture;

	private readonly System.Collections.Generic.Dictionary<string, StatusEffectState> _statuses = new();

	protected virtual int BaseTier => 1;
	protected virtual string BaseTowerName => "";
	protected virtual int BaseCost => 0;
	protected virtual int BaseCostStep => 0;
	protected virtual Color BaseColor => Colors.White;
	protected virtual bool BaseNonBlocking => false;
	protected virtual string BaseFlavor => "";
	protected virtual string BaseAbbreviation => "";

	// Subclasses that override _Process (BoomerangBase, ContinuousBase, LightningTower)
	// must call base._Process(delta) so status ticking still runs for them.
	public override void _Process(double delta) {
		UpdateStatuses((float)delta);
	}

	public void setup(Node2D projContainer, float cellSize, List<TileShape> cellShapes) {
		TowerColor = BaseColor;
		NonBlocking = BaseNonBlocking;
		InitializeStats();

		ProjectileContainer = projContainer;
		CellSize = cellSize;
		CellShapes = cellShapes;

		ConfigureCombat();
		QueueRedraw();
	}

	// Per-tier stat initialization hook - each tier that adds fields overrides this,
	// calls base.InitializeStats() first, then copies its own Base* props into fields.
	protected virtual void InitializeStats() {
		string iconPath = $"res://icons/towers/{tower_type}.png";
		_towerIconTexture = ResourceLoader.Exists(iconPath) ? GD.Load<Texture2D>(iconPath) : null;
	}

	// Placement-specific buffs (which platform(s) a tower sits on, which tiles it
	// occupies) aren't known until Main.gd resolves them, so they're applied here,
	// right after setup(), rather than baked into setup() itself.
	public void apply_buffs(int outpostStacks, int bigOutpostStacks, Array tileBuffs) {
		tileBuffs ??= new Array();

		outpost_stacks = outpostStacks;
		big_outpost_stacks = bigOutpostStacks;
		buff_stacks = outpostStacks + bigOutpostStacks;
		IsBuffed = buff_stacks > 0;
		buff_tile_types = new Array();
		foreach (var t in tileBuffs)
			buff_tile_types.Add(t);

		if (outpostStacks > 0)
			apply_status("outpost_buff", outpostStacks, float.PositiveInfinity);
		if (bigOutpostStacks > 0)
			apply_status("big_outpost_buff", bigOutpostStacks, float.PositiveInfinity);

		foreach (var t in tileBuffs) {
			TileBuff buff = GameData.GetTileBuff((string)t);
			if (buff != null)
				apply_status(buff.StatusType, GameData.TileBuffMult, float.PositiveInfinity);
		}

		QueueRedraw();
	}

	protected virtual void ConfigureCombat() {
	}

	// No-op default - only towers that cap damage per round (see DamageCapBase) track
	// anything here. Main.cs calls this unconditionally on every placed tower each round.
	public virtual void reset_round_damage() {
	}

	// General status-effect entry point. Applying an already-active status STACKS
	// (its tracked magnitude compounds, and the new incremental factor is applied
	// on top of the tower's current stats) rather than replacing it in place -
	// unlike BaseEnemy's refresh semantics, every buff/debuff a tower can carry
	// today is a permanent, repeatable multiplier (curses re-picked later in the
	// run, tile buffs from several tiles at once), so re-application must compound.
	public void apply_status(string type, float magnitude, float duration) {
		if (duration <= 0.0f)
			return;
		if (_statuses.TryGetValue(type, out StatusEffectState existing)) {
			existing.Magnitude *= magnitude;
			existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, duration);
		}
		else {
			_statuses[type] = new StatusEffectState { Magnitude = magnitude, RemainingDuration = duration };
		}
		ApplyStatusEffect(type, magnitude);
	}

	private void UpdateStatuses(float delta) {
		if (_statuses.Count == 0)
			return;
		List<string> expired = null;
		foreach (KeyValuePair<string, StatusEffectState> entry in _statuses) {
			entry.Value.RemainingDuration -= delta;
			if (entry.Value.RemainingDuration <= 0.0f)
				(expired ??= new List<string>()).Add(entry.Key);
		}
		if (expired == null)
			return;
		foreach (string type in expired) {
			float magnitude = _statuses[type].Magnitude;
			_statuses.Remove(type);
			RevertStatusEffect(type, magnitude);
		}
	}

	// Per-status "on applied/refreshed" hook, given the INCREMENTAL factor being
	// applied right now (not the cumulative total tracked in _statuses). Add a
	// case here (and in RevertStatusEffect) for each new tower status type. The
	// actual stat mutation is delegated to ApplyToX virtuals so this dispatch stays
	// valid for every tower regardless of which combat fields it actually has.
	private void ApplyStatusEffect(string type, float magnitude) {
		switch (type) {
			case "tile_damage":
				ApplyToDamage(magnitude);
				break;
			case "tile_range":
				ApplyToRange(magnitude);
				break;
			case "outpost_buff":
				ApplyToDamage(Mathf.Pow(GameData.OutpostBuffDamageMult, magnitude));
				ApplyToRange(Mathf.Pow(GameData.OutpostBuffRangeMult, magnitude));
				break;
			case "big_outpost_buff":
				ApplyToDamage(Mathf.Pow(GameData.BigOutpostBuffDamageMult, magnitude));
				ApplyToRange(Mathf.Pow(GameData.BigOutpostBuffRangeMult, magnitude));
				break;
			case "tile_atk_speed":
				ApplyToAttackSpeed(magnitude);
				break;
			case "tile_multishot":
				ApplyToMultishot(1);
				break;
		}
	}

	// Per-status "on expired" hook, given the TOTAL cumulative factor that was
	// active, undoing it via its reciprocal. Nothing a tower carries today ever
	// actually expires (everything is applied with INF duration), but this keeps
	// the system genuinely general for a future temporary tower debuff.
	private void RevertStatusEffect(string type, float magnitude) {
		float inverse = magnitude != 0.0f ? 1.0f / magnitude : 1.0f;
		switch (type) {
			case "tile_damage":
				ApplyToDamage(inverse);
				break;
			case "tile_range":
				ApplyToRange(inverse);
				break;
			case "outpost_buff":
				ApplyToDamage(1.0f / Mathf.Pow(GameData.OutpostBuffDamageMult, magnitude));
				ApplyToRange(1.0f / Mathf.Pow(GameData.OutpostBuffRangeMult, magnitude));
				break;
			case "big_outpost_buff":
				ApplyToDamage(1.0f / Mathf.Pow(GameData.BigOutpostBuffDamageMult, magnitude));
				ApplyToRange(1.0f / Mathf.Pow(GameData.BigOutpostBuffRangeMult, magnitude));
				break;
			case "tile_atk_speed":
				ApplyToAttackSpeed(inverse);
				break;
			case "tile_multishot":
				ApplyToMultishot(-1);
				break;
		}
	}

	// Empty by default - only a tower that actually has the given stat overrides it.
	// Applying a curse/buff to a tower that doesn't have the targeted stat is a no-op,
	// same as it always was (previously guarded by e.g. "if (Range > 0.0f)").
	protected virtual void ApplyToDamage(float mult) { }
	protected virtual void ApplyToRange(float mult) { }
	protected virtual void ApplyToFireRate(float mult) { }
	protected virtual void ApplyToMultishot(int stackDelta) { }

	// "Attack speed" means different things per combat style (fire rate, projectile
	// speed, or - lacking either - a damage boost); each tier redirects this to
	// whichever ApplyToX actually matches its own notion of attack speed.
	protected virtual void ApplyToAttackSpeed(float mult) => ApplyToDamage(mult);

	protected static Array OnHitStatusesToVariant(List<StatusEffectState> statuses) {
		var arr = new Array();
		foreach (StatusEffectState status in statuses) {
			if (status.Type == null)
				continue;
			arr.Add(new Dictionary {
				{ "type", status.Type },
				{ "magnitude", status.Magnitude },
				{ "duration", status.RemainingDuration },
			});
		}
		return arr;
	}

	public Dictionary get_preview_data() {
		var data = new Dictionary {
			{ "tier", BaseTier },
			{ "name", BaseTowerName },
			{ "cost", BaseCost },
			{ "cost_step", BaseCostStep },
			{ "color", BaseColor },
			{ "non_blocking", BaseNonBlocking },
			{ "flavor", BaseFlavor },
			{ "abbreviation", BaseAbbreviation },
		};
		CollectPreviewData(data);
		return data;
	}

	// Per-tier dict-population hook for get_preview_data() - each tier that adds
	// fields overrides this, calls base.CollectPreviewData(data) first, then merges
	// in its own keys. A tower type that lacks a given concept simply never adds
	// its key, which every consumer (GameData.DescribeStats, Main.cs, Projectile.cs,
	// TowerIcon.cs) already treats identically to "present with a zero/false default".
	protected virtual void CollectPreviewData(Dictionary data) {
	}

	public Dictionary get_live_stats_dict() {
		var data = new Dictionary {
			{ "tier", BaseTier },
			{ "name", BaseTowerName },
			{ "cost", BaseCost },
			{ "cost_step", BaseCostStep },
			{ "color", TowerColor },
			{ "non_blocking", NonBlocking },
			{ "flavor", BaseFlavor },
			{ "abbreviation", BaseAbbreviation },
		};
		CollectLiveStats(data);
		return data;
	}

	protected virtual void CollectLiveStats(Dictionary data) {
	}

	protected void DrawTileAt(TileShape shape, Color fill, Color outline, float outlineWidth) {
		DrawColoredPolygon(shape.CornerOffsets, fill);
		for (int i = 0; i < shape.CornerOffsets.Length; i++)
			DrawLine(shape.CornerOffsets[i], shape.CornerOffsets[(i + 1) % shape.CornerOffsets.Length], outline, outlineWidth);
	}

	protected void DrawBuffRing() {
		if (!IsBuffed) return;
		foreach (TileShape shape in CellShapes)
			DrawArc(shape.CenterOffset, CellSize * 1.15f, 0.0f, Mathf.Tau, 24, new Color(1.0f, 0.85f, 0.3f, 0.85f), 3.0f);
	}

	protected void DrawPlatformBase() {
		foreach (TileShape shape in CellShapes)
			DrawTileAt(shape.Scaled(1.12f), OutpostPlinthColor, new Color(0, 0, 0, 0.45f), 2.0f);
	}

	protected void DrawTileBuffIcon() {
		if (buff_tile_types.Count == 0) return;
		var uniqueTypes = new List<string>();
		foreach (var t in buff_tile_types) {
			string ts = (string)t;
			if (!uniqueTypes.Contains(ts))
				uniqueTypes.Add(ts);
		}
		foreach (TileShape shape in CellShapes) {
			for (int i = 0; i < uniqueTypes.Count; i++) {
				Color buffColor = GameData.GetTileBuff(uniqueTypes[i])?.Color ?? Colors.White;
				DrawArc(shape.CenterOffset, CellSize * (0.5f + 0.14f * i), 0.0f, Mathf.Tau, 20, buffColor, 2.5f);
			}
		}
	}

	protected virtual void DrawEmblem(TileShape shape) {
		DrawTowerIcon(shape);
	}

	// The same badge art shown in the shop, drawn on a placed tower's body (or one
	// cell of a multi-cell tower) so it fills most of the tile. No-op for a type with
	// no icon (Rock/Water draw their own body art directly and never call this).
	//
	// Rotated to match this specific tile's real orientation rather than drawn
	// axis-aligned: a Penrose tiling is aperiodic, so two tiles of the same shape
	// essentially never share an orientation, but the badge art (see gen_icons.py) is
	// always authored long-axis-vertical (its own corner 0 points straight up in the
	// unrotated texture - see TileShape.Rhombus/Generic, which both put their own
	// corner 0 there too). Rotating the texture so corner 0 of THIS shape points the
	// same real-world direction its corner 0 points in the art brings the two into
	// agreement, instead of the badge reading as rotated against the tile under it.
	//
	// The badge art is a diamond inscribed in the icon's 64x64 canvas with
	// circumradius 29, using the same corner formula as TileShape.Generic - scaling
	// the draw by exactly 32/29 would land that inscribed diamond's vertex path
	// exactly on the tower's tile edges, but the badge's own stroke extends a little
	// past its vertex path, so an exact match still pokes out past the tile at the
	// points. Scaling by 1.0 instead leaves a deliberate small margin.
	protected void DrawTowerIcon(TileShape shape) {
		if (_towerIconTexture == null)
			return;
		float radius = CellSize;
		Vector2 size = new Vector2(radius, radius) * 2.0f;
		Vector2 cornerDir = (shape.CornerOffsets[0] - shape.CenterOffset).Normalized();
		float rotation = Mathf.Atan2(cornerDir.Y, cornerDir.X) + Mathf.Pi / 2.0f;
		DrawSetTransform(shape.CenterOffset, rotation, Vector2.One);
		DrawTextureRect(_towerIconTexture, new Rect2(-size / 2.0f, size), false);
		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	public override void _Draw() {
		Color c = TowerColor;
		if (IsBuffed)
			DrawPlatformBase();
		if (CellShapes.Count > 1) {
			var pts = new Vector2[CellShapes.Count];
			for (int i = 0; i < CellShapes.Count; i++)
				pts[i] = CellShapes[i].CenterOffset;
			DrawColoredPolygon(pts, c);
		}
		foreach (TileShape shape in CellShapes)
			DrawTileAt(shape, c, new Color(0, 0, 0, 0.5f), 2.0f);
		// A single cell has one real orientation to match; a fused multi-cell body
		// (a merged T2 tower spanning 3 cells) has no single correct one to pick, so
		// its one central badge stays upright rather than favoring any one cell.
		DrawEmblem(CellShapes.Count == 1 ? CellShapes[0] : TileShape.Generic(Vector2.Zero, CellSize));
		DrawBuffRing();
		DrawTileBuffIcon();
	}
}
