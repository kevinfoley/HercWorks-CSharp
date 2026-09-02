using Herculan.Engine.Audio;
using Herculan.Engine.Numerics;
using Herculan.Engine.World;

namespace Herculan.Engine.Sim;

/// <summary>
/// Missile lock — the second half of <c>Mech_PerTickSystemsUpdate</c> (<c>0041aa5c</c>), everything
/// after the reactor and shield bookkeeping <see cref="PowerTick"/> ports.
///
/// <para><b>What <c>manager+0x0a</c> actually is.</b> The symbol table calls its reader
/// <c>Mech_MissileAmmoCount</c> and that name is wrong: it counts nothing. The array is five flags,
/// one per <c>PROJ.DAT</c> missile subtype, meaning <i>this class of launcher has achieved lock on
/// the machine's current target</i>. It is cleared and rebuilt every tick, for every machine, by
/// the block ported here — the player's included, which is why a player's missiles lock in retail
/// and why <c>Rocket_Fire</c>'s gate on it is a real gate rather than something to skip. The genuine
/// ammunition count is a separate local array built by <c>FUN_0040fbdc</c>
/// (<see cref="WeaponMounts.RoundsByMissileType"/>), which this block uses only to decide which
/// timers to run.</para>
///
/// <para><b>How a lock is built.</b> Each subtype the machine actually carries rounds for has its
/// own countdown. Every tick the block either <i>reloads</i> that countdown — holding it at full,
/// so no lock can form — or lets it tick down; when one reaches zero its flag is set. The reload
/// value is <b>range over four</b>, so a distant target takes proportionally longer to lock, and
/// the reload happens wholesale whenever the target leaves the cone, the machine has just switched
/// target, or line of sight is broken. That is the whole mechanism: lock is "how long have you held
/// this thing in front of you", scaled by how far away it is.</para>
///
/// <para><b>Each subtype has its own hold condition</b>, and they are what distinguish the four
/// guided weapons:</para>
/// <list type="table">
/// <item><term>0 and 4</term><description>Held while the machine's <b>own</b> scanner is off. These
/// need your radar running.</description></item>
/// <item><term>1</term><description>No emission condition at all — it locks on sight.</description></item>
/// <item><term>2</term><description>Held while the <b>target</b> is silent, and released the moment
/// the target's scanner or jammer comes on. This is the anti-radiation missile, and it is the same
/// pair of flags its guidance homes on (see <see cref="Rocket"/>).</description></item>
/// <item><term>3</term><description>Never locked, because the pilot flies it himself — see
/// <see cref="Rocket.PlayerFlownSubtype"/> and <see cref="SimWorld.FireRocket"/>.</description></item>
/// </list>
///
/// <para><b>ECM.</b> A jamming target rolls <see cref="EcmSpoofed"/> a few times a minute, and while
/// it is set no subtype but the anti-radiation one can complete a lock. That is the mechanical form
/// of the manual's ECM, and it is the same flag that makes a missile already in the air weave.</para>
/// </summary>
public sealed partial class MechObject {
	/// <summary>
	/// Half-width of the cone a target has to stay inside for lock to build, about the turret
	/// centreline — the block's own <c>(bearing + 0x3000) &lt; 0x6000</c>, so ±67.5°. Wider than
	/// <see cref="TargetSelection.ForwardConeHalfWidth"/>: you can keep a lock on something you could
	/// not have selected from here.
	/// </summary>
	public const int LockConeHalfWidth = 0x3000;

	/// <summary>
	/// What the lock countdown is reloaded with: the range to the target over four, saturated at a
	/// short. Lock time is therefore linear in range.
	/// </summary>
	private const int LockTimeRangeShift = 2;

	/// <summary>
	/// The ECM roll's numerator, out of <see cref="EcmRollRange"/> — the block's own
	/// <c>0x14 * 0x29</c>, about a 20% chance each time the roll comes round.
	/// </summary>
	private const int EcmRollWeight = 0x14 * 0x29;

	/// <inheritdoc cref="EcmRollWeight"/>
	private const int EcmRollRange = 0x1000;

	/// <summary>Reload for <see cref="_ecmRollTimer"/> after a roll that spoofed.</summary>
	private const short EcmSpoofedInterval = 5000;

	/// <summary>Reload for <see cref="_ecmRollTimer"/> after a roll that did not.</summary>
	private const short EcmClearInterval = 0x5dc;

	/// <summary>
	/// One countdown per missile subtype — <c>mech+0x258</c>, <c>+0x25b</c>, <c>+0x25e</c> and
	/// <c>+0x264</c> in the original, which are four separate fields rather than an array. They are
	/// gathered here because they are addressed by the same subtype index everything else in this
	/// file is; slot 3 is never used, exactly as the original has no field for it.
	/// </summary>
	private readonly short[] _lockTimer = new short[WeaponMount.NotAMissile];

	private readonly short[] _missileRounds = new short[WeaponMount.NotAMissile];

	private short _ecmRollTimer;

