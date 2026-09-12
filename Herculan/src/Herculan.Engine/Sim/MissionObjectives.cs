using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// The mission's objective layer — the block-12 records, the poll that reads them, and the one
/// number the whole thing produces: <see cref="Status"/>.
///
/// <para>Three of the original's functions live here.
/// <c>Mission_EvaluateObjectives</c> (<c>00413280</c>) walks the record array and reduces it to a
/// status; <c>Mission_Status</c> (<c>004135e8</c>) puts the player's own condition and the mission
/// box in front of that and <b>posts the computer message</b> when the answer changes;
/// <c>Mission_PollStatus</c> (<c>004131ac</c>) is what <c>Sim_MainTick</c> calls, and it is the
/// throttle — the status is only worked out every <see cref="PollInterval"/>, and an alert-worthy
/// one has to hold for <see cref="AlertDelay"/> before it is handed up.</para>
///
/// <para>See docs/simulation/mission-objectives.md.</para>
/// </summary>
public sealed class MissionObjectives {
	private readonly MissionObjectiveState[] _objectives;

	public MissionObjectives(IReadOnlyList<MissionObjectiveState> objectives) {
		_objectives = objectives.ToArray();
	}

	/// <summary>
	/// An objective layer with no records — every mission is in progress and nothing ends it. A fresh
	/// one each time: the layer carries running countdowns, so a shared instance would let one world's
	/// poll clock another's.
	/// </summary>
	public static MissionObjectives Empty => new(Array.Empty<MissionObjectiveState>());

	/// <summary>
	/// <c>DAT_004a9ec4</c>, count <c>DAT_004a9ec0</c> — the objective records, in file order.
	/// </summary>
	public IReadOnlyList<MissionObjectiveState> Objectives => _objectives;

	/// <summary>
	/// <c>DAT_004a9ecc</c>, count <c>DAT_004a9ec8</c> — block 13, the <c>data\mission.str</c> lines
	/// the objectives screen lists. It is a separate list from the records above and is not derived
	/// from them: an author writes the briefing lines the player reads and the conditions the
	/// simulation tests independently, and nothing cross-checks them.
	/// </summary>
	public IReadOnlyList<int> BriefingLines { get; init; } = Array.Empty<int>();

	/// <summary>
	/// The mission's own selector — <c>script.dat</c>'s header at <c>+0x06</c> (<c>DAT_004a9ed8</c>),
	/// which picks which arm of the player's think watches for progress. See
	/// <see cref="MechObject.PlayerThink"/>.
	/// </summary>
	public int ObjectiveType { get; init; }

	/// <summary>
	/// <c>DAT_004a9ed0</c> — the last status handed up to the caller, which is what stops the same
	/// alert being raised twice. Starts at <see cref="MissionStatus.None"/>, which the spawn pass
	/// sets as <c>0xffff</c>.
	/// </summary>
	public MissionStatus Announced { get; private set; } = MissionStatus.None;

	/// <summary>The status the last completed evaluation produced.</summary>
	public MissionStatus Status { get; private set; } = MissionStatus.None;

	/// <summary>
	/// The objective whose failure text the alert would print — <c>DAT_004d1f1c</c>, which the
	/// evaluator points at the <b>first</b> mandatory record that is not satisfied. Null once they
	/// all are.
	/// </summary>
	public MissionObjectiveState? Outstanding { get; private set; }

