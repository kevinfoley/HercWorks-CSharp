namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/OFS/PILOTn.OFS — per-pilot portrait compositing offsets, one entry per frame of
/// the matching /SIMVOL0/DBA/PILOTn.DBA sprite sheet. No header/count field — the file is just a
/// flat array of 12-byte (6x INT16) entries filling the whole content, so entry count is simply
/// content length / 12.
///
/// New (no Java equivalent — not a ported format): reverse-engineered 2026-08-08 by cross-checking
/// against every real PILOTn.OFS/PILOTn.DBA pair. Entry.Index counts up sequentially (0, 1, 2...)
/// matching DBA frame order. The remaining 4 fields are constant in blocks that line up exactly
/// with DBA's own frame-size groups (e.g. PILOT0.DBA's 24 identical 56x59 "head rotation" frames
/// all share one constant 4-tuple in PILOT0.OFS, then its 3 wider 104x17 "talking" frames share a
/// different one) — strongly suggesting these are per-frame-size compositing offsets (where to
/// place/align a frame of unusual size against the fixed portrait display area), though the exact
/// meaning of each of the 4 values isn't confirmed. Entry count is consistently one less than the
/// matching DBA's frame count for 11 of 12 real pilots (PILOT9 is the one exception, with an exact
/// 1:1 count) — confirmed real per-pilot variation, not a fixed off-by-one to special-case around.
/// </summary>
public class PilotOffsetFile {
	public Entry[]? Entries { get; set; }

	public class Entry {
		/// <summary>Sequential frame index (0-based), matching the paired PILOTn.DBA's frame order.</summary>
		public short Index { get; set; }

		/// <summary>Always 0 in every real file checked.</summary>
		public short Unk1 { get; set; }

		/// <summary>Undecoded — see class doc comment. Plausibly a compositing offset/size field.</summary>
		public short OffsetA { get; set; }

		/// <summary>Undecoded — see class doc comment.</summary>
		public short OffsetB { get; set; }

		/// <summary>Undecoded — see class doc comment.</summary>
		public short OffsetC { get; set; }

		/// <summary>Undecoded — see class doc comment.</summary>
		public short OffsetD { get; set; }
	}
}
