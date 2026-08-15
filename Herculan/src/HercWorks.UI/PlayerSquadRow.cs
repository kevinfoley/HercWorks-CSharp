using HercWorks.Core.Data.File.Sav;

namespace HercWorks.UI;

/// <summary>
/// One Herc in the player's squad (<c>data\player.mec</c>), wrapping a live <see cref="MecEntry"/>
/// so grid edits write straight through to it.
///
/// <para>The two weapon arrays are variable-length and are what makes the record variable-length, so
/// unlike script.dat's fixed ref arrays they may be any length — but both must stay the same length
/// as each other, and <see cref="MecEntry.SlotCount"/> must match, or every entry after this one is
/// read at the wrong offset. <see cref="Slots"/> is therefore derived rather than editable, and
/// PlayerSquadForm re-syncs SlotCount on save.</para>
/// </summary>
internal sealed class PlayerSquadRow {
	public required MecEntry Source { get; init; }

	/// <summary>Position in the squad — settable because add/remove renumbers the rows below.</summary>
	public int Index { get; set; }

	/// <summary>
	/// Index into <c>nam\MECHS.NAM</c>, the same numbering script.dat's Herc roster uses. Presented
	/// as a name via <see cref="HercTypeOption"/>; the underlying model field keeps Core's
	/// <c>MechType</c> name.
	/// </summary>
	public short HercType { get => Source.MechType; set => Source.MechType = value; }

	public int Slots => Source.WeaponRefs.Length;

	public string WeaponRefs {
		get => ShortCsv.Format(Source.WeaponRefs);
		set => Source.WeaponRefs = ShortCsv.Parse(value);
	}

	public string WeaponCounts {
		get => ShortCsv.Format(Source.WeaponCounts);
		set => Source.WeaponCounts = ShortCsv.Parse(value);
	}

	public short Unk00 { get => Source.Unk00; set => Source.Unk00 = value; }
	public short Unk02 { get => Source.Unk02; set => Source.Unk02 = value; }
	public short Unk3A { get => Source.Unk3A; set => Source.Unk3A = value; }
}
