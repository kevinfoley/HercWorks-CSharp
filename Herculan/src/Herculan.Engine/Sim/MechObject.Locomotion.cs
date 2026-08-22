using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

// The HERC control law: input to throttle, throttle to a desired speed, and the gait state machine
// that keeps the animation thread playing the right sequence at the right rate. Ported from
// Mech_ApplyThrottleInput (004160dc), Mech_LocomotionTick (00416a04) and
// Mech_ApplyTerrainSlopeToSpeed (0041693c). See docs/simulation/mech-locomotion.md.
public sealed partial class MechObject {
	/// <summary>Throttle movement per tick at full stick deflection, Q8 against the axis.</summary>
	private const int ThrottleRate = 0x91;

	/// <summary>Full throttle, either way. A throttle lever clamps one side of this to zero.</summary>
	private const short ThrottleFull = 0x400;

	/// <summary>
	/// Below this speed a HERC does not turn at all, and the turn-rate tent is measured from here
	/// rather than from zero.
	/// </summary>
	private const int MinimumTurningSpeed = 0x2d;

	/// <summary>The animation rate a stop or turn-in-place sequence is played at.</summary>
	private const short GaitAnimRate = 0x3c;

	/// <summary>Playback rate for a stop / step-off sequence, in either direction.</summary>
	private const short StopAnimRate = 100;

	/// <summary>Q10 multiplier from stick deflection to turn-in-place playback rate.</summary>
	private const int TurnInPlaceRate = 0x15e;

	/// <summary>Q10 multiplier applied to the turn-rate tent before the stick scales it.</summary>
	private const int TurnRateGain = 0x640;

	/// <summary>Stick deflection below which a standing HERC does not start turning in place.</summary>
	private const int TurnInPlaceDeadzone = 0x33;

	/// <summary>Divisor turning the slope/heading dot product into a speed adjustment.</summary>
	private const int SlopeSpeedDivisor = 0x960;

	/// <summary>
	/// <c>Mech_ApplyThrottleInput</c> (<c>004160dc</c>) — turns this tick's stick position into a
	/// throttle setting and a desired speed, then runs the control law.
	///
	/// <para>The throttle is a rate control: the stick moves it rather than setting it, over a range
	/// that spans both directions. Crossing zero snaps to zero for one tick instead of passing
	/// straight through, so holding the axis against its stop runs a machine down from full forward,
	/// through a one-tick pause at rest, and on into full reverse. There is no gear to select — the
	/// sign of the setting <i>is</i> the direction of travel.</para>
	///
	/// <para>A physical throttle lever (<see cref="MechControls.ThrottleLever"/>) replaces the rate
	/// path with an absolute one and closes the clamp to one side of zero, since a lever's travel
	/// only spans one direction.</para>
	/// </summary>
	private void ApplyThrottleInput(SimWorld world) {
		var controls = Controls;
		short turn = controls.Turn;
		short throttleAxis = controls.Throttle;

		if (controls.ThrottleLever != 0) {
			// The absolute-lever path: the axis is a position, not a rate. It is measured from the
			// lever's own centre detent at 0x100 and doubled to cover the throttle's range, with a
			// deadband around the detent, and the rate path below is then skipped for this tick.
			int fromCentre = System.Math.Abs(throttleAxis - MechControls.AxisFull);
			int setting = fromCentre * 2;
			if (setting < 100) {
				setting = 0;
			} else if (controls.ThrottleLever < 0) {
				setting = -setting;
			}

			Throttle = (short)setting;
			throttleAxis = 0;
			ThrottleDirty = true;
		}

		if (controls.ThrottleLever < 0) {
			throttleAxis = (short)-throttleAxis;
		}

		// Steering inverts with the direction of travel. Near the stick's centre the throttle
		// setting decides which way that is; away from it, the stick itself does.
		int deflection = System.Math.Abs((int)throttleAxis);
		if (deflection < 0x3d) {
			if (Throttle >= 0) {
				turn = (short)-turn;
			}
		} else if (throttleAxis < 1) {
			turn = (short)-turn;
		}

		short step = (short)SimMath.Q8Multiply(ThrottleRate, -throttleAxis);
		if (step != 0) {
			ThrottleDirty = true;
			short next = (short)(step + Throttle);

			if (Throttle == 0 || next < 0 == Throttle < 0) {
				// With no lever both limits stand, which is what lets the keyboard reach reverse.
				short upper = controls.ThrottleLever < 0 ? (short)0 : ThrottleFull;
				short lower = controls.ThrottleLever > 0 ? (short)0 : (short)-ThrottleFull;
				Throttle = next >= upper ? upper : next <= lower ? lower : next;
			} else {
				Throttle = 0;
			}
		}

		short maximum = Throttle < 0 ? (short)-Type.MaxReverse : Type.MaxForward;
		LocomotionTick(world, turn, (short)SimMath.Q10Multiply(maximum, Throttle));
	}

