using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// The player's target selection — the cockpit's own, not the machine's.
///
/// <para><b>Where this lives is the finding, and it is not where it looks.</b> The selected target
/// the simulation reads is <c>mech+0x1a4</c>, and every homing weapon and half the AI reads it — but
/// for the player's machine nothing in the simulation ever writes it. It is written once a frame by
/// <c>Player_PerFrameCockpitUpdate</c> (<c>0041b130</c>), which copies it out of the cockpit widget
/// tree's <c>+0x210</c>. The selection itself is made in the cockpit
/// (<c>FUN_004332dc</c>/<c>FUN_004333c8</c>/<c>FUN_0043349c</c>, all taking that widget tree as
/// their subject), and the machine is told afterwards. That is why this class is a peer of
/// <see cref="MechObject"/> rather than a property of it, and it is also why an AI machine's target
/// gets there by a different route entirely (<c>FUN_0041c0f4</c>, which is not ported).</para>
///
/// <para><b>The three commands</b> are the manual's, confirmed against
/// <c>CockpitWidgets_HandleCommand</c> and <c>Sim_DispatchCommand</c> by scancode:</para>
/// <list type="bullet">
/// <item><c>Enter</c> (<c>0x1c</c>) — <see cref="Cycle"/>, the manual's "Select Target". Rebuilds the
/// four-deep shortlist and takes its head, or steps to the next entry when that would not change
/// anything.</item>
/// <item><c>'</c> (<c>0x28</c>) — <see cref="SelectNearest"/>, the manual's "Nearest Target". Ignores
/// where the machine is pointing entirely, and considers only HERCs and flyers.</item>
/// <item><c>;</c> (<c>0x27</c>) — <see cref="Clear"/>. Undocumented; it is
/// <c>FUN_004332dc(view, 0)</c>.</item>
/// </list>
///
/// <para>The MFD scanner's TARGET button and a click on the gunsight reach <see cref="Select"/>, the
/// same entry point <see cref="Clear"/> uses.</para>
///
/// <para><b>Not ported:</b> the widget-tree side of each call — every one of the three ends by
/// pushing the new target into the gunsight widget (<c>Gunsight_SetValues</c>), which draws the HUD
/// target box. That box's own child was never traced; see the cockpit-hud notes.</para>
/// </summary>
public sealed class TargetSelection {
	/// <summary>
	/// How deep the shortlist goes, and how many candidates each priority bucket keeps — both are 4
	/// in <c>FUN_0043349c</c>, which allocates four buckets of four.
	/// </summary>
	public const int ShortlistDepth = 4;

	/// <summary>
	/// Half-width of the cone <see cref="InForwardCone"/> accepts, about the machine's heading plus
	/// its turret twist — <c>FUN_00433250</c>'s own <c>±8999</c>, a little under 50°.
	/// </summary>
	public const int ForwardConeHalfWidth = 8999;

	/// <summary>
	/// Width of one priority bucket, in binary angle units — <c>FUN_0043349c</c>'s <c>err &gt;&gt;
	/// 10</c>. Four buckets of about 5.6° each, with everything past the fourth folded into it.
	/// </summary>
	public const int BucketWidth = 1 << 10;

	private readonly SimWorld _world;
	private readonly SimObject _viewer;
	private readonly SimObject?[] _shortlist = new SimObject?[ShortlistDepth];
	private readonly Bucket[] _buckets = CreateBuckets();

	private int _cursor;

	/// <param name="world">The world whose object list is searched.</param>
	/// <param name="viewer">
	/// The machine the selection is made from — the cockpit widget tree's <c>+0x203</c>. Every range
	/// and every bearing below is measured from it.
	///
	/// <para>The original picks between this and a second object (<c>DAT_004d2708</c>) on the
	/// <c>DAT_0049ef5c</c> flag, which is set while the player is watching a machine other than their
	/// own. There is no such mode here, so this is always the pilot's machine.</para>
	/// </param>
	public TargetSelection(SimWorld world, SimObject viewer) {
		_world = world;
		_viewer = viewer;
	}

	/// <summary>The machine the selection is made from.</summary>
	public SimObject Viewer => _viewer;

