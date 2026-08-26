using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// Base class for everything the simulation ticks — mechs, projectiles, flyers. This is the
/// traditional OOP / virtual-dispatch model docs/engine/planning.md settles on instead of ECS, and
/// the choice is grounded in RE evidence rather than a guess about 1996-era convention: DBSIM's own
/// simulation objects are built on a shared base-object constructor (<c>FUN_00402188</c>) called by
/// every derived class right after its vtable pointer is set, with a 34-slot vtable for the Mech
/// class and smaller 6–9-slot ones for rockets and bullets.
///
/// <para>Only the slots the engine currently needs are declared. Several more are already
/// identified in the disassembly — direct-fire hit-test-and-damage (<c>+0x20</c>), the shield
/// charge getter (<c>+0x34</c>), the AI "I just took fire" notification (<c>+0x50</c>), the
/// lock-on tracking-handle request (<c>+0x54</c>), explosive damage (<c>+0x70</c>) and the shared
/// component health write (<c>+0x74</c>) — but declaring them before combat exists would only mean
/// stubbing them on every subclass. They get added alongside the systems that call them; the point
/// of recording the shape here is that when that happens it is a translation, not a redesign.</para>
/// </summary>
public abstract class SimObject {
	/// <summary>Position in world units, X/Y on the ground plane and Z up (see <see cref="Vec3i"/>).</summary>
	public Vec3i Position { get; set; }

	/// <summary>Facing as a binary angle (see <see cref="BinaryAngle"/>).</summary>
	public int Heading { get; set; }

	/// <summary>
	/// Whether the object is still part of the simulation. DBSIM's per-frame tick
	/// (<c>FUN_0045f464</c>) walks its global object lists and skips entries flagged removed rather
	/// than compacting the list mid-walk; <see cref="SimWorld"/> does the same.
	/// </summary>
	public bool Removed { get; set; }

	/// <summary>
	/// Coarse collision radius, in world units — the original's vtable slot <c>+0x5c</c>, called by
	/// the area-of-effect sweep on every candidate object before comparing against a blast radius.
	/// </summary>
	public abstract int HitRadius { get; }

	/// <summary>
	/// Vtable <c>+0x20</c> — <b>the hit test and the damage application are the same call</b>, which
	/// is the shape of the original and not a shortcut here: <c>Sim_RaycastObjectList</c>
	/// (<c>00426528</c>) offers each live object the shot and the object decides both whether it was
	/// struck and what that did to it. See <c>Mech_DirectFireHitTest</c> (<c>00418ba8</c>) for the
	/// only implementation that exists.
	///
	/// <para>The base returns "missed". <see cref="BaseObject"/> and <see cref="FlyerObject"/> both
	/// have their own <c>+0x20</c> in the original and neither is ported, so beams pass through
	/// structures and aircraft — see docs/simulation/damage-system.md.</para>
	/// </summary>
	/// <returns>
	/// How far along the ray the object was struck, or zero for a miss. The caller shortens the ray
	/// to this, so it has to be a distance rather than a flag.
	/// </returns>
	public virtual int DirectFireHitTest(WeaponShot shot) => 0;

	/// <summary>
	/// One simulation step. Rate-based motion inside an override should go through
	/// <see cref="SimMath.IntegrateRateOverTick"/> rather than multiplying by a float delta —
	/// <see cref="SimWorld"/> maintains <see cref="SimMath.TickDelta"/> for exactly that.
	/// </summary>
	public abstract void Tick(SimWorld world);
}
