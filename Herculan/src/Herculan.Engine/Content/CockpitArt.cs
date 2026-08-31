using System.Numerics;
using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.File.Gau;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Core.Io.Transform.Dbsim;

namespace Herculan.Engine.Content;

/// <summary>One decoded cockpit-art frame, RGBA8, top row first — CPU-side, no GL.</summary>
public sealed record CockpitFrame(byte[] Pixels, int Width, int Height) {
	/// <summary>
	/// Where this frame's shield-meter ring pixels ended up, one list of <see cref="Pixels"/> byte
	/// offsets per ramp slot (<see cref="CockpitPalette.ShieldRampFirstSlot"/> + index).
	///
	/// <para>The meter's rings are part of the canopy art, not geometry the HUD draws, and the widget
	/// animates them by rewriting six palette slots every frame. Decoding indexed art to RGBA at load
	/// bakes the palette in and loses that, so the positions are kept here and
	/// <see cref="CockpitArt.UpdateShieldRings"/> repaints them in place. Retail frames put a few
	/// hundred pixels in these six indices, so a repaint is cheap and only happens when the charge
	/// actually moves.</para>
	/// </summary>
	public int[][] ShieldRingPixels { get; init; } =
		Enumerable.Range(0, CockpitPalette.ShieldRampLength).Select(_ => Array.Empty<int>()).ToArray();
}

/// <summary>
/// A herc's cockpit canopy art and HUD widget layout — see docs/formats/cockpit-hud.md (Milestone 8
/// Phase 0) for the RE and the real-data verification behind every constant here.
///
/// <para><see cref="Front"/> is <c>(herc).HB0</c> (the center/front view) and <see cref="Side"/> is
/// <c>(herc).HB2</c> (a real, distinct side view — not a duplicate of the front). There is no
/// separate mirrored asset: the left panel reuses <see cref="Side"/> with its UVs flipped
/// horizontally at draw time. <see cref="HeadsDown"/> is <c>(herc).HB1</c>, DBSIM's view 1 — the
/// Heads-Down Display below the dashboard that <c>[F7]</c>/<c>[F8]</c> pan down to. (An earlier
/// revision of this comment called it a rear/overhead equipment-bay view; it is not.)</para>
///
/// <para>Both frames decode through the live palette (<see cref="CockpitPalette"/>): the theater
/// palette in full, with this herc's own 24-entry cockpit colour scheme installed over slots 42-65.
/// Indices are used <b>as authored</b> — no shift, no offset. The per-herc canopy colour difference
/// that <c>PaletteIndexOffset</c> used to approximate is entirely a property of which scheme window
/// gets installed, and is now resolved from the herc's own data file (see
/// <see cref="ColorSchemeIndex"/>).</para>
///
/// <para><b>The 3D-viewport cutout is data, not a colour key.</b> Each herc ships a per-view region
/// file — <c>hd0</c> for the forward view, <c>hd2</c> for the side view — that states per scanline
/// exactly which columns the live 3D scene shows through; DBSIM's rasterizer is physically span-clipped
/// to it (see <see cref="CockpitClipRegions"/>). <see cref="Load"/> punches those spans into each
/// frame's alpha channel (0 = viewport hole, 255 = opaque art). The previous border-flood-fill over
/// pure-black pixels survives only as a fallback for when the region file can't be read: it inferred
/// the hole from art that happens to be black there, which is close but not the same thing, and it had
/// no way to know about the rows the original leaves opaque despite their colour.</para>
/// </summary>
public sealed class CockpitArt {
	/// <summary>
	/// GAU widget coordinates address a 320x400 logical space; cockpit art is 640x480 pixels. A plain
	/// uniform 2x scale on both axes maps one onto the other — confirmed by overlaying five
	/// independently-positioned real widgets (MFD panel, shield display, throttle, chain button,
	/// energy meter) on real <c>APOCA.HB0</c> art and finding every one lands exactly on its physical
	/// console graphic at this scale, and no other tried combination (1x, 2x/1x, 2x/1.2x) fits more
	/// than one widget at once. See docs/formats/cockpit-hud.md, "Q3".
	/// </summary>
	public const float GauToPixelScale = 2f;

	/// <summary>DBSIM's own view numbering: the forward view the console instruments belong to.</summary>
	public const int ForwardViewIndex = 0;

