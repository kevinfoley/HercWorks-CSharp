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
/// <para><b>Three things the original's constructor also does are not here</b>, each belonging to a
/// system that does not exist yet: the light source
/// (<see cref="ExplosionTypeEntry.LightMode"/> and the per-frame intensity ramp it drives), the
/// sound (<see cref="ExplosionTypeEntry.SoundId"/>, played as <c>id + 10</c>), and the second
/// attached effect at <see cref="ExplosionTypeEntry.TrailEffect"/> — which no retail row asks for
/// anyway. The proximity radius the type's own query slot reports on is likewise unread: nothing
/// queries it.</para>
/// </summary>
public sealed class ImpactEffect {
	private readonly ExplosionTypeEntry _record;
	private readonly int _frameCount;
	private short _timer;

	/// <param name="typeId">The <c>EXPLOS.DAT</c> type row, which is what a <c>PROJ.DAT</c> <c>ImpactFX</c> array holds.</param>
	/// <param name="record">That row.</param>
	/// <param name="frameCount">How many frames the row's shape has — see <see cref="ExplosionCatalog.FrameCount"/>.</param>
	/// <param name="position">Where the shot landed, in world units.</param>
	internal ImpactEffect(short typeId, ExplosionTypeEntry record, int frameCount, Vec3i position) {
		TypeId = typeId;
		_record = record;
		_frameCount = frameCount;
		Position = position;

		// The constructor resets the shape instance's own frame counter for this sequence, so an
		// effect always opens on frame 0 however the shape was left by the last one to use it.
		Frame = 0;
		_timer = record.FrameInterval;
	}

	/// <summary>The <c>EXPLOS.DAT</c> type row this effect is, <c>obj+0x41</c>.</summary>
	public short TypeId { get; }

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
			return true;
		}

		Frame = (Frame + 1) % _frameCount;
		if (Frame == 0) {
			return true;
		}

		_timer = _record.FrameInterval;
		return false;
	}
}
