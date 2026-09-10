namespace Herculan.Engine.Shell;

/// <summary>
/// Which of the mission tab's three faces is up. It is the shell's <c>DAT_0048106c</c>, and its three
/// values are the ones the palette switch and the screen builder both test for; nothing has been read
/// that uses 2 or 3.
/// </summary>
public enum ShellMissionView {
	/// <summary>The campaign map, which is where the tab lands at the start of a stage.</summary>
	Map = 0,

	/// <summary>The pre-mission briefing.</summary>
	Briefing = 1,

	/// <summary>The post-mission debrief, which the campaign layer selects rather than the tab.</summary>
	Debriefing = 4,
}

/// <summary>
/// The shell's palettes, and which one each screen is drawn through.
///
/// <para><b>The palette is a widget, not a call.</b> Every tab screen's builder makes one more child
/// than the tab strip — a <see cref="ShellLayout.PaletteScope"/>-sized window of its own class
/// (<c>0040ca6c</c>, vtable <c>PTR_FUN_0046ef04</c>) held in <c>DAT_0048d444</c>, whose <c>+0x45</c>
/// is a palette index. Its event handler (<c>0040cab7</c>) installs that index's palette when it is
/// shown, so the shell changes palette by hiding the widget, writing <c>+0x45</c> and showing it again
/// — which is the whole of <c>FUN_00439da0(index)</c> (<c>00439da0</c>).</para>
///
/// <para><see cref="Names"/> is the table at <c>0046dcdc</c> that <c>FUN_004075b2(index)</c> indexes,
/// read out of the executable's data segment: twenty <c>dpl\*.dpl</c> paths, in this order. The
/// entries past the first three are what tie the campaign's five stages to their art — five briefing
/// palettes, five debrief palettes and five theater palettes, all indexed by the stage number, which
/// is what says the stage counts from one at runtime rather than from zero as it is stored.</para>
///
/// <para><b>Stage 5 is the Moon.</b> The three per-stage runs end on <c>luna</c>, the campaign map
/// switches from <c>cam_er</c> to <c>cam_moon</c> at exactly the same stage, and the mission tab's
/// location art (<c>maybe_Mission_UpdateLocationTab</c>, <c>004446xx</c>) carries only four
/// <c>dba\</c> names — <c>alph2</c>, <c>delt1</c>, <c>omic1</c>, <c>brav1</c> — and branches away
/// entirely when <c>stage - 1 == 4</c>. Three independent tables agreeing on the same boundary is what
/// makes this a reading rather than a guess.</para>
/// </summary>
public static class ShellPalette {
	/// <summary>
	/// The pointer table at <c>0046dcdc</c>, as resource names — <c>dpl\</c> and <c>.dpl</c> stripped,
	/// since <see cref="ShellArt"/> composes both back on. Index into it exactly as
	/// <c>FUN_004075b2</c> does.
	/// </summary>
	public static readonly string[] Names = {
		"INTR_PT1", "PALETTE", "ARMING", "CAM_ER", "CAM_MOON",
		"BR_W1", "BR_W2", "BR_W3", "BR_W4", "BR_W5",
		"DB_W1", "DB_W2", "DB_W3", "DB_W4", "DB_W5",
		"ALPH", "DELT", "OMIC", "BRAV", "LUNA",
	};

	/// <summary>The intro sequence's palette, and the only entry nothing in the tab layer selects.</summary>
	public const int Interstitial = 0;

	/// <summary>
	/// The bay palette: what the shell installs on entry, what <c>0043b23d</c> re-installs whenever the
	/// service bay is shown, and what the main menu, the save screen and the repair screen are drawn
	/// through.
	/// </summary>
	public const int ServiceBay = 1;

	/// <summary>The weapons, build, armory and crew screens' palette.</summary>
	public const int Arming = 2;

	/// <summary>The campaign map's two palettes, picked on whether the stage is the lunar one.</summary>
	public const int CampaignMapEarth = 3;
	public const int CampaignMapMoon = 4;

	/// <summary>Stage 1's briefing palette; the five run consecutively from here.</summary>
	public const int FirstBriefing = 5;

	/// <summary>Stage 1's debrief palette; likewise five consecutive.</summary>
	public const int FirstDebriefing = 10;

	/// <summary>Stage 1's theater palette; likewise five consecutive, ending on <c>LUNA</c>.</summary>
	public const int FirstTheater = 15;

	/// <summary>How many stages the campaign has, and so how long each of the three per-stage runs is.</summary>
	public const int StageCount = 5;

	/// <summary>The stage the campaign moves to the Moon on — the last one.</summary>
	public const int LunarStage = StageCount;

	/// <summary>
	/// The palette a tab is drawn through, or null where the original installs none. That case is real
	/// rather than defensive: <c>FUN_0043b162</c>'s mission arm is three bare <c>if</c>s against
	/// <c>DAT_0048106c</c> with no <c>else</c>, so a fourth value leaves whatever palette was up.
	///
	/// <para><paramref name="stage"/> is the campaign stage, counting from 1 (see the class remarks).
	/// The arithmetic is the original's and is unguarded there: it is reproduced unguarded here, so a
	/// stage outside 1-5 gives an index outside the briefing or debrief run exactly as it does in
	/// retail, and <see cref="Name"/> is what refuses it.</para>
	/// </summary>
	public static int? ForTab(int tab, ShellMissionView view = ShellMissionView.Map, int stage = 1) => tab switch {
		// Main menu and save do not go through the tab switch at all — their handlers install this
		// directly, and 0043b23d installs it again on the way in.
		0 or 1 => ServiceBay,
		3 => ServiceBay,
		2 or 4 or 5 or 6 => Arming,
		7 => view switch {
			ShellMissionView.Map => stage >= LunarStage ? CampaignMapMoon : CampaignMapEarth,
			ShellMissionView.Briefing => FirstBriefing - 1 + stage,
			ShellMissionView.Debriefing => FirstDebriefing - 1 + stage,
			_ => null,
		},
		_ => null,
	};

	/// <summary>
	/// The theater palette for a campaign stage — <c>FUN_004075b2(stage + 0xe)</c> in
	/// <c>maybe_Mission_UpdateLocationTab</c>. Stage 5 never reaches it in the original, which branches
	/// to the lunar cutscene before the call, so <c>LUNA</c> is in the table and unused by this path.
	/// </summary>
	public static int ForStage(int stage) => FirstTheater - 1 + stage;

	/// <summary>The resource name at an index, or null when the index is outside the table.</summary>
	public static string? Name(int index) =>
		index >= 0 && index < Names.Length ? Names[index] : null;

	/// <summary>The index a resource name sits at, or null when the table does not carry it.</summary>
	public static int? IndexOf(string name) {
		for (int i = 0; i < Names.Length; i++) {
			if (string.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase)) {
				return i;
			}
		}

		return null;
	}
}
