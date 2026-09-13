using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class SniperTower : AttackBase {
	public SniperTower() {
		tower_type = "sniper";
	}

	protected override int BaseTier => 2;
	protected override string BaseTowerName => "Sniper Tower";
	protected override int BaseCost => 110;
	protected override int BaseCostStep => 22;
	protected override float BaseFireRate => 0.45f;
	protected override float BaseDamage => 70.0f;
	protected override Color BaseColor => new Color(0.38f, 0.42f, 0.32f);
	protected override Color BaseProjectileColor => new Color(0.85f, 0.9f, 0.75f);
	protected override float BaseProjectileSpeed => 1100.0f;
	protected override string BaseFlavor => "2 Turret + 1 Lightning in a triangle. No range limit at all, but it can only fire at an enemy it has a clear, unobstructed line to.";
	protected override string BaseAbbreviation => "SNPR";

	// No RangeArea to set up (there's no finite range), and AttackBase's own
	// ConfigureCombat refuses to start the fire timer at all when Range <= 0 - so
	// this replaces the whole chain rather than extending it, wiring the timer to
	// the same handler AttackBase normally uses.
	protected override void ConfigureCombat() {
		if (FireRate <= 0.0f)
			return;
		FireTimer.WaitTime = 1.0 / FireRate;
		FireTimer.Timeout += OnFireTimerTimeout;
		FireTimer.Start();
	}

	// Every enemy on the field is a candidate, not just ones inside a collision
	// shape - filtered down to whichever have a clear line to this tower, then
	// prioritized by path progress (closest to the exit first), same "biggest
	// threat first" intent as EnemiesInRange's roughly-FIFO order elsewhere.
	protected override List<BaseEnemy> GetTargets(int count, HashSet<BaseEnemy> excluding = null) {
		// If this tower is the result of a merge onto an outpost's platform, the
		// outpost's own supporting walls occupy cells well outside its single anchor
		// point - without this, they'd block its view in most directions.
		HashSet<int> ignoreCells = Main.Instance.GetOwnStructureCells(this);
		var candidates = new List<BaseEnemy>();
		foreach (Node node in GetTree().GetNodesInGroup("enemies")) {
			if (node is BaseEnemy enemy && IsInstanceValid(enemy)
				&& (excluding == null || !excluding.Contains(enemy))
				&& Main.Instance.HasLineOfSight(GlobalPosition, enemy.GlobalPosition, ignoreCells)) {
				candidates.Add(enemy);
			}
		}
		candidates.Sort((a, b) => b.DistanceTraveled.CompareTo(a.DistanceTraveled));
		if (candidates.Count > count)
			candidates.RemoveRange(count, candidates.Count - count);
		return candidates;
	}

	protected override void CollectPreviewData(Dictionary data) {
		base.CollectPreviewData(data);
		data["global_range"] = true;
	}

	protected override void CollectLiveStats(Dictionary data) {
		base.CollectLiveStats(data);
		data["global_range"] = true;
	}
}
