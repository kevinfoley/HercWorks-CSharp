using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Scene;
using Herculan.Engine.World;

namespace Herculan.Engine.Host.Editor;

/// <summary>
/// One route the mission's orders name — a <c>script.dat</c> block-3 waypoint group, resolved to
/// points.
/// </summary>
/// <param name="Reference">The block-3 record index.</param>
/// <param name="Points">Its waypoints, in order.</param>
/// <param name="WalkedBy">
/// The groups whose order slot 0 names it — the only route a group ever walks, see
/// docs/retail/simulation/ai-goals.md#the-route-cursor-is-loaded-once.
/// </param>
/// <param name="NamedBy">The groups that name it only in a later order slot, which no group walks.</param>
internal sealed record EditorRoute(int Reference, IReadOnlyList<Vec3i> Points, IReadOnlyList<int> WalkedBy,
	IReadOnlyList<int> NamedBy) {

	/// <summary>
	/// Whether the route closes on itself — its last waypoint is its first — and so is walked as a loop
	/// that never ends. Compared by position, as the simulation's route cursor compares it (see
	/// <see cref="Sim.MissionGroup.RouteCursor"/>).
	/// </summary>
	public bool Closed => Points.Count > 1 && Points[^1] == Points[0];

	/// <summary>Whether any group walks it.</summary>
	public bool Walked => WalkedBy.Count > 0;
}

/// <summary>One order slot of one group.</summary>
internal readonly record struct OrderSlot(int Group, int Slot, MissionOrder Order);

/// <summary>
/// Everything that activates one mission action, and everything that waits on it — the four
/// activation routes and the three consumers in docs/retail/simulation/mission-deployment.md.
/// </summary>
internal sealed record ActionLinks(
	IReadOnlyList<int> TimersFiringIt,
	IReadOnlyList<int> TimersItStarts,
	IReadOnlyList<SceneObject> EngagedObjects,
	IReadOnlyList<SceneObject> DefeatedObjects,
	IReadOnlyList<int> WaitingGroups,
	IReadOnlyList<OrderSlot> OrdersItEnds);

/// <summary>
/// The editor's cross-reference over one loaded mission, built once: which actions name which
/// trigger area, which groups walk which route, what fires each action and what each one sets off.
/// Nothing in a mission moves while the editor shows it, so none of this is recomputed per frame.
/// </summary>
internal sealed class MissionIndex {
	private readonly Dictionary<(MissionUnitKind, int), SceneObject> _bySlot = new();
	private readonly ActionLinks[] _actionLinks;

	public MissionIndex(MissionScene scene, SquadMessages? commandMessages) {
		Mission = scene.Mission;
		CommandMessages = commandMessages;

		var members = new List<SceneObject>[Mission.GroupKinds.Count];
		for (int i = 0; i < members.Length; i++) {
			members[i] = new List<SceneObject>();
		}

		foreach (var placed in scene.Objects) {
			if (placed.Placement.GroupIndex >= 0 && placed.Placement.GroupIndex < members.Length) {
				members[placed.Placement.GroupIndex].Add(placed);
			}

			if (placed.Placement.SlotIndex >= 0) {
				_bySlot[(placed.Placement.Kind, placed.Placement.SlotIndex)] = placed;
			}
		}

		GroupMembers = members;
		Areas = BuildAreas(Mission);
		Routes = BuildRoutes(Mission);

		_actionLinks = new ActionLinks[Mission.Actions.Count];
		for (int i = 0; i < _actionLinks.Length; i++) {
			_actionLinks[i] = BuildLinks(i, scene.Objects);
		}
	}

	public Mission Mission { get; }

	/// <summary>
	/// The speakerless message set an action's message is looked up in — <c>COMMAND&lt;n&gt;.STR</c>,
	/// see <see cref="MissionAction.MessageId"/> — or null when the install has none.
	/// </summary>
	public SquadMessages? CommandMessages { get; }

	/// <summary>Each group's placed members, by block-11 record index.</summary>
	public IReadOnlyList<IReadOnlyList<SceneObject>> GroupMembers { get; }

	/// <summary>
	/// Every trigger area some action tests, once each, in block-4 order. An area no action names, or
	/// that sits behind its action's first unset slot, is never tested and is not listed.
	/// </summary>
	public IReadOnlyList<MissionTriggerArea> Areas { get; }

