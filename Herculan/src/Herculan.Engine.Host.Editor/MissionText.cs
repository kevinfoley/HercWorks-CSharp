using Herculan.Engine.Scene;
using Herculan.Engine.World;

namespace Herculan.Engine.Host.Editor;

/// <summary>
/// How the editor names mission records in its panels. The meanings are the simulation's — each
/// table cites where its codes are documented — and only the wording is the editor's.
/// </summary>
internal static class MissionText {
	/// <summary>An object, by type and the roster slot the mission's refs name it by.</summary>
	public static string Describe(SceneObject placed) {
		var placement = placed.Placement;
		string type = placement.TypeName ?? $"{placement.Kind} type {placement.TypeIndex}";
		string where = placement.IsPlayerLance
			? "player squad"
			: $"{placement.Kind.ToString().ToLowerInvariant()} slot {placement.SlotIndex}";
		return $"{type} ({where})";
	}

	/// <summary>A group: its index, roster and side.</summary>
	public static string DescribeGroup(Mission mission, int group) {
		if (group == 0) {
			return "Group 0 (player squad)";
		}

		string kind = group < mission.GroupKinds.Count ? mission.GroupKinds[group].ToString() : "?";
		string side = group < mission.GroupSides.Count ? mission.GroupSides[group].ToString() : "?";
		return $"Group {group} ({kind}, {side})";
	}

	/// <summary>
	/// Whose position an action's trigger areas are offered — the type table in
	/// docs/retail/simulation/mission-deployment.md#trigger-areas--actions_evaluatetriggers-00426b70.
	/// </summary>
	public static string TriggerSubject(short type) => type switch {
		MissionAction.SubjectPlayer => "the player's machine",
		MissionAction.SubjectPlayerGroup => "any machine of the player's group",
		MissionAction.SubjectHumanGroups => "any member of a deployed human group",
		MissionAction.SubjectCybridGroups => "any member of a deployed Cybrid group",
		MissionAction.SubjectMechGroups => "any member of a deployed mech group",
		MissionAction.SubjectFlyerGroups => "any member of a deployed flyer group",
		MissionAction.SubjectBaseGroups => "any member of a deployed structure group",
		MissionAction.SubjectTargetMech => "its target mech",
		MissionAction.SubjectTargetFlyer => "its target flyer",
		MissionAction.SubjectTargetBase => "its target structure",
		MissionAction.SubjectTargetGroup => "any member of its target group",
		_ => $"nothing (type {type})"
	};

	/// <summary>
	/// How a group waiting on an action arrives — the verb table in
	/// docs/retail/simulation/mission-deployment.md#arrival--group_deploymentcheck-004236c4.
	/// </summary>
	public static string Arrival(short verb) => verb switch {
		MissionAction.VerbPodWide => "drop pod, within 90° of the player's heading",
		MissionAction.VerbPodNarrow => "drop pod, within 22.5° of the player's heading",
		MissionAction.VerbWalkBehind => "on foot, from behind the player",
		MissionAction.VerbWalkAhead => "on foot, ahead of the player",
		_ => "in place"
	};

	/// <summary>The state an order verb installs — docs/retail/simulation/ai-goals.md#when-an-order-is-finished--group_isordercomplete-004239fc.</summary>
	public static string OrderVerb(short verb) => verb switch {
		MissionOrder.VerbSearchDestroy => "search/destroy",
		MissionOrder.VerbRam => "ramming",
		MissionOrder.VerbGuard => "guarding",
		MissionOrder.VerbPatrol => "patrolling",
		MissionOrder.VerbSleep => "sleeping",
		MissionOrder.VerbTravel => "travelling",
		MissionOrder.VerbFollow => "following",
		_ => $"verb {verb}"
	};

	/// <summary>What one of an action's counter operations does to its counter.</summary>
	public static string ActionCounterOp(short op) => op switch {
		MissionAction.CounterIncrement => "+1",
		MissionAction.CounterClear => "cleared",
		_ => $"op {op}, no effect"
	};

	/// <summary>A distance in world units, with metres beside it.</summary>
	public static string Distance(int worldUnits) =>
		$"{worldUnits} ({worldUnits / Render.WorldScale.WorldUnitsPerMeter:F0} m)";
}