	/// <summary>
	/// <c>Mission_PollStatus</c> (<c>004131ac</c>), run once a tick from <c>Sim_MainTick</c> with the
	/// player's machine, and only while that machine is not destroyed.
	///
	/// <para>Two countdowns shape it, and between them they are why a finished mission takes tens of
	/// seconds to say so. <b>The poll interval</b> (<c>DAT_004a9ee6</c>) is re-armed to
	/// <see cref="PollInterval"/> every time the answer is not worth raising, so the objectives are
	/// actually read about once every ten seconds — except that a player outside the mission box is
	/// read every tick, which is what makes the boundary warning prompt. <b>The alert delay</b>
	/// (<c>DAT_004a9ee9</c>) is armed once, the first time an alert-worthy status appears, and the
	/// status is not handed up until it runs out. A destroyed player skips it.</para>
	/// </summary>
	/// <returns>
	/// The status the caller should raise its modal alert for, or <see cref="MissionStatus.None"/>
	/// for "carry on". The engine has no such alert yet; <see cref="Announced"/> is the record of it.
	/// </returns>
	public MissionStatus Poll(SimWorld world, MechObject player) {
		SimMath.CountdownTimerTick(ref _alertDelay);

		if (SimMath.CountdownTimerTick(ref _pollInterval) != 0
				&& !IsOutsideMissionBox(world, player, 0)) {
			return MissionStatus.None;
		}

		var status = Evaluate(world, player, quiet: false);

		if (!RaisesAlert(status) || status == Announced) {
			_pollInterval = PollInterval;
			return MissionStatus.None;
		}

		if (!_alertArmed) {
			_alertArmed = true;
			_alertDelay = AlertDelay;
		}

		if (_alertDelay != 0 && status != MissionStatus.PlayerDestroyed) {
			_pollInterval = PollInterval;
			return MissionStatus.None;
		}

		Announced = status;
		return status;
	}

	/// <summary>
	/// <c>Mission_Status</c> (<c>004135e8</c>) — the whole judgement, in the original's order: the
	/// player's own condition first, then the mission box, then the objectives.
	///
	/// <para><b>This is where the computer speaks.</b> Four of the statuses carry a
	/// <c>SYSTEM.STR</c> line, and the post happens on the <i>change</i>: the running baseline is
	/// the last status announced, and after a post a <see cref="MessageLockout"/> countdown holds the
	/// answer still so the same line cannot be queued twice while the caller keeps polling. The
	/// baseline catches up on the first evaluation after that lockout expires, which is what
	/// sequences the spoken line ahead of the alert panel.</para>
	/// </summary>
	/// <param name="quiet">
	/// The original's third argument. Set, it skips the lockout, the mission-box arms and the message
	/// post, and just answers the question — what the player's own "how am I doing" key uses.
	/// </param>
	public MissionStatus Evaluate(SimWorld world, MechObject player, bool quiet) {
		if (!quiet && SimMath.CountdownTimerTick(ref _messageLockout) != 0) {
			SimMath.CountdownTimerTick(ref _messageLockout);
			return _heldStatus;
		}

		MissionStatus status;

		if (player.Destroyed) {
			status = MissionStatus.PlayerDestroyed;
		} else if (player.Immobilised) {
			status = MissionStatus.PlayerImmobilised;
		} else if (!quiet && IsOutsideMissionBox(world, player, RulesOfEngagementMargin)) {
			status = MissionStatus.RulesOfEngagementViolated;
		} else if (!quiet && IsOutsideMissionBox(world, player, 0)) {
			status = MissionStatus.LeavingMissionZone;
		} else {
			status = EvaluateObjectives(world, player);
		}

		Status = status;

		if (quiet) {
			return status;
		}

		if (_heldStatus != MissionStatus.None) {
			_announcedStatus = status;
			_heldStatus = MissionStatus.None;
		}

		int message = status == _announcedStatus ? -1 : MessageFor(status);

		if (message < 0) {
			_announcedStatus = status;
			return status;
		}

		world.Sounds?.Say(message);
		_messageLockout = MessageLockout;
		_heldStatus = _announcedStatus;
		return _announcedStatus;
	}

