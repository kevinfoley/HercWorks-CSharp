using Herculan.Engine.Content;
using Herculan.Engine.Render;

namespace Herculan.Engine.Input;

/// <summary>Which mouse buttons a cockpit event carries. Bit positions, so a mask holds both at once.</summary>
[Flags]
public enum CockpitMouseButtons {
	/// <summary>No button held.</summary>
	None = 0,

	/// <summary>The left button.</summary>
	Left = 1,

	/// <summary>The right button.</summary>
	Right = 2,
}

/// <summary>One completed click on a cockpit widget.</summary>
/// <param name="Id">Which widget was clicked.</param>
/// <param name="Button">Which button completed the click.</param>
public readonly record struct CockpitClick(CockpitWidgetId Id, CockpitMouseButtons Button);

/// <summary>
/// The cockpit's mouse pipeline: raw window events in, completed widget clicks out.
/// Reverse-engineered from DBSIM — see docs/formats/cockpit-input.md, whose section numbers this
/// file's comments refer to.
///
/// <para><b>What is reproduced.</b> Events are queued as they arrive and processed once per frame
/// rather than handled in the callback (§3-4), because that is what keeps input aligned to the sim
/// tick. A press arms a widget; a release completes the click only if it lands back on the widget
/// that was pressed (§7) and within <see cref="ClickHoldSeconds"/> of the press (§4) — the original's
/// click-vs-drag gate, which is player-visible: hold a cockpit button down for half a second and
/// releasing it does nothing at all.</para>
///
/// <para>A held button's widget draws depressed while the pointer is on it and pops back up when the
/// pointer leaves — see <see cref="Depressed"/>. There is deliberately <b>no hover state</b>: the
/// original has none, and the function long mistaken for one turns out to be this same press
/// tracking.</para>
///
/// <para><b>What is not.</b> The Win32 hook chain and the two ten-slot subscriber tables the original
/// routes events through (§1-2) are a message-pump workaround with no purpose here. Drag capture
/// (§7) is omitted: every widget traced in the original constructs with its drag flag clear, so no
/// retail cockpit control is draggable. The cursor (§9) is the host's.</para>
///
/// <para><b>Where the timing differs.</b> The original stamps each event as it arrives and debounces
/// pushes to one per ~16ms coarse tick; this stamps events with the frame they are processed in, and
/// does not debounce. At any sane frame rate the two resolutions are comparable, and stamping at
/// drain time keeps the whole class free of an ambient clock — the gate is then measured in whole
/// frames, which is also the granularity the original's own tick gives it in practice.</para>
/// </summary>
public sealed class CockpitInput {
	/// <summary>
	/// The original's click-vs-drag gate, <c>DAT_004d1e70</c> — <c>0x1e</c> coarse UI ticks of roughly
	/// 16ms each. A release later than this after its press is a drag that ended, not a click, and
	/// fires nothing.
	///
	/// <para>The literal is <c>0x1e</c> in the disassembly, the same value as
	/// <c>CockpitMouse_Init</c>'s mouse-event mask; they are unrelated fields that happen to share a
	/// number (§3).</para>
	/// </summary>
	public const float ClickHoldSeconds = 30 * 0.016f;

	/// <summary>
	/// How many unprocessed events are kept before further ones are dropped. The original's queue caps
	/// at 99 and drops pushes past that; this is larger only because it does not debounce.
	/// </summary>
	public const int QueueCapacity = 256;

	private readonly Queue<Event> _queue = new();
	private CockpitMouseButtons _lastButtons;
	private float _elapsedSeconds;

	private CockpitWidgetId? _pressed;
	private CockpitSurface _pressedSurface;
	private float _pressedAtSeconds;

	private readonly List<CockpitClick> _clicks = new();

	/// <summary>
	/// The widget a press is armed on, or null. Stays set while the pointer wanders off it — the
	/// original keeps the press armed and simply fires nothing if the release lands elsewhere.
	/// </summary>
	public CockpitWidgetId? Pressed => _pressed;

	/// <summary>
	/// The widget that should draw held-down right now: the armed widget while the pointer is actually
	/// on it, and null the moment the pointer leaves. Distinct from <see cref="Pressed"/>, which stays
	/// armed either way.
	///
	/// <para>This is <c>FUN_00452954</c> (<c>00452954</c>), which the symbol table long called
	/// <c>Widget_OnMouseHover</c> — a misreading. The function early-outs unless
	/// <c>DAT_0049dbdc</c>, the globally remembered pressed-widget index, is valid, and that global is
	/// set only by <c>Widget_OnMouseDown</c> and cleared to -1 by <c>Widget_OnMouseUp</c>. It therefore
	/// cannot run unless a button is held, and what it does is toggle the held widget's state byte
	/// between 1 and 0 as the pointer moves on and off it, repainting each time: a button that pops
	/// back up when you drag off it and depresses again when you come back. <b>DBSIM has no hover state
	/// at all</b> — corroborated by <c>CockpitMouse_Init</c>'s event mask excluding plain movement
	/// (§3), which leaves nothing to drive one.</para>
	/// </summary>
	public CockpitWidgetId? Depressed { get; private set; }

	/// <summary>
	/// Queues one mouse event, in window pixels with the origin at the top-left. Called straight from
	/// the host's mouse callbacks; does no hit-testing and touches no widget state.
	/// </summary>
	/// <param name="windowX">Pointer x in window pixels.</param>
	/// <param name="windowY">Pointer y in window pixels.</param>
	/// <param name="buttons">Every button held at this moment, not just the one that changed.</param>
	public void Enqueue(float windowX, float windowY, CockpitMouseButtons buttons) {
		if (_queue.Count >= QueueCapacity) {
			return;
		}

		_queue.Enqueue(new Event(windowX, windowY, buttons));
	}

