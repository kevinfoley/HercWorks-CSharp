using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// A mission group — the in-memory record <c>DBSim_BuildGroupRecord</c> (<c>00423b34</c>) builds
/// from one <c>script.dat</c> block-11 entry, and the thing every object points at through
/// <c>obj+0x45</c>.
///
/// <para><b>The group, not the object, is what the AI is driven from.</b>
/// <c>Mech_AiTick</c>'s sole caller is <c>Group_OrderTick</c> (<c>00423a74</c>), which runs it over
/// every member of a group that has entered the mission — so a machine that is not a live group
/// member never thinks at all. See docs/simulation/ai-dispatch.md, "The AI tick".</para>
///
/// <para><b>The order array is not here.</b> The record also carries the group's row-15 orders at
/// <c>group+0x44</c> and the current index at <c>group+0x6c</c>, which is what
/// <c>Mech_AiSelectBehaviour</c> reads to give a machine its state and what
/// <see cref="OrderTarget"/> would name. That layer is decoded but unported (see ROADMAP.md, "Group
/// orders"), so this record answers exactly as the original does for a group whose current order
/// entry is null: <see cref="OrderVerb"/> is <see cref="NoOrder"/> and nothing is designated. Every
/// consumer in <see cref="Ai.AiTargeting"/> already has that case, because the original has it.</para>
/// </summary>
public sealed class MissionGroup {
	/// <summary>
	/// The verb <c>Mech_AiSelectBehaviour</c> substitutes for a null order entry. It matches no case
	/// in either the state map or <see cref="Ai.AiTargeting.SelectTarget"/>'s designation test.
	/// </summary>
	public const short NoOrder = 0x0b;

	public MissionGroup(int index, MissionSide side) {
		Index = index;
		Side = side;
	}

	/// <summary>Which block-11 record this is, which is also what a placement's group index names.</summary>
	public int Index { get; }

	/// <summary><c>group+0x12</c> — the side every member of the group is on.</summary>
	public MissionSide Side { get; }

	/// <summary>
	/// <c>group+0x0c</c>/<c>+0x10</c> — the member array and its count, in attachment order.
	/// </summary>
	public IReadOnlyList<SimObject> Members => _members;

	/// <summary>
	/// The group's first member, <c>**(group+0x0c)</c>. The AI reads it for two decisions and both
	/// only ask whether it is the local player: whether this group is the player's own squad (which
	/// keeps its radar passive) and whether a machine is the one that drags the rest into a fight.
	/// </summary>
	public SimObject? Leader => _members.Count > 0 ? _members[0] : null;

	/// <summary>Whether the group is led by the machine the player is flying.</summary>
	public bool LedByPlayer => Leader is { LocallyPiloted: true };

	/// <summary>
	/// <c>group+0x44[group+0x6c]</c>'s first <c>short</c> — the current order's verb, or
	/// <see cref="NoOrder"/> when the entry is null, which is the only answer this record can give
	/// until the order layer is ported.
	/// </summary>
	public short OrderVerb => NoOrder;

	/// <summary>
	/// <c>Group_OrderTargetObject</c> (<c>004238a0</c>) — the object the current order names, or null.
	/// Always null here; see the class remarks.
	/// </summary>
	public SimObject? OrderTarget => null;

	/// <summary>
	/// <c>Group_IsOrderTarget</c> (<c>00423918</c>) — whether an object is what the current order
	/// names, by group identity for a group-valued order and by object identity otherwise.
	/// </summary>
	public bool IsOrderTarget(SimObject candidate) =>
		OrderTarget is { } target
			&& (ReferenceEquals(target, candidate) || ReferenceEquals(target.Group, candidate.Group));

	/// <summary>
	/// <c>FUN_00423974</c> — the live member nearest a given object, within 100000 units, excluding
	/// that object itself. The raycast's friendly-fire path uses it to pick who complains when the
	/// player shoots someone else's machine.
	/// </summary>
	public SimObject? NearestLiveMember(SimObject excluding) {
		SimObject? best = null;
		int bestDistance = 100000;

		for (int i = 0; i < _members.Count; i++) {
			var member = _members[i];
			if (ReferenceEquals(member, excluding) || member.Neutralised) {
				continue;
			}

			int distance = member.Position.ApproxDistanceTo(excluding.Position);
			if (distance < bestDistance) {
				best = member;
				bestDistance = distance;
			}
		}

		return best;
	}

	/// <summary>
	/// Attaches an object to the group, in the order the mission places them — the first one attached
	/// is the <see cref="Leader"/>.
	/// </summary>
	public void Add(SimObject member) {
		_members.Add(member);
		member.Group = this;
	}

	/// <summary>
	/// <c>Group_OrderTick</c> (<c>00423a74</c>), reduced to the half that exists: it runs the AI over
	/// every member. The other half — advancing the group through its row-15 orders, and zeroing
	/// every member's dwell countdown when it does — belongs to the unported order layer.
	/// </summary>
	public void AiTick(SimWorld world) {
		for (int i = 0; i < _members.Count; i++) {
			if (_members[i] is MechObject { Removed: false, AwaitingDeployment: false } mech) {
				mech.AiTick(world);
			}
		}
	}

	private readonly List<SimObject> _members = new();
}
