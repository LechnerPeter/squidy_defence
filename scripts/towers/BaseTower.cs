using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;

// A tower's real footprint on the board: one entry per occupied tile, each with its
// own actual corners (Penrose tiles aren't uniform the way hex cells were, so a
// multi-cell tower's shape has to be built from real per-tile geometry rather than
// a single formula). Offsets are local to the tower's own world position.
public readonly struct TileShape {
	// Names of the body nodes under a tower scene's "Visual" node, one per shape it
	// can stand on (see BaseTower.PlaceVisual).
	public const string ThickBody = "Thick";
	public const string ThinBody = "Thin";
	public const string DiamondBody = "Diamond";

	// Not authored by any scene: a hat-board tile's body is built at placement from
	// whichever body its scene does author (see BaseTower.PlaceVisual), since no
	// rotation of a rhombus would fit a hat.
	public const string HatBody = "Hat";

	public Vector2 CenterOffset { get; init; }
	public Vector2[] CornerOffsets { get; init; }
	public string BodyName { get; init; }

	// A generic diamond shape for a "virtual" cell that isn't a real generated
	// Penrose tile - namely an Outpost/Big Outpost platform, which sits at the
	// centroid of its supporting walls rather than on any specific tile of its own.
	// Used only for a tower with no fixed shape of its own (Wall/Flipper, or a T2
	// tower spanning several platforms) - see Rhombus below for one that does.
	public static TileShape Generic(Vector2 centerOffset, float radius) {
		var corners = new Vector2[4];
		for (int i = 0; i < 4; i++)
			corners[i] = centerOffset + new Vector2(radius, 0.0f).Rotated(Mathf.DegToRad(90.0f * i - 90.0f));
		return new TileShape { CenterOffset = centerOffset, CornerOffsets = corners, BodyName = DiamondBody };
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
	// (32,3)/(32,61)) - that's the reference direction PlaceVisual rotates a tower's
	// body from, so a shape built here needs to agree with the scenes on which way "up" is.
	public static TileShape Rhombus(Vector2 centerOffset, float edgeLength, bool isThin) {
		float acute = Mathf.DegToRad(isThin ? 36.0f : 72.0f);
		float p = edgeLength * Mathf.Cos(acute / 2.0f);
		float q = edgeLength * Mathf.Sin(acute / 2.0f);
		Vector2 top = centerOffset + new Vector2(0.0f, -p);
		Vector2 right = centerOffset + new Vector2(q, 0.0f);
		Vector2 bottom = centerOffset + new Vector2(0.0f, p);
		Vector2 left = centerOffset + new Vector2(-q, 0.0f);
		return new TileShape { CenterOffset = centerOffset, CornerOffsets = new[] { top, right, bottom, left }, BodyName = isThin ? ThinBody : ThickBody };
	}

	// Rhombus's counterpart on a hat board: a hat at an arbitrary "virtual" position,
	// for a tower dropped directly onto an outpost platform there - drawn the same
	// shape as every tile around it rather than as a rhombus.
	public static TileShape Hat(Vector2 centerOffset, float hatUnit) {
		Vector2[] corners = HatTiling.PlatformHatCorners(hatUnit);
		for (int i = 0; i < corners.Length; i++)
			corners[i] += centerOffset;
		return new TileShape { CenterOffset = centerOffset, CornerOffsets = corners, BodyName = HatBody };
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
	private const float PlinthScale = 1.12f;

	// How close, in world units, a scene's tile corner must land on a real one - far
	// tighter than any difference between two tile arrangements, and far looser than
	// the rounding in the scene files' coordinates.
	private const float ArrangementFitTolerance = 0.5f;

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

	protected bool NonBlocking;

	// The color authored on this tower's body in its scene (see PlaceVisuals).
	protected Color BodyColor { get; private set; }

	private readonly List<Polygon2D> _bodies = new List<Polygon2D>();
	private Color _currentBodyColor;

	private readonly System.Collections.Generic.Dictionary<string, StatusEffectState> _statuses = new();

	protected virtual int BaseTier => 1;
	protected virtual string BaseTowerName => "";
	protected virtual int BaseCost => 0;
	protected virtual int BaseCostStep => 0;
	protected virtual bool BaseNonBlocking => false;
	protected virtual string BaseFlavor => "";
	protected virtual string BaseAbbreviation => "";

	// Whether standing on an outpost platform raises this tower on a visible plinth.
	protected virtual bool HasPlinth => true;

	// Subclasses that override _Process (BoomerangBase, ContinuousBase, LightningTower)
	// must call base._Process(delta) so status ticking still runs for them.
	public override void _Process(double delta) {
		UpdateStatuses((float)delta);
	}

	public void setup(Node2D projContainer, float cellSize, List<TileShape> cellShapes) {
		NonBlocking = BaseNonBlocking;
		InitializeStats();

		ProjectileContainer = projContainer;
		CellSize = cellSize;
		CellShapes = cellShapes;

		PlaceVisuals();
		ConfigureCombat();
		QueueRedraw();
	}

	// A tower's look lives in its own scene, under a "Visual" node authored upright
	// around the origin (see the scene files - their shapes are built for an edge
	// length of GameData.PenroseEdgeLength). This is the only place it's ever moved or
	// rotated: once, at placement, to fit the tile(s) the tower landed on - and only its
	// shape turns (see RotateShape). The tower's own root stays unrotated, so range
	// checks, projectile spawns and the buff rings in _Draw aren't affected.
	private void PlaceVisuals() {
		Node2D visual = GetNode<Node2D>("Visual");
		if (CellShapes.Count > 1)
			PlaceMergedVisual(visual);
		else
			PlaceVisual(visual, CellShapes[0]);
		BodyColor = _bodies[0].Color;
		_currentBodyColor = BodyColor;
	}

	// The bodies a scene can author under Visual, in order of preference as the base
	// for a body no scene authors (see BuildBodyOver).
	private static readonly string[] AuthoredBodyNames = { TileShape.ThickBody, TileShape.ThinBody, TileShape.DiamondBody };

	// Rotated to match this specific tile's real orientation: a Penrose tiling is
	// aperiodic, so two tiles of the same shape essentially never share an orientation,
	// but a scene's visual is always authored long-axis-vertical (corner 0 pointing
	// straight up - see TileShape.Rhombus/Generic, which both put their own corner 0
	// there too). Rotating so corner 0 points the way THIS tile's corner 0 does lines
	// the body up exactly with the tile. A hat has no authored body to rotate, so one
	// is built over its real corners instead (see BuildBodyOver).
	private void PlaceVisual(Node2D visual, TileShape shape) {
		Polygon2D body = visual.GetNodeOrNull<Polygon2D>(shape.BodyName);
		Polygon2D template = body == null ? AuthoredBodyNames.Select(name => visual.GetNodeOrNull<Polygon2D>(name)).First(found => found != null) : null;
		foreach (string name in AuthoredBodyNames) {
			CanvasItem candidate = visual.GetNodeOrNull<CanvasItem>(name);
			if (candidate != null)
				candidate.Visible = name == shape.BodyName;
		}
		visual.Position = shape.CenterOffset;
		if (template == null) {
			Vector2 cornerDir = (shape.CornerOffsets[0] - shape.CenterOffset).Normalized();
			RotateShape(body, Mathf.Atan2(cornerDir.Y, cornerDir.X) + Mathf.Pi / 2.0f);
		}
		else {
			body = BuildBodyOver(template, shape);
			visual.AddChild(body);
			visual.MoveChild(body, template.GetIndex()); // keeps anything drawn over the authored body (see RockTower's highlight) drawn over this one too
		}
		_bodies.Add(body);
		OnVisualPlaced(body);
	}

	// A copy of an authored body reshaped over a tile shape no scene authors (a hat),
	// unrotated, with its glyph and any decoration kept at the shape's center and
	// scaled by how much room there is around it compared to the authored body - the
	// scene files size each glyph to its own body. A tile's center is the roomiest
	// point inside it (see HatTiling.Visual), so that's where the glyph has the most
	// space.
	private static Polygon2D BuildBodyOver(Polygon2D template, TileShape shape) {
		var corners = new Vector2[shape.CornerOffsets.Length];
		for (int i = 0; i < corners.Length; i++)
			corners[i] = shape.CornerOffsets[i] - shape.CenterOffset;
		float fit = PolygonMath.DistanceToEdges(corners, Vector2.Zero) / PolygonMath.DistanceToEdges(template.Polygon, Vector2.Zero);

		var body = (Polygon2D)template.Duplicate();
		body.Name = shape.BodyName;
		body.Visible = true;
		body.Polygon = corners;
		if (body.VertexColors.Length > 0)
			body.VertexColors = ShadeLike(template.VertexColors, corners, corners);
		Line2D outline = body.GetNodeOrNull<Line2D>("Outline");
		if (outline != null)
			outline.Points = corners;
		foreach (Node2D part in body.GetChildren().OfType<Node2D>()) {
			if (part == outline)
				continue;
			part.Position *= fit;
			part.Scale *= fit;
		}
		return body;
	}

	// An authored body's light-to-dark vertex shading, laid over new corners: every
	// authored body is lit from its upper left before it's turned to fit its tile, so
	// each corner here takes the authored lightest color in the upper left of `extent`
	// (the whole shape being shaded - several tiles, for a merged tower) through to the
	// darkest in the lower right.
	private static Color[] ShadeLike(Color[] authored, Vector2[] corners, Vector2[] extent) {
		Color light = authored.MaxBy(Luminance);
		Color dark = authored.MinBy(Luminance);
		float Lightness(Vector2 p) => -(p.X + p.Y);
		float lowest = extent.Min(Lightness);
		float highest = extent.Max(Lightness);
		var colors = new Color[corners.Length];
		for (int i = 0; i < corners.Length; i++)
			colors[i] = dark.Lerp(light, highest > lowest ? (Lightness(corners[i]) - lowest) / (highest - lowest) : 0.5f);
		return colors;
	}

	private static float Luminance(Color c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;

	// Turns a shape to fit its tile(s) while its glyph stays upright. The glyph is a
	// child of the shape so that it stays centered on it, which only needs its own
	// rotation undoing - the scene files size each glyph to fit inside its shape at every
	// rotation the tiling can give it.
	private static void RotateShape(Node2D shape, float rotation) {
		shape.Rotation = rotation;
		Sprite2D glyph = shape.GetNodeOrNull<Sprite2D>("Icon");
		if (glyph != null)
			glyph.Rotation = -rotation;
	}

	// Per-instance touch-ups to a just-placed body (see RockTower).
	protected virtual void OnVisualPlaced(Polygon2D body) {
	}

	// A merged tower's shape is decided by its tiles, so its scene holds the real thing:
	// one child of Visual per arrangement of tiles the tower can occupy, each drawn
	// exactly where those tiles sit around the tower's origin (Main puts that origin at
	// the tiles' centroid, or a Crusher's middle tile). All that's left to work out at
	// placement is which arrangement this is and how far round it's turned.
	private void PlaceMergedVisual(Node2D visual) {
		Node2D placed = null;
		foreach (Node2D arrangement in visual.GetChildren().OfType<Node2D>()) {
			float? rotation = placed == null ? FindFitRotation(arrangement) : null;
			arrangement.Visible = rotation != null;
			if (rotation != null) {
				placed = arrangement;
				RotateShape(arrangement, rotation.Value);
			}
		}
		placed ??= RebuildOverRealTiles(visual);
		_bodies.AddRange(placed.GetChildren().OfType<Polygon2D>());
	}

	// The rotation that lands every tile of `arrangement` on one of this tower's real
	// tiles, or null if it isn't this arrangement at all. Aims the corner farthest from
	// the origin at each real corner in turn, since one of those must be right - any
	// corner would do, except that some arrangements have one sitting on the origin
	// itself, which has no direction to aim.
	private float? FindFitRotation(Node2D arrangement) {
		List<Vector2[]> tiles = arrangement.GetChildren().OfType<Polygon2D>().Select(tile => tile.Polygon).ToList();
		if (tiles.Count != CellShapes.Count)
			return null;
		Vector2 reference = tiles[0].MaxBy(corner => corner.LengthSquared());
		foreach (Vector2 realCorner in CellShapes.SelectMany(shape => shape.CornerOffsets)) {
			float rotation = realCorner.Angle() - reference.Angle();
			if (tiles.All(tile => CellShapes.Any(shape => TileLandsOn(tile, shape.CornerOffsets, rotation))))
				return rotation;
		}
		return null;
	}

	private static bool TileLandsOn(Vector2[] tile, Vector2[] realCorners, float rotation) =>
		tile.All(corner => realCorners.Any(real => real.DistanceTo(corner.Rotated(rotation)) < ArrangementFitTolerance));

	// For tiles no scene arrangement matches - an open fan at the patch's rim, say, or
	// outpost platforms rather than tiles. Re-lays the first arrangement's tile over each
	// real one, so the tower still covers exactly what it occupies.
	// On a hat board this is every merged tower, since every arrangement is authored in
	// rhombi.
	private Node2D RebuildOverRealTiles(Node2D visual) {
		Node2D arrangement = visual.GetChild<Node2D>(0);
		List<Polygon2D> authored = arrangement.GetChildren().OfType<Polygon2D>().ToList();
		Vector2[] allCorners = CellShapes.SelectMany(shape => shape.CornerOffsets).ToArray();
		foreach (TileShape shape in CellShapes) {
			var tile = (Polygon2D)authored[0].Duplicate();
			tile.Polygon = shape.CornerOffsets;
			tile.GetNode<Line2D>("Outline").Points = shape.CornerOffsets;
			// A rhombus keeps the authored tile's own 4 colors; a hat has more corners
			// than that, so it's shaded afresh, across the whole tower at once.
			if (tile.VertexColors.Length > 0 && tile.VertexColors.Length != shape.CornerOffsets.Length)
				tile.VertexColors = ShadeLike(authored[0].VertexColors, shape.CornerOffsets, allCorners);
			arrangement.AddChild(tile);
		}
		foreach (Polygon2D tile in authored) {
			arrangement.RemoveChild(tile);
			tile.QueueFree();
		}
		arrangement.MoveChild(arrangement.GetNode("Icon"), -1);
		arrangement.Visible = true;
		return arrangement;
	}

	// For a body whose color changes during play (see ContinuousBase). A tint rather
	// than a new Polygon2D.Color, since a merged tower's tiles are shaded per vertex and
	// vertex colors override that. Skips a repeat of the current color, since setting
	// one always queues a redraw.
	protected void SetBodyColor(Color color) {
		if (color == _currentBodyColor)
			return;
		_currentBodyColor = color;
		var tint = new Color(color.R / BodyColor.R, color.G / BodyColor.G, color.B / BodyColor.B);
		foreach (Polygon2D body in _bodies)
			body.SelfModulate = tint;
	}

	// Slightly larger than the tile it sits under, grown from that tile's own center.
	private void AddPlinths() {
		foreach (Polygon2D body in _bodies) {
			Vector2[] corners = body.Polygon;
			Vector2 center = PolygonMath.VertexAverage(corners);
			for (int i = 0; i < corners.Length; i++)
				corners[i] = center + (corners[i] - center) * PlinthScale;
			var plinth = new Polygon2D {
				Polygon = corners,
				Color = OutpostPlinthColor,
				ShowBehindParent = true,
			};
			plinth.AddChild(new Line2D {
				Points = corners,
				Closed = true,
				Width = 2.0f,
				DefaultColor = new Color(0, 0, 0, 0.45f),
			});
			body.AddChild(plinth);
		}
	}

	// Per-tier stat initialization hook - each tier that adds fields overrides this,
	// calls base.InitializeStats() first, then copies its own Base* props into fields.
	protected virtual void InitializeStats() {
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
		if (IsBuffed && HasPlinth)
			AddPlinths();
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
	// its key, which every consumer (GameData.DescribeStats, Main.cs, Projectile.cs)
	// already treats identically to "present with a zero/false default".
	protected virtual void CollectPreviewData(Dictionary data) {
	}

	public Dictionary get_live_stats_dict() {
		var data = new Dictionary {
			{ "tier", BaseTier },
			{ "name", BaseTowerName },
			{ "cost", BaseCost },
			{ "cost_step", BaseCostStep },
			{ "non_blocking", NonBlocking },
			{ "flavor", BaseFlavor },
			{ "abbreviation", BaseAbbreviation },
		};
		CollectLiveStats(data);
		return data;
	}

	protected virtual void CollectLiveStats(Dictionary data) {
	}

	protected void DrawBuffRing() {
		if (!IsBuffed) return;
		foreach (TileShape shape in CellShapes)
			DrawArc(shape.CenterOffset, CellSize * 1.15f, 0.0f, Mathf.Tau, 24, new Color(1.0f, 0.85f, 0.3f, 0.85f), 3.0f);
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

	// Only what depends on the tower's current state is still drawn in code - its
	// body and badge are the Visual node from its scene, kept behind this (see
	// show_behind_parent there) so rings and effects always land on top of them.
	public override void _Draw() {
		DrawBuffRing();
		DrawTileBuffIcon();
	}
}
