using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Io.Transform.Common;
using Herculan.Engine.Content;
using Herculan.Engine.World;

namespace Herculan.Engine.Shell;

/// <summary>
/// Why <c>Rock &amp; Roll &gt;</c> refused to launch — the code <c>Mission_OnRockAndRoll</c> (<c>00445509</c>)
/// hands the refusal dialog, <c>FUN_0044d27c</c>, which prints the two <c>estext.bin</c> lines from
/// <c>0x13c + 2 * code</c>.
/// </summary>
public enum ShellLaunchRefusal {
	/// <summary><c>Your herc is not functional.</c> — the player's machine is not built or not flightworthy.</summary>
	NotFunctional = 0,

	/// <summary><c>Your herc is unarmed.</c></summary>
	Unarmed = 1,

	/// <summary><c>You have no herc assignment.</c> — the player has no bay.</summary>
	NoAssignment = 2,

	/// <summary><c>One or more hercs of your squad is unarmed.</c></summary>
	SquadUnarmed = 3,
}

/// <summary>
/// <c>Rock &amp; Roll &gt;</c>, <c>Mission_OnRockAndRoll</c> (<c>00445509</c>): four tests in order, the
/// first to fail refusing the launch, and otherwise <c>Game_ExportMissionHandoff</c> (<c>0040f0d4</c>)
/// and the exit code that tells the launcher to run the simulator. See
/// docs/shell/screen-layout.md#the-mission-screen and docs/shell/campaign-loop.md for the handoff.
/// </summary>
public static class ShellMissionLaunch {
	/// <summary>
	/// The first weapon id that does not arm a machine: <c>FUN_004116ec</c> counts a mount only while its
	/// id is below <c>0x1d</c>, so the four pods, <c>TARG</c> to <c>ENRG</c>, leave a machine unarmed.
	/// </summary>
	public const int FirstUnarmingWeapon = 0x1d;

	/// <summary>How many catalog ids the export's closing table carries, <c>PlayerMec_WriteUnlockTable</c>'s literal <c>0x21</c>.</summary>
	public const int WeaponCatalogCount = 0x21;

	/// <summary>The mount slots <c>FUN_004116ec</c> walks — all ten a record has, whatever its capacity.</summary>
	private const int MountSlots = 10;

	/// <summary>
	/// The four tests, in <c>Mission_OnRockAndRoll</c>'s order: the player has a bay (<c>00482a9e != -1</c>);
	/// its machine is built and flightworthy (<c>FUN_00410a9d</c>); it is armed (<c>FUN_00410b11</c>); and
	/// every squad member on strength in a position in play has an armed machine (<c>FUN_0040f6c6</c>).
	/// Null when the launch goes ahead.
	/// </summary>
	public static ShellLaunchRefusal? Check(ShellHangar hangar) {
		if (hangar.Player is not { Bay: not -1 } player) {
			return ShellLaunchRefusal.NoAssignment;
		}

		if (hangar.Bay(player.Bay) is not { IsBuilt: true, IsFlightworthy: true } machine) {
			return ShellLaunchRefusal.NotFunctional;
		}

		if (!IsArmed(machine)) {
			return ShellLaunchRefusal.Unarmed;
		}

		for (int position = 1; position < hangar.SquadPositions; position++) {
			if (hangar.SquadMemberAt(position) is { OnStrength: true } member && !IsArmed(hangar.Bay(member.Bay))) {
				return ShellLaunchRefusal.SquadUnarmed;
			}
		}

		return null;
	}