	/// <summary>
	/// <c>view+0x210</c> — what is selected right now. Null until something is, and pushed onto the
	/// machine by <see cref="PushToPilot"/>.
	/// </summary>
	public SimObject? Selected { get; private set; }

	/// <summary>
	/// The gunsight complex's <c>+0xd5</c> flag - whether the target indicator has been armed. All
	/// three selection entry points set it (and never clear it) on a successful press, by pushing a
	/// literal 1 into the gunsight's state block before handing that block back; nothing else in the
	/// image writes it, and the target box's paint refuses to draw until it is set. Since the only
	/// way to acquire a target is one of those three keys, it is true whenever there is anything to
	/// draw - it is carried because the original carries it, not because it gates anything here.
	/// </summary>
	public bool IndicatorArmed { get; private set; }

	/// <summary>
	/// <c>DAT_004d0490</c> — the four best candidates the last <see cref="Cycle"/> found, in priority
	/// order. It is what <see cref="Cycle"/> steps through on a repeat press, and the original also
	/// draws it on the scanner. Entries past the number found are null.
	/// </summary>
	public IReadOnlyList<SimObject?> Shortlist => _shortlist;

	/// <summary>
	/// <c>FUN_00433174</c> — may this object be selected at all.
	///
	/// <list type="number">
	/// <item>It has to be alive and mobile — see <see cref="SimObject.Neutralised"/>, which folds in
	/// the immobilised case as the original does.</item>
	/// <item>It has to be on the other side.</item>
	/// <item>It has to be <i>known</i>, by either of the two routes
	/// <see cref="Detection"/> describes, and inside that route's own range:
	/// <see cref="Detection.RadarTargetingRange"/> for something showing on radar,
	/// <see cref="Detection.ContactTargetingRange"/> for a contact this machine holds.</item>
	/// </list>
	///
	/// <para>Rule 3 is why target selection could not be built before detection was: with no contact
	/// table and nothing painted, every candidate fails it and nothing is ever selectable.</para>
	/// </summary>
	public bool CanTarget(SimObject candidate) {
		if (candidate.Neutralised || candidate.Side == _viewer.Side) {
			return false;
		}

		int distance = _viewer.Position.ApproxDistanceTo(candidate.Position);

		return (candidate.RadarVisible && distance < Detection.RadarTargetingRange)
			|| (_viewer.Detects(candidate) && distance < Detection.ContactTargetingRange);
	}

	/// <summary>
	/// <c>FUN_00433250</c> — is the object roughly in front of the turret. The bearing is taken
	/// relative to the machine's heading and then <b>offset by the turret twist</b>, which is the
	/// original's own sign and is transcribed rather than corrected: <c>Mech_PerTickSystemsUpdate</c>
	/// and the sensor sweep both fold the twist in the same direction.
	/// </summary>
	public bool InForwardCone(SimObject candidate) {
		short error = BearingError(candidate);
		return error >= -ForwardConeHalfWidth && error <= ForwardConeHalfWidth;
	}

	/// <summary>
	/// <c>FUN_004333c8</c> — the nearest selectable HERC or flyer, whichever way the machine happens
	/// to be pointing. <b>No cone test</b>, so this reaches something behind you, and
	/// <b>structures are excluded</b> by target class, so it will not lock a building when a machine
	/// is further away.
	/// </summary>
	/// <returns>The new selection, or null when nothing qualified — in which case the old one stands.</returns>
	public SimObject? SelectNearest() {
		SimObject? best = null;
		int bestDistance = int.MaxValue;

		var objects = _world.Objects;
		for (int i = 0; i < objects.Count; i++) {
			var candidate = objects[i];
			if (candidate.Removed || candidate.AwaitingDeployment) {
				continue;
			}

			if (candidate.TargetClass is not (TargetClass.Herc or TargetClass.Flyer)
					|| !CanTarget(candidate)) {
				continue;
			}

			int distance = _viewer.Position.ApproxDistanceTo(candidate.Position);
			if (distance < bestDistance) {
				best = candidate;
				bestDistance = distance;
			}
		}

		if (best != null && best != Selected) {
			Selected = best;
			IndicatorArmed = true;
		}

		return best;
	}