	/// <summary>
	/// <c>Mission_EvaluateObjectives</c> (<c>00413280</c>) — one walk of the record array, and the
	/// only place the objectives are read.
	///
	/// <para>Each record's condition is asked of its own subject and the answer is used twice: a
	/// mandatory record that answers no clears the "all met" flag and, if it is the first to do so,
	/// becomes <see cref="Outstanding"/>; a non-mandatory record that answers yes sets the "lost"
	/// flag. Either way an answer of yes applies that record's counters, once.</para>
	///
	/// <para>The three outcomes are then each split by whether the player is
	/// <see cref="IsClearOfThreats">clear</see>, and the not-clear side of all three is the same
	/// value: the mission does not conclude while the player is still in a fight.</para>
	/// </summary>
	private MissionStatus EvaluateObjectives(SimWorld world, MechObject player) {
		bool lost = false;
		bool allMet = true;
		bool satisfied = false;

		Outstanding = null;

		for (int i = 0; i < _objectives.Length; i++) {
			var objective = _objectives[i];

			// The original leaves the working register untouched for a subject kind past 3 and for
			// condition 5, which has no case in either of its two switches -- so such a record
			// silently reuses the previous record's answer. Carried rather than corrected: nothing
			// here should quietly disagree with the original about a mission that reaches it.
			if ((uint)objective.Record.SubjectKind <= (uint)MissionObjectiveSubject.Base) {
				satisfied = Test(world, player, objective);
			}

			objective.Satisfied = satisfied;

			if (objective.IsMandatory) {
				if (!satisfied && allMet) {
					allMet = false;
					Outstanding = objective;
				}
			} else if (satisfied) {
				lost = true;
			}

			if (satisfied) {
				objective.ApplyCounters(world);
			}
		}

		if (!IsClearOfThreats(world, player)) {
			return MissionStatus.DecidedButEngaged;
		}

		return lost ? MissionStatus.Failed
			: allMet ? MissionStatus.Complete
			: MissionStatus.InProgress;
	}

	/// <summary>
	/// One record's condition, asked of its own subject. The original writes the eleven cases out
	/// twice, once for a group subject and once for an object one; they are the same eleven questions
	/// and only the subject differs, so they are folded here.
	/// </summary>
	private static bool Test(SimWorld world, MechObject player, MissionObjectiveState objective) {
		var record = objective.Record;
		bool isGroup = record.SubjectKind == MissionObjectiveSubject.Group;
		var group = isGroup ? objective.SubjectGroup : objective.SubjectObject?.Group;
		var subject = objective.SubjectObject;

		switch (record.Condition) {
			case MissionObjective.ConditionOrderComplete:
				return group?.OrderCompletedForRoute(record.RouteRef) ?? false;

			case MissionObjective.ConditionLost:
				return isGroup
					? group is { } lostGroup && lostGroup.ConditionTier() >= WriteOffTier(lostGroup)
					: subject is { Neutralised: true };

			case MissionObjective.ConditionClear:
				return isGroup
					? group is { } safeGroup && IsGroupClear(world, safeGroup)
					: subject is { } safeObject && IsClearOfThreats(world, safeObject);

			case MissionObjective.ConditionDataLink:
			case MissionObjective.ConditionDataLinkAlso:
				return player.DataLinkComplete;

			case MissionObjective.ConditionNoDataLink:
			case MissionObjective.ConditionNoDataLinkAlso:
				return !player.DataLinkComplete;

			case MissionObjective.ConditionEngaged:
				return isGroup ? AnyMemberEngaged(group) : subject is { Engaged: true };

			case MissionObjective.ConditionUnengaged:
				return isGroup ? !AnyMemberEngaged(group) : subject is { Engaged: false };

			case MissionObjective.ConditionDisarmed:
				return isGroup ? AllMembersDisarmed(group) : subject is { } armed && IsDisarmed(armed);

			default:
				return false;
		}
	}

	/// <summary>
	/// <c>FUN_00413920</c> — the condition tier at which a group counts as written off, which is
	/// <b>not the same for both sides</b>: a human group is written off at tier 3 and a Cybrid one
	/// only at 4, destroyed outright. So "wipe out this Cybrid group" means every machine, and "this
	/// convoy did not make it" is answered a tier earlier.
	/// </summary>
	private static int WriteOffTier(MissionGroup group) =>
		group.Side == MissionSide.Cybrid ? MissionGroup.ConditionDestroyed : 3;