	/// <summary>Every route some order names, once each, in block-3 order.</summary>
	public IReadOnlyList<EditorRoute> Routes { get; }

	/// <summary>The route the player's squad walks, or null.</summary>
	public EditorRoute? PlayerRoute => Routes.FirstOrDefault(route => route.WalkedBy.Contains(0));

	public MissionTriggerArea? Area(int reference) {
		foreach (var area in Areas) {
			if (area.Reference == reference) {
				return area;
			}
		}

		return null;
	}

	public EditorRoute? Route(int reference) => Routes.FirstOrDefault(route => route.Reference == reference);

	/// <summary>The actions that test <paramref name="areaReference"/>, in block-5 order.</summary>
	public IEnumerable<int> ActionsTesting(int areaReference) {
		for (int i = 0; i < Mission.Actions.Count; i++) {
			if (Mission.Actions[i].Areas.Any(area => area.Reference == areaReference)) {
				yield return i;
			}
		}
	}

	public ActionLinks LinksOf(int action) => _actionLinks[action];

	/// <summary>
	/// The placed object a mission ref names — an order, objective or action subject — or null when
	/// nothing was placed in that slot.
	/// </summary>
	public SceneObject? ObjectAt(MissionUnitKind kind, int slot) => _bySlot.GetValueOrDefault((kind, slot));

	private static List<MissionTriggerArea> BuildAreas(Mission mission) {
		var byReference = new SortedDictionary<int, MissionTriggerArea>();
		foreach (var action in mission.Actions) {
			foreach (var area in action.Areas) {
				byReference.TryAdd(area.Reference, area);
			}
		}

		return byReference.Values.ToList();
	}

	private static List<EditorRoute> BuildRoutes(Mission mission) {
		var points = new SortedDictionary<int, IReadOnlyList<Vec3i>>();
		var walkedBy = new Dictionary<int, List<int>>();
		var namedBy = new Dictionary<int, List<int>>();

		for (int group = 0; group < mission.GroupOrders.Count; group++) {
			var orders = mission.GroupOrders[group];
			for (int slot = 0; slot < orders.Count; slot++) {
				if (orders[slot] is not { RouteRef: >= 0 } order || order.Route.Count == 0) {
					continue;
				}

				points.TryAdd(order.RouteRef, order.Route);
				var into = slot == 0 ? walkedBy : namedBy;
				if (!into.TryGetValue(order.RouteRef, out var groups)) {
					into[order.RouteRef] = groups = new List<int>();
				}

				if (!groups.Contains(group)) {
					groups.Add(group);
				}
			}
		}

		return points
			.Select(entry => {
				var walked = walkedBy.GetValueOrDefault(entry.Key) ?? new List<int>();
				var named = (namedBy.GetValueOrDefault(entry.Key) ?? new List<int>()).Except(walked).ToList();
				return new EditorRoute(entry.Key, entry.Value, walked, named);
			})
			.ToList();
	}

	private ActionLinks BuildLinks(int action, IReadOnlyList<SceneObject> objects) {
		var firing = new List<int>();
		var starts = new List<int>();
		for (int i = 0; i < Mission.ActionTimers.Count; i++) {
			var timer = Mission.ActionTimers[i];
			if (timer.SequenceRefs.Any(reference => reference == action)) {
				firing.Add(i);
			}

			if (timer.PrimaryActionRef == action) {
				starts.Add(i);
			}
		}

		var waiting = new List<int>();
		for (int group = 0; group < Mission.GroupDeploymentActions.Count; group++) {
			if (Mission.GroupDeploymentActions[group] == action) {
				waiting.Add(group);
			}
		}

		var ends = new List<OrderSlot>();
		for (int group = 0; group < Mission.GroupOrders.Count; group++) {
			var orders = Mission.GroupOrders[group];
			for (int slot = 0; slot < orders.Count; slot++) {
				if (orders[slot] is { } order && order.ActionRef == action) {
					ends.Add(new OrderSlot(group, slot, order));
				}
			}
		}

		return new ActionLinks(
			firing,
			starts,
			objects.Where(o => o.Placement.EngagementActionRef == action).ToList(),
			objects.Where(o => o.Placement.DefeatActionRef == action).ToList(),
			waiting,
			ends);
	}
}
