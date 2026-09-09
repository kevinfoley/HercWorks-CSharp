using Herculan.Engine.Shell;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// The shell's geometry and its widget interaction, both of which are pure and neither of which
/// touches GL. Worth pinning for the same reason <see cref="CockpitScreenLayoutTests"/> pins the
/// cockpit's: the failure mode is a click region sitting a few pixels off the button it belongs to,
/// which is invisible until someone misses a click.
/// </summary>
public class ShellLayoutTests {
	/// <summary>The id of the button under a canvas point, asserting there is one.</summary>
	private static int ButtonIdAt(ShellScreen screen, float x, float y) {
		var button = screen.ButtonAt(x, y);
		Assert.NotNull(button);
		return button.Id;
	}

	/// <summary>
	/// The eight tab rects, straight out of <c>ServiceBay_BuildScreen</c>'s immediates. The strip is
	/// generated from a pitch rather than a table, so this is what says the pitch is right — get it
	/// wrong by one and tab 0 still lands correctly while tab 7 is eight pixels out.
	/// </summary>
	[Theory]
	[InlineData(0, 0x19, 0x63)]
	[InlineData(1, 0x65, 0xaf)]
	[InlineData(2, 0xb1, 0xfb)]
	[InlineData(3, 0xfd, 0x147)]
	[InlineData(4, 0x149, 0x193)]
	[InlineData(5, 0x195, 0x1df)]
	[InlineData(6, 0x1e1, 0x22b)]
	[InlineData(7, 0x22d, 0x277)]
	public void TabRectsMatchTheOriginalsImmediates(int index, int x0, int x1) {
		var tab = ShellLayout.Tab(index);

		Assert.Equal(new ShellRect(x0, 4, x1, 0x1b), tab);
		Assert.Equal(75, tab.Width);
		Assert.Equal(24, tab.Height);
	}

	/// <summary>The strip stays inside the canvas, and its last tab stops short of the right edge.</summary>
	[Fact]
	public void TabStripFitsTheCanvas() {
		Assert.True(ShellLayout.MenuButton.X0 > 0);
		Assert.True(ShellLayout.Tab(ShellLayout.TabCount - 1).X1 < ShellLayout.CanvasWidth);
		Assert.Equal(ShellLayout.CanvasWidth - 1, ShellLayout.Screen.X1);
		Assert.Equal(ShellLayout.CanvasHeight - 1, ShellLayout.Screen.Y1);
	}

	/// <summary>A spread of window shapes: the native 4:3, wider, taller, and one with no clean scale.</summary>
	public static TheoryData<int, int> Windows() {
		var data = new TheoryData<int, int>();
		foreach ((int w, int h) in new[] { (640, 480), (1280, 960), (1920, 480), (640, 960), (1367, 769) }) {
			data.Add(w, h);
		}

		return data;
	}

	[Theory]
	[MemberData(nameof(Windows))]
	public void WindowToCanvasAndBackIsIdentity(int width, int height) {
		var layout = ShellScreenLayout.Create(width, height);

		foreach ((float x, float y) in new[] { (0f, 0f), (1f, 1f), (319.5f, 240.25f), (639f, 479f) }) {
			var (windowX, windowY) = layout.CanvasToWindow(x, y);
			var (canvasX, canvasY) = layout.WindowToCanvas(windowX, windowY);

			Assert.Equal(x, canvasX, 3);
			Assert.Equal(y, canvasY, 3);
		}
	}

	/// <summary>
	/// On any window at least 4:3 the scale is purely the height ratio, the canvas is flush to the top
	/// and bottom, and whatever is left over is split evenly either side.
	/// </summary>
	[Theory]
	[InlineData(640, 480)]
	[InlineData(1280, 960)]
	[InlineData(1920, 480)]
	public void ScalesByHeightAndCentresHorizontally(int width, int height) {
		var layout = ShellScreenLayout.Create(width, height);

		Assert.Equal(height / (float)ShellLayout.CanvasHeight, layout.Scale, 4);
		Assert.Equal(0f, layout.OriginY, 4);

		var (_, top) = layout.CanvasToWindow(0f, 0f);
		var (_, bottom) = layout.CanvasToWindow(0f, ShellLayout.CanvasHeight);
		Assert.Equal(0f, top, 3);
		Assert.Equal(height, bottom, 3);

		var (left, _) = layout.CanvasToWindow(0f, 0f);
		var (right, _) = layout.CanvasToWindow(ShellLayout.CanvasWidth, 0f);
		Assert.Equal(left, width - right, 3);
	}

