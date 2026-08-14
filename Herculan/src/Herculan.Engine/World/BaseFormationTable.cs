using System.Buffers.Binary;
using Herculan.Engine.Content;

namespace Herculan.Engine.World;

/// <summary>An unrotated (x, y) spread offset for one follower slot of a base formation.</summary>
public readonly record struct BaseFormationOffset(int X, int Y);

/// <summary>
/// <c>dat\BFORMS.DAT</c> — per-formation spread offsets for a <c>script.dat</c> block-11 base/
/// structure group. This is what stops a multi-structure group (a fortress cluster, a turret ring)
/// from placing every member on the group's single point.
///
/// <para>Read from the base-group-attach chain: <c>DBSim_BuildGroupRecord</c> (<c>00423b34</c>)
/// carries the group's formation id (script.dat block-11's <c>SmallDiscrete</c> field, raw msn
/// offset <c>0x30</c> — see <c>docs/formats/msn-mission-file.md</c>'s row #16 decode) into
/// <c>FUN_00405c3c</c>, which stores it at the attached object's group-relative member index
/// (<c>+0x49</c>, the object's position within the group's <c>DiscriminatedRefs</c> array — <b>not</b>
/// a compacted live-member count) and then unconditionally calls the object's own vtable
/// <c>+0x78</c>. For every base subtype's vtable that slot is <c>FUN_00405c04</c>, which — when the
/// member index is nonzero, i.e. every member but the group's first-claimed ("leader") slot — looks
/// up this table's <c>(formationId, memberIndex-1)</c> entry via <c>FUN_00405b9c</c> and rotates it
/// into world space by the leader's heading (<c>Formation_RotateAndAddOffset</c>, <c>00411d64</c>)
/// before adding it to the group's position. This exactly mirrors <c>Mech_ApplyFormationOffset</c>
/// (<c>00417898</c>) — same vtable slot, same "member index 0 gets no offset" rule — except this
/// table's load site <b>is</b> confirmed (unlike the mech/flyer tables, still open — see
/// <c>MissionLoader</c>'s doc comment), because <c>FUN_00405fac</c> demonstrably streams it from a
/// file literally named <c>"bforms"</c>. Confirmed byte-exact: the retail file is 3,186 content
/// bytes, walking it as modelled below consumes exactly that with nothing left over, its declared
/// formation count is 17 (matching the block-11 formation id field's documented 0-16 range), and
/// formation 0's seven offsets are a visibly symmetric wedge — one point dead ahead, three mirrored
/// left/right pairs behind it.
///
/// <para><b>Not modelled: grid-snap.</b> When the block-11 record's own <c>BinaryFlag</c> (raw msn
/// offset <c>0x06</c>) is set, <c>FUN_00405c3c</c> additionally snaps the group's shared anchor point
/// to a per-formation grid (using three more fields this table skips — a cell-size class and two
/// axis multipliers) <i>before</i> the per-member offset below is added. That step moves the whole
/// group's anchor by one shared amount, so it does not cause members to stack on each other — the
/// bug this table exists to fix — and it wasn't chased further.</para>
/// </summary>
public sealed class BaseFormationTable {
	/// <summary>VOL folder and name of the table.</summary>
	public const string ResourceFolder = "dat";

	/// <summary>The table's resource name.</summary>
	public const string ResourceName = "BFORMS.DAT";

	private readonly BaseFormationOffset[][] _formations;

	private BaseFormationTable(BaseFormationOffset[][] formations) {
		_formations = formations;
	}

	/// <summary>How many formations the table declares.</summary>
	public int Count => _formations.Length;

	/// <summary>
	/// The spread offset for a group's <paramref name="memberIndex"/>-th member (its position within
	/// the claiming block-11 record's <c>DiscriminatedRefs</c> array), or null when the slot takes no
	/// offset — member index 0 (the group's first-claimed member), an out-of-range formation id, or a
	/// formation with fewer follower slots than this index needs.
	/// </summary>
	public BaseFormationOffset? OffsetFor(int formationId, int memberIndex) {
		if (memberIndex <= 0 || formationId < 0 || formationId >= _formations.Length) {
			return null;
		}

		var slots = _formations[formationId];
		int slotIndex = memberIndex - 1;
		return slotIndex < slots.Length ? slots[slotIndex] : null;
	}

	public static BaseFormationTable Load(GameContent content) {
		byte[] bytes = content.ReadRequired(ResourceFolder, ResourceName);
		int offset = 0;

		int NextInt32() {
			int value = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
			offset += 4;
			return value;
		}

		int formationCount = NextInt32();
		var formations = new BaseFormationOffset[formationCount][];

		for (int f = 0; f < formationCount; f++) {
			int slotCount = NextInt32();
			var slots = new BaseFormationOffset[slotCount];
			for (int s = 0; s < slotCount; s++) {
				int x = NextInt32();
				int y = NextInt32();
				offset += 2; // trailing short per slot entry — unused by the offset lookup
				slots[s] = new BaseFormationOffset(x, y);
			}
			formations[f] = slots;

			offset += 4; // grid-snap "cell size class" field — see the grid-snap note above
			offset += 8; // grid-snap axis-multiplier pair
			int bufferCount = NextInt32();
			for (int b = 0; b < bufferCount; b++) {
				byte length = bytes[offset];
				offset += 1 + length;
			}
		}

		if (offset != bytes.Length) {
			throw new InvalidDataException(
				$"{ResourceFolder}\\{ResourceName}: walked {offset} of {bytes.Length} bytes across " +
				$"{formationCount} formations — the record shape does not match this file.");
		}

		return new BaseFormationTable(formations);
	}
}
