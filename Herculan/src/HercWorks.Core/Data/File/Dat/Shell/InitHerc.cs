using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// TODO (carried over from Java): unclear if these files are directly used in /SHELL/? — zeroing
/// out INIT_OUTL's hardpoints produced no observed changes. Wrapper for ShellHercData.
/// FILE - /SHELL/GAM/INI_[herc].DAT — presumably deals with initializing herc stats at runtime.
///   0 - UINT16 - herc id
///   2 - UINT16 - '100', purpose unknown
///   4 - UINT16 - 'completeness' number, linked to [herc]_bod.DBA frames
///   6 - UINT16 - hardpoint count
///   SEQ (hardpoints): S0 hardpoint id, S2 weapon id, S4 health percent, S6 missile_num (5 = none)
/// Ported from org.hercworks.core.data.file.dat.shell.InitHerc.
/// </summary>
public class InitHerc : DataFile {
	/// <summary>
	/// Original: Bytes.from("661FAF55", StandardCharsets.UTF_8) — despite looking like a hex
	/// string, this is literally the UTF-8/ASCII bytes of that 8-character text, not a decoded
	/// hex value. Ported literally.
	/// </summary>
	public static readonly byte[] Header = System.Text.Encoding.UTF8.GetBytes("661FAF55");

	public ShellHercData? Data { get; set; }

	public InitHerc() { }

	public InitHerc(string fileName, string dirPath) : base(fileName, dirPath) { }
}