	/// <summary>
	/// A window narrower than 4:3 falls back to fitting by width, so no widget is pushed off the edge
	/// — see <see cref="ShellScreenLayout"/> for why that clamp is there.
	/// </summary>
	[Fact]
	public void NarrowWindowFitsByWidthInstead() {
		var layout = ShellScreenLayout.Create(640, 960);

		Assert.Equal(1f, layout.Scale, 4);
		Assert.Equal(0f, layout.OriginX, 4);
		Assert.Equal(240f, layout.OriginY, 4);
	}

	/// <summary>The frame's buttons answer for their own rects and for nothing between them.</summary>
	[Fact]
	public void HitTestPicksTheButtonUnderThePoint() {
		var screen = ShellScreen.CreateFrame(null);

		Assert.Equal(ShellScreen.MenuButtonId, ButtonIdAt(screen, 10, 10));
		Assert.Equal(0, ButtonIdAt(screen, ShellLayout.Tab(0).X0, ShellLayout.TabTop));
		Assert.Equal(7, ButtonIdAt(screen, ShellLayout.Tab(7).X1, ShellLayout.TabBottom));

		// The one-pixel gutter between two tabs, and the content area below the strip.
		Assert.Null(screen.ButtonAt(ShellLayout.Tab(0).X1 + 1, ShellLayout.TabTop));
		Assert.Null(screen.ButtonAt(320, ShellLayout.TabBottom + 1));
	}

	/// <summary>Exactly one tab is latched, and clicking another moves the latch to it.</summary>
	[Fact]
	public void SelectingATabLatchesOnlyThatOne() {
		var screen = ShellScreen.CreateFrame(null);
		Assert.Equal(0, screen.SelectedTab);

		screen.SelectTab(4);

		Assert.Equal(4, screen.SelectedTab);
		Assert.Equal(1, screen.Buttons.Count(button => button.Selected));
		Assert.True(screen.Button(4)!.Selected);

		// The menu button is not a tab and is never latched by one.
		Assert.False(screen.Button(ShellScreen.MenuButtonId)!.Selected);
	}

	/// <summary>A press activates on release over the same button, and cancels when dragged off it.</summary>
	[Fact]
	public void PressActivatesOnReleaseOverTheSameButton() {
		var screen = ShellScreen.CreateFrame(null);
		var tab = ShellLayout.Tab(3);

		screen.PointerDown(tab.X0 + 1, tab.Y0 + 1);
		Assert.Equal(3, screen.PressedId);
		Assert.Equal(3, screen.PointerUp(tab.X0 + 1, tab.Y0 + 1));
		Assert.Null(screen.PressedId);

		screen.PointerDown(tab.X0 + 1, tab.Y0 + 1);
		Assert.Null(screen.PointerUp(320, ShellLayout.CanvasHeight - 1));
		Assert.Null(screen.PressedId);
	}

	/// <summary>A disabled button neither answers a hit test nor takes a press.</summary>
	[Fact]
	public void DisabledButtonTakesNoClicks() {
		var screen = ShellScreen.CreateFrame(null);
		var tab = ShellLayout.Tab(2);
		screen.Button(2)!.Enabled = false;

		Assert.Null(screen.ButtonAt(tab.X0 + 1, tab.Y0 + 1));

		screen.PointerDown(tab.X0 + 1, tab.Y0 + 1);
		Assert.Null(screen.PointerUp(tab.X0 + 1, tab.Y0 + 1));
	}
}
