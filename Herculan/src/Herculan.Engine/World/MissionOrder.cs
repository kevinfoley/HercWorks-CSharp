using Herculan.Engine.Numerics;

namespace Herculan.Engine.World;

/// <summary>
/// What kind of thing a <see cref="MissionOrder"/> names — the order record's <c>+0x0c</c>
/// discriminator, which <c>Mission_ResolveRefByKind</c> (<c>00425348</c>) uses to turn the ref
/// beside it into a pointer.
/// </summary>
public enum MissionOrderSubject {
	/// <summary>The order names nothing.</summary>
	None = -1,

	/// <summary>Another mission group, by block-11 record index.</summary>
	Group = 0,

	/// <summary>One HERC, by block-7 roster slot.</summary>
	Mech = 1,

	/// <summary>One flyer, by block-8 roster slot.</summary>
	Flyer = 2,

	/// <summary>One structure, by block-9 roster slot.</summary>
	Base = 3
}

/// <summary>
/// One of the ten orders a mission group works through — the 22-byte record
/// <c>DBSim_SpawnMissionObjects</c> (<c>004253d8</c>) builds from one <c>script.dat</c> block-10
/// entry, with each of its refs resolved. See docs/simulation/ai-goals.md for the layout and for
/// what each verb means; <see cref="Sim.MissionGroup"/> is what runs them.
///
/// <para>The two record fields with no reader — the order's own point and the short beside the verb
/// — are not carried here.</para>
/// </summary>
/// <param name="Verb">
/// Record <c>+0x00</c>, 0-6. <c>Mech_AiSelectBehaviour</c> maps it onto a behaviour state and
/// <c>Group_IsOrderComplete</c> onto a completion test; both are keyed by the raw number, so it is
/// kept as one rather than turned into an enum that would need a name for each of the two roles.
/// </param>
/// <param name="SubjectKind">Record <c>+0x0c</c> — what <paramref name="SubjectRef"/> indexes.</param>
/// <param name="SubjectRef">
/// Record <c>+0x0e</c> before resolution: a block-11 group index for
/// <see cref="MissionOrderSubject.Group"/>, otherwise a roster slot. <c>-1</c> when the order names
/// nothing.
/// </param>
/// <param name="Route">
/// Record <c>+0x08</c> resolved to block-1 points. Only slot 0's is ever installed as the group's
/// route; see <see cref="Sim.MissionGroup.Route"/>.
/// </param>
/// <param name="GatedOnAction">
/// Whether record <c>+0x12</c> names a block-5 action. When it fires the group moves to its next
/// order whether or not this one finished. No mission action is ported, so it never does.
/// </param>
public sealed record MissionOrder(
	short Verb,
	MissionOrderSubject SubjectKind,
	int SubjectRef,
	IReadOnlyList<Vec3i> Route,
	bool GatedOnAction) {

	/// <summary>Hunt down what the order names — installs <c>search/destroy</c>.</summary>
	public const short VerbSearchDestroy = 0;

	/// <summary>Installs <c>ramming</c>.</summary>
	public const short VerbRam = 1;

	/// <summary>Installs <c>guarding</c>.</summary>
	public const short VerbGuard = 2;

	/// <summary>Installs <c>patrolling</c>.</summary>
	public const short VerbPatrol = 3;

	/// <summary>Installs <c>sleeping</c>.</summary>
	public const short VerbSleep = 4;

	/// <summary>Installs <c>travelling</c>, or <c>bulldog travel</c> on a chassis that never shipped.</summary>
	public const short VerbTravel = 5;

	/// <summary>Installs <c>following</c>.</summary>
	public const short VerbFollow = 6;

	/// <summary>
	/// The verb <c>Mech_AiSelectBehaviour</c> substitutes for a null order slot. It matches no case
	/// in the state map, in <c>Group_IsOrderComplete</c>, or in
	/// <see cref="Sim.Ai.AiTargeting.SelectTarget"/>'s designation test.
	/// </summary>
	public const short VerbNone = 0x0b;

	/// <summary>How many orders a block-11 record can name.</summary>
	public const int Slots = 10;
}
