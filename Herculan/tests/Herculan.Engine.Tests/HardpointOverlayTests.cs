using Herculan.Engine.Content;
using Herculan.Engine.Shell;
using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Io.Transform.Shell;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// <c>gam\arm_hots.dat</c> and <c>gam\rpr_hots.dat</c> — the clickable regions laid over a chassis
/// picture, both read by <c>Squad_BuildScreen</c> (<c>0043c1a0</c>) through one piece of code.
///
/// <para>What these pin is the reading that a parse alone cannot settle: the four <c>int32</c> are
/// two inclusive corners, not a corner and a size. Retail's values stay inside the canvas either way,
/// so the discriminator is the geometry — a chassis's left and right hardpoints are mirror images
/// only when read as corners — plus the fact that the bytes go straight to <c>Panel_Ctor</c>'s rect
/// argument. Get it wrong and every hotspot is the right shape in the wrong place.</para>
///
/// <para>Retail-data cases skip silently without an install, as the rest of the suite does.</para>
/// </summary>
public class HardpointOverlayTests {
	/// <summary>The chassis count the reader asserts.</summary>
	private const int ChassisCount = 9;

	private static HardpointOverlayConfig? Load(string name) {
		if (GameInstall.Locate(null) is not { } root) {
			return null;
		}

		var content = GameContent.Mount(GameInstall.ArchiveDirectory(root), new[] { "SHELL0.VOL" });
		return content.Read("gam", name) is { } bytes
			? new HardpointOverlayTransformer().Parse(bytes)
			: null;
	}

	/// <summary>
	/// Nine groups with sequential ids in both files, and the per-chassis area counts. The arming
	/// counts are each chassis's mount capacity and the repair counts are a flat six, which is what
	/// says the two files carry different things through the same format.
	/// </summary>
	[Theory]
	[InlineData("ARM_HOTS.DAT", new[] { 3, 5, 5, 8, 9, 9, 10, 4, 7 })]
	[InlineData("RPR_HOTS.DAT", new[] { 6, 6, 6, 6, 6, 6, 6, 6, 6 })]
	public void RetailFileHasNineChassisGroups(string name, int[] areaCounts) {
		if (Load(name) is not { } overlay) {
			return;
		}

		Assert.NotNull(overlay.Entries);
		Assert.Equal(ChassisCount, overlay.Entries.Length);

		for (int i = 0; i < overlay.Entries.Length; i++) {
			Assert.Equal(i, overlay.Entries[i].HercId);
			Assert.Equal(areaCounts[i], overlay.Entries[i].Areas!.Length);
		}
	}

	/// <summary>
	/// Every area is a well-formed inclusive rect inside the 640x480 canvas. The all-zero areas two of
	/// the repair groups pad their tails with are the one exception, and they are counted rather than
	/// waved past, so a parse that silently produced more of them would fail here.
	/// </summary>
	[Theory]
	[InlineData("ARM_HOTS.DAT", 0)]
	[InlineData("RPR_HOTS.DAT", 4)]
	public void EveryAreaIsARectInsideTheCanvas(string name, int expectedEmpty) {
		if (Load(name) is not { } overlay) {
			return;
		}

		int empty = 0;
		foreach (var herc in overlay.Entries!) {
			foreach (var area in herc.Areas!) {
				if (area is { X0: 0, Y0: 0, X1: 0, Y1: 0 }) {
					empty++;
					continue;
				}

				Assert.True(area.X1 > area.X0, $"chassis {herc.HercId} area {area.Id}: x1 <= x0");
				Assert.True(area.Y1 > area.Y0, $"chassis {herc.HercId} area {area.Id}: y1 <= y0");
				Assert.InRange(area.X0, 0, ShellLayout.CanvasWidth - 1);
				Assert.InRange(area.X1, 0, ShellLayout.CanvasWidth - 1);
				Assert.InRange(area.Y0, 0, ShellLayout.CanvasHeight - 1);
				Assert.InRange(area.Y1, 0, ShellLayout.CanvasHeight - 1);
			}
		}

		Assert.Equal(expectedEmpty, empty);
	}