	/// <summary>
	/// <c>Mech_LocomotionTick</c> (<c>00416a04</c>) — the control law proper. It settles three
	/// numbers: the speed scalar, the turn rate, and the animation playback rate, and switches the
	/// animation thread between walk, run, stop and turn-in-place sequences as those numbers cross
	/// the type's own thresholds.
	///
	/// <para>Nothing here moves the machine. Turning in place is not produced here either — at zero
	/// speed the turn-rate tent is zero, and the rotation comes from the turn-in-place sequence's
	/// own root rotation.</para>
	///
	/// <para>Three of the original's terms are omitted because they are all exactly zero at full
	/// health and there is no damage system yet: the two flat speed penalties gated on damage flags,
	/// and the <c>mech+0x317</c> subsystem's throttle runaway.</para>
	/// </summary>
	private void LocomotionTick(SimWorld world, short turn, short desired) {
		if (Thread is not { } thread) {
			return;
		}

		var type = Type;

		if (_backoffTimer > 0) {
			_backoffTimer -= SimMath.TickDelta;
			if (_backoffTimer < 0) {
				_backoffTimer = 0;
			}
		}

		if (_backoffTimer == 0) {
			_backoffReverse = false;
			desired = ApplyTerrainSlope(world, desired);
			desired = desired >= type.MaxForward ? type.MaxForward
				: desired <= type.MaxReverse ? type.MaxReverse
				: desired;
		} else {
			// Walking clear of something it collided with; the pilot has no say until it expires.
			desired = _backoffReverse ? type.MaxReverse : type.MaxForward;
		}

		short previousSpeed = Speed;

		turn = turn >= MechControls.AxisFull ? MechControls.AxisFull
			: turn <= -MechControls.AxisFull ? (short)-MechControls.AxisFull
			: turn;

		short speed = Speed;
		SimMath.RateLimitedMoveToward(ref speed, desired, SimMath.ScalePerTickStep(type.SpeedAccel));
		Speed = speed;

		bool suppressTurning = UpdateGait(thread, turn, previousSpeed);

		int speedMagnitude = System.Math.Abs((int)Speed);
		if (speedMagnitude != 0 && speedMagnitude < MinimumTurningSpeed) {
			speedMagnitude = MinimumTurningSpeed;
			if (AnimRate != 0 && System.Math.Abs((int)AnimRate) < GaitAnimRate) {
				AnimRate = AnimRate < 1 ? (short)-GaitAnimRate : GaitAnimRate;
			}
		}

		short turnRate = TurnRate;
		SimMath.RateLimitedMoveToward(ref turnRate,
			(short)SimMath.Q8Multiply(
				(short)SimMath.Q10Multiply(TurnRateGain, TurnRateTent(speedMagnitude, suppressTurning)),
				turn),
			SimMath.ScalePerTickStep(type.TurnAccel));
		TurnRate = turnRate;

		Heading = (Heading + TurnRate) & 0xffff;
		_rotationValid = false;
	}