	/// <summary><c>FUN_00410b11</c> through <c>FUN_004116ec</c>: a machine is armed when any of its ten mounts holds a weapon below <see cref="FirstUnarmingWeapon"/>.</summary>
	public static bool IsArmed(ShellBayMachine? machine) {
		if (machine == null) {
			return false;
		}

		for (int slot = 0; slot < MountSlots; slot++) {
			if (machine.Mount(slot) is { WeaponId: < FirstUnarmingWeapon }) {
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// <c>data\player.mec</c> as <c>Game_ExportMissionHandoff</c> writes it: the player's entry index, always
	/// 0; the count at <c>00482a7a</c>; the player's machine; then the machine of each squad member on
	/// strength, walking the positions in play from 1; and the weapon unlock table. Each entry is the
	/// pilot's name index and skill, then <c>FUN_004106b7</c>'s record of the pilot's bay. The count is
	/// written as the player structure holds it, whatever the entries come to, and a bay with no machine
	/// writes its two leading fields and nothing after them, as the original's does.
	/// </summary>
	public static byte[] ExportPlayerMec(ShellHangar hangar) {
		var entries = new List<MecEntry>();
		var hasMachine = new List<bool>();

		void Add(ShellBayPilot pilot) {
			var entry = new MecEntry { PilotNameIndex = (short)pilot.NameIndex, Unk02 = (short)pilot.Skill };
			if (hangar.Bay(pilot.Bay) is { } machine) {
				int capacity = Math.Max(machine.MountCapacity, 0);
				entry.MechType = (short)machine.ChassisType;
				entry.SlotCount = (short)capacity;
				entry.WeaponRefs = Enumerable.Range(0, capacity).Select(slot => (short)machine.WeaponAt(slot)).ToArray();
				entry.WeaponAmmoTypes = Enumerable.Range(0, capacity)
					.Select(slot => (short)(machine.Mount(slot)?.Guidance ?? ShellWeaponUnit.NoGuidance)).ToArray();
				(entry.BlockA, entry.BlockB, entry.BlockC) = machine.StatusBlock();
				hasMachine.Add(true);
			} else {
				hasMachine.Add(false);
			}

			entries.Add(entry);
		}

		if (hangar.Player is { } player) {
			Add(player);
		}

		for (int position = 1; position < hangar.SquadPositions; position++) {
			if (hangar.SquadMemberAt(position) is { OnStrength: true } member) {
				Add(member);
			}
		}

		var flags = Enumerable.Range(0, WeaponCatalogCount).Select(id => (byte)(hangar.IsWeaponUnlocked(id) ? 1 : 0)).ToArray();

		// MecFileTransformer writes whole entries and takes the count from the array, so the header and
		// any bay-less entry are laid down here and only full entries go through it.
		using var stream = new MemoryStream();
		var writer = new BinaryWriter(stream);
		writer.Write((short)0);
		writer.Write((short)hangar.MachinesOnStrength);
		var transformer = new MecFileTransformer();
		for (int i = 0; i < entries.Count; i++) {
			if (hasMachine[i]) {
				byte[] single = transformer.Write(new MecFile { Entries = new[] { entries[i] } })!;
				writer.Write(single, 4, single.Length - 4);
			} else {
				writer.Write(entries[i].PilotNameIndex);
				writer.Write(entries[i].Unk02);
			}
		}

		writer.Write((short)flags.Length);
		writer.Write(flags);
		writer.Flush();
		return stream.ToArray();
	}

	/// <summary>
	/// Writes the handoff the simulator loads into <paramref name="directory"/>: the loaded slot's own
	/// <c>script%d.dat</c> and <c>missn%d.str</c>, which <c>Career_LoadSlot</c> copies into <c>data\</c>
	/// when the slot is loaded; <c>mission.var</c>, the 2000-byte campaign flag array
	/// <c>MissionVar_Write</c> (<c>0040e9cb</c>) writes; and <c>player.mec</c> from the hangar as the
	/// screens have left it. Returns the path of the <c>script.dat</c> written, or null when the slot has
	/// no mission file to copy.
	/// </summary>
	public static string? WriteHandoff(string directory, string installRoot, int slot, PlayerSave save, ShellHangar hangar) {
		string saves = ShellSaveSlots.Directory(installRoot);
		string script = Path.Combine(saves, $"script{slot}.dat");
		if (!File.Exists(script)) {
			return null;
		}

		Directory.CreateDirectory(directory);
		string scriptPath = Path.Combine(directory, MissionLoader.ScriptFileName);
		File.Copy(script, scriptPath, overwrite: true);

		string text = Path.Combine(saves, $"missn{slot}.str");
		string textPath = Path.Combine(directory, MissionLoader.TextFileName);
		if (File.Exists(text)) {
			File.Copy(text, textPath, overwrite: true);
		} else if (File.Exists(textPath)) {
			File.Delete(textPath);
		}

		if (save.HasCampaignState) {
			var flags = new byte[PlayerSave.CampaignFlagCount * 2];
			for (int i = 0; i < PlayerSave.CampaignFlagCount; i++) {
				BitConverter.GetBytes(save.GetCampaignFlag(i)).CopyTo(flags, i * 2);
			}

			File.WriteAllBytes(Path.Combine(directory, MissionVarFileName), flags);
		}

		File.WriteAllBytes(Path.Combine(directory, MissionLoader.PlayerFileName), ExportPlayerMec(hangar));
		return scriptPath;
	}

	/// <summary>The flag array's file, <c>data\mission.var</c>.</summary>
	public const string MissionVarFileName = "mission.var";
}

/// <summary>
/// The dialog <c>Rock &amp; Roll &gt;</c> refuses through: a <c>WARNING!</c> alert with two centred lines
/// and <c>OKAY</c>, built once at startup by the function ending at <c>0044d27c</c>, filled and put up by
/// <c>FUN_0044d27c(code)</c> and taken down by <c>OKAY</c>'s handler, <c>FUN_0044d404</c>. Like the scrap
/// dialog it is placed in a window the size of the display, so its rect is a canvas rect, and while it
/// is up this engine hit-tests nothing but <c>OKAY</c>, which is this engine's choice.
/// </summary>
public sealed class ShellLaunchRefusalDialog {
	/// <summary>The alert, an <c>ESAlert</c>, in the canvas.</summary>
	public static readonly ShellRect PanelRect = new(0xb1, 0x67, 0x1d2, 0xc6);

	private static readonly ShellRect FirstLineRect = new(5, 0x1e, 0x117, 0x2b);
	private static readonly ShellRect SecondLineRect = new(5, 0x2c, 0x117, 0x39);
	private static readonly ShellRect OkayRect = new(0x5f, 0x45, 0xc3, 0x54);

	/// <summary>Whether the dialog is up.</summary>
	public bool IsOpen { get; private set; }

	/// <summary>What the dialog is saying, while it is up.</summary>
	public ShellLaunchRefusal Refusal { get; private set; }

	public void Open(ShellLaunchRefusal refusal) {
		Refusal = refusal;
		IsOpen = true;
	}

	public void Close() => IsOpen = false;

	/// <summary><c>OKAY</c>'s rect, in the canvas.</summary>
	public static ShellRect OkayButtonRect => Inside(PanelRect, OkayRect);

	/// <summary><c>OKAY</c> under a canvas point, or null — a click anywhere else is swallowed.</summary>
	public ShellHit? HitAt(float canvasX, float canvasY) =>
		OkayButtonRect.Contains(canvasX, canvasY)
			? ShellHit.Button(new ShellWidget(ShellWidgetKind.LaunchRefusalOkay, 0), OkayButtonRect, canvasX, canvasY)
			: null;

	/// <summary>
	/// Draws the dialog over whatever <paramref name="surface"/> holds. The builder writes the face and the
	/// title plate and leaves <c>TitledPanel_Ctor</c>'s filled body; the two lines are centred in <c>0x29</c>
	/// with no backing.
	/// </summary>
	public void Paint(ShellSurface surface, ShellText? text, HudSpriteSheet? sprites) {
		if (!IsOpen) {
			return;
		}

		var font = sprites?.Font(ShellArt.ScreenFont);
		ShellChrome.PaintTitledPanel(surface, PanelRect, PanelBorder, PanelFace, ShellChrome.InteriorColor, TitleHeight,
			headerChrome: true, TitlePlateFirst, TitlePlateLast, fill: true);
		ShellChrome.PaintText(surface, new ShellRect(PanelRect.X0, PanelRect.Y0, PanelRect.X1, PanelRect.Y0 + TitleHeight),
			font, text?.Text(TitleText), ShellTextAlign.Center, ShellChrome.FontInkColor);

		int first = FirstLineText + 2 * (int)Refusal;
		ShellChrome.PaintText(surface, Inside(PanelRect, FirstLineRect), font, text?.Text(first), ShellTextAlign.Center,
			ShellChrome.FontInkColor);
		ShellChrome.PaintText(surface, Inside(PanelRect, SecondLineRect), font, text?.Text(first + 1),
			ShellTextAlign.Center, ShellChrome.FontInkColor);

		var okay = OkayButtonRect;
		ShellChrome.PaintButton(surface, okay, ButtonBorder);
		ShellChrome.PaintText(surface, new ShellRect(okay.X0 + 1, okay.Y0, okay.X1, okay.Y1), font, text?.Text(OkayText),
			ShellTextAlign.Center, ShellChrome.FontInkColor);
	}

	private static ShellRect Inside(ShellRect parent, ShellRect child) =>
		new(parent.X0 + child.X0, parent.Y0 + child.Y0, parent.X0 + child.X1, parent.Y0 + child.Y1);

	/// <summary><c>ESAlert_Ctor</c>'s border argument and its header height, and the face and plate the builder writes.</summary>
	private const byte PanelBorder = 0x15;
	private const int TitleHeight = 0x14;
	private const byte PanelFace = 0x25;
	private const int TitlePlateFirst = 100;
	private const int TitlePlateLast = 0xbd;

	private const byte ButtonBorder = 0x22;

	/// <summary><c>estext.bin</c> indices: <c>WARNING!</c>, the first of the eight refusal lines, and <c>OKAY</c>.</summary>
	private const int TitleText = 0x13b;
	private const int FirstLineText = 0x13c;
	private const int OkayText = 0x144;
}