	/// <summary>
	/// DBSIM's own view numbering for the sideways glance whose canopy bitmap is authored (view 3
	/// reuses this same bitmap mirrored, which is what the renderer's mirror flag does).
	/// </summary>
	public const int SideViewIndex = 2;

	/// <summary>
	/// The sprite banks this milestone draws, all from <c>hba\</c> — see <see cref="HudSpriteSheet"/>
	/// for why the <c>hba</c> half and not <c>dba</c>, and <see cref="Sprites"/> for which widget each
	/// one serves.
	/// </summary>
	public static readonly string[] HudBankNames =
		{ "HUD", "HUDHTICK", "MFD", "RADAR", "THROTTLE", "WPN_DMG", "PWEAPONS", "HDD", "BASES", "VEHICLES" };

	/// <summary>
	/// Banks the game ships only at 320-wide, loaded through
	/// <see cref="HudSpriteSheet.LoResResourceFolder"/> and drawn doubled.
	/// <c>MfdStatusScreen_Ctor</c> asks for <c>bases</c>, <c>vehicles</c> and <c>flyers</c> together as
	/// its target silhouettes, but only the first two exist in <c>hba</c>.
	/// </summary>
	public static readonly string[] LoResHudBankNames = { "FLYERS" };

	/// <summary>
	/// The <c>.HFN</c> fonts the cockpit draws text with, out of the 18 <c>ColorSchemePanels</c>
	/// (<c>0049b0ac</c>) DBSIM loads. In this format the font is the colour — each file is the same
	/// typeface stencilled in one palette index — so a widget picks its text colour by picking a font.
	/// These are the ones the widgets reached so far name: <c>WHITE</c> (shield readouts, the selected
	/// weapon's name, an idle hardpoint digit), <c>GRAY</c> (an unselected weapon's name and its round
	/// count), <c>GREEN</c> (the selected weapon's round count), <c>DARK</c> (a lit hardpoint digit),
	/// and <c>HUD1</c>/<c>HUD2</c>/<c>HUD3</c>, the three theater-coloured fonts the gunsight
	/// complex's readouts use.
	/// </summary>
	/// <para><c>CPGREEN</c> and <c>CPRED</c> are <c>ColorSchemePanels[1]</c> and <c>[2]</c>, the pair
	/// the MFD's FLASH COMM screen lists its squadmate orders in — available orders in green, ones the
	/// squad cannot take in red.</para>
	/// <para><c>CPON</c> and <c>CPPRESS</c> are <c>[4]</c> and <c>[5]</c>, which the Heads-Down
	/// Display's XMIT and CANCEL buttons caption themselves in unlit and lit
	/// (<see cref="HddLayout.TransmitButtonFont"/>); <c>CPYLW</c> is <c>[3]</c>, that display's
	/// subject caption.</para>
	public static readonly string[] HudFontNames = {
		"WHITE", "GRAY", "GREEN", "DARK", "RED", "HUD1", "HUD2", "HUD3",
		"CPGREEN", "CPRED", "CPON", "CPPRESS", "CPYLW", "CPBLUE",
	};

	private CockpitArt(CockpitFrame front, CockpitFrame side, CockpitFrame? headsDown, GAUFile gau, HudSpriteSheet? sprites,
			HudColorTable? colors, (Vector3, Vector3, Vector3)? gaugeColors,
			int colorSchemeIndex, bool clipRegionsLoaded, string hercName, SimStringTable? strings) {
		Front = front;
		Side = side;
		HeadsDown = headsDown;
		Gau = gau;
		Sprites = sprites;
		Colors = colors;
		GaugeColors = gaugeColors;
		ColorSchemeIndex = colorSchemeIndex;
		ClipRegionsLoaded = clipRegionsLoaded;
		HercName = hercName;
		Strings = strings;
	}

	/// <summary>
	/// The herc this cockpit belongs to, which is also the name of its own sprite bank — the paper-doll
	/// wireframe the MFD's status screen draws comes from <c>hba\&lt;HERC&gt;.HBA</c>.
	/// </summary>
	public string HercName { get; }

	/// <summary>
	/// <c>str\STRINGS0.STR</c>, the simulator's UI text — every caption and readout label the cockpit
	/// prints, including the MFD's screen titles and button captions. Null when the resource is
	/// missing, in which case text-bearing widgets draw their art and no words.
	/// </summary>
	public SimStringTable? Strings { get; }

