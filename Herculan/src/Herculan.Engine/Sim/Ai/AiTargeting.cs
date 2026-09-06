using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim.Ai;

/// <summary>
/// Which of <see cref="AiTargeting.SelectTarget"/>'s behaviours a caller wants — the mask its
/// second argument carries. Bit meanings and the caller that passes each are in
/// docs/simulation/ai-targeting.md.
/// </summary>
[Flags]
public enum TargetFilter {
	/// <summary>No bits: every candidate counts as designated and every weight applies.</summary>
	None = 0,

	/// <summary>
	/// <c>0x01</c> — only the group order's designated target gets the tier bonus. The combat
	/// reassess and the state think functions set it.
	/// </summary>
	MissionTargetOnly = 0x01,

	/// <summary><c>0x02</c> — drop the "it is shooting at me" weight.</summary>
	IgnoreIncoming = 0x02,

	/// <summary><c>0x04</c> — drop the divisor that discounts a candidate other machines already hold.</summary>
	IgnoreCrowding = 0x04,

	/// <summary><c>0x10</c> — reject candidates of the asking object's own class.</summary>
	RejectOwnClass = 0x10,

	/// <summary><c>0x20</c> — ignore bearing entirely and score on range alone.</summary>
	IgnoreBearing = 0x20
}

/// <summary>
/// The AI's target handling: what may be shot at, what is worth shooting at, and when to stop.
/// Ported from docs/simulation/ai-targeting.md, which owns the derivation; the constants and the
/// order of the tests are its.
///
/// <para>These are the sim's shared routines rather than a mech's own —
/// <see cref="SelectTarget"/> is called by structures as well as machines, which is why it takes the
/// asking object rather than living on <see cref="MechObject"/>.</para>
/// </summary>
public static class AiTargeting {
	/// <summary>
	/// <c>Ai_KnowsObject</c> (<c>00411c58</c>) — the AI's knowledge test. Radar-visible within
	/// <see cref="RadarKnowledgeRange"/>, or a contact this object holds at <b>any</b> range.
	///
	/// <para>It is far looser than the player's own (<see cref="TargetSelection.CanTarget"/>), which
	/// caps radar at 200000 and contacts at 30000/60000. An AI machine can shoot at things the
	/// player could not select.</para>
	/// </summary>
	public static bool Knows(SimObject self, SimObject candidate) =>
		(candidate.RadarVisible
				&& self.Position.ApproxDistanceTo(candidate.Position) <= RadarKnowledgeRange)
			|| self.Detects(candidate);

	/// <summary>The radar half of <see cref="Knows"/> — the original's literal 999999.</summary>
	public const int RadarKnowledgeRange = 999999;

	/// <summary>
	/// <c>Ai_IsTargetable</c> (<c>00411e80</c>) — whether an AI object may target a candidate at all.
	/// The tests, in the original's order: not the same side, not destroyed, collapsed or
	/// invulnerable, currently <see cref="Knows"/>n, not a dead flyer, and with
	/// <see cref="TargetFilter.RejectOwnClass"/> not the asking object's own class.
	///
	/// <para>Two of the original's tests are not here. One reads a global
	/// (<c>DAT_004a9ed8 == 3</c>) whose meaning is unresolved; the other refuses the player's current
	/// selection to a machine whose <c>+0x9a</c> is set, and nothing found so far writes that field.
	/// Both are listed under docs/simulation/ai-targeting.md, "Open questions". Their effect is to
	/// <i>narrow</i> the candidate set, so leaving them out can only make the AI consider more than
	/// the original would, never less.</para>
	/// </summary>
	public static bool IsTargetable(SimObject self, SimObject candidate, TargetFilter filter) {
		if (candidate.Side == self.Side || candidate.Removed || candidate.AwaitingDeployment) {
			return false;
		}

		if (IsWrecked(candidate)) {
			return false;
		}

		if (candidate is MechObject { Collapsed: true } || candidate.Invulnerable) {
			return false;
		}

		if (!Knows(self, candidate)) {
			return false;
		}

		if (filter.HasFlag(TargetFilter.RejectOwnClass) && candidate.TargetClass == self.TargetClass) {
			return false;
		}

		// A flyer gets the extra liveness test the original spells out for target class 2 alone.
		return candidate.TargetClass != TargetClass.Flyer || !candidate.Neutralised;
	}