	/// <summary>
	/// The first chassis's first two arming areas, verbatim, and the mirror symmetry that settles the
	/// corner-versus-size reading. Read as <c>{x, y, w, h}</c> these would be two overlapping boxes
	/// 70 and 191 pixels wide; read as corners they are a 34x32 and a 35x32 hardpoint either side of
	/// x≈114.
	/// </summary>
	[Fact]
	public void ArmingHardpointsAreMirroredAboutTheChassisCentre() {
		if (Load("ARM_HOTS.DAT") is not { } overlay) {
			return;
		}

		var areas = overlay.Entries![0].Areas!;
		var left = areas[0];
		var right = areas[1];

		Assert.Equal((37, 94, 70, 125), (left.X0, left.Y0, left.X1, left.Y1));
		Assert.Equal((157, 94, 191, 125), (right.X0, right.Y0, right.X1, right.Y1));

		// Same band, and the two gaps to the mirror axis agree to within a pixel.
		Assert.Equal(left.Y0, right.Y0);
		Assert.Equal(left.Y1, right.Y1);
		Assert.InRange(Math.Abs((left.X0 + right.X1) - (left.X1 + right.X0)), 0, 2);
	}

	/// <summary>
	/// The repair screen's twenty-five hotspots resolve to the three condition arrays, which are the
	/// same three modes the status-block accessor takes. Pinned as a whole table because the split
	/// point is arithmetic — row 6 is hardpoint 0 — and an off-by-one there silently repairs the wrong
	/// component rather than failing.
	/// </summary>
	[Theory]
	[InlineData(0, 0, ShellComponentKind.ExternalGroup, 0)]
	[InlineData(0, 5, ShellComponentKind.ExternalGroup, 5)]
	[InlineData(0, 6, ShellComponentKind.Hardpoint, 0)]
	[InlineData(0, 15, ShellComponentKind.Hardpoint, 9)]
	[InlineData(1, 0, ShellComponentKind.Internal, 0)]
	[InlineData(1, 8, ShellComponentKind.Internal, 8)]
	public void RepairHotspotResolvesToItsComponent(int column, int row, ShellComponentKind kind, int index) =>
		Assert.Equal(new ShellComponentSelection(kind, index), ShellRepairHotspots.Resolve(column, row));

	/// <summary>
	/// The counts are the arrays' own lengths, and the table is exactly as long as the handler table
	/// the screen builds — twenty-five, which is what says neither column has a row unaccounted for.
	/// </summary>
	[Fact]
	public void EveryHandlerInTheTableResolves() {
		Assert.Equal(25, ShellRepairHotspots.HandlerCount);

		int resolved = 0;
		foreach (int column in new[] { ShellRepairHotspots.PictureColumn, ShellRepairHotspots.ListColumn }) {
			for (int row = 0; row < 16; row++) {
				if (ShellRepairHotspots.Resolve(column, row) != null) {
					resolved++;
				}
			}
		}

		Assert.Equal(ShellRepairHotspots.HandlerCount, resolved);
		Assert.Null(ShellRepairHotspots.Resolve(ShellRepairHotspots.PictureColumn, 16));
		Assert.Null(ShellRepairHotspots.Resolve(ShellRepairHotspots.ListColumn, 9));
		Assert.Null(ShellRepairHotspots.Resolve(2, 0));
	}

	/// <summary>
	/// A hardpoint row past the machine's mount capacity, or one with nothing fitted, is refused; a
	/// body group or an internal never is. This is the guard that stops an empty slot being selected.
	/// </summary>
	[Fact]
	public void EmptyOrOutOfRangeHardpointIsNotSelectable() {
		var hardpoint = new ShellComponentSelection(ShellComponentKind.Hardpoint, 3);

		Assert.True(ShellRepairHotspots.IsSelectable(hardpoint, mountCapacity: 5, fittedWeaponId: 12));
		Assert.False(ShellRepairHotspots.IsSelectable(hardpoint, mountCapacity: 3, fittedWeaponId: 12));
		Assert.False(ShellRepairHotspots.IsSelectable(hardpoint, mountCapacity: 5, fittedWeaponId: 0));

		// The other two arrays are always present, so nothing gates them.
		foreach (var kind in new[] { ShellComponentKind.ExternalGroup, ShellComponentKind.Internal }) {
			Assert.True(ShellRepairHotspots.IsSelectable(new ShellComponentSelection(kind, 0), 0, 0));
		}
	}