	/// <summary>
	/// The herc's <c>pdg\&lt;HERC&gt;.PDG</c> damage diagram — three views, each an origin/size pair
	/// and a list of body regions. The MFD's status screen positions its wireframe by the view's
	/// origin; the regions are what the original tints per body part as damage lands. Null when the
	/// file is missing or does not parse, in which case no wireframe is drawn.
	/// </summary>
	public PaperDollGraphic? PaperDoll { get; private init; }

	/// <summary>
	/// The same diagram for every machine in the mission, keyed by upper-case herc name - what F5
	/// needs, since the status screen draws the <i>subject's</i> doll and the subject there is the
	/// current target. The player's own entry is in here too and <see cref="PaperDoll"/> is its alias.
	/// </summary>
	public IReadOnlyDictionary<string, PaperDollGraphic> PaperDolls { get; private init; }
		= new Dictionary<string, PaperDollGraphic>();

	/// <summary>This herc's diagram, or another machine's by name. Null when that file was missing.</summary>
	public PaperDollGraphic? PaperDollFor(string? hercName) =>
		hercName != null && PaperDolls.TryGetValue(hercName.ToUpperInvariant(), out var doll) ? doll : null;

	/// <summary>
	/// Where this herc's cockpit views sit in the cockpit canvas — <c>vue\&lt;HERC&gt;.VUE</c>. Null
	/// when the file is missing, in which case callers fall back to
	/// <see cref="CockpitViewGeometry.DefaultHeadsDownOriginY"/>. Loaded here because
	/// <see cref="HeadsDown"/>'s own widget layout is expressed against view 1's canvas origin.
	/// </summary>
	public CockpitViewGeometry? ViewGeometry { get; private init; }

	/// <summary>
	/// The Heads-Down Display's widget layout, from the herc's own <c>.GAU</c> — see
	/// <see cref="HddLayout"/>. Null when the block could not be read, in which case the pan still
	/// reaches <see cref="HeadsDown"/> but nothing is drawn over its art.
	/// </summary>
	public HddLayout? HeadsDownLayout { get; private init; }

	/// <summary>
	/// The two flat colours the Heads-Down Display fills with: colour id 19 for every screen and label
	/// background, and id 15 for the small indicator block beside the title. Null when
	/// <c>COLORS.DAT</c> is missing, in which case the display draws its sprites and text and floods
	/// nothing — better than flooding a colour of the engine's own choosing over the art.
	/// </summary>
	public (Vector3 Background, Vector3 Indicator, Vector3 SubjectPlate)? HeadsDownColors { get; private init; }

	/// <summary>
	/// The two colours the front-window HUD's off-screen target arrow is filled with, unlocked then
	/// locked - <see cref="TargetBox.ArrowColorId"/> and <see cref="TargetBox.ArrowLockedColorId"/>
	/// through <c>COLORS.DAT</c>, green and red. The arrow is a flat polygon rather than a sprite,
	/// which is why it needs a resolved colour at all.
	/// </summary>
	public (Vector3 Unlocked, Vector3 Locked)? TargetArrowColors { get; private init; }


	/// <summary>
	/// Which of <c>COCKPIT.DPL</c>'s nine 24-entry cockpit colour schemes this herc renders through —
	/// mech type record <c>+0x52</c>, i.e. offset 80 of <c>dat\&lt;MECH&gt;.DAT</c>. See
	/// <see cref="CockpitPalette"/>. -1 when the herc's data file could not be read, in which case no
	/// scheme is installed and slots 42-65 keep the theater's own filler green.
	/// </summary>
	public int ColorSchemeIndex { get; }

	/// <summary>
	/// True when both views' cutouts came from the herc's own <c>.HD0</c>/<c>.HD2</c> region files.
	/// False means at least one fell back to inferring the hole from black pixels — worth surfacing,
	/// since the fallback is an approximation.
	/// </summary>
	public bool ClipRegionsLoaded { get; }

	/// <summary>The center/front cockpit view — console, HUD instruments, and the 3D viewport cutout.</summary>
	public CockpitFrame Front { get; }

	/// <summary>A side cockpit view — mirror horizontally at draw time for the opposite panel.</summary>
	public CockpitFrame Side { get; }

