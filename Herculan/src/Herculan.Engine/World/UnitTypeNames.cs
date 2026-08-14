using System.Text;
using Herculan.Engine.Content;

namespace Herculan.Engine.World;

/// <summary>
/// The lists that turn a mission's numeric unit types into resource names — <c>nam\MECHS.NAM</c> and
/// <c>nam\FLYERS.NAM</c>, each a flat run of NUL-terminated ASCII names indexed by type.
///
/// <para>This is the missing half of <c>script.dat</c>'s mech roster: block 7's
/// <c>SmallDiscrete</c> is a mech type, but nothing in <c>script.dat</c> says what type 13 is.
/// <c>MechType_InitOne</c> (<c>004201a8</c>) answers it — its first act is
/// <c>nameTable[typeIndex]</c> followed by joining that name to the <c>dat\</c>, <c>dts\</c> and
/// <c>bnd\</c> folder prefixes, so the name is simultaneously the mech's stats file, its model and
/// its collision data. Cross-checked against the retail install: <c>MECHS.NAM</c> holds exactly 21
/// names and every one has a matching <c>dat\&lt;name&gt;.DAT</c> and <c>dts\&lt;name&gt;.DTS</c>,
/// which also matches the 0-20 range <c>msn-mission-file.md</c> measured for row #12's type
/// field.</para>
/// </summary>
public sealed class UnitTypeNames {
	/// <summary>VOL folder both lists live in.</summary>
	public const string ResourceFolder = "nam";

	/// <summary>Mech type names, indexed by <c>script.dat</c> block 7's type field.</summary>
	public const string MechListName = "MECHS.NAM";

	/// <summary>Flyer/vehicle type names, indexed by <c>script.dat</c> block 8's type field.</summary>
	public const string FlyerListName = "FLYERS.NAM";

	private readonly string[] _names;

	private UnitTypeNames(string[] names) {
		_names = names;
	}

	/// <summary>How many types the list declares.</summary>
	public int Count => _names.Length;

	/// <summary>
	/// The resource base name for a type, or null when the index falls outside the list. Out of range
	/// is a real possibility on hand-edited mission data and is not worth throwing over — the caller
	/// draws nothing and says so.
	/// </summary>
	public string? this[int typeIndex] =>
		typeIndex >= 0 && typeIndex < _names.Length ? _names[typeIndex] : null;

	/// <summary>Every name, in type order.</summary>
	public IReadOnlyList<string> Names => _names;

	public static UnitTypeNames LoadMechs(GameContent content) => Load(content, MechListName);

	public static UnitTypeNames LoadFlyers(GameContent content) => Load(content, FlyerListName);

	private static UnitTypeNames Load(GameContent content, string resourceName) {
		byte[] bytes = content.ReadRequired(ResourceFolder, resourceName);

		// One NUL-terminated name after another. The retail files end with a stray newline after the
		// last terminator, which splitting on NUL leaves as a trailing whitespace-only fragment.
		var names = new List<string>();
		int start = 0;
		for (int i = 0; i < bytes.Length; i++) {
			if (bytes[i] != 0) {
				continue;
			}

			string name = Encoding.ASCII.GetString(bytes, start, i - start).Trim();
			if (name.Length > 0) {
				names.Add(name.ToUpperInvariant());
			}
			start = i + 1;
		}

		return new UnitTypeNames(names.ToArray());
	}
}
