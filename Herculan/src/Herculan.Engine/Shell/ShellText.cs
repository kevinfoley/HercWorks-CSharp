using HercWorks.Core.Data.File;
using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>
/// <c>estext.bin</c> — every caption, title and readout label the shell prints, by index. The tab
/// strip's eight captions are entries <see cref="ShellLayout.FirstTabCaption"/> onward, which is why
/// nothing here hardcodes a tab's name: the original does not either, and reading them is what makes
/// the strip say what the game says.
///
/// <para>The container is the <c>.BIN</c> string table described in docs/formats/weapons-dat.md, and
/// <see cref="BinStringFileTransformer"/> already parses it. VSHELL reaches it through
/// <c>WeaponsBin_LookupName</c> (<c>00408240</c>) against the handle at <c>0046dcc0</c>, opened by
/// literal filename in the shell's global init (<c>esglobal.cpp</c>, <c>004073bc</c>).</para>
///
/// <para>It lives in <c>LANG0.VOL</c>, not <c>SHELL0.VOL</c>, under one folder per language. The
/// three folders are byte-identical in the retail build, so <see cref="LanguageFolders"/> is a
/// fallback chain rather than a language setting — picking a different one changes nothing until
/// somebody ships a translated archive.</para>
/// </summary>
public sealed class ShellText {
	/// <summary>The shell's own string table.</summary>
	public const string ResourceName = "ESTEXT.BIN";

	/// <summary>Folders inside <c>LANG0.VOL</c> a <c>.BIN</c> may live under, in the order tried.</summary>
	public static readonly string[] LanguageFolders = { "ENG", "FRE", "GER" };

	private readonly string[] _values;

	private ShellText(string[] values) => _values = values;

	/// <summary>How many entries the table holds — 342 in the retail <c>estext.bin</c>.</summary>
	public int Count => _values.Length;

	/// <summary>
	/// Entry <paramref name="index"/>, or null when the index is out of range. Callers draw no text on
	/// null rather than substituting a placeholder: a missing caption should read as missing, the same
	/// call the cockpit's own label paths make.
	/// </summary>
	public string? Text(int index) =>
		index >= 0 && index < _values.Length && _values[index].Length > 0 ? _values[index] : null;

	/// <summary>
	/// Loads one <c>.BIN</c> table out of whichever language folder has it. Returns null when no
	/// mounted archive carries the file, in which case the shell draws its art and no words.
	/// </summary>
	public static ShellText? Load(GameContent content, string resourceName = ResourceName) {
		foreach (string folder in LanguageFolders) {
			if (content.Read(folder, resourceName) is not { } bytes
				|| new BinStringFileTransformer().Parse(bytes) is not StringBinaryFile { Values: { } values }) {
				continue;
			}

			// The transformer slices each entry up to the next offset, so every string but the last
			// carries its own NUL terminator, and Trim() does not consider NUL whitespace.
			var trimmed = new string[values.Length];
			for (int i = 0; i < values.Length; i++) {
				trimmed[i] = values[i].Trim('\0').Trim();
			}

			return new ShellText(trimmed);
		}

		return null;
	}
}