	/// <summary>
	/// The Heads-Down Display's background — <c>(herc).HB1</c>, DBSIM's view 1. Null when the file is
	/// missing, in which case the pan has nothing to pan to and the caller should stay forward.
	///
	/// <para>No 3D-viewport hole is punched into it, unlike <see cref="Front"/> and
	/// <see cref="Side"/>. That is what the data says rather than a simplification: every herc's
	/// <c>.HD1</c> region file is 16 bytes of zeroes and its <c>.VUE</c> view-1 rect is zero-size, so
	/// the heads-down view shows no live world. RAZOR is the sole exception — a 2368-byte <c>.HD1</c>
	/// and a real <c>0,0-320,181</c> viewport rect — and rendering that is left for the pass that
	/// gives the HDD live content, since a hole cut now would only expose cleared background.</para>
	/// </summary>
	public CockpitFrame? HeadsDown { get; }

	/// <summary>The HUD widget layout to overlay on <see cref="Front"/> — center panel only (see Overlay2DRenderer).</summary>
	public GAUFile Gau { get; }

	/// <summary>
	/// The HUD's own sprite art, keyed by bank name, or null when none of the banks could be loaded
	/// (in which case the renderer draws the canopy alone).
	///
	/// <para>Bank-to-widget mapping, from the loader functions the bank-name string literals xref to
	/// in DBSIM: <c>HUD</c> is the gunsight/reticle set (<c>Gau_RovingGunsightWidget</c>, 0043c7d8),
	/// <c>HUDHTICK</c> the heading tick tape (FUN_0043b57c), <c>MFD</c> the multi-function display
	/// screen (FUN_00445218, which also owns <c>MFD_DMG</c> and <c>RADAR</c>), <c>THROTTLE</c> the
	/// slider knob (FUN_00447b84), <c>WPN_DMG</c> the weapon hardpoint plates (FUN_0044080c, which
	/// also owns <c>PWEAPONS</c>).</para>
	///
	/// <para>Not yet drawn, and deliberately: the chain/link/autotrack buttons, energy meter and
	/// shield display have their bezels painted into the canopy art already, and their dynamic part is
	/// a <c>LEDBarGraphH</c>/<c>LEDBarGraphV</c> fill plus <c>.HFN</c> font text rather than a sprite —
	/// neither of which this milestone has. Drawing a bank at them on a size hunch would be worse than
	/// leaving the painted bezel alone.</para>
	/// </summary>
	public HudSpriteSheet? Sprites { get; }

	/// <summary>
	/// The logical-colour-id lookup every HUD data file's colour fields go through — see
	/// <see cref="HudColorTable"/>. Null when <c>dat\COLORS.DAT</c> is missing.
	///
	/// </summary>
	public HudColorTable? Colors { get; }

	/// <summary>
	/// An LED gauge bar's three resolved colours — even-column fill, odd-column fill, and unfilled
	/// remainder (see <see cref="HudColorTable.GaugeFillEvenId"/>). Null when <c>COLORS.DAT</c> is
	/// missing, in which case gauges draw nothing rather than a colour of the engine's own choosing.
	/// </summary>
	public (Vector3 FillEven, Vector3 FillOdd, Vector3 Remainder)? GaugeColors { get; }

	/// <summary>
	/// Every <c>COLORS.DAT</c> id resolved against the live palette, or null when the table or the
	/// palette is missing. The pre-resolved tuples above cover the widgets that name a fixed pair or
	/// triple; this is for the ones that pick an id at draw time from what they are drawing — the
	/// scanner's contacts, whose colour is a function of the contact's class and side.
	/// </summary>
	private Vector3?[]? LogicalColors { get; init; }

	/// <summary>
	/// One <c>COLORS.DAT</c> id as a colour, or null when the id is out of range, the table is
	/// missing, or the palette has no such slot — callers draw nothing rather than substituting.
	/// </summary>
	public Vector3? LogicalColor(int id) =>
		LogicalColors is { } table && id >= 0 && id < table.Length ? table[id] : null;

	/// <summary>Every live-palette slot as a colour — see <see cref="PaletteEntry"/>.</summary>
	private Vector3?[]? PaletteEntries { get; init; }

