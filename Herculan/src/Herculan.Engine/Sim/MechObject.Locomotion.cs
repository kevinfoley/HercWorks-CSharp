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
	/// What a machine's asked-for speed is scaled by once its legs are past
	/// <see cref="LegsCrippledDamage"/> or its reactor is <see cref="ReactorCondition.Critical"/> —
	/// the original's Q10 400, so a little under two fifths.
	/// </summary>
	private const int SeverelyDamagedSpeedScale = 400;

	/// <summary>
	/// And the milder pair's, <see cref="LegsDamaged"/> or a <see cref="ReactorCondition.Degraded"/>
	/// reactor: the original's Q10 750, near enough three quarters.
	/// </summary>
	private const int LightlyDamagedSpeedScale = 750;

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
	/// <param name="turn">
	/// The steering stick, normally <see cref="MechControls.Turn"/> straight through. Center Body
	/// (<see cref="CenterBodyTick"/>) substitutes its own, which is why the caller passes it rather
	/// than this reading it off the controls with everything else.
	/// </param>
	private void ApplyThrottleInput(SimWorld world, short turn) {
		var controls = Controls;
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
	/// <para><b>An immobilised machine gets no say.</b> The original zeroes both the throttle and the
	/// steer before anything else reads them, which is what stops a HERC that has lost its legs — the
	/// wreck still runs this every tick through the inert think, and still decelerates through the
	/// same rate limiter, so it walks its remaining momentum off over the next few ticks rather than
	/// stopping dead. Its obstacle avoidance is skipped with it.</para>
	///
	/// <para>One of the original's terms is still not modelled: the Turbo Pod's speed bonus
	/// (<c>mech+0x317</c>, catalog id 31), which is <i>maximal</i> at full health and decays with
	/// damage — so a machine here runs slightly slower than a fully-podded vanilla one, not faster.
	/// See docs/simulation/mech-locomotion.md, "Damage effects on movement".</para>
	/// </summary>
	private void LocomotionTick(SimWorld world, short turn, short desired) {
		if (Thread is not { } thread) {
			return;
		}

		var type = Type;

		if (Immobilised) {
			// The original's own first branch, and the whole of why a HERC that has lost its legs
			// stops: no unstick countdown, no slope term, no clamp -- both inputs are simply taken
			// away. Everything below still runs, so the machine decelerates through the same rate
			// limiter and walks its momentum off over the next few ticks.
			turn = 0;
			desired = 0;
		} else {
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

			// Mech_AiObstacleAvoidance amends the steer and the speed the caller settled on, in place.
			// The original's own gate is "not the player's machine", which there is the same set as
			// "driven by a think function" because only the player has a pilot. Here Controls can fly
			// any machine, and a machine somebody is flying should not have its stick taken off it.
			if (UnderAiControl) {
				ObstacleAvoidance(world, ref turn, ref desired);
			}
		}

		// The two damage penalties, and they go here rather than higher up because the avoidance
		// step writes a fresh speed of its own: a machine steering round an obstacle is still a
		// damaged machine. They scale what it is asking for, not what it has, so a crippled HERC
		// still accelerates at its own rate -- it just cannot ask for as much. The severe pair wins
		// outright where both apply.
		desired = LegsCrippled || Reactor == ReactorCondition.Critical
			? (short)SimMath.Q10Multiply(SeverelyDamagedSpeedScale, desired)
			: LegsDamaged || Reactor == ReactorCondition.Degraded
				? (short)SimMath.Q10Multiply(LightlyDamagedSpeedScale, desired)
				: desired;

		short previousSpeed = Speed;

		turn = turn >= MechControls.AxisFull ? MechControls.AxisFull
			: turn <= -MechControls.AxisFull ? (short)-MechControls.AxisFull
			: turn;

		short speed = Speed;
		SimMath.RateLimitedMoveToward(ref speed, desired, SimMath.ScalePerTickStep(type.SpeedAccel));
		Speed = speed;

		bool suppressTurning = UpdateGait(world, thread, turn, previousSpeed);

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
	private bool UpdateGait(SimWorld world, Anim.AnimationThread thread, short turn,
			short previousSpeed) {
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

		// A machine that cannot walk any more goes down instead of running the gait machine at all.
		// The turn-in-place arm wins over it, which is the original's own precedence: a machine
		// immobilised mid-pirouette keeps turning rather than falling.
		if (Immobilised && !turningInPlace) {
			FallDown(world, thread, type);
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

	/// <summary>The animation rate the fall is entered at — the original's own literal.</summary>
	private const short FallAnimRate = 100;

	/// <summary>And the rate it plays out at, once the death sequence is actually running.</summary>
	private const short CollapseAnimRate = 0x78;

	/// <summary>
	/// The worst a single component can take from hitting the ground, before the armour scale. The
	/// roll is uniform over it and then biased up by half of it again, so the band is
	/// <c>[max/2, max*3/2)</c>.
	/// </summary>
	private const short CollapseImpactDamage = 0x96;

	/// <summary>
	/// The odds, out of 256, that any one component is caught by the landing at all — a shade under
	/// half. Every live component draws separately.
	/// </summary>
	private const short CollapseImpactOdds = 0x78;

	/// <summary>
	/// <c>Mech_LocomotionTick</c>'s immobilised arm — <b>the fall</b>, and the answer to why a HERC
	/// that loses a leg ends up face down rather than standing still.
	///
	/// <para>It is an animation, not a physics result. Every walking chassis ships a full-body
	/// sequence at <see cref="MechTypeRecord.DeathSequence"/> that nothing else references, and this
	/// is its one consumer: the machine is asked to transition into it, plays it out, and the pose
	/// at its last frame is where it stays. There is no rigid body, no angular velocity and no
	/// ground contact solve anywhere in DBSIM's mech path — the pitch in the final pose is
	/// keyframed.</para>
	///
	/// <list type="number">
	/// <item><b>Entering.</b> With neither the running nor the targeted sequence being the death
	/// one, playback is aimed at it — through <see cref="Anim.AnimationThread.SetTarget"/> rather
	/// than a hard set, so the anim list's own transition into it is used. A machine caught in the
	/// reverse step-off is first snapped to the forward one, because only that has a transition to
	/// take. The fall's own sound goes with it.</item>
	/// <item><b>Falling.</b> The rate goes to <see cref="CollapseAnimRate"/> and the sequence
	/// runs.</item>
	/// <item><b>Landing.</b> On the frame playback stops advancing the machine latches
	/// <see cref="Collapsed"/> — which is what finally takes it off the AI's target lists — takes
	/// the impact damage, and thumps.</item>
	/// </list>
	/// </summary>
	private void FallDown(SimWorld world, Anim.AnimationThread thread, MechTypeRecord type) {
		if (thread.Sequence == type.DeathSequence) {
			AnimRate = CollapseAnimRate;

			// The last frame: playback has nowhere further to advance to. Guarded on the latch, so
			// the landing lands once however long the wreck lies there.
			if (!Collapsed && thread.Frame == thread.NextFrame) {
				Collapsed = true;
				SpreadImpactDamage(world, CollapseImpactDamage, CollapseImpactOdds);
				world.Sounds?.PlayAt(Audio.SoundId.Collision, Position);
			}

			return;
		}

		if (thread.TargetSequence != type.DeathSequence) {
			if (thread.Sequence == type.StopReverseSequence) {
				thread.SetSequence(type.StopForwardSequence, 0, 0);
			}

			thread.SetTarget(type.DeathSequence, -1, 0);

			if (!Collapsed) {
				world.Sounds?.PlayAt(Audio.SoundId.LocomotionCallA, Position);
			}
		}

		AnimRate = FallAnimRate;
	}

	/// <summary>
	/// <c>FUN_00417a04</c> — spreads one impact over the whole machine. Every live component draws
	/// its own <paramref name="odds"/> roll out of 256, and one that is caught takes
	/// <c>Q8(roll + max/2, totalArmor)</c>, so the share scales with what that component had to lose
	/// rather than being flat. The damage goes in through
	/// <see cref="ComponentDamageWrite"/> like any other, cascade and death gate included — which is
	/// how a bad enough landing finishes a machine off.
	///
	/// <para>The original's other caller is the graded hard-landing handler; see
	/// <see cref="Collapsed"/>.</para>
	/// </summary>
	private void SpreadImpactDamage(SimWorld world, short maxDamage, short odds) {
		if (_damage == null || maxDamage == 0) {
			return;
		}

		for (int i = 0; i < ComponentDamage.MechComponentCount; i++) {
			if (!_damage.IsActive(i) || (world.Random.Next() & 0xff) >= odds) {
				continue;
			}

			int roll = world.Random.NextBelow(maxDamage) + (maxDamage >> 1);
			ComponentDamageWrite(world, (short)i,
				(short)SimMath.Q8Multiply(roll, _damage.TotalArmor(i)), null);
		}
	}

	/// <summary>At or above this the machine spawns untouched — the top of the four condition bands.</summary>
	public const short UndamagedCondition = 0x50;

	/// <summary>Below this a machine is placed as a wreck rather than merely pre-damaged.</summary>
	private const short WreckedCondition = 0x14;

	/// <summary>The two leg components a wreck loses one of, and the rear pair on a four-legged chassis.</summary>
	private static readonly short[] FrontLegComponents = { 7, 8 };

	private static readonly short[] RearLegComponents = { 13, 14 };

	/// <summary>What a leg is written off with — past any leg component's armour.</summary>
	private const short LegWriteOff = 32000;

	/// <summary>
	/// <c>FUN_004178e8</c> — applies a machine's <b>starting condition</b> as the mission file states
	/// it, called once from the spawn path before the machine has ever been shot at. The argument is
	/// a percentage: 100 is pristine and the bands widen downwards.
	///
	/// <list type="table">
	/// <item><term>80-100, or negative</term><description>untouched.</description></item>
	/// <item><term>60-79</term><description>a light knocking about: 50 damage at 75/256 a component.</description></item>
	/// <item><term>40-59</term><description>80 at 105.</description></item>
	/// <item><term>20-39</term><description>120 at 145, and the <b>reactor is written off outright</b>
	/// — the dependent is set to its own maximum rather than damaged toward it.</description></item>
	/// <item><term>under 20</term><description>a <b>wreck</b>: one leg destroyed (one of the rear pair
	/// too, on a four-legged chassis), immobilised and collapsed where it stands, then 150 at 175 over
	/// everything else. This is a derelict placed as scenery, not a machine that will fight.</description></item>
	/// </list>
	///
	/// <para>The odds are always the damage figure plus 25, which is the original's own arithmetic
	/// rather than four separate constants.</para>
	///
	/// <para><b>How much the campaign uses it is unmeasured.</b> The ten available
	/// <c>script.dat</c> files are saves taken from a few of the 50-odd <c>.MSN</c> missions, and in
	/// those, 138 of 139 mech records read 100 and one reads 50 — enough to confirm the field is a
	/// percentage, not enough to say which bands the campaign exercises. See
	/// <c>ScriptSpawnRecordExport.StartingCondition</c>.</para>
	///
	/// <para>Left out: a byte the original raises at <c>mech+0xb3</c> on the two worst grades. Its
	/// role is not established and nothing ported reads it.</para>
	/// </summary>
	internal void ApplyStartingCondition(SimWorld world, short condition) {
		if (_damage == null) {
			return;
		}

		short spread = 0;

		if (condition >= 0 && condition < UndamagedCondition) {
			if (condition >= 0x3c) {
				spread = 50;
			} else if (condition >= 0x28) {
				spread = 80;
			} else if (condition >= WreckedCondition) {
				spread = 120;

				// Not damage toward the maximum -- the maximum itself, written straight in.
				_damage.SetDependentDamage(ReactorDependent, _damage.DependentMax(ReactorDependent));
			} else {
				spread = 150;

				ComponentDamageWrite(world, FrontLegComponents[world.Random.Next() & 1], LegWriteOff, null);
				if (Type.LegCount > 2) {
					ComponentDamageWrite(world, RearLegComponents[world.Random.Next() & 1], LegWriteOff, null);
				}

				Immobilised = true;
				Collapsed = true;
			}
		}

		SpreadImpactDamage(world, spread, (short)(spread + 0x19));
	}
}
