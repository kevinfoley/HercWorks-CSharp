namespace Herculan.Engine.Shell;

/// <summary>
/// Which of a machine's three condition arrays a repair-screen selection addresses. The values are
/// the original's own: <c>FUN_00433410</c> returns exactly these, and they are the same three modes
/// <c>FUN_00411d06(block, mode, index)</c> takes to read the 66-byte status block — so a selection is
/// literally an accessor argument pair. See docs/formats/save-games.md.
/// </summary>
public enum ShellComponentKind {
	/// <summary>One of the six external component groups — Cockpit through Right Leg.</summary>
	ExternalGroup = 0,

	/// <summary>One of the nine internal components — Left Leg Servos through Life Support.</summary>
	Internal = 1,

	/// <summary>One of the ten per-hardpoint conditions, one per mount slot.</summary>
	Hardpoint = 2,
}

/// <summary>One resolved repair-screen selection: which array, and which entry of it.</summary>
public readonly record struct ShellComponentSelection(ShellComponentKind Kind, int Index);

/// <summary>
/// The repair screen's hit model: how a click on one of its twenty-five hotspots becomes a component.
///
/// <para>The screen holds one table of twenty-five click handlers at <c>0048d1f8</c>, each a thunk
/// calling <c>FUN_00433eb9(column, row)</c> with its own pair baked in. Sixteen are column 0 — the
/// clickable rects laid over the chassis picture, whose geometry is <c>gam\rpr_hots.dat</c> — and
/// nine are column 1, a list beside it. <c>FUN_00433410</c> turns the pair into a
/// <see cref="ShellComponentKind"/> and <c>FUN_00433431</c> into an index within it.</para>
///
/// <para>Nothing draws this yet; see Herculan/ROADMAP.md. What is here is the mapping, which three
/// independent facts agree on — the counts 6/9/10, the accessor modes, and <c>rpr_hots.dat</c>
/// carrying exactly six areas for every chassis.</para>
/// </summary>
public static class ShellRepairHotspots {
	/// <summary>Clickable rects over the chassis picture: six body groups then ten weapon rows.</summary>
	public const int PictureColumn = 0;

	/// <summary>The list beside the picture, one row per internal component.</summary>
	public const int ListColumn = 1;

	/// <summary>How many of the picture's rows are body groups; the rest are weapon rows.</summary>
	public const int ExternalGroupCount = 6;

	/// <summary>Mount slots the picture carries rows for, and the length of the hardpoint array.</summary>
	public const int HardpointCount = 10;

	/// <summary>Internal components, and the length of the list column.</summary>
	public const int InternalCount = 9;

	/// <summary>Rows in the picture column: the body groups and the weapon rows together.</summary>
	public const int PictureRowCount = ExternalGroupCount + HardpointCount;

	/// <summary>Handlers in the screen's single table — both columns end to end.</summary>
	public const int HandlerCount = PictureRowCount + InternalCount;

	/// <summary>
	/// Resolves a hotspot to the component it selects, or null when the pair is outside the table.
	/// The original does no bounds check at all — its twenty-five thunks are the only callers, so
	/// every pair it can see is in range — and this refuses the rest rather than inventing one.
	/// </summary>
	public static ShellComponentSelection? Resolve(int column, int row) {
		if (row < 0) {
			return null;
		}

		if (column == ListColumn) {
			return row < InternalCount ? new ShellComponentSelection(ShellComponentKind.Internal, row) : null;
		}

		if (column != PictureColumn || row >= PictureRowCount) {
			return null;
		}

		return row < ExternalGroupCount
			? new ShellComponentSelection(ShellComponentKind.ExternalGroup, row)
			: new ShellComponentSelection(ShellComponentKind.Hardpoint, row - ExternalGroupCount);
	}

	/// <summary>
	/// Whether a resolved selection is one the screen will actually take. Only hardpoints are ever
	/// refused, and on two counts: a slot past the machine's own mount capacity (<c>+0x4c</c>), and a
	/// slot with nothing fitted. Refusal in the original is total — the selection does not move and
	/// nothing repaints — so an unfitted hardpoint cannot be selected on the repair screen at all.
	/// </summary>
	public static bool IsSelectable(ShellComponentSelection selection, int mountCapacity, int fittedWeaponId) =>
		selection.Kind != ShellComponentKind.Hardpoint
		|| (selection.Index < mountCapacity && fittedWeaponId != 0);
}

/// <summary>
/// The repair screen's Condition readout: a 0-100 component condition turned into a damage level,
/// which picks both the word printed and the colour it is printed in.
///
/// <para>The level is <c>FUN_0043d9cf</c> (<c>0043d9cf</c>), and its bands are the repair ladder's —
/// its table is <c>{89, 79, 59, 29, 0}</c> tested with <c>&lt;</c> where
/// <c>Repair_LevelForCondition</c>'s is <c>{90, 80, 60, 30, 1, 0}</c> tested with <c>&lt;=</c>, which
/// partitions the same way. So the word the screen prints is the repair level the machine would be
/// worked to. See docs/shell/screen-layout.md.</para>
/// </summary>
public static class ShellDamageLevel {
	/// <summary>
	/// The band table at <c>004765c0</c>. A condition lands on the first index it is <b>strictly</b>
	/// greater than; greater than none of them gives <see cref="Destroyed"/>.
	/// </summary>
	private static readonly int[] Bands = { 89, 79, 59, 29, 0 };

	/// <summary>The level a fully destroyed component takes — one past the band table.</summary>
	public const int Destroyed = 5;

	/// <summary>Text colours by level, from the table at <c>004765ca</c>. Palette entries, not RGB.</summary>
	public static readonly int[] Colors = { 14, 13, 12, 11, 10, 39 };

	/// <summary>
	/// The <c>estext.bin</c> index the caption is fetched from — <c>mov esi,eax</c> / <c>add si,0x68</c>
	/// in <c>FUN_00433445</c>, read off the instruction stream.
	/// </summary>
	public const int FirstCaption = 0x68;

	/// <summary>
	/// How many damage words <c>estext.bin</c> actually carries at <see cref="FirstCaption"/> —
	/// <c>Nominal</c>, <c>Light</c>, <c>Moderate</c>, <c>Heavy</c>. There is no fifth or sixth, so
	/// levels 4 and 5 read the two unrelated entries that follow. See Herculan/KNOWN_ISSUES.md.
	/// </summary>
	public const int CaptionCount = 4;

	/// <summary>The damage level of a 0-100 condition, 0 (Nominal) through 5 (destroyed).</summary>
	public static int For(int condition) {
		for (int i = 0; i < Bands.Length; i++) {
			if (condition > Bands[i]) {
				return i;
			}
		}

		return Destroyed;
	}

	/// <summary>
	/// The <c>estext.bin</c> index a level's word is fetched from. Reproduces the overrun rather than
	/// clamping: levels 4 and 5 genuinely index past the four-word run in retail, and
	/// <see cref="HasCaption"/> is how a caller tells the two cases apart.
	/// </summary>
	public static int CaptionIndex(int level) => FirstCaption + level;

	/// <summary>Whether a level has a damage word of its own, rather than reading past the run.</summary>
	public static bool HasCaption(int level) => level >= 0 && level < CaptionCount;

	/// <summary>The text colour for a level, or the destroyed colour for anything out of range.</summary>
	public static int Color(int level) =>
		level >= 0 && level < Colors.Length ? Colors[level] : Colors[Destroyed];
}
