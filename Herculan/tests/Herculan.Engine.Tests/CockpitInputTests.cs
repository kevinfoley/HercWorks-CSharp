using Herculan.Engine.Content;
using Herculan.Engine.Input;
using Herculan.Engine.Render;
using Xunit;

namespace Herculan.Engine.Tests;

/// <summary>
/// The click state machine, exercised through <see cref="CockpitInput"/>'s hit-test seam so no
/// cockpit needs loading. Two widgets sit side by side: A occupies window x 0-99, B occupies 100-199,
/// and anything else is bare art.
/// </summary>
public class CockpitInputTests {
	private static readonly CockpitWidgetId A = CockpitWidgetId.Mfd(0);
	private static readonly CockpitWidgetId B = CockpitWidgetId.Mfd(1);

	/// <summary>Which widget covers a window x, in the two-button strip the tests click on.</summary>
	private static CockpitWidget? HitTest(float x, float y) => x switch {
		>= 0 and < 100 => new CockpitWidget(A, CockpitSurface.Forward, 0, 0, 99, 50, Lit: false),
		>= 100 and < 200 => new CockpitWidget(B, CockpitSurface.Forward, 100, 0, 199, 50, Lit: false),
		_ => null,
	};

	/// <summary>A frame's worth of drain, at a nominal 60Hz unless a longer step is asked for.</summary>
	private static IReadOnlyList<CockpitClick> Frame(CockpitInput input, double deltaSeconds = 1 / 60d) =>
		input.Drain(deltaSeconds, HitTest);

	private static void Press(CockpitInput input, float x) =>
		input.Enqueue(x, 10, CockpitMouseButtons.Left);

	private static void Release(CockpitInput input, float x) =>
		input.Enqueue(x, 10, CockpitMouseButtons.None);

	[Fact]
	public void PressAndReleaseOnTheSameWidgetFiresOneClick() {
		var input = new CockpitInput();
		Press(input, 50);
		Release(input, 50);

		var clicks = Frame(input);

		Assert.Equal(new[] { new CockpitClick(A, CockpitMouseButtons.Left) }, clicks);
	}

	/// <summary>
	/// §7: the release has to land back on the widget that was pressed. Pressing A and releasing over
	/// B fires nothing — not a click on A, and not one on B either.
	/// </summary>
	[Fact]
	public void ReleasingOnADifferentWidgetFiresNothing() {
		var input = new CockpitInput();
		Press(input, 50);
		Release(input, 150);

		Assert.Empty(Frame(input));
	}

	/// <summary>Releasing over bare art fires nothing, and disarms the press.</summary>
	[Fact]
	public void ReleasingOffAnyWidgetFiresNothing() {
		var input = new CockpitInput();
		Press(input, 50);
		Release(input, 500);

		Assert.Empty(Frame(input));
		Assert.Null(input.Pressed);
	}

	/// <summary>A press that started on bare art arms nothing, so a release over a widget is not a click on it.</summary>
	[Fact]
	public void PressingOffAnyWidgetArmsNothing() {
		var input = new CockpitInput();
		Press(input, 500);
		Release(input, 50);

		Assert.Empty(Frame(input));
	}

	/// <summary>
	/// §4's click-vs-drag gate, the player-visible half of this pipeline: hold past
	/// <see cref="CockpitInput.ClickHoldSeconds"/> and the release fires nothing even though it landed
	/// on the widget that was pressed.
	/// </summary>
	[Fact]
	public void HoldingPastTheGateFiresNothing() {
		var input = new CockpitInput();
		Press(input, 50);
		Frame(input);

		// Well past the ~480ms gate.
		Frame(input, deltaSeconds: 1.0);

		Release(input, 50);
		Assert.Empty(Frame(input));
	}

	/// <summary>A hold just inside the gate still clicks — the boundary is a limit, not a hair trigger.</summary>
	[Fact]
	public void HoldingJustInsideTheGateStillClicks() {
		var input = new CockpitInput();
		Press(input, 50);
		Frame(input);

		Frame(input, deltaSeconds: CockpitInput.ClickHoldSeconds * 0.9f);

		Release(input, 50);
		Assert.Single(Frame(input));
	}

	/// <summary>A press held across frames stays armed until it is released.</summary>
	[Fact]
	public void PressSurvivesAcrossFrames() {
		var input = new CockpitInput();
		Press(input, 50);
		Assert.Empty(Frame(input));
		Assert.Equal(A, input.Pressed);

		Release(input, 50);
		Assert.Single(Frame(input));
		Assert.Null(input.Pressed);
	}

