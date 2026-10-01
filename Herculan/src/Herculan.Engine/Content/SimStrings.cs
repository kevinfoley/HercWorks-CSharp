using HercWorks.Core.Data.File;
using HercWorks.Core.Io.Transform.Common;

namespace Herculan.Engine.Content;

/// <summary>
/// Where DBSIM's <c>str\*.STR</c> text lives, and the reader for it. The simulator keeps every piece
/// of UI text out of its code; the layout and the <c>STRINGS0.STR</c> group index are in
/// docs/formats/str-strings.md, and <see cref="StringFile"/> models it.
/// </summary>
public static class SimStrings {
	/// <summary>The resource folder <c>.STR</c> files live in.</summary>
	public const string ResourceFolder = "str";

	/// <summary>The simulator's general UI text, and the file the MFD's own captions come from.</summary>
	public const string SimulatorStrings = "STRINGS0.STR";

	/// <summary>
	/// Reads one <c>.STR</c> out of the mounted archives, or null when it is absent or does not parse —
	/// see <see cref="StringFileTransformer.Parse"/> for why a partial table is never returned.
	/// </summary>
	public static StringFile? Load(GameContent content, string name = SimulatorStrings) =>
		content.Read(ResourceFolder, name) is { } bytes ? Parse(bytes) : null;

	/// <summary>Parses a <c>.STR</c>'s bytes, or null when they do not parse.</summary>
	public static StringFile? Parse(byte[] bytes) => new StringFileTransformer().Parse(bytes);
}
