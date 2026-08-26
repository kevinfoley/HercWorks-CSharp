using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// The visible half of a beam shot — the tracer object <c>Bullet_FireBurst</c> (<c>0040bf74</c>)
/// spawns once the hit has already been resolved, constructed by <c>FUN_0040b804</c> and drawn by
/// its class's vtable slot 0 (<c>FUN_0040bc14</c>).
///
/// <para>It is not a <see cref="SimObject"/> in the original either: tracers live in their own pool
/// (<c>DAT_004a9746</c>), which <c>Sim_MainTick</c> walks <b>before</b> the machine list — ticking
/// each one through vtable <c>+0x14</c> and freeing it the moment its countdown reaches zero — and
/// which the frame submit walks separately. Nothing raycasts against them and they carry no damage;
/// the shot they came from was resolved and finished before the first one was allocated.</para>
///
/// <para><b>One tracer per shot, not one per 5000 units.</b> The original splits the run into
/// 5000-unit spans and allocates a tracer for each, because its rasterizer interpolates a poly's
/// screen-space width linearly between the two ends and a single quad spanning a kilometre would
/// taper visibly wrong. The engine builds the quad in world space and lets the projection do the
/// perspective, which is exact over any length, so the split has nothing left to buy.</para>
/// </summary>
public sealed class BeamTracer {
	/// <summary>
	/// The countdown the constructor arms, at the object's <c>+0x5d</c> (<c>Math_CountdownTimerTick</c>
	/// takes a pointer one byte below the short it decrements, which is why the two offsets in the
	/// disassembly appear to disagree).
	///
	/// <para>It is in the same Q8-of-125 ms unit as every other timer in the simulation, so 56 is
	/// 27 ms — less than one tick's worth of <see cref="SimWorld.TickDelta"/>. A tracer is therefore
	/// alive for exactly one tick however fast the machine runs, which is what makes a held trigger
	/// read as a train of separate flashes rather than a continuous beam.</para>
	/// </summary>
	public const short InitialLife = 0x38;

	internal BeamTracer(Vec3i start, Vec3i end, short missileId) {
		Start = start;
		End = end;
		MissileId = missileId;
		Life = InitialLife;
	}

	/// <summary>The muzzle point — <c>FUN_0040b804</c>'s <c>param_3</c>, the first of the two points it stores.</summary>
	public Vec3i Start { get; }

	/// <summary>
	/// Where the beam stopped: the shot's own frame at <c>(0, travelled, 0)</c>, where travelled is
	/// the raycast's reported distance or the weapon's full range when it struck nothing.
	/// </summary>
	public Vec3i End { get; }

	/// <summary>
	/// The <c>PROJ.DAT</c> record's subtype id, which is what indexes <c>BEAM.DAT</c> — not the
	/// weapon id. It is <c>Bullet_FireBurst</c>'s own first parameter, passed straight through to the
	/// tracer and read back by the draw at <c>+0x52</c>.
	/// </summary>
	public short MissileId { get; }

	/// <summary>Ticks remaining, in the timer unit <see cref="InitialLife"/> documents.</summary>
	public short Life { get; private set; }

	/// <summary>
	/// <c>FUN_0040c2a0</c>, vtable <c>+0x14</c> — one <c>Math_CountdownTimerTick</c> and nothing else.
	/// </summary>
	/// <returns>Whether the tracer has expired and should be freed.</returns>
	internal bool Tick() {
		Life = (short)(Life - SimMath.TickDelta);
		if (Life < 0) {
			Life = 0;
		}

		return Life == 0;
	}
}