	/// <summary>
	/// The press stays armed while the pointer wanders off the widget and back — the original does not
	/// cancel on leave, it only checks where the release lands.
	/// </summary>
	[Fact]
	public void WanderingOffAndBackStillClicks() {
		var input = new CockpitInput();
		Press(input, 50);
		input.Enqueue(500, 10, CockpitMouseButtons.Left);
		input.Enqueue(50, 10, CockpitMouseButtons.Left);
		Release(input, 50);

		Assert.Single(Frame(input));
	}

	/// <summary>
	/// Moving the pointer with no button held changes nothing. DBSIM has no hover state — its cockpit
	/// listener does not even subscribe to plain movement (§3) — so neither does this.
	/// </summary>
	[Fact]
	public void MovingWithNoButtonHeldChangesNothing() {
		var input = new CockpitInput();

		input.Enqueue(50, 10, CockpitMouseButtons.None);
		input.Enqueue(150, 10, CockpitMouseButtons.None);
		input.Enqueue(500, 10, CockpitMouseButtons.None);
		Frame(input);

		Assert.Null(input.Depressed);
		Assert.Null(input.Pressed);
	}

	/// <summary>
	/// <c>FUN_00452954</c>'s whole behaviour: a held widget draws depressed, pops back up when the
	/// pointer slides off it, and depresses again when it comes back — all while the press stays armed.
	/// </summary>
	[Fact]
	public void HeldWidgetDepressesAndPopsBackUpAsThePointerMoves() {
		var input = new CockpitInput();

		Press(input, 50);
		Frame(input);
		Assert.Equal(A, input.Depressed);
		Assert.Equal(A, input.Pressed);

		// Still held, but dragged off onto the neighbouring widget.
		input.Enqueue(150, 10, CockpitMouseButtons.Left);
		Frame(input);
		Assert.Null(input.Depressed);
		Assert.Equal(A, input.Pressed);

		// B does not depress on the way past: only the widget the press armed can.
		input.Enqueue(500, 10, CockpitMouseButtons.Left);
		Frame(input);
		Assert.Null(input.Depressed);

		input.Enqueue(50, 10, CockpitMouseButtons.Left);
		Frame(input);
		Assert.Equal(A, input.Depressed);
	}

	/// <summary>Releasing clears the depressed look whether or not the click fired.</summary>
	[Fact]
	public void ReleaseClearsTheDepressedLook() {
		var input = new CockpitInput();

		Press(input, 50);
		Release(input, 50);
		Assert.Single(Frame(input));
		Assert.Null(input.Depressed);

		Press(input, 50);
		Release(input, 500);
		Assert.Empty(Frame(input));
		Assert.Null(input.Depressed);
	}

	/// <summary>The right button completes its own click, and reports itself as the button that did.</summary>
	[Fact]
	public void RightButtonClicksAreReportedSeparately() {
		var input = new CockpitInput();
		input.Enqueue(50, 10, CockpitMouseButtons.Right);
		input.Enqueue(50, 10, CockpitMouseButtons.None);

		Assert.Equal(new[] { new CockpitClick(A, CockpitMouseButtons.Right) }, Frame(input));
	}

	/// <summary>
	/// Events queue up between drains rather than being handled as they arrive (§3), so a whole click
	/// enqueued mid-frame is still one click when the frame gets to it.
	/// </summary>
	[Fact]
	public void EventsAreProcessedOnlyOnDrain() {
		var input = new CockpitInput();
		Press(input, 50);
		Release(input, 50);
		Press(input, 150);
		Release(input, 150);

		Assert.Equal(
			new[] { new CockpitClick(A, CockpitMouseButtons.Left), new CockpitClick(B, CockpitMouseButtons.Left) },
			Frame(input));
	}

	/// <summary>A drain with nothing queued reports nothing and disturbs no state.</summary>
	[Fact]
	public void AnEmptyFrameIsQuiet() {
		var input = new CockpitInput();
		Assert.Empty(Frame(input));
		Assert.Null(input.Depressed);
		Assert.Null(input.Pressed);
	}

	/// <summary>Reset drops an armed press, so a release arriving afterwards completes nothing.</summary>
	[Fact]
	public void ResetDisarmsAnInFlightPress() {
		var input = new CockpitInput();
		Press(input, 50);
		Frame(input);

		input.Reset();
		Release(input, 50);

		Assert.Empty(Frame(input));
	}

	/// <summary>The queue is bounded; events past its capacity are dropped rather than growing it.</summary>
	[Fact]
	public void TheQueueIsBounded() {
		var input = new CockpitInput();
		for (int i = 0; i < CockpitInput.QueueCapacity * 2; i++) {
			input.Enqueue(50, 10, CockpitMouseButtons.None);
		}

		// One drain empties it: the overflow was dropped at enqueue time, not buffered for later.
		Frame(input);
		Assert.Empty(Frame(input));
	}
}