	/// <summary>
	/// <c>Ai_SelectTarget</c> (<c>00411fa0</c>) — the sim's one AI target-acquisition routine, shared
	/// by machines and by base turrets. Walks the live object list and returns the best-scoring
	/// candidate, or null.
	///
	/// <para>Two bars come before the score. The <b>tier</b> is <c>2 * designated + alive</c>, where
	/// designated means the candidate is what the group's current order names <i>and</i> that order's
	/// verb is 0; it starts at 1 and drops to 0 under order verb 3, so with
	/// <see cref="TargetFilter.MissionTargetOnly"/> clear every candidate clears it and with the bit
	/// set only a live one does. The <b>range cap</b> is <see cref="DesignatedRange"/> for the
	/// designated target and <see cref="OrdinaryRange"/> for everything else. A candidate that is
	/// crippled (<c>+0xa4</c>) is skipped outright unless it is designated and this object is Cybrid
	/// — human-side machines leave a crippled target alone, Cybrids finish it.</para>
	///
	/// <para>The score is bearing-led but <b>range takes over inside 60000</b>: at point-blank the
	/// proximity term is an order of magnitude above the bearing term, so a close enemy outranks a
	/// better-aligned distant one. Four weight tables then apply, three of them indexed by
	/// <see cref="MechObject.CompareCombatRating"/> and the fourth by the candidate's object class,
	/// and finally the candidate is discounted by how many other machines already hold it.</para>
	/// </summary>
	/// <param name="self">The object asking. Its group supplies the side, the order verb and the designation.</param>
	/// <param name="filter">The original's mask argument.</param>
	/// <param name="coneLimit">
	/// An optional bearing limit in binary-angle units, rejecting anything outside it. Zero means no
	/// limit; a base turret passes <c>0x3000</c>.
	/// </param>
	public static SimObject? SelectTarget(SimWorld world, SimObject self, TargetFilter filter,
			short coneLimit = 0) {
		short orderVerb = self.Group?.OrderVerb ?? MissionGroup.NoOrder;

		SimObject? best = null;
		int bestScore = 0;
		int bestTier = orderVerb == PatrolOrderVerb ? 0 : 1;

		var objects = world.Objects;
		for (int i = 0; i < objects.Count; i++) {
			var candidate = objects[i];
			if (!IsTargetable(self, candidate, filter)) {
				continue;
			}

			bool designated = orderVerb == SearchDestroyOrderVerb
				&& self.Group is { } group && group.IsOrderTarget(candidate);

			bool counts = designated || !filter.HasFlag(TargetFilter.MissionTargetOnly);
			int tier = (counts ? 2 : 0) + (candidate.Neutralised ? 0 : 1);

			// The crippled skip. `designated && Cybrid` is the only way past it, so on the human side
			// it reads as "never bother with something that cannot move".
			if ((!designated || self.Side == World.MissionSide.Human)
					&& candidate is MechObject { Immobilised: true }) {
				continue;
			}

			if (tier < bestTier) {
				continue;
			}

			int range = self.Position.ApproxDistanceTo(candidate.Position);
			if (range > (designated ? DesignatedRange : OrdinaryRange)) {
				continue;
			}

			int bearingTerm;
			if (filter.HasFlag(TargetFilter.IgnoreBearing)) {
				bearingTerm = BearingTermSpan;
			} else {
				int error = System.Math.Abs((int)BearingError(self, candidate));
				if (coneLimit != 0 && error > coneLimit) {
					continue;
				}

				bearingTerm = BearingTermSpan - (error >> 3);
			}

			int score = SimMath.Q10Multiply(BearingWeight, bearingTerm);

			if (range < ProximityRange) {
				int divisor = System.Math.Max(range >> 12, 1);
				score = System.Math.Max(score, bearingTerm / divisor);
			}

			int rating = self is MechObject asker ? asker.CompareCombatRating(world, candidate) : 0;

			if (!filter.HasFlag(TargetFilter.IgnoreIncoming)
					&& candidate is MechObject { Target: { } held } && ReferenceEquals(held, self)) {
				score = SimMath.Q10Multiply(score, ShootingAtMe[rating]);
			}

			// "Not engaged" is read off the candidate's own behaviour state, so it only means
			// anything for a machine — a structure has no behaviour block and the original's null
			// check skips it.
			if (candidate is MechObject { LocallyPiloted: false } idle
					&& idle.Behaviour.State is { Committed: false }) {
				score = SimMath.Q10Multiply(score, NotEngaged[rating]);
			}

			if (candidate is MechObject { Target: { } other } && !ReferenceEquals(other, self)) {
				score = SimMath.Q10Multiply(score, AlreadyTaken[rating]);
			}

			// A structure that is shooting at this object is re-indexed as a HERC, which gives it a
			// HERC's weight instead of a building's.
			int classIndex = (int)candidate.TargetClass;
			if (candidate is MechObject { Target: { } aimed } && ReferenceEquals(aimed, self)
					&& classIndex == (int)TargetClass.Structure) {
				classIndex = (int)TargetClass.Herc;
			}

			if (classIndex >= 0 && classIndex < ByClass.Count) {
				score = SimMath.Q10Multiply(score, ByClass[classIndex]);
			}

			// The asking object's own hold is not counted against the candidate, so re-picking what
			// it already has is not penalised for being taken.
			int holders = candidate.TargetedBy
				- (self is MechObject holder && ReferenceEquals(holder.Target, candidate) ? 1 : 0);

			if (!filter.HasFlag(TargetFilter.IgnoreCrowding) && holders != 0) {
				score /= holders + 1;
			}

			if (score > bestScore || tier > bestTier) {
				best = candidate;
				bestScore = score;
				bestTier = tier;
			}
		}

		return best;
	}