	/// <summary>
	/// <c>FUN_0043349c</c> — the [Enter] key, and the one that does the real work.
	///
	/// <para>Everything selectable and inside <see cref="InForwardCone"/> is filed into one of four
	/// buckets by <i>how far off the crosshair it is</i> — <see cref="BucketWidth"/> of bearing error
	/// per bucket — and sorted by range within its bucket, nearest first, keeping four. Flattening
	/// the buckets in order gives <see cref="Shortlist"/>: what is closest to the crosshair wins, and
	/// range only breaks ties inside a band. That is what makes pressing [Enter] pick the thing you
	/// are looking at rather than the thing that is nearest.</para>
	///
	/// <para><b>A repeat press steps.</b> If the rebuilt head is the same object the previous
	/// shortlist began with, or is already selected, the current selection is looked up in the new
	/// shortlist and the entry after it is taken instead — which is the manual's "keep pressing
	/// [Enter] to cycle through all targets".</para>
	///
	/// <para><b>One correction that is not applied because the original does not apply it.</b> The
	/// bucket is computed from the raw bearing error less the target's own angular half-width, which
	/// <c>FUN_0043349c</c> works out from its range and shape radius and then passes through
	/// <c>Math_Q10Multiply</c> with a <b>literal zero</b> as the other operand (<c>PUSH 0x0</c> at
	/// <c>004335d0</c>). The correction is therefore always zero and a large target near the edge of
	/// a band is not promoted. Reproduced by omission.</para>
	/// </summary>
	/// <returns>The selection after the press, which may be unchanged.</returns>
	public SimObject? Cycle() {
		var previousHead = _shortlist[0];
		var previous = Selected;

		foreach (var bucket in _buckets) {
			bucket.Clear();
		}

		Array.Clear(_shortlist);

		var objects = _world.Objects;
		for (int i = 0; i < objects.Count; i++) {
			var candidate = objects[i];
			if (candidate.Removed || candidate.AwaitingDeployment
					|| !CanTarget(candidate) || !InForwardCone(candidate)) {
				continue;
			}

			int error = Math.Abs((int)BearingError(candidate));
			int bucket = Math.Min(error / BucketWidth, _buckets.Length - 1);
			_buckets[bucket].Insert(candidate, _viewer.Position.ApproxDistanceTo(candidate.Position));
		}

		int filled = 0;
		foreach (var bucket in _buckets) {
			for (int i = 0; i < bucket.Count && filled < _shortlist.Length; i++) {
				_shortlist[filled++] = bucket.Objects[i];
			}
		}

		var chosen = _shortlist[0];

		// The step: a head that is not new tells us the press was a repeat, so the shortlist is
		// walked to whatever follows the current selection instead. A selection that is not in the
		// new shortlist at all, or is its last entry, leaves the head standing.
		if ((chosen == previousHead || chosen == previous) && Selected != null) {
			for (int i = 0; i < _shortlist.Length - 1; i++) {
				if (Selected == _shortlist[i] && _shortlist[i + 1] != null) {
					chosen = _shortlist[i + 1];
					break;
				}
			}
		}

		if (chosen != previous) {
			Selected = chosen;
			IndicatorArmed = true;
		}

		return Selected;
	}

	/// <summary>
	/// <c>FUN_004332dc</c> — select one named object, the entry point the MFD scanner's TARGET button
	/// and a click on the gunsight both use. Passing null clears the selection, which is what the
	/// <c>;</c> key does.
	///
	/// <para><b>It walks rather than assigns</b>, and the walk is visible in the result: the selection
	/// is stepped through the world's object list from a cursor this class keeps, one object per
	/// iteration, until it lands on the one asked for and that object passes both filters. A request
	/// for something that is not selectable therefore ends with the selection back where it started
	/// (the walk stops when it wraps) and returns false. Transcribed as-is; a plain assignment would
	/// be equivalent for a legal request and not for an illegal one.</para>
	/// </summary>
	/// <returns>Whether the object asked for is what ended up selected.</returns>
	public bool Select(SimObject? target) {
		if (target == null) {
			Selected = null;
			IndicatorArmed = true;
			return true;
		}

		if (Selected == null) {
			Cycle();
		}

		var start = Selected;
		var objects = _world.Objects;

		do {
			if (target == Selected && CanTarget(Selected!) && InForwardCone(target)) {
				break;
			}

			Selected = _cursor < objects.Count ? objects[_cursor] : null;
			_cursor++;

			if (Selected == null) {
				_cursor = 0;
			}
		} while (Selected != start);

		if (target == Selected) {
			IndicatorArmed = true;
		}

		return target == Selected;
	}