	/// <summary>
	/// One <b>raw</b> palette index as a colour, or null when the palette has no such slot.
	///
	/// <para>Which of this and <see cref="LogicalColor"/> a widget wants is decided by where its
	/// number came from, and the original is consistent about it: a colour that arrives in a
	/// <i>data file</i> is a logical id and is resolved through <c>COLORS.DAT</c> once at load, while
	/// a colour a <i>constructor states as an immediate</i> is already a palette index. The scanner
	/// uses both — its contact colours are read out of the resolved table at paint time, its screen
	/// background is the literal <c>0x11</c> its constructor writes — and so does the weapon panel,
	/// whose bar colours are the raw 32/34/46 (see <see cref="WeaponBarColors"/>).</para>
	/// </summary>
	public Vector3? PaletteEntry(int index) =>
		PaletteEntries is { } table && index >= 0 && index < table.Length ? table[index] : null;

	/// <summary>
	/// A weapon row's charge bar, which does <b>not</b> take its colours from <c>COLORS.DAT</c> the
	/// way <see cref="GaugeColors"/> does: <c>FUN_00442950</c> overwrites the bar's three colour
	/// fields with the raw palette indices 32, 34 and 46 immediately after constructing it. That is
	/// why a capacitor bar is blue where the Master Energy Pool's meter is grey.
	///
	/// <para>The override is conditional on one byte of the widget geometry the constructor is handed,
	/// which lands in the <c>.GAU</c>'s confirmed-zero padding in every retail file — so the override
	/// always applies.</para>
	/// </summary>
	public (Vector3 FillEven, Vector3 FillOdd)? WeaponBarColors { get; private init; }

	/// <summary>Palette index for the even columns of a weapon row's charge bar.</summary>
	public const int WeaponBarFillEvenIndex = 0x20;

	/// <summary>And the odd ones — one shade apart, so the pair reads as a single shaded fill.</summary>
	public const int WeaponBarFillOddIndex = 0x22;

	/// <summary>
	/// Loads and decodes one herc's cockpit art and HUD layout. Returns null when any required
	/// resource is missing from the mounted archives, in which case the caller should fall back to
	/// drawing no cockpit overlay rather than a partially-wrong one.
	/// </summary>
	public static CockpitArt? Load(GameContent content, string hercName, string? worldPaletteName = null,
			IEnumerable<string>? targetHercNames = null) {
		int schemeIndex = ReadColorSchemeIndex(content, hercName);
		if (CockpitPalette.Load(content, worldPaletteName, schemeIndex) is not { } palette) {
			return null;
		}

		byte[]? gauBytes = content.Read("gau", hercName + ".GAU");
		if (gauBytes == null || new GauFileTransformer().Parse(gauBytes) is not { } gau) {
			return null;
		}

		var front = LoadFrame(content, "hb0", hercName + ".HB0", palette);
		var side = LoadFrame(content, "hb2", hercName + ".HB2", palette);
		if (front == null || side == null) {
			return null;
		}

		// Not required: a missing HB1 costs the heads-down view, not the cockpit.
		var headsDown = LoadFrame(content, "hb1", hercName + ".HB1", palette);

		bool clipped = CutViewportHole(content, hercName, ForwardViewIndex, front)
			& CutViewportHole(content, hercName, SideViewIndex, side);

		var colors = HudColorTable.Load(content);
		var viewGeometry = CockpitViewGeometry.Load(content, hercName);

		// The herc's own bank goes in alongside the shared ones: it holds the paper-doll wireframe
		// frames the MFD status screen draws, and its name is the herc's.
		// The herc's own bank and every other machine's go in alongside the shared ones: each holds the
		// paper-doll wireframe frames the MFD status screen draws for that machine, and the bank's name
		// is the machine's. F5 shows the target's doll, so the mission's other types are needed too.
		var hercBanks = new List<string> { hercName.ToUpperInvariant() };
		foreach (string target in targetHercNames ?? Array.Empty<string>()) {
			string upper = target.ToUpperInvariant();
			if (!hercBanks.Contains(upper)) {
				hercBanks.Add(upper);
			}
		}

		var banks = HudBankNames.Concat(hercBanks).ToArray();

		return new CockpitArt(front, side, headsDown, gau,
			HudSpriteSheet.Load(content, palette, banks, HudFontNames, LoResHudBankNames),
			colors,
			ResolveGaugeColors(colors, palette),
			schemeIndex,
			clipped,
			hercName.ToUpperInvariant(),
			SimStringTable.Load(content)) {
			WeaponBarColors = (
				PaletteColor(palette, WeaponBarFillEvenIndex),
				PaletteColor(palette, WeaponBarFillOddIndex)),
			PaperDoll = LoadPaperDoll(content, hercName),
			PaperDolls = hercBanks
				.Select(name => (Name: name, Doll: LoadPaperDoll(content, name)))
				.Where(entry => entry.Doll != null)
				.ToDictionary(entry => entry.Name, entry => entry.Doll!, StringComparer.OrdinalIgnoreCase),
			ViewGeometry = viewGeometry,
			HeadsDownLayout = HddLayout.Load(gau,
				viewGeometry?.CanvasOriginY(CockpitViewGeometry.HeadsDownViewIndex)
					?? CockpitViewGeometry.DefaultHeadsDownOriginY),
			HeadsDownColors = ResolveHeadsDownColors(colors, palette),
			TargetArrowColors = ResolveArrowColors(colors, palette),
			LogicalColors = ResolveLogicalColors(colors, palette),
			PaletteEntries = ResolvePaletteEntries(palette),
		};
	}