	/// <summary>
	/// <c>Ai_SelectDefenceTarget</c> (<c>0041e0e0</c>) — the machine threatening a defended point.
	/// Scored on distance to the post rather than to the defender, so a guard picks off whatever has
	/// come closest to what it is guarding.
	/// </summary>
	/// <param name="self">The defender.</param>
	/// <param name="post">The place being held — see <c>Mech_AiGoalPosition</c>.</param>
	/// <param name="limit">
	/// <see cref="DefenceRange"/> normally; <see cref="OpenDefenceRange"/> when the group's current
	/// order names no object, which is what makes a guard with nothing specific to protect look much
	/// further afield for whatever is coming.
	/// </param>
	public static SimObject? SelectDefenceTarget(SimWorld world, SimObject self, Vec3i post,
			int limit = OpenDefenceRange) {
		SimObject? best = null;
		int bestScore = 0;
		bool bestAlive = false;

		var objects = world.Objects;
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i] is not MechObject candidate
					|| !IsTargetable(self, candidate, TargetFilter.None)
					|| candidate.Immobilised) {
				continue;
			}

			bool alive = !candidate.Neutralised;
			if (!alive && bestAlive) {
				continue;
			}

			int score = limit - candidate.Position.ApproxDistanceTo(post);
			if (score <= 0) {
				continue;
			}

			if (candidate.TargetedBy != 0) {
				score /= candidate.TargetedBy + 1;
			}

			if (score > bestScore || (alive && !bestAlive)) {
				best = candidate;
				bestScore = score;
				bestAlive = alive;
			}
		}

		return best;
	}

	/// <summary>
	/// <c>Ai_SumAttackerRatings</c> (<c>0041cb44</c>) — the combined combat rating of every machine
	/// currently holding <paramref name="subject"/> as its target, which is what
	/// <see cref="MechObject.FleeCheck"/> weighs its own against.
	/// </summary>
	public static int SumAttackerRatings(SimWorld world, SimObject subject) {
		int total = 0;

		var objects = world.Objects;
		for (int i = 0; i < objects.Count; i++) {
			if (objects[i] is MechObject attacker && ReferenceEquals(attacker.Target, subject)) {
				total += attacker.CombatRating;
			}
		}

		return total;
	}

	/// <summary>
	/// <c>Ai_TargetStateTier</c> (<c>00411cb4</c>) — how finished a target is, read off
	/// <i>its own</i> behaviour descriptor: 2 for a state carrying flag bit 5 (<c>in limbo</c>,
	/// <c>dead</c>, <c>disabled</c>), 1 for bit 4 (<c>fleeing</c>), 0 otherwise.
	/// </summary>
	public static int TargetStateTier(SimObject? target) =>
		target is MechObject mech && mech.Behaviour.State is { } state ? state.DisengageTier : 0;

	/// <summary>
	/// <c>Ai_ShouldAbandonTarget</c> (<c>0041c4a8</c>) — whether a combat state should let go. Three
	/// answers in order: under a squad order with a target, when the target's
	/// <see cref="TargetStateTier"/> passes the order's threshold; for the group order's designated
	/// target, dead or crippled on the human side but only destroyed on the Cybrid side; otherwise
	/// dead or dying.
	///
	/// <para>The squad-order branch cannot be reached here — squad orders belong to the unported
	/// squadmate slice, so <see cref="MechObject.SquadOrderVerb"/> is always zero.</para>
	/// </summary>
	public static bool ShouldAbandonTarget(MechObject self) {
		if (self.Target is not { } target) {
			return true;
		}

		if (self.SquadOrderVerb == MechObject.SquadOrderEngage && self.SquadOrderTarget != null) {
			return TargetStateTier(target) > self.SquadOrderAbandonTier;
		}

		if (self.Group is { } group && group.IsOrderTarget(target)) {
			return self.Side == World.MissionSide.Human ? target.Neutralised : IsWrecked(target);
		}

		return target.Neutralised;
	}

	/// <summary>
	/// <c>obj+0x99</c> alone, without the crippled half <see cref="SimObject.Neutralised"/> folds in.
	/// The three shootable classes each carry their own flag; nothing else can be destroyed.
	/// </summary>
	private static bool IsWrecked(SimObject candidate) => candidate switch {
		MechObject mech => mech.Destroyed,
		BaseObject structure => structure.Destroyed,
		FlyerObject flyer => flyer.Destroyed,
		_ => false
	};

	/// <summary>
	/// The bearing from this object's facing to a candidate, the way the acquisition measures it.
	/// </summary>
	private static short BearingError(SimObject self, SimObject candidate) =>
		(short)(Detection.HeadingToward(candidate.Position, self.Position) - self.Heading);

	/// <summary>The group order verb that designates a target for the tier bonus — search/destroy.</summary>
	private const short SearchDestroyOrderVerb = 0;

	/// <summary>The group order verb that lowers the tier bar to zero — patrolling.</summary>
	private const short PatrolOrderVerb = 3;

	/// <summary>The range cap for the group order's designated target.</summary>
	public const int DesignatedRange = 1000000;

	/// <summary>The range cap for everything else.</summary>
	public const int OrdinaryRange = 100000;

	/// <summary>Inside this the proximity term can override the bearing term.</summary>
	public const int ProximityRange = 60000;

	/// <summary>The bearing term at dead ahead, falling to zero astern.</summary>
	private const int BearingTermSpan = 0x1000;

	/// <summary>The Q10 weight the bearing term is scaled by before anything else applies.</summary>
	private const int BearingWeight = 70;

	/// <summary>Ordinary defence range; <see cref="SelectDefenceTarget"/>'s narrow form.</summary>
	public const int DefenceRange = 90000;

	/// <summary>The wider defence range for a group whose current order names no object.</summary>
	public const int OpenDefenceRange = 980000;

	/// <summary><c>0049933c</c> — the candidate is shooting at me.</summary>
	private static readonly IReadOnlyList<int> ShootingAtMe = new[] { 1500, 2000, 2500 };

	/// <summary><c>00499342</c> — the candidate is not engaged with anyone.</summary>
	private static readonly IReadOnlyList<int> NotEngaged = new[] { 600, 400, 200 };

	/// <summary><c>00499348</c> — the candidate is already someone else's target.</summary>
	private static readonly IReadOnlyList<int> AlreadyTaken = new[] { 700, 850, 1000 };

	/// <summary><c>0049934e</c> — by object class: HERC, structure, flyer, emplacement.</summary>
	private static readonly IReadOnlyList<int> ByClass = new[] { 1500, 700, 500, 500 };
}
