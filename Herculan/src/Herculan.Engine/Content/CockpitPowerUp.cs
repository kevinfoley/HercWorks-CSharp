using Herculan.Engine.Sim;

namespace Herculan.Engine.Content;

/// <summary>
/// The weapon panel's and shield meter's power-up animations: on taking a walking machine the weapon
/// rows wink on one at a time, each energy row's charge bar fills from empty, and the shield rings
/// light from dark up to the real charge. <c>Cockpit_PowerUpTick</c> (<c>00432924</c>) arms each
/// widget with <c>Widget_BeginPowerUpAnimation</c> (<c>00438ddc</c>) and each widget runs its own
/// ramp from the tick it was armed on. The delays, the ramps and their sources are
/// docs/formats/cockpit-hud-widgets.md's power-up section.
///
/// <para>The compass's wind-up is the same sequence's third animation, and is
/// <see cref="HeadingTapeSweep"/>. A flyer's cockpit skips all three.</para>
/// </summary>
public sealed class CockpitPowerUp {
	/// <summary>
	/// How many weapon rows the sequence arms — the ten widget slots at <c>cockpit+0x70</c>, indexed by
	/// <c>.GAU</c> weapon row.
	/// </summary>
	public const int RowCount = 10;

	/// <summary>
	/// The gap between one row winking on and the next, in coarse ticks. <c>DAT_0049b05a</c> is ten
	/// shorts reading 20, 40, … 200, so row <c>n</c> (0-based) arms once more than <c>20 * (n + 1)</c>
	/// ticks have passed: 320 ms apart, the last at 3.2 s.
	/// </summary>
	public const int RowArmStep = 20;

	/// <summary>How far a charge bar's ramp climbs per coarse tick — the <c>0x19</c> of <c>FUN_00440e84</c> and <c>FUN_00441d88</c>.</summary>
	public const int ChargeRampPerTick = 0x19;

	/// <summary>How many coarse ticks after arming a charge bar stops ramping and reads its live value.</summary>
	public const int ChargeRampTicks = 0x33;

	private readonly long _poweredAt;
	private readonly long?[] _rowArmedAt = new long?[RowCount];
	private long? _shieldsArmedAt;
	private bool _finished;
	private bool _shieldsDone;

	private CockpitPowerUp(long poweredAt, bool finished) {
		_poweredAt = poweredAt;
		_finished = finished;
		_shieldsDone = finished;
	}

	/// <summary>
	/// The sequence for a cockpit powering up at <paramref name="coarseTicks"/>. A flyer's comes back
	/// finished, every widget armed and done — <c>Gau_BuildCockpitWidgets</c>' own branch on
	/// <c>InputFlagFlyer</c>, the same one <see cref="HeadingTapeSweep.ForPowerUp"/> follows.
	/// </summary>
	public static CockpitPowerUp ForPowerUp(MechObject pilot, long coarseTicks) {
		ArgumentNullException.ThrowIfNull(pilot);
		return new CockpitPowerUp(coarseTicks, finished: pilot.Type.IsFlyer);
	}

	/// <summary>
	/// A sequence that has already run: every row showing and every value live. For a host that wants
	/// the steady-state cockpit from the first frame.
	/// </summary>
	public static CockpitPowerUp Finished { get; } = new(0, finished: true);

	/// <summary>
	/// <c>Cockpit_PowerUpTick</c>'s arming pass, once a frame: each row whose delay has passed, and the
	/// shield meter on the first tick after the power-up began.
	/// </summary>
	public void Tick(long coarseTicks) {
		if (_finished) {
			return;
		}

		long elapsed = coarseTicks - _poweredAt;
		bool rowsDone = true;
		for (int row = 0; row < RowCount; row++) {
			if (_rowArmedAt[row] == null && elapsed > RowArmStep * (row + 1)) {
				_rowArmedAt[row] = coarseTicks;
			}

			rowsDone &= _rowArmedAt[row] is { } armedAt && coarseTicks - armedAt >= ChargeRampTicks;
		}

		if (_shieldsArmedAt == null && elapsed != 0) {
			_shieldsArmedAt = coarseTicks;
		}

		_finished = rowsDone && _shieldsDone;
	}

	/// <summary>
	/// Whether a weapon row is drawn yet. Every weapon and pod gauge's paint, and each of its children's,
	/// opens on the owning gauge's armed byte at <c>+0x8c</c>, so a row the sequence has not reached
	/// draws nothing at all and the console art shows where it will be.
	/// </summary>
	public bool RowPowered(int row) =>
		_finished || row < 0 || row >= RowCount || _rowArmedAt[row] != null;

	/// <summary>
	/// The most a row's charge bar can show this tick, in the bar's own units, or
	/// <see cref="int.MaxValue"/> once the ramp is over. <c>FUN_00440e84</c> (energy) and
	/// <c>FUN_00441d88</c> (Turbo Pod) both clamp the live value to <c>elapsed * 0x19</c> until
	/// <see cref="ChargeRampTicks"/> have passed since the row was armed, then hand it over whole — which
	/// for the Turbo Pod, whose bar runs to 2500, is a visible jump from 1250 to a full tank.
	/// </summary>
	public int RowChargeCap(int row, long coarseTicks) {
		if (_finished || row < 0 || row >= RowCount || _rowArmedAt[row] is not { } armedAt) {
			return int.MaxValue;
		}

		long elapsed = coarseTicks - armedAt;
		return elapsed >= ChargeRampTicks ? int.MaxValue : (int)(elapsed * ChargeRampPerTick);
	}

	/// <summary>
	/// What the shield meter shows for a pair of live facing charges, on the rings' 0..<c>0x800</c>
	/// scale. Before the meter is armed <c>ShieldsGauge_SetStateBlock</c> (<c>00443858</c>) zeroes both,
	/// so the rings are dark; once armed, <c>FUN_004437a4</c> shows each facing as the lesser of its live
	/// value and the coarse ticks since arming, and latches done the first tick both have caught up. An
	/// even split's <c>0x200</c> is therefore about eight seconds of fill. Call it once a frame — it
	/// latches as it goes.
	/// </summary>
	public (int Front, int Rear) ShieldCharges(int front, int rear, long coarseTicks) {
		if (_shieldsDone) {
			return (front, rear);
		}

		if (_shieldsArmedAt is not { } armedAt) {
			return (0, 0);
		}

		long ramp = coarseTicks - armedAt;
		int shownFront = (int)Math.Min(ramp, front);
		int shownRear = (int)Math.Min(ramp, rear);
		if (shownFront == front && shownRear == rear) {
			_shieldsDone = true;
		}

		return (shownFront, shownRear);
	}
}
