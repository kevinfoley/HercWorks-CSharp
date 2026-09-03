using HercWorks.Core.Data.File.Dat.Sim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// What happens where a shot lands — DBSIM's explosion class, built by <c>FUN_00407f1c</c> and
/// advanced by <c>FUN_0040813c</c>, allocated from the pool at <c>DAT_004a96a2</c>.
///
/// <para>An effect is a <c>dts\EXPLOS.DTS</c> root standing still at the point of impact, playing
/// its flipbook of billboards through exactly once. <c>FUN_0040813c</c> is the whole of its life:
/// count the type's <see cref="ExplosionTypeEntry.FrameInterval"/> down, step the shape's
/// cell-animation frame when it expires, and end the effect on the step that wraps the frame back to
/// zero. Nothing moves it and nothing else can stop it.</para>
///
/// <para>Like a <see cref="BeamTracer"/> and a <see cref="Projectile"/> it is <b>not</b> a
/// <see cref="SimObject"/> in the original either — it comes from the effect pool
/// <c>Sim_MainTick</c> walks ahead of the machine list, so nothing can shoot it and nothing collides
/// with it.</para>
///
/// <para>A row with a nonzero <see cref="ExplosionTypeEntry.LightMode"/> also claims a dynamic
/// light for as long as the flipbook runs — <see cref="EffectLightField"/>, whose slot this drives
/// from the row's per-frame intensity ramp. <c>LightMode</c> 1 and 2 reach the same code; the
/// original tests the field only against zero.</para>
///
/// <para><b>One thing the original's constructor also does is not here</b>: the second attached
/// effect at <see cref="ExplosionTypeEntry.TrailEffect"/>, which no retail row asks for. The
/// proximity radius the type's own query slot reports on is likewise unread — nothing queries
/// it.</para>
/// </summary>
public sealed class ImpactEffect {
	private readonly ExplosionTypeEntry _record;
	private readonly int _frameCount;
	private readonly EffectLightField? _lights;
	private short _timer;

	/// <param name="typeId">The <c>EXPLOS.DAT</c> type row, which is what a <c>PROJ.DAT</c> <c>ImpactFX</c> array holds.</param>
	/// <param name="record">That row.</param>
	/// <param name="frameCount">How many frames the row's shape has — see <see cref="ExplosionCatalog.FrameCount"/>.</param>
	/// <param name="position">Where the shot landed, in world units.</param>
	/// <param name="lights">
	/// The field a light-bearing row claims a slot in, or null to run the effect without one.
	/// </param>
	internal ImpactEffect(
			short typeId, ExplosionTypeEntry record, int frameCount, Vec3i position,
			EffectLightField? lights = null) {
		TypeId = typeId;
		_record = record;
		_frameCount = frameCount;
		Position = position;

		// The constructor resets the shape instance's own frame counter for this sequence, so an
		// effect always opens on frame 0 however the shape was left by the last one to use it.
		Frame = 0;
		_timer = record.FrameInterval;

		// FUN_00407604: the row's LightMode is tested against zero and nothing else, and the slot
		// opens on FrameIntensity[0] — the one ramp entry the tick never reaches, because it reads
		// the ramp at the frame it has just stepped to and stops the effect when that wraps to 0.
		_lights = record.LightMode != 0 ? lights : null;
		LightHandle = _lights?.Claim(position, FrameIntensity(0)) ?? -1;
	}

	/// <summary>The <c>EXPLOS.DAT</c> type row this effect is, <c>obj+0x41</c>.</summary>
	public short TypeId { get; }

	/// <summary>
	/// Which <see cref="EffectLightField"/> slot this effect's light occupies, or -1 when the row
	/// asks for no light or every slot was busy. The handle the original keeps at
	/// <c>handle+0x0c</c>.
	/// </summary>
	public int LightHandle { get; private set; } = -1;

	/// <summary>Which <c>EXPLOS.DTS</c> root it draws — the type row's own first field.</summary>
	public int ShapeIndex => _record.ShapeIndex;

	/// <summary>Where it sits, in world units. Written once at construction; nothing moves it.</summary>
	public Vec3i Position { get; }

	/// <summary>
	/// The shape's cell-animation frame. The original keeps it on the shape instance rather than on
	/// the effect, which is the same thing given one instance per effect.
	/// </summary>
	public int Frame { get; private set; }

	/// <summary>
	/// <c>FUN_0040813c</c>, vtable <c>+0x14</c>. Returns whether the effect is finished and should be
	/// freed — which happens the moment the flipbook wraps, so the animation plays exactly once.
	///
	/// <para>A shape with no frames at all ends on its first timer expiry, matching the original's
	/// own branch for a negative animation sequence: there is no frame to step, so there is nothing
	/// left to draw.</para>
	/// </summary>
	internal bool Tick() {
		if (SimMath.CountdownTimerTick(ref _timer) != 0) {
			return false;
		}

		if (_frameCount <= 0) {
			ReleaseLight();
			return true;
		}

		Frame = (Frame + 1) % _frameCount;
		if (Frame == 0) {
			ReleaseLight();
			return true;
		}

		// FUN_004076a0, driven from the ramp at the frame just stepped to. Reached only for a
		// nonzero frame, which is why FrameIntensity[0] is the constructor's business alone.
		_lights?.SetIntensity(LightHandle, FrameIntensity(Frame));

		_timer = _record.FrameInterval;
		return false;
	}

	/// <summary>
	/// The type row's intensity ramp at one frame, as the original reads it — the entry's low byte,
	/// and 0 for a frame past the twelve the row has room for. A shape with a longer flipbook than
	/// that runs the original off the end of the row into <c>ProximityRadius</c>; stopping at the
	/// ramp's own length is this engine's, and it only differs for data no retail shape supplies.
	/// </summary>
	private int FrameIntensity(int frame) =>
		frame >= 0 && frame < _record.FrameIntensity.Length ? _record.FrameIntensity[frame] & 0xff : 0;

	/// <summary><c>FUN_0040765c</c> — hands the slot back when the effect is over.</summary>
	private void ReleaseLight() {
		_lights?.Release(LightHandle);
		LightHandle = -1;
	}
}