	/// <summary>
	/// <c>mech+0x9c</c> — the ECM spoof flag, rolled against a jamming target. While it is set no
	/// lock can complete except the anti-radiation one, and a missile already in flight weaves rather
	/// than converging.
	/// </summary>
	public bool EcmSpoofed { get; private set; }

	/// <summary>
	/// <c>mech+0x9b</c> — <b>LOCK</b>. Whether the armed mount's own missile class has lock; a mount
	/// that is not a launcher lights it when <i>any</i> class does. This is what drives the cockpit's
	/// lock lamp and the intermittent lock tone — see <see cref="LockToneTick"/>.
	/// </summary>
	public bool LockAcquired { get; private set; }

	/// <summary>
	/// The blink period the lock tone repeats on: bit 6 of the coarse-tick clock, so the tone sounds
	/// once every time that bit goes high — one beep per 128 coarse ticks, a little over two seconds.
	/// </summary>
	private const long LockToneBlinkBit = 0x40;

	/// <summary><c>DAT_0049a1d1</c> — whether a lock was held, so its loss can be announced once.</summary>
	private bool _lockToneWasLocked;

	/// <summary><c>DAT_0049a1d0</c> — whether this blink phase's beep has already sounded.</summary>
	private bool _lockToneSounded;

	/// <summary>
	/// <c>Mech_LockTonePlay</c> (<c>0041b0bc</c>) — the cockpit's lock audio, run from the tail of
	/// <c>Mech_PerTickSystemsUpdate</c> <b>for the locally-piloted machine only</b>. Three sounds
	/// come out of one flag:
	///
	/// <list type="bullet">
	/// <item>Holding lock: <see cref="SoundId.LockTone"/> once per blink phase.</item>
	/// <item>Losing a lock that was held: <see cref="SoundId.LockLost"/>, once.</item>
	/// <item>No lock, but the target changed this tick: <see cref="SoundId.TargetSelect"/> — the
	/// acquisition blip, which is why selecting a target beeps even for a machine carrying no
	/// missiles.</item>
	/// </list>
	///
	/// <para>Note what the original does <i>not</i> do: the "lock lost" branch returns before the
	/// target-changed test, so switching target while locked plays the loss tone and not the
	/// acquisition blip.</para>
	/// </summary>
	internal void LockToneTick(SimWorld world) {
		if (world.Sounds is not { } sounds || !LocallyPiloted) {
			return;
		}

		if (LockAcquired) {
			_lockToneWasLocked = true;

			if ((world.CoarseTicks & LockToneBlinkBit) == 0) {
				_lockToneSounded = false;
				return;
			}

			if (!_lockToneSounded) {
				sounds.Play(SoundId.LockTone);
				_lockToneSounded = true;
			}

			return;
		}

		if (_lockToneWasLocked) {
			sounds.Play(SoundId.LockLost);
			_lockToneWasLocked = false;
			return;
		}

		if (TargetChanged) {
			sounds.Play(SoundId.TargetSelect);
		}
	}

	/// <summary>Whether the given missile subtype currently holds lock on <see cref="Target"/>.</summary>
	public bool MissileLocked(int missileType) => Weapons.Locked(missileType);

