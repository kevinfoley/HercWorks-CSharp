using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Core.Io.Transform.Shell;
using Herculan.Engine.Content;

namespace Herculan.Engine.Shell;

/// <summary>
/// The squad panel's picture of the selected bay on the WEAPONS, BUILD and CREW tabs: the machine in
/// three-quarter view as two stacked halves, with each fitted weapon drawn over its socket. Filled by
/// <c>Squad_BuildBayPictures</c> (<c>00414e5b</c>) from <c>gam\arm_*.dat</c> into the eight bay pictures <c>Squad_BuildRosterList</c>
/// built, and by the same function into the empty picture shown when no bay is selected. See
/// docs/shell/screen-layout.md, "The bay picture".
/// </summary>
public sealed class ShellBayPictures {
	/// <summary>
	/// Where <c>Squad_BuildBayPictures</c> (<c>00414e5b</c>) puts the eight bay pictures: moved to <c>{5, 0x2b}</c> and sized
	/// <c>0xe7</c> by <c>0x105</c>, which with the far corner inclusive is one row short of the rect
	/// they were built at.
	/// </summary>
	public static readonly ShellRect PictureRect = new(5, 0x2b, 5 + 0xe7 - 1, 0x2b + 0x105 - 1);

	/// <summary>The empty picture (<c>DAT_0048d4dc</c>), which keeps <c>Squad_BuildRosterList</c>'s own rect.</summary>
	public static readonly ShellRect EmptyPictureRect = new(5, 0x2b, 0xeb, 0x130);

	/// <summary>The bank the empty picture and an unstarted machine are drawn from: two halves of an empty bay.</summary>
	private const string EmptyBayBankName = "MT_3QTR";

	/// <summary>Where the empty bay's two halves sit, both with flags 0.</summary>
	private const int EmptyHalfX = 1;
	private const int EmptyTopY = 1;
	private const int EmptyBottomY = 0x8c;

	/// <summary>
	/// The part slot a mount's weapon is looked up by in its weapon's group, <c>slot + 2</c> — past the
	/// two halves of the body, which every retail layout puts in slots 0 and 1.
	/// </summary>
	private const int FirstWeaponPart = 2;

	/// <summary>
	/// The body frames an unfinished machine shows: 2 and 3 below this build percentage, 4 and 5 from it
	/// on. The retail banks do not all carry them — <c>out_bod</c> and <c>mav_bod</c> have two frames,
	/// <c>rap_bod</c> and <c>tom_bod</c> four and the rest six — and a frame the bank lacks draws nothing.
	/// </summary>
	private const int HalfBuiltPercent = 0x33;
	private const int EarlyBuildTopFrame = 2;
	private const int LateBuildTopFrame = 4;

	/// <summary>The layout files, in chassis-type order — the table at <c>0046ff2c</c>.</summary>
	private static readonly string[] LayoutStems = { "OUTL", "RAPT", "TOMA", "SAMS", "COLO", "APOC", "OGRE", "MAVR", "RAZR" };

	/// <summary>
	/// The banks' own stems, in the same order — the tables at <c>0046ff74</c> (<c>_bod</c>) and
	/// <c>0046ff9c</c> (<c>_wep</c>). They are not the layout stems: the Razor's is <c>fly</c>.
	/// </summary>
	private static readonly string[] BankStems = { "OUT", "RAP", "TOM", "SAM", "COL", "APOC", "OGR", "MAV", "FLY" };

	private readonly ArmHerc?[] _layouts;
	private readonly DynamixBitmap[]?[] _bodyBanks;
	private readonly DynamixBitmap[]?[] _weaponBanks;
	private readonly DynamixBitmap[]? _emptyBay;

	private ShellBayPictures(ArmHerc?[] layouts, DynamixBitmap[]?[] bodyBanks, DynamixBitmap[]?[] weaponBanks,
			DynamixBitmap[]? emptyBay) {
		_layouts = layouts;
		_bodyBanks = bodyBanks;
		_weaponBanks = weaponBanks;
		_emptyBay = emptyBay;
	}

	/// <summary>
	/// Reads the nine <c>gam\arm_*.dat</c> layouts and the nineteen banks they draw from. Anything
	/// missing leaves that chassis's picture empty rather than failing the screen.
	/// </summary>
	public static ShellBayPictures Load(GameContent content) {
		var layouts = new ArmHerc?[LayoutStems.Length];
		var bodyBanks = new DynamixBitmap[]?[LayoutStems.Length];
		var weaponBanks = new DynamixBitmap[]?[LayoutStems.Length];

		for (int type = 0; type < LayoutStems.Length; type++) {
			layouts[type] = content.Read(ShellRepairCosts.CatalogFolder, $"ARM_{LayoutStems[type]}.DAT") is { } bytes
				? new ArmHercTransformer().Parse(bytes) : null;
			bodyBanks[type] = ShellArt.ReadBankFrames(content, $"{BankStems[type]}_BOD");
			weaponBanks[type] = ShellArt.ReadBankFrames(content, $"{BankStems[type]}_WEP");
		}

		return new ShellBayPictures(layouts, bodyBanks, weaponBanks,
			ShellArt.ReadBankFrames(content, EmptyBayBankName));
	}