	/// <summary>
	/// Reads the herc's cockpit colour-scheme index out of its own <c>dat\&lt;MECH&gt;.DAT</c>, or -1
	/// when that file is missing or unparseable. The field is
	/// <see cref="HercWorks.Core.Data.File.Dat.Sim.HercSimDat.Unk80_ValHudId"/> — record offset 80,
	/// which is the mech type struct's <c>+0x52</c> that
	/// <c>CockpitViewManager_LoadViews</c> indexes <c>COCKPIT.DPL</c> with.
	/// </summary>
	/// <summary>One machine's <c>pdg&lt;HERC&gt;.PDG</c>, or null when it is missing or unparseable.</summary>
	private static PaperDollGraphic? LoadPaperDoll(GameContent content, string hercName) =>
		content.Read("pdg", hercName + ".PDG") is { } bytes
			&& new PaperDiagramGraphTransformer().Parse(bytes) is PaperDollGraphic doll
				? doll
				: null;

	private static int ReadColorSchemeIndex(GameContent content, string hercName) =>
		content.Read("dat", hercName + ".DAT") is { } bytes
			&& new HercSimDataTransformer().Parse(bytes) is HercSimDat data
			? data.Unk80_ValHudId
			: -1;

	/// <summary>All three gauge colours or none — a bar drawn with only some of them resolved would
	/// be worse than one not drawn at all.</summary>
	private static (Vector3, Vector3, Vector3)? ResolveGaugeColors(HudColorTable? colors, DynamixPalette palette) {
		if (colors?.Resolve(HudColorTable.GaugeFillEvenId, palette) is not { } even
			|| colors.Resolve(HudColorTable.GaugeFillOddId, palette) is not { } odd
			|| colors.Resolve(HudColorTable.GaugeRemainderId, palette) is not { } remainder) {
			return null;
		}

		return (ToVector(even), ToVector(odd), ToVector(remainder));
	}

	/// <summary>All three Heads-Down Display fill colours or none, for the reason in
	/// <see cref="ResolveGaugeColors"/>.</summary>
	private static (Vector3, Vector3, Vector3)? ResolveHeadsDownColors(HudColorTable? colors, DynamixPalette palette) {
		if (colors?.Resolve(HudColorTable.HeadsDownBackgroundId, palette) is not { } background
			|| colors.Resolve(HudColorTable.HeadsDownIndicatorId, palette) is not { } indicator
			|| colors.Resolve(HudColorTable.HeadsDownSubjectPlateId, palette) is not { } plate) {
			return null;
		}

		return (ToVector(background), ToVector(indicator), ToVector(plate));
	}

	/// <summary>Both arrow colours or neither, for the reason in <see cref="ResolveGaugeColors"/>.</summary>
	private static (Vector3, Vector3)? ResolveArrowColors(HudColorTable? colors, DynamixPalette palette) {
		if (colors?.Resolve(TargetBox.ArrowColorId, palette) is not { } unlocked
			|| colors.Resolve(TargetBox.ArrowLockedColorId, palette) is not { } locked) {
			return null;
		}

		return (ToVector(unlocked), ToVector(locked));
	}

	/// <summary>Every id in the table, resolved once — see <see cref="LogicalColor"/>.</summary>
	private static Vector3?[]? ResolveLogicalColors(HudColorTable? colors, DynamixPalette palette) {
		if (colors == null) {
			return null;
		}

		var resolved = new Vector3?[HudColorTable.EntryCount];
		for (int id = 0; id < resolved.Length; id++) {
			resolved[id] = colors.Resolve(id, palette) is { } color ? ToVector(color) : null;
		}

		return resolved;
	}

