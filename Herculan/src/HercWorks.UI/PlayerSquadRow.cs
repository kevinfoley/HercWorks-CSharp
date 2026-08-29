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

	/// <summary>
	/// The fit as words, for the roster grid — the slots themselves are edited one at a time in the
	/// loadout panel (see <see cref="PlayerWeaponSlotRow"/>), since a raw id list is unreadable and
	/// says nothing about what a launcher is loaded with. Empty slots are left out.
	/// </summary>
	public string WeaponFit => WeaponFitOption.Summarize(Source.WeaponRefs, Source.WeaponAmmoTypes);

	public short Unk00 { get => Source.Unk00; set => Source.Unk00 = value; }
	public short Unk02 { get => Source.Unk02; set => Source.Unk02 = value; }
	public short Unk3A { get => Source.Unk3A; set => Source.Unk3A = value; }
}

/// <summary>
/// One weapon slot of the squad entry selected in the roster grid — the two parallel arrays
/// presented a slot at a time, so each one can be picked by name. <see cref="AmmoType"/> only means
/// anything on a launcher (see <see cref="WeaponFitOption.IsLauncher"/>); every other mount ignores
/// it and retail leaves the filler 5 there.
///
/// <para>Unlike script.dat's fixed ten slots, this list is as long as the machine's real slot count,
/// so slots are added and removed here — always to both arrays at once, which is the invariant
/// PlayerSquadForm's save check exists to catch.</para>
/// </summary>
internal sealed class PlayerWeaponSlotRow {
	public required MecEntry Source { get; init; }

	public required int Slot { get; init; }

	public short WeaponId {
		get => Source.WeaponRefs[Slot];
		set => Source.WeaponRefs[Slot] = value;
	}

	public short AmmoType {
		get => Slot < Source.WeaponAmmoTypes.Length ? Source.WeaponAmmoTypes[Slot] : AmmoTypeOption.Filler;
		set {
			if (Slot >= Source.WeaponAmmoTypes.Length) {
				throw new InvalidOperationException(
					$"This entry declares {Source.WeaponRefs.Length} weapon slots but only " +
					$"{Source.WeaponAmmoTypes.Length} paired ammunition values, so slot {Slot} has none to set.");
			}

			Source.WeaponAmmoTypes[Slot] = value;
		}
	}

	public bool IsLauncher => WeaponFitOption.IsLauncher(WeaponId);
}
