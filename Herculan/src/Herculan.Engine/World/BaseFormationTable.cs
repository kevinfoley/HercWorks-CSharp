using System.Buffers.Binary;
using Herculan.Engine.Content;

namespace Herculan.Engine.World;

/// <summary>
/// One follower slot of a base formation: an unrotated (x, y) spread offset, and how far the
/// structure standing there is turned relative to the rest of its group.
/// </summary>
/// <param name="HeadingNudge">
/// Binary-angle turn added to the group's heading for this slot — the trailing <c>int16</c> of the
/// 10-byte slot record. Every nonzero value in the retail table is a clean quarter turn: 8190 (45
/// degrees), 16380 (90), 32760 (180) or their negatives. See
/// <see cref="BaseFormationTable.HeadingNudgeFor"/>.
/// </param>
public readonly record struct BaseFormationOffset(int X, int Y, short HeadingNudge);

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
/// before adding it to the group's position — mirrors <c>Mech_ApplyFormationOffset</c>
/// (<c>00417898</c>): same vtable slot, same "member index 0 gets no offset" rule. Load site:
/// <c>FUN_00405fac</c> streams the table from a file literally named <c>"bforms"</c>. Byte-exact: the
/// retail file is 3,186 content bytes, formation count 17 (matches block-11 formation id's 0-16
/// range), formation 0's seven offsets a symmetric wedge — one point ahead, three mirrored pairs
/// behind.</para>
///
/// <para><b>A slot turns its structure as well as placing it</b> — see
/// <see cref="HeadingNudgeFor"/>. That half arrives by a different path (<c>Base_AttachToGroup</c>
/// itself, not the vtable slot above) and was missed when this table was first read, which left one
/// structure of a group standing in the right place facing the wrong way.</para>
///
/// <para><b>Not modelled: grid-snap.</b> When the block-11 record's own <c>BinaryFlag</c> (raw msn
/// offset <c>0x06</c>) is set, <c>Base_AttachToGroup</c> additionally snaps the group's shared anchor
/// to a per-formation grid, using three fields this table skips (a cell-size class and two axis
/// multipliers), before the per-member offset above is added — doesn't cause stacking, so left
/// unimplemented. A 2026-08-15 port attempt, decompiled and formula-matched against
/// <c>Base_AttachToGroup</c>, shipped a real regression: verified stacking-only checks passed but the
/// snap moved real structures tens of thousands of world units off their intended pads (visually
/// confirmed against the mission editor) and was reverted same-day. The bit-mask formula as literally
/// decompiled produces this; either a field-mapping or scale error remains unfound. Don't reattempt
/// without a visual check against the real game, not just a distinct-positions check.</para>
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

	/// <summary>
	/// How far this slot's structure is turned relative to its group, in binary-angle units. Zero for
	/// the group's first-claimed member and for any slot the table does not reach.
	///
	/// <para><b>This is not the same thing as the spread offset</b>, and missing it is why one
	/// structure of a group could stand at the right spot facing the wrong way.
	/// <c>Base_AttachToGroup</c> (<c>00405c3c</c>) fills a structure's heading only when its own
	/// record names none (the <c>-0x8000</c> sentinel — a block-9 record whose heading ref is -1),
	/// and when it does, it adds this slot's turn on top of the group's:</para>
	/// <code>
	/// h = group.heading;
	/// if (slot != 0) h += formation.slots[slot - 1].headingNudge;   // +405c9a, the slot's trailing int16
	/// object.heading = (short)h;
	/// </code>
	/// <para>Note this is applied at <i>attach</i> time and is entirely separate from
	/// <c>Base_ApplyFormationOffset</c>, which reads the same slot record's two <c>int32</c>s and only
	/// moves the structure. The heading is a short in the original, so the sum wraps.</para>
	///
	/// <para>Confirmed on the Scramble training base: its group uses formation 9, whose slots 6 and 8
	/// carry 16380 (90 degrees) and 32760 (180). Roster slots 6 and 8 of that group are two of the
	/// three identical silo-cluster structures, and in retail they stand turned by exactly those
	/// amounts while the third does not.</para>
	/// </summary>
	public short HeadingNudgeFor(int formationId, int memberIndex) =>
		OffsetFor(formationId, memberIndex)?.HeadingNudge ?? 0;

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
				short nudge = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
				offset += 2;
				slots[s] = new BaseFormationOffset(x, y, nudge);
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