	/// <summary>The whole live palette, resolved once — see <see cref="PaletteEntry"/>.</summary>
	private static Vector3?[] ResolvePaletteEntries(DynamixPalette palette) {
		var resolved = new Vector3?[256];
		for (int index = 0; index < resolved.Length; index++) {
			resolved[index] = palette.Colors.TryGetValue(index, out var entry) ? ToVector(entry.GetColor()) : null;
		}

		return resolved;
	}

	/// <summary>One live-palette slot as a colour, for the widgets that name a raw index rather than
	/// a <c>COLORS.DAT</c> id. Black when the palette has no such slot.</summary>
	private static Vector3 PaletteColor(DynamixPalette palette, int index) =>
		palette.Colors.TryGetValue(index, out var entry) ? ToVector(entry.GetColor()) : Vector3.Zero;

	private static Vector3 ToVector(HercWorks.Core.Data.Struct.RgbaColor c) =>
		new(c.R / 255f, c.G / 255f, c.B / 255f);

	/// <summary>
	/// Reads and decodes one <c>.HBx</c> file's single frame through <paramref name="palette"/>.
	/// Mirrors <c>TextureAtlas.DecodeFrame</c>'s indexed-colour expansion exactly (including no
	/// special-casing of index 0 here — the viewport cutout is a separate, deliberate pass, not a
	/// side effect of decoding).
	/// </summary>
	// The ring colours currently painted into the frames, so a repaint is skipped on the many frames
	// where the charge did not move far enough to change any of the six.
	private HercWorks.Core.Data.Struct.ColorBytes[]? _shieldRingColors;

	/// <summary>
	/// Repaints the shield meter's rings for the charge the machine is actually holding —
	/// <c>ShieldsGauge</c>'s per-frame palette write, applied to already-decoded art.
	///
	/// <para>Charges are on the widget's own 0..<c>0x800</c> scale; use
	/// <see cref="CockpitPalette.ShieldFacingCharge"/> to get there from raw simulation charge. The
	/// rings genuinely go dark as a facing empties, which is the whole point of the meter and is what
	/// the palette baked at load could never show.</para>
	/// </summary>
	/// <returns>
	/// True when pixels changed and the caller must re-upload the affected textures. False means the
	/// six colours resolved identically to last time and nothing was touched.
	/// </returns>
	public bool UpdateShieldRings(int frontCharge, int rearCharge) {
		var colors = CockpitPalette.ShieldRampEntries(frontCharge, rearCharge);

		if (_shieldRingColors is { } previous) {
			bool same = true;
			for (int i = 0; i < colors.Length && same; i++) {
				var a = previous[i].GetColor();
				var b = colors[i].GetColor();
				same = a.R == b.R && a.G == b.G && a.B == b.B;
			}

			if (same) {
				return false;
			}
		}

		_shieldRingColors = colors;

		// Every frame gets the pass, not just the front one: a herc whose side or heads-down art
		// happens to use these indices would otherwise drift out of step with the front panel.
		Paint(Front);
		Paint(Side);
		if (HeadsDown is { } headsDown) {
			Paint(headsDown);
		}

		return true;

		void Paint(CockpitFrame frame) {
			for (int slot = 0; slot < colors.Length; slot++) {
				var color = colors[slot].GetColor();
				foreach (int offset in frame.ShieldRingPixels[slot]) {
					frame.Pixels[offset] = color.R;
					frame.Pixels[offset + 1] = color.G;
					frame.Pixels[offset + 2] = color.B;
				}
			}
		}
	}

