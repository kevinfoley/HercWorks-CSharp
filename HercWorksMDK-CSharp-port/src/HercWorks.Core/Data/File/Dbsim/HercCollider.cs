using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/COL/&lt;herc&gt;.COL
///   0 - UINT16 - always 6, seems to be number of main collider components
///   2 - UINT16 - always 3 for hercs, FFFF for skimmer
///   4 - UINT16 - possible collider type — 0x00 registers hits but does 0 damage, 0x01 seems
///     default, values above 0x01 cause crash issues
///   6 - UINT16 - unknown, hercs have 0x07
///   8 - UINT16 - unknown, hercs 0x02, skimmer 0x6, razor 0x03
///   10.. - collider component data. Confirmed present (real ACHILLES.COL content is 290 bytes:
///     10-byte header above + 280 bytes here) but its internal layout is NOT decoded — it doesn't
///     divide evenly across the "6 main collider components" the header claims (280 / 6 isn't a
///     whole number of bytes), so it isn't 6 fixed-size records. Read as INT16 LE values, some
///     runs look like they could be bounding-box-ish quads (4 shorts) or small
///     index/count pairs, but no consistent record boundary was confirmed against real data.
///     Kept as raw shorts rather than guessed structure — see [[project_es2_translation_status]].
/// Ported from org.hercworks.core.data.file.dbsim.HercCollider. No getters/setters in the
/// original (fields unused externally); exposed as public properties here for the usual reason.
/// Extended here (no Java equivalent) with the raw component data, previously dropped entirely.
/// </summary>
public class HercCollider : DataFile {
	public short PrimaryBBoxesTotal { get; set; }
	public short Unk2_flag { get; set; }

	/// <summary>Usually 1.</summary>
	public short CollideType { get; set; } = 0x01;

	/// <summary>Hercs have 7, spider/skimmer 0.</summary>
	public short Unk4_val { get; set; } = 0x07;

	public short Unk8_val { get; set; } = 0x02;

	/// <summary>Undecoded collider component data following the header — see class doc comment.</summary>
	public short[]? ComponentData { get; set; }
}
