using Herculan.Engine.Content;

namespace Herculan.Engine.Sim;

/// <summary>
/// The think slot of <c>player</c> and <c>player fly</c> — <c>Mech_BehaviourPlayerThink</c>
/// (<c>0041c194</c>). The machine the player is flying holds a behaviour state like any other, and
/// its think decides nothing about movement: it is where the simulation watches the player's own
/// progress through the mission. The derivation is docs/simulation/player-waypoints.md.
/// </summary>
public partial class MechObject {
	/// <summary>
	/// <c>Mech_BehaviourPlayerThink</c> (<c>0041c194</c>). Everything it does is gated on the machine
	/// being its group's <b>leader</b>, which for the player's own squad it always is, and it always
	/// returns zero — the player's state never ends itself.
	///
	/// <para><b>The waypoint arm, ported here.</b> Identical in shape to
	/// <see cref="FollowRoute"/>'s: the route cursor names the last waypoint reached, so the one being
	/// walked at is the one after it, and reaching it inside <see cref="ArrivalRange"/> on the ground
	/// plane steps the cursor. The player's arrival differs from an AI machine's in two ways — it
	/// announces itself on the computer's message port, and it refuses the step when the waypoint
	/// ahead is a closed route's terminating duplicate (<see cref="MissionGroup.NextWaypointClosesRoute"/>),
	/// so a patrol circuit stops announcing rather than repeating its first waypoint forever.</para>
	///
	/// <para><b>What the original does here besides.</b> Three further arms, switched on a
	/// mission-objective global, watch for the player detecting an order target, closing on a goal
	/// position, and standing still beside a data-link subject long enough to run the four-message
	/// <c>ENGAGING DATA LINK</c> → <c>DATA TRANSFER COMPLETE</c> sequence. They belong to the mission
	/// objective layer, which is not ported; none of them touches the route.</para>
	/// </summary>
	private bool PlayerThink(SimWorld world) {
		if (Group is not { } group || !ReferenceEquals(group.Leader, this)) {
			return false;
		}

		if (group.WaypointAt(group.RouteCursor + 1) is { } next
				&& GroundDistanceTo(next) < ArrivalRange
				&& !group.NextWaypointClosesRoute) {
			world.Sounds?.Say(SystemMessages.WaypointReached);
			group.AdvanceRouteCursor();
		}

		return false;
	}
}
