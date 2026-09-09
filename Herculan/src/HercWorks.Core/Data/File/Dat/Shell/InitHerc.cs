using HercWorks.Core.Data.Struct.Vshell.Hercs;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/INI_[herc].DAT — the stock weapon fit a chassis of this type is delivered
/// with, one file per type. A bare ShellHercData with no leading bay id.
///   0 - UINT16 - herc id
///   2 - UINT16 - build percent, 100 in all nine files (these describe a delivered machine)
///   4 - UINT16 - build missions remaining, 0 in all nine
///   6 - UINT16 - hardpoint count
///   SEQ (hardpoints): S0 hardpoint id, S2 weapon id, S4 health percent, S6 missile_num (5 = none)
///
/// <para>Hardpoints serialize sparsely, occupied entries only, each preceded by its index — so the
/// count is authoritative and the indices need not be contiguous. INI_APOC fits 0-5 and 7, leaving
/// 6 empty. Every file's leading id matches its filename stem, and every hardpoint count is within
/// that chassis's capacity. See <c>docs/formats/herc-catalogs.md</c>.</para>
///
/// <para>TODO (carried over from Java): unclear if these files are read at runtime — zeroing out
/// INIT_OUTL's hardpoints produced no observed changes. VSHELL reaches them through a nine-entry
/// filename table, so the load path exists; what has not been traced is a call site that fires
/// outside a chassis purchase.</para>
///
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