	private static CockpitFrame? LoadFrame(GameContent content, string folder, string name, DynamixPalette palette) {
		byte[]? bytes = content.Read(folder, name);
		if (bytes == null
			|| new DynamixBitmapArrayTransformer().Parse(bytes) is not DynamixBitmapArray array
			|| array.Images is not { Length: > 0 } images
			|| images[0] is not { } frame
			|| frame.Cols <= 0 || frame.Rows <= 0) {
			return null;
		}

		int width = frame.Cols;
		int height = frame.Rows;
		var pixels = new byte[width * height * 4];
		byte[] indices = frame.ImageData ?? Array.Empty<byte>();
		int count = Math.Min(indices.Length, width * height);

		var ringPixels = new List<int>[CockpitPalette.ShieldRampLength];
		for (int i = 0; i < ringPixels.Length; i++) {
			ringPixels[i] = new List<int>();
		}

		for (int i = 0; i < count; i++) {
			int index = indices[i];
			var color = palette.Colors.TryGetValue(index, out var entry)
				? entry.GetColor()
				: new HercWorks.Core.Data.Struct.RgbaColor(255, (byte)index, (byte)index, (byte)index);

			pixels[i * 4] = color.R;
			pixels[i * 4 + 1] = color.G;
			pixels[i * 4 + 2] = color.B;
			pixels[i * 4 + 3] = 255;

			int slot = index - CockpitPalette.ShieldRampFirstSlot;
			if (slot >= 0 && slot < CockpitPalette.ShieldRampLength) {
				ringPixels[slot].Add(i * 4);
			}
		}

		return new CockpitFrame(pixels, width, height) {
			ShieldRingPixels = ringPixels.Select(list => list.ToArray()).ToArray(),
		};
	}

	/// <summary>
	/// Punches the view's 3D-viewport hole into <paramref name="frame"/>'s alpha channel using the
	/// herc's own <c>.HD&lt;view&gt;</c> region file — the same data DBSIM's rasterizer is span-clipped
	/// to. Returns true when that file supplied the mask, false when it was missing and
	/// <see cref="CutViewportHoleByColor"/> had to infer one instead.
	///
	/// <para>A file that parses to zero spans is honoured as zero spans, not treated as a failure: that
	/// is a legitimate "this view shows no 3D" (the heads-down view's file is 16 bytes of zeroes). The
	/// forward and side views this class loads both have real spans, so a zero-span result here would
	/// mean something else is wrong — but silently substituting a guessed mask would hide it.</para>
	/// </summary>
	private static bool CutViewportHole(GameContent content, string hercName, int viewIndex, CockpitFrame frame) {
		if (CockpitClipRegions.Load(content, hercName, viewIndex) is not { } regions) {
			CutViewportHoleByColor(frame);
			return false;
		}

		byte[] pixels = frame.Pixels;
		int rows = Math.Min(regions.RowCount, frame.Height);
		for (int y = 0; y < rows; y++) {
			foreach (var span in regions.Row(y)) {
				int from = Math.Max(span.Start, 0);
				int to = Math.Min(span.Start + span.Length, frame.Width);
				for (int x = from; x < to; x++) {
					pixels[(y * frame.Width + x) * 4 + 3] = 0;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Fallback cutout for when the region file is unavailable: flood-fills every connected region of
	/// pure-black (0,0,0) pixels that touches the image's outer edge, setting each one's alpha to 0.
	/// Multi-source (every border pixel is a candidate seed) rather than a single interior point,
	/// because the canopy strut splits the hole into 2-3 disconnected islands and a single seed only
	/// catches the one it sits in.
	///
	/// <para>Kept only as a fallback. It infers the hole from art that happens to be black there, which
	/// is not the same statement as the region file's, and it cannot know about rows the original leaves
	/// opaque regardless of their colour.</para>
	/// </summary>
	private static void CutViewportHoleByColor(CockpitFrame frame) {
		byte[] pixels = frame.Pixels;
		bool IsBlackOpaque(int x, int y) {
			int i = (y * frame.Width + x) * 4;
			return pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0 && pixels[i + 3] != 0;
		}

		var queue = new Queue<(int X, int Y)>();
		for (int x = 0; x < frame.Width; x++) {
			queue.Enqueue((x, 0));
			queue.Enqueue((x, frame.Height - 1));
		}
		for (int y = 0; y < frame.Height; y++) {
			queue.Enqueue((0, y));
			queue.Enqueue((frame.Width - 1, y));
		}

		while (queue.Count > 0) {
			var (x, y) = queue.Dequeue();
			if (x < 0 || x >= frame.Width || y < 0 || y >= frame.Height || !IsBlackOpaque(x, y)) {
				continue;
			}

			pixels[(y * frame.Width + x) * 4 + 3] = 0;

			queue.Enqueue((x + 1, y));
			queue.Enqueue((x - 1, y));
			queue.Enqueue((x, y + 1));
			queue.Enqueue((x, y - 1));
		}
	}
}