	/// <summary>Clears the selection — <c>FUN_004332dc(view, 0)</c>, the <c>;</c> key.</summary>
	public void Clear() => Select(null);

	/// <summary>
	/// Drops a selection that is no longer legal. Not a function of the original, which leaves a dead
	/// or vanished target selected until something else clears it — but the original's owner of that
	/// job is the AI target-abandon check (<c>FUN_0041c4a8</c>) plus the death path's own clear
	/// (<c>FUN_0041eb34</c>), neither of which is ported, so without this a destroyed machine stays
	/// locked and homing rounds keep chasing its wreck.
	/// </summary>
	public void DropIfInvalid() {
		if (Selected != null && (Selected.Removed || !CanTarget(Selected))) {
			Selected = null;
		}
	}

	/// <summary>
	/// The copy <c>Player_PerFrameCockpitUpdate</c> makes once a frame: the cockpit's selection
	/// becomes the machine's <c>+0x1a4</c>. Only the pilot's machine is written, and only when the
	/// selection has actually changed — see <see cref="MechObject.Target"/> for the bookkeeping that
	/// hangs off the change.
	/// </summary>
	public void PushToPilot() {
		if (_viewer is MechObject mech) {
			mech.Target = Selected;
		}
	}

	/// <summary>
	/// Bearing to <paramref name="candidate"/> measured from the turret's centreline, as both the
	/// cone test and the bucket sort want it.
	/// </summary>
	private short BearingError(SimObject candidate) {
		short bearing = Detection.HeadingToward(candidate.Position, _viewer.Position);
		return (short)(bearing - _viewer.Heading + _viewer.AimTwist);
	}

	private static Bucket[] CreateBuckets() {
		var buckets = new Bucket[ShortlistDepth];
		for (int i = 0; i < buckets.Length; i++) {
			buckets[i] = new Bucket();
		}

		return buckets;
	}

	/// <summary>
	/// One of <c>DAT_004d0494</c>'s four <c>{count, objects[4], distances[4]}</c> triples: the
	/// candidates in one band of bearing error, nearest first.
	/// </summary>
	private sealed class Bucket {
		public readonly SimObject?[] Objects = new SimObject?[ShortlistDepth];

		private readonly int[] _distances = new int[ShortlistDepth];

		public int Count { get; private set; }

		public void Clear() {
			Count = 0;
			Array.Clear(Objects);
			Array.Fill(_distances, EmptySlotDistance);
		}

		/// <summary>
		/// An insertion sort over four slots, straight from the original — including that a candidate
		/// further than every slot already holds is dropped rather than appended, which is what caps
		/// the bucket without a separate length test.
		/// </summary>
		public void Insert(SimObject candidate, int distance) {
			int slot = 0;
			while (_distances[slot] <= distance) {
				if (++slot >= Objects.Length) {
					return;
				}
			}

			// The original shifts from `count - 1`, clamped to the last slot that has somewhere to go,
			// and then records `count = clamped + 2` — so a bucket that was empty ends up at 1.
			int last = Math.Min(Count - 1, Objects.Length - 2);
			for (int i = last; i >= slot; i--) {
				Objects[i + 1] = Objects[i];
				_distances[i + 1] = _distances[i];
			}

			Objects[slot] = candidate;
			_distances[slot] = distance;
			Count = last + 2;
		}

		/// <summary>
		/// What an unused slot's distance holds — <c>0x7ffffff</c>, the original's own literal. Note
		/// it is one digit short of <c>int.MaxValue</c>; that is what the disassembly writes, and a
		/// candidate past 134 million world units is not a case that arises.
		/// </summary>
		private const int EmptySlotDistance = 0x7ffffff;
	}
}