	/// <summary>
	/// Drains a frame's queued events and returns the clicks they completed.
	///
	/// <para>Call once per frame, before the sim tick, so a click and the tick that consumes it stay
	/// in a fixed order. The returned list is reused between calls — copy it if it needs to outlive
	/// the frame.</para>
	///
	/// <para>Every event in one drain is hit-tested against the <paramref name="state"/> passed in, so
	/// a click that changes which widgets are visible does not affect a second click in the same
	/// frame. The original re-reads its list as handlers run and so would; with one pointer at frame
	/// rate, two clicks in one frame does not arise.</para>
	/// </summary>
	/// <param name="deltaSeconds">Real time since the previous drain, for the click-hold gate.</param>
	/// <param name="layout">This frame's placement, for window-to-art conversion.</param>
	/// <param name="art">The cockpit whose widgets to test against.</param>
	/// <param name="state">The HUD state deciding which widgets are visible, and so hit-testable.</param>
	public IReadOnlyList<CockpitClick> Drain(double deltaSeconds, CockpitScreenLayout layout,
			CockpitArt art, CockpitHudState state) {
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(art);

		return Drain(deltaSeconds, (x, y) => HitTest(layout, art, state, x, y));
	}

	/// <summary>
	/// Drains a frame's queued events against an arbitrary hit test — the form the cockpit overload
	/// above is built on.
	///
	/// <para>The split is where the two halves of this pipeline meet: press/release edges and the hold
	/// gate are one problem, and "which widget is under this window point" is a wholly separate one
	/// owned by <see cref="CockpitScreenLayout"/> and <see cref="CockpitWidgets"/>. Keeping the seam
	/// open also lets the state machine be exercised without a loaded cockpit.</para>
	/// </summary>
	/// <param name="deltaSeconds">Real time since the previous drain, for the click-hold gate.</param>
	/// <param name="hitTest">Window x and y to the widget under them, or null for bare art.</param>
	public IReadOnlyList<CockpitClick> Drain(double deltaSeconds, Func<float, float, CockpitWidget?> hitTest) {
		ArgumentNullException.ThrowIfNull(hitTest);

		_elapsedSeconds += (float)Math.Max(deltaSeconds, 0d);
		_clicks.Clear();

		while (_queue.Count > 0) {
			var e = _queue.Dequeue();
			var hit = hitTest(e.X, e.Y);

			// A held button's widget depresses and pops back up as the pointer moves on and off it,
			// which is all FUN_00452954 does. Nothing happens here when no button is held: there is no
			// hover state to track.
			if (_pressed is { } armed) {
				Depressed = hit is { } over && over.Id == armed && over.Surface == _pressedSurface
					? armed
					: null;
			}

			var pressedNow = e.Buttons & ~_lastButtons;
			var releasedNow = _lastButtons & ~e.Buttons;
			_lastButtons = e.Buttons;

			if (pressedNow != CockpitMouseButtons.None) {
				OnPress(hit);
			}

			if (releasedNow != CockpitMouseButtons.None) {
				OnRelease(hit, releasedNow);
			}
		}

		return _clicks;
	}

	/// <summary>
	/// Forgets any in-flight press — for a lost window focus or a cockpit teardown, where a release
	/// will never arrive and an armed press would otherwise complete against whatever the pointer
	/// happened to be over on the way back.
	/// </summary>
	public void Reset() {
		_queue.Clear();
		_lastButtons = CockpitMouseButtons.None;
		_pressed = null;
		Depressed = null;
	}

	/// <summary>§7's <c>Widget_OnMouseDown</c>: a press on a widget arms it and starts the hold clock.</summary>
	private void OnPress(CockpitWidget? hit) {
		if (hit is not { } widget) {
			_pressed = null;
			Depressed = null;
			return;
		}

		_pressed = widget.Id;
		_pressedSurface = widget.Surface;
		_pressedAtSeconds = _elapsedSeconds;
		Depressed = widget.Id;
	}

	/// <summary>
	/// §7's <c>Widget_OnMouseUp</c>, gated by §4's hold timer: the click fires only when the release
	/// lands back on the armed widget and soon enough after the press.
	/// </summary>
	private void OnRelease(CockpitWidget? hit, CockpitMouseButtons released) {
		if (_pressed is not { } armed) {
			return;
		}

		_pressed = null;
		Depressed = null;

		if (_elapsedSeconds - _pressedAtSeconds > ClickHoldSeconds) {
			return;
		}

		if (hit is { } widget && widget.Id == armed && widget.Surface == _pressedSurface) {
			_clicks.Add(new CockpitClick(armed, released));
		}
	}

	private static CockpitWidget? HitTest(CockpitScreenLayout layout, CockpitArt art,
			CockpitHudState state, float windowX, float windowY) =>
		layout.WindowToArt(windowX, windowY) is { } surfaceHit
			? CockpitWidgets.HitTest(art, state, surfaceHit.Surface, surfaceHit.ArtX, surfaceHit.ArtY)
			: null;

	/// <summary>One queued event: where the pointer was and everything held at the time (§3's record).</summary>
	private readonly record struct Event(float X, float Y, CockpitMouseButtons Buttons);
}
