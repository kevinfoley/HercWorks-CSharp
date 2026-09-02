using HercWorks.Core.Data.Struct.Vshell.Hercs;

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
public class InitHerc {
	/// <summary>
	/// Source file name and directory, and the file's own bytes. Unlike every other parsed model,
	/// these are genuinely consumed: <see cref="Io.Read.DatFileReader.ParseIniHercDatStats"/>
	/// builds an InitHerc from a VOL entry's name/path and then walks <see cref="RawBytes"/> to
	/// fill in the hardpoint table. Declared here rather than inherited from DataFile.
	/// </summary>
	public string? FileName { get; set; }

	/// <inheritdoc cref="FileName"/>
	public string? GameDirPath { get; set; }

	/// <inheritdoc cref="FileName"/>
	public byte[]? RawBytes { get; set; }

	/// <summary>Unused by either <see cref="Io.Read.DatFileReader.ParseIniHercDatStats"/> or <see cref="Io.Transform.Shell.InitHercTransformer"/> — confirmed against real `SHELL0\GAM\INI_*.DAT` files, which carry no such prefix.</summary>
	public static readonly byte[] Header = { 0x66, 0x1F, 0xAF, 0x55 };

	public ShellHercData? Data { get; set; }

	public InitHerc() { }

	public InitHerc(string fileName, string dirPath) {
		FileName = fileName;
		GameDirPath = dirPath;
	}
}
