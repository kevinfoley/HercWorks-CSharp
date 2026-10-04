using Herculan.Engine.Content;

namespace Herculan.Engine.Input;

/// <summary>
/// The cockpit's press flash: a button pressed for the player rather than by the pointer — a key, a joystick
/// button, or a click on a list that presses the display's XMIT — shows pressed for <see cref="FlashTicks"/>
/// coarse ticks. <c>WidgetRoot_FlashPress</c> (<c>00453078</c>) and <c>WidgetRoot_ServicePressFlashes</c>
/// (<c>004530b8</c>); see docs/retail/formats/cockpit-input.md#the-press-flash.
///
/// <para>A flash only shows on a button whose paint reads the press byte — see
/// <see cref="CockpitHudState.ShowsPressed"/>. The original flashes every button it presses for the player;
/// the callers here leave out the ones that draw nothing from it — the latching MFD and Heads-Down Display
/// buttons, the weapon rows and the shield facings.</para>
///
/// <para>The original's list holds eight entries with no bound check; this one is unbounded.</para>
/// </summary>
public sealed class CockpitPressFlashes {
	/// <summary>How long a flash holds a button down: the deadline is <c>Time_GetCoarseTicks() + 10</c>.</summary>
	public const int FlashTicks = 10;

	private readonly List<(CockpitWidgetId Id, long Deadline)> _entries = new();
	private readonly List<CockpitWidgetId> _popped = new();
	private bool _queued;

	/// <summary>
	/// The buttons showing pressed as of the last <see cref="Service"/>. Replaced rather than changed in place,
	/// so a <see cref="CockpitHudState"/> holding it keeps the set it was built with.
	/// </summary>
	public IReadOnlyList<CockpitWidgetId> Lit { get; private set; } = Array.Empty<CockpitWidgetId>();

	/// <summary>
	/// <c>WidgetRoot_FlashPress</c>: queues <paramref name="id"/> to show pressed until <see cref="FlashTicks"/>
	/// after <paramref name="nowTicks"/>. A button already flashing gets a second entry, as in the original.
	/// </summary>
	public void Flash(CockpitWidgetId id, long nowTicks) {
		_entries.Add((id, nowTicks + FlashTicks));
		_queued = true;
	}

	/// <summary>
	/// <c>WidgetRoot_ServicePressFlashes</c>: drops every entry whose deadline has come and rebuilds
	/// <see cref="Lit"/> from the rest.
	///
	/// <para>The original sets an entry's button to state 1 while it lasts and to 0 when it expires, so a
	/// button the pointer was holding down pops up at its flash's deadline. The buttons that popped are
	/// returned for the caller to take off the pointer's press: every one that expired this pass, less any a
	/// later entry keeps lit.</para>
	/// </summary>
	/// <param name="nowTicks"><c>Time_GetCoarseTicks</c>, on the clock <see cref="Flash"/> was given.</param>
	public IReadOnlyList<CockpitWidgetId> Service(long nowTicks) {
		_popped.Clear();
		for (int i = _entries.Count - 1; i >= 0; i--) {
			if (nowTicks >= _entries[i].Deadline) {
				_popped.Add(_entries[i].Id);
				_entries.RemoveAt(i);
			}
		}

		if (_queued || _popped.Count > 0) {
			Lit = _entries.Count == 0
				? Array.Empty<CockpitWidgetId>()
				: _entries.Select(entry => entry.Id).Distinct().ToArray();
			_queued = false;
		}

		_popped.RemoveAll(Lit.Contains);
		return _popped;
	}
}