	/// <summary>
	/// <c>FUN_00413950</c> — a group is clear when it is not written off <i>and</i> every member
	/// still alive is itself clear. A single member with something on it answers no for the whole
	/// group.
	/// </summary>
	private static bool IsGroupClear(SimWorld world, MissionGroup group) {
		if (group.ConditionTier() >= WriteOffTier(group)) {
			return false;
		}

		var members = group.Members;
		for (int i = 0; i < members.Count; i++) {
			var member = members[i];
			if (!member.Destroyed && !IsClearOfThreats(world, member)) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// <c>Mission_IsClearOfThreats</c> (<c>004137b4</c>) — whether nothing hostile is both aware of
	/// <paramref name="subject"/> and near it. This is the escort objective's whole test, and it is
	/// also what gates every conclusive mission status.
	///
	/// <para>It walks the live object list and skips anything undeployed, on the subject's own side,
	/// or already out of the fight. What is left disqualifies the subject two ways:</para>
	///
	/// <list type="bullet">
	/// <item><description>it knows about the subject and is within <see cref="ThreatRange"/> — or
	/// <see cref="DesignatedThreatRange"/>, the wider of the two, when the subject is the thing it is
	/// currently shooting at; or</description></item>
	/// <item><description><b>for a subject that is not on the player's own group</b>, its group's
	/// current order names the subject and it is either still on its way (its route has somewhere
	/// left to go) or already knows where the subject is. An assigned hunter counts as a threat at
	/// any range, which is what stops an escort being called safe while something is walking towards
	/// it.</description></item>
	/// </list>
	/// </summary>
	public static bool IsClearOfThreats(SimWorld world, SimObject subject) {
		if (subject.Group is not { } subjectGroup) {
			return true;
		}

		var objects = world.Objects;
		bool subjectIsPlayerGroup = ReferenceEquals(world.PlayerMech?.Group, subjectGroup);

		for (int i = 0; i < objects.Count; i++) {
			var other = objects[i];

			if (other.Removed || other.AwaitingDeployment || other.Group is not { } otherGroup
					|| otherGroup.Side == subjectGroup.Side || other.OutOfAction) {
				continue;
			}

			bool knows = Ai.AiTargeting.Knows(other, subject);

			if (!subjectIsPlayerGroup && otherGroup.IsOrderTarget(subject)
					&& (otherGroup.WaypointAt(otherGroup.RouteCursor + 1) != null || knows)) {
				return false;
			}

			if (knows) {
				int range = ReferenceEquals(SelectedTargetOf(other), subject)
					? DesignatedThreatRange
					: ThreatRange;

				if (subject.Position.ApproxDistanceTo(other.Position) < range) {
					return false;
				}
			}
		}

		return true;
	}

	/// <summary><c>FUN_00412d90</c> — any member of the group has been engaged.</summary>
	private static bool AnyMemberEngaged(MissionGroup? group) {
		if (group == null) {
			return false;
		}

		for (int i = 0; i < group.Members.Count; i++) {
			if (group.Members[i].Engaged) {
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// <c>FUN_00412c58</c> — <b>every</b> member of the group is disarmed. An empty group answers
	/// yes, which is the original's own loop shape: it returns 1 when the walk runs off the end.
	/// </summary>
	private static bool AllMembersDisarmed(MissionGroup? group) {
		if (group == null) {
			return false;
		}

		for (int i = 0; i < group.Members.Count; i++) {
			if (!IsDisarmed(group.Members[i])) {
				return false;
			}
		}

		return true;
	}

	/// <summary><c>obj+0xa5</c> on its own — only the two machine classes can carry it.</summary>
	private static bool IsDisarmed(SimObject subject) => subject switch {
		MechObject mech => mech.Disarmed,
		FlyerObject flyer => flyer.Disarmed,
		_ => false
	};

	/// <summary>
	/// <c>obj+0x1a4</c> — what this object is shooting at. Declared on the two classes that can hold
	/// one rather than on <see cref="SimObject"/>, so the threat sweep asks for it here.
	/// </summary>
	private static SimObject? SelectedTargetOf(SimObject subject) => subject switch {
		MechObject mech => mech.Target,
		FlyerObject flyer => flyer.Target,
		_ => null
	};

	/// <summary>
	/// <c>FUN_0041373c</c> — whether a position is outside the mission's bounding box, grown by
	/// <paramref name="margin"/> on every side. The box is block 1's own extent, accumulated as the
	/// coordinates are read; the Heads-Down Display's map is framed by the same one.
	/// </summary>
	private static bool IsOutsideMissionBox(SimWorld world, SimObject subject, int margin) {
		var box = world.MissionBounds;

		if (box.IsEmpty) {
			return false;
		}

		return !(box.MinX - margin < subject.Position.X && subject.Position.X < box.MaxX + margin
			&& box.MinY - margin < subject.Position.Y && subject.Position.Y < box.MaxY + margin);
	}

	/// <summary>Whether a status is one the table at <c>0049935c</c> marks worth interrupting for.</summary>
	private static bool RaisesAlert(MissionStatus status) => status is MissionStatus.PlayerDestroyed
		or MissionStatus.PlayerImmobilised or MissionStatus.Failed or MissionStatus.LeavingMissionZone
		or MissionStatus.RulesOfEngagementViolated or MissionStatus.Complete;

	/// <summary>
	/// The <c>SYSTEM.STR</c> line a status change speaks, or <c>-1</c> for one that says nothing.
	/// Four of the nine carry one; the two that say nothing but are still alert-worthy — the player's
	/// machine destroyed and immobilised — are the two the alert panel itself is about.
	/// </summary>
	private static int MessageFor(MissionStatus status) => status switch {
		MissionStatus.Failed => SystemMessages.MissionFailed,
		MissionStatus.LeavingMissionZone => SystemMessages.ApproachingZoneBoundary,
		MissionStatus.RulesOfEngagementViolated => SystemMessages.RulesOfEngagementViolated,
		MissionStatus.Complete => SystemMessages.MissionSuccessful,
		_ => -1
	};

	/// <summary>
	/// How far beyond the mission box counts as abandoning the mission rather than drifting out of
	/// it — <c>FUN_00413780</c>'s literal, and the difference between the two boundary statuses.
	/// </summary>
	public const int RulesOfEngagementMargin = 110000;

	/// <summary>
	/// Range at which a hostile that knows about a subject disqualifies it from being clear —
	/// <c>Mission_IsClearOfThreats</c>'s 80000, about 480 m.
	/// </summary>
	public const int ThreatRange = 80000;

	/// <summary>The wider range used when the subject is that hostile's own current target.</summary>
	public const int DesignatedThreatRange = 100000;

	/// <summary>
	/// How long the poll waits between readings once it has one that is not worth raising —
	/// <c>DAT_004a9ee6</c>'s re-arm, in milliseconds.
	/// </summary>
	public const short PollInterval = 10000;

	/// <summary>
	/// How long an alert-worthy status has to stand before the poll hands it up —
	/// <c>DAT_004a9ee9</c>'s arm, in milliseconds. Armed once and never re-armed.
	/// </summary>
	public const short AlertDelay = 10000;

	/// <summary>
	/// How long <see cref="Evaluate"/> holds its answer still after posting a message, in
	/// milliseconds — <c>DAT_004a9bec</c>'s arm.
	/// </summary>
	public const short MessageLockout = 500;

	private short _pollInterval;
	private short _alertDelay;
	private bool _alertArmed;
	private short _messageLockout;
	private MissionStatus _announcedStatus;
	private MissionStatus _heldStatus = MissionStatus.None;
}
