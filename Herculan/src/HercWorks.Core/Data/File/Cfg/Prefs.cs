using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// FILE - [ROOT]/DATA/PREFS.CFG. Mostly-unidentified 16-bit preference flags (music/sfx
/// on-off, resolution, fullscreen, herc-repair mode, weapon-build mode, and many unknowns) —
/// see the Java source for the full byte-offset notes. No fields modeled yet in the original.
/// Ported from org.hercworks.core.data.file.cfg.Prefs.
/// </summary>
public class Prefs : DataFile {
	public Prefs() : base("PREFS.CFG", "DATA/") { }
}