	/// <summary>
	/// The six areas <c>rpr_hots.dat</c> carries per chassis are the six external groups — the file's
	/// own count agreeing with the hit model's split point, which is one of the three facts that
	/// identifies them.
	/// </summary>
	[Fact]
	public void RepairFileCarriesOneAreaPerExternalGroup() {
		if (Load("RPR_HOTS.DAT") is not { } overlay) {
			return;
		}

		foreach (var herc in overlay.Entries!) {
			Assert.Equal(ShellRepairHotspots.ExternalGroupCount, herc.Areas!.Length);
		}
	}

	/// <summary>
	/// The damage ladder, at every band edge. The comparison is strictly-greater, so the boundary
	/// values are where an off-by-one would show and nowhere else.
	/// </summary>
	[Theory]
	[InlineData(100, 0)]
	[InlineData(90, 0)]
	[InlineData(89, 1)]
	[InlineData(80, 1)]
	[InlineData(79, 2)]
	[InlineData(60, 2)]
	[InlineData(59, 3)]
	[InlineData(30, 3)]
	[InlineData(29, 4)]
	[InlineData(1, 4)]
	[InlineData(0, ShellDamageLevel.Destroyed)]
	public void DamageLevelBandsMatchTheRepairLadder(int condition, int level) =>
		Assert.Equal(level, ShellDamageLevel.For(condition));

	/// <summary>
	/// The retail string overrun: six levels, four words. Levels 4 and 5 index past the run, and the
	/// port reproduces that rather than clamping — see Herculan/KNOWN_ISSUES.md. Pinned so that a later
	/// change which "fixes" it has to do so deliberately.
	/// </summary>
	[Fact]
	public void LastTwoDamageLevelsHaveNoWordOfTheirOwn() {
		for (int level = 0; level < ShellDamageLevel.CaptionCount; level++) {
			Assert.True(ShellDamageLevel.HasCaption(level));
			Assert.Equal(ShellDamageLevel.FirstCaption + level, ShellDamageLevel.CaptionIndex(level));
		}

		Assert.False(ShellDamageLevel.HasCaption(4));
		Assert.False(ShellDamageLevel.HasCaption(ShellDamageLevel.Destroyed));

		// 0x6c and 0x6d — "% Complete" and "Unassigned", which is what retail prints there.
		Assert.Equal(0x6c, ShellDamageLevel.CaptionIndex(4));
		Assert.Equal(0x6d, ShellDamageLevel.CaptionIndex(ShellDamageLevel.Destroyed));
	}

	/// <summary>Every level has a colour, and a destroyed component's is the odd one out.</summary>
	[Fact]
	public void EveryDamageLevelHasAColour() {
		Assert.Equal(new[] { 14, 13, 12, 11, 10, 39 }, ShellDamageLevel.Colors);
		Assert.Equal(39, ShellDamageLevel.Color(ShellDamageLevel.Destroyed));
		Assert.Equal(14, ShellDamageLevel.Color(ShellDamageLevel.For(100)));
	}

	/// <summary>
	/// Both files round-trip byte for byte. The writer is independent of the reader, so a field the
	/// reader misordered would come back transposed here rather than cancelling out.
	/// </summary>
	[Theory]
	[InlineData("ARM_HOTS.DAT")]
	[InlineData("RPR_HOTS.DAT")]
	public void RoundTripsRetailFileByteForByte(string name) {
		if (GameInstall.Locate(null) is not { } root) {
			return;
		}

		var content = GameContent.Mount(GameInstall.ArchiveDirectory(root), new[] { "SHELL0.VOL" });
		if (content.Read("gam", name) is not { } original) {
			return;
		}

		var transformer = new HardpointOverlayTransformer();
		var parsed = transformer.Parse(original);

		Assert.NotNull(parsed);
		Assert.Equal(original, transformer.Write(parsed));
	}
}