	/// <summary>
	/// The target block of <c>Mech_PerTickSystemsUpdate</c>, run once per machine per tick.
	///
	/// <para><b>It runs after the sensor model, not before</b> — <c>Sim_MainTick</c> calls
	/// <c>FUN_004123ac</c> and then walks the mech list calling this, and the order matters because
	/// the gate below reads the line-of-sight cache that pass maintains. <see cref="SimWorld.Tick"/>
	/// keeps the same order.</para>
	/// </summary>
	internal void MissileLockTick(SimWorld world) {
		// Cleared for every machine every tick, whether or not it has a target — so losing a target
		// drops every lock on the following tick with nothing else needing to know.
		Weapons.ClearMissileLock();
		LockAcquired = false;

		Weapons.RoundsByMissileType(_missileRounds);

		if (Target is not { } target) {
			return;
		}

		bool spoofedThisRoll = EcmRollTick(world, target);

		// Where the target is, and how long a lock on it should take. A turretless chassis — the
		// RAZOR, the one type whose record sets the flag MechTypeRecord.IsFlyer reads — measures the
		// bearing without a twist it does not have, and locks instantly.
		short bearing = Detection.HeadingToward(target.Position, Position);
		short lockTime;
		short bearingError;

		if (!Type.IsFlyer) {
			int range = Position.ApproxDistanceTo(target.Position) >> LockTimeRangeShift;
			lockTime = (short)Math.Min(range, short.MaxValue);
			bearingError = (short)(bearing - Heading + TorsoTwistAngle);
		} else {
			lockTime = 0;
			bearingError = (short)(bearing - Heading);
		}

		// Line of sight, asked from whichever end keeps the cache row on the object that maintains
		// it: a human-side machine asks about the target, a Cybrid asks the target about itself. Only
		// human-side objects run a sensor sweep, so only their rows are ever refreshed.
		bool sighted = Side == MissionSide.Human
			? Detection.LineOfSight(world, this, target)
			: Detection.LineOfSight(world, target, this);

		bool holding = !TargetChanged
			&& (ushort)(bearingError + LockConeHalfWidth) < LockConeHalfWidth * 2
			&& sighted;

		if (!holding) {
			// Everything resets and no lock builds this tick. Subtype 4's timer is deliberately not
			// among them: the original resets 0, 1 and 2 here and leaves 4 running, so that one class
			// keeps a partial lock across a moment of broken sight where the others do not.
			_lockTimer[0] = lockTime;
			_lockTimer[1] = lockTime;
			_lockTimer[2] = lockTime;

			// And the target-changed flag is cleared here and only here, which is what makes it cost
			// exactly one tick: the switch sets it, the next tick lands in this branch and clears it,
			// and locking may begin on the one after.
			TargetChanged = false;
			return;
		}

		// Subtypes 0 and 4 need this machine's own scanner running; 1 needs nothing; 2 needs the
		// target to be emitting. Every one of them is also blocked by ECM except 2.
		StepLock(0, ScannerActive && !spoofedThisRoll, lockTime, blockedByEcm: true);
		StepLock(4, ScannerActive && !spoofedThisRoll, lockTime, blockedByEcm: true);
		StepLock(1, !spoofedThisRoll, lockTime, blockedByEcm: true);
		StepLock(2, target.ScannerActive || target.JammerActive, lockTime, blockedByEcm: false);

		// The lock lamp, from the armed mount's own class. A mount that fires no missile at all reads
		// the whole array instead, which is why the lamp lights for a gun when a launcher elsewhere on
		// the machine has lock.
		short armedType = Weapons.Slots.ElementAtOrDefault(Weapons.Selected)?.AmmoType
			?? WeaponMount.NotAMissile;

		LockAcquired = armedType == WeaponMount.NotAMissile
			? Weapons.AnyLocked
			: Weapons.Locked(armedType);
	}

	/// <summary>
	/// One subtype's countdown. <paramref name="building"/> false holds the timer at
	/// <paramref name="lockTime"/> so no lock can form; true lets it run, and reaching zero latches
	/// the flag.
	/// </summary>
	/// <param name="missileType">Which <c>PROJ.DAT</c> missile subtype.</param>
	/// <param name="building">That subtype's own emission condition.</param>
	/// <param name="lockTime">The reload, which is the range-derived lock time.</param>
	/// <param name="blockedByEcm">
	/// Whether a standing <see cref="EcmSpoofed"/> also stops the lock completing. Only the
	/// anti-radiation subtype passes false.
	/// </param>
	private void StepLock(int missileType, bool building, short lockTime, bool blockedByEcm) {
		// A class the machine carries no rounds for is not tracked at all — its timer is left exactly
		// as it was, which is the original's behaviour: the whole branch is inside `if (rounds != 0)`.
		if (_missileRounds[missileType] == 0) {
			return;
		}

		if (!building) {
			_lockTimer[missileType] = lockTime;
			return;
		}

		if (SimMath.CountdownTimerTick(ref _lockTimer[missileType]) == 0
				&& !(blockedByEcm && EcmSpoofed)) {
			Weapons.SetMissileLock(missileType);
		}
	}

	/// <summary>
	/// The ECM roll. A target that is not a HERC, or one with its jammer off, clears
	/// <see cref="EcmSpoofed"/> outright; a jamming HERC re-rolls it whenever
	/// <see cref="_ecmRollTimer"/> expires, and the interval that follows depends on which way the
	/// roll went — a spoof holds for <see cref="EcmSpoofedInterval"/>, a clear result is re-rolled
	/// after the much shorter <see cref="EcmClearInterval"/>.
	/// </summary>
	/// <returns>
	/// Whether the roll <i>just now</i> came up spoofed. Distinct from <see cref="EcmSpoofed"/>,
	/// which persists: the original tests both, and the fresh result additionally suppresses the
	/// scanner-gated classes for this one tick.
	/// </returns>
	private bool EcmRollTick(SimWorld world, SimObject target) {
		if (target.TargetClass != TargetClass.Herc || !target.JammerActive) {
			EcmSpoofed = false;
			return false;
		}

		if (SimMath.CountdownTimerTick(ref _ecmRollTimer) != 0) {
			return false;
		}

		// The original scales the weight down to a quarter when mech+0x30b — the targeting computer
		// pod's mount (see MechPods) — is present and its +0x7f is under 0x33. What +0x7f means on a
		// pod mount is untested, so the base weight is always used here. That makes ECM at most as
		// strong as the original's, never more.
		if ((world.Random.Next() & (EcmRollRange - 1)) < EcmRollWeight) {
			EcmSpoofed = true;
			_ecmRollTimer = EcmSpoofedInterval;
			return true;
		}

		EcmSpoofed = false;
		_ecmRollTimer = EcmClearInterval;
		return false;
	}
}