	/// <summary>
	/// The turn-rate curve: a symmetric tent over speed, zero below
	/// <see cref="MinimumTurningSpeed"/>, peaking at half top speed and falling back to half its
	/// peak at top speed. Returns the peak-scaled rate the stick then deflects.
	/// </summary>
	private int TurnRateTent(int speedMagnitude, bool suppressTurning) {
		if (suppressTurning || speedMagnitude < MinimumTurningSpeed) {
			return 0;
		}

		int maxForward = Type.MaxForward;
		if (speedMagnitude > maxForward) {
			speedMagnitude = maxForward;
		}

		int above = speedMagnitude - MinimumTurningSpeed;
		int half = (maxForward - MinimumTurningSpeed) >> 1;
		int maxTurn = Type.MaxTurnRate;

		if (half <= 0) {
			return 0;
		}

		return half < above
			? maxTurn - maxTurn * (above - half) / (((maxForward - MinimumTurningSpeed) - half) * 2)
			: maxTurn * (above + half) / (half * 2);
	}

	/// <summary>
	/// The gait state machine — about 60% of <c>Mech_LocomotionTick</c>'s body. It compares this
	/// tick's speed against last tick's to tell accelerating from decelerating, then picks the
	/// sequence and the playback rate that go with the machine's state.
	///
	/// <para>Returns whether the turn-rate tent should be suppressed this tick, which it is
	/// whenever a stop or turn-in-place sequence is driving the machine — those animations carry
	/// their own rotation and the tent would double it.</para>
	///
	/// <para>Note that in steady walking or running the playback rate <i>is</i> the speed scalar.
	/// That is what makes the walk/run threshold a real discontinuity: a run stride covers about
	/// twice the ground of a walk stride, so crossing the threshold roughly doubles actual ground
	/// speed while the HUD number moves continuously.</para>
	/// </summary>
	private bool UpdateGait(Anim.AnimationThread thread, short turn, short previousSpeed) {
		var type = Type;

		int targetSequence = thread.TargetSequence;
		int sequence = thread.Sequence;
		int speedMagnitude = System.Math.Abs((int)Speed);
		int previousMagnitude = System.Math.Abs((int)previousSpeed);

		bool turningInPlace = sequence == type.TurnInPlaceSequence;
		bool stopping = sequence == type.StopForwardSequence || sequence == type.StopReverseSequence;
		bool suppressTurning = false;

		// The turn-in-place sequence and forward motion are mutually exclusive.
		if (speedMagnitude != 0 && turningInPlace && !stopping) {
			speedMagnitude = 0;
			turn = 0;
			Speed = 0;
		}

		if (!thread.IsSettled) {
			// Mid-sequence-change the speed scalar is frozen at last tick's value.
			Speed = previousSpeed;
			return false;
		}

		if (previousMagnitude < speedMagnitude) {
			if (stopping) {
				// Stepping off from a standstill: hold at the walk threshold until the step-off
				// animation hands over to the walk cycle.
				if (Speed < 1) {
					if (Speed < -type.GaitThreshold) {
						Speed = (short)-type.GaitThreshold;
					}
					AnimRate = Speed > -GaitAnimRate ? (short)-GaitAnimRate : Speed;
				} else {
					if (Speed > type.GaitThreshold) {
						Speed = type.GaitThreshold;
					}
					AnimRate = Speed < GaitAnimRate ? GaitAnimRate : Speed;
				}

				suppressTurning = true;
				if (targetSequence != type.WalkSequence) {
					thread.SetSequence(
						Speed < 0 ? type.StopReverseSequence : type.StopForwardSequence, 0, 0);
					thread.SetTarget(type.WalkSequence, -1, 0);
				}
			} else if (sequence == type.WalkSequence) {
				AnimRate = Speed;
				if (targetSequence == type.StopForwardSequence
					|| targetSequence == type.StopReverseSequence) {
					thread.ClearTarget();
				}

				if (Speed > type.GaitThreshold && targetSequence != type.RunSequence) {
					thread.SetTarget(type.RunSequence, -1, 0);
				}
			} else {
				thread.ClearTarget();
				AnimRate = Speed;
			}
		} else if (speedMagnitude < previousMagnitude) {
			if (sequence == type.RunSequence) {
				if (speedMagnitude < type.GaitThreshold) {
					// Dropping out of the run gait pins the speed to the threshold rather than
					// letting it fall through, so the walk cycle picks up where the run left off.
					Speed = previousSpeed < 1 ? type.ReverseGaitThreshold : type.GaitThreshold;
					AnimRate = Speed;
					if (targetSequence != type.WalkSequence) {
						thread.SetTarget(type.WalkSequence, -1, 0);
					}
				} else {
					thread.ClearTarget();
					AnimRate = Speed;
				}
			} else if (sequence == type.WalkSequence) {
				if (speedMagnitude == 0) {
					AnimRate = 0;
					if (!stopping) {
						short stopSequence = previousSpeed < 1
							? type.StopReverseSequence
							: type.StopForwardSequence;
						if (targetSequence != stopSequence) {
							thread.SetTarget(stopSequence, -1, 0);
						}

						AnimRate = previousSpeed < 1 ? (short)-StopAnimRate : StopAnimRate;
						suppressTurning = true;
					}
				} else {
					AnimRate = Speed;
					if (targetSequence == type.RunSequence) {
						thread.ClearTarget();
					}
				}
			}
		} else if (speedMagnitude == 0) {
			int deflection = System.Math.Abs((int)turn);

			if (deflection < TurnInPlaceDeadzone || (!stopping && !turningInPlace)) {
				// Standing still with the stick centred: leave the turn-in-place cycle if it is
				// running, and otherwise do nothing at all.
				if (turningInPlace && !stopping && targetSequence != type.StopForwardSequence) {
					thread.SetTarget(type.StopForwardSequence, -1, 0);
					AnimRate = AnimRate < 1 ? (short)-StopAnimRate : StopAnimRate;
				}
			} else if (turningInPlace) {
				// Already turning: the stick sets the playback rate, and the sequence's own root
				// rotation is what actually turns the machine.
				AnimRate = (short)SimMath.Q10Multiply(TurnInPlaceRate, turn);
			} else {
				if (targetSequence != type.TurnInPlaceSequence) {
					thread.SetSequence(
						turn < 0 ? type.StopReverseSequence : type.StopForwardSequence, 0, 0);
					thread.SetTarget(type.TurnInPlaceSequence, -1, 0);
				}

				AnimRate = turn < 0 ? (short)-StopAnimRate : StopAnimRate;
			}
		} else {
			AnimRate = Speed;
		}

		return suppressTurning;
	}

	/// <summary>
	/// <c>Mech_ApplyTerrainSlopeToSpeed</c> (<c>0041693c</c>) — nudges the desired speed by how much
	/// of the ground's slope lies along the machine's heading: uphill costs speed, downhill gains
	/// it. If the adjustment would flip the sign of the desired speed, the speed goes to zero
	/// instead — a slope can stop a HERC but never reverse it.
	/// </summary>
	private short ApplyTerrainSlope(SimWorld world, short desired) {
		var normal = world.Terrain.SurfaceNormalAt(Position.X, Position.Y);
		if (normal is not { } slope || desired == 0) {
			return desired;
		}

		// Model forward is +Y, so the heading vector is the speed rotated by the object matrix.
		var (forwardX, forwardY) = Rotation().RotateVector2D(0, desired);

		int along = slope.X * forwardX + slope.Y * forwardY;
		short adjustment = (short)(along / SlopeSpeedDivisor);
		if (desired < 0) {
			adjustment = (short)-adjustment;
		}

		short adjusted = (short)(adjustment + desired);
		bool flipped = (adjusted < 1 || desired < 1) && (adjusted >= 0 || desired >= 0);
		return flipped ? (short)0 : adjusted;
	}
}