	/// <summary>How many chassis have a layout — nine when the archive is complete.</summary>
	public int LayoutCount => _layouts.Count(layout => layout != null);

	/// <summary>
	/// Paints the picture <c>Squad_ShowPanel</c> puts up: the selected bay's, or the empty picture when
	/// no bay is selected. Both have their grid lines off.
	/// </summary>
	public void Paint(ShellSurface surface, ShellHangar hangar, int selectedBay) {
		if (selectedBay < 0) {
			ShellGrid.Paint(surface, EmptyPictureRect, gridLines: false, EmptyBayParts());
			return;
		}

		ShellGrid.Paint(surface, PictureRect, gridLines: false,
			hangar.Bay(selectedBay) is { } machine ? MachineParts(machine) : EmptyBayParts());
	}

	/// <summary>The empty bay: <c>mt_3qtr</c>'s two frames, top and bottom, in slots 0 and 1.</summary>
	private ShellGridPart?[] EmptyBayParts() {
		var parts = new ShellGridPart?[ShellGrid.PartSlots];
		SetPart(parts, 0, Frame(_emptyBay, 0), EmptyHalfX, EmptyTopY, 0);
		SetPart(parts, 1, Frame(_emptyBay, 1), EmptyHalfX, EmptyBottomY, 0);
		return parts;
	}

	/// <summary>
	/// One machine: the layout's two body records, each in the slot its id names, and each fitted
	/// mount's record from its weapon's group. The body's frames follow the build: the record's own
	/// frame once the machine is finished, the empty bay while it is at 0%, and the body bank's frames 2
	/// and 3 or 4 and 5 in between. None of the parts is recoloured — every remap pair is left at the
	/// default.
	/// </summary>
	private ShellGridPart?[] MachineParts(ShellBayMachine machine) {
		var parts = new ShellGridPart?[ShellGrid.PartSlots];
		int type = machine.ChassisType;
		if (type < 0 || type >= _layouts.Length || _layouts[type] is not { } layout) {
			return parts;
		}

		var body = _bodyBanks[type];
		if (layout.HercTopImg is { } top) {
			var frame = machine.IsBuilt ? Frame(body, top.FrameId)
				: machine.BuildPercent == 0 ? Frame(_emptyBay, 0)
				: Frame(body, machine.BuildPercent < HalfBuiltPercent ? EarlyBuildTopFrame : LateBuildTopFrame);
			SetPart(parts, layout.TopImgArrId, frame, top.OriginX, top.OriginY, top.Flags?.Val ?? 0);
		}

		if (layout.HercBotImg is { } bottom) {
			var frame = machine.IsBuilt ? Frame(body, bottom.FrameId)
				: machine.BuildPercent == 0 ? Frame(_emptyBay, 1)
				: Frame(body, (machine.BuildPercent < HalfBuiltPercent ? EarlyBuildTopFrame : LateBuildTopFrame) + 1);
			SetPart(parts, layout.BottomImgArrId, frame, bottom.OriginX, bottom.OriginY, bottom.Flags?.Val ?? 0);
		}

		for (int mount = 0; mount < machine.MountCapacity; mount++) {
			if (WeaponRecord(layout, machine.WeaponAt(mount), mount) is { } record) {
				// LoadArmoryLayouts adds one to both corners of every weapon record as it reads them, and
				// to neither of the two body records.
				SetPart(parts, record.Id, Frame(_weaponBanks[type], record.FrameId), record.OriginX + 1,
					record.OriginY + 1, record.Flags?.Val ?? 0);
			}
		}

		return parts;
	}

	/// <summary>
	/// <c>RepairLayout_FindWeaponPart</c> (<c>00413ccc</c>) over the arming layout — the record in the fitted weapon's group whose id is
	/// <c>slot + 2</c>. An empty mount, or a weapon with no art for that socket, has none, and so does a
	/// record whose frame is <c>-1</c>.
	/// </summary>
	private static UiHardpointGraphic? WeaponRecord(ArmHerc layout, int weaponId, int mount) {
		if (weaponId <= 0 || layout.WeaponHardpoints is not { } groups
			|| !groups.TryGetValue((short)weaponId, out var sockets)) {
			return null;
		}

		return sockets.FirstOrDefault(socket => socket.Id == FirstWeaponPart + mount && socket.FrameId != -1);
	}

	private static void SetPart(ShellGridPart?[] parts, int slot, DynamixBitmap? frame, int x, int y, int flags) {
		if (slot >= 0 && slot < ShellGrid.PartSlots && frame != null) {
			parts[slot] = new ShellGridPart(frame, x, y, flags, Array.Empty<(byte, byte)>());
		}
	}

	private static DynamixBitmap? Frame(DynamixBitmap[]? bank, int frame) =>
		bank != null && frame >= 0 && frame < bank.Length ? bank[frame] : null;
}
