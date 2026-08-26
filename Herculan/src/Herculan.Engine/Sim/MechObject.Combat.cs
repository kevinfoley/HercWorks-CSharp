using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// Taking fire and giving it: the trigger path (<c>FUN_00415608</c> → <c>FUN_00410dbc</c>) and the
/// mech's own direct-fire hit test (<c>Mech_DirectFireHitTest</c>, <c>00418ba8</c>).
/// </summary>
public sealed partial class MechObject {
	/// <summary>
	/// <c>FUN_00415608</c>, the player's own fire path, called once a frame from
	/// <c>Sim_PollPlayerInput</c> with the input device struct.
	///
	/// <para><b>The trigger is a held state, not a keypress.</b> The mount's own vtable <c>+0x30</c>
	/// (<c>FUN_0040f8ad</c>) does nothing but read the device struct's byte at <c>+0x0d</c> — the
	/// fire button — so holding it fires again the moment the refire timer runs out and the capacitor
	/// is back over the threshold. Nothing edge-detects it anywhere along the path.</para>
	///
	/// <para>Only a machine with a pilot ever reaches this: the original calls it from the input poll
	/// for <c>LocalPlayerMech</c> alone, and AI machines fire through their own think function, which
	/// is unported. Here that falls out of <see cref="Controls"/>, which is
	/// <see cref="MechControls.Neutral"/> for everything the player is not flying.</para>
	///
	/// <para>The rest of <c>FUN_00415608</c> — the lead-indicator trail it lays down along the
	/// bearing to the selected target on a successful shot — is a HUD feature and is not here.</para>
	/// </summary>
	private void FireTick(SimWorld world) {
		Weapons.FireTick(this, world, Controls.Fire);
	}

	/// <summary>
	/// Total damage this machine has taken, <c>mech+0x288</c> — the running sum the original keeps of
	/// everything both shields and armour have absorbed. It is the one damage figure the engine can
	/// carry honestly: <see cref="PenetratingHits"/> explains why.
	/// </summary>
	public int DamageTaken { get; private set; }

	/// <summary>
	/// How many shots have got past this machine's shields. <b>Not part of the original</b> — it
	/// stands in for the two steps that follow shield absorption and are not modelled:
	/// <c>Mech_SelectStruckComponent</c> (<c>0040c9d4</c>), which does a ray-versus-subshape test
	/// against the 29-slot component array to find the one component struck, and
	/// <c>Mech_ApplyDirectFireDamage</c> (<c>004188c8</c>), which writes that component's health.
	/// There is no component array yet, so penetrating damage is counted rather than applied and
	/// nothing on this machine is ever destroyed.
	/// </summary>
	public int PenetratingHits { get; private set; }

	/// <summary>
	/// <c>Mech_DirectFireHitTest</c> (<c>00418ba8</c>), the mech's vtable <c>+0x20</c> — the hit test
	/// and the damage application in one call, exactly as the original has it.
	///
	/// <list type="number">
	/// <item><b>Reject by distance.</b> Muzzle to machine, against the ray's remaining length plus
	/// this machine's radius plus <see cref="WeaponShot.MuzzleClearance"/>. A coarse first pass that
	/// keeps the transform work off everything nowhere near the shot.</item>
	/// <item><b>Geometry, in the shot's own frame.</b> The machine's centre of mass is brought into
	/// muzzle space, where the ray is the Y axis: the hit needs the centre in front and within range,
	/// and its distance off the axis under this machine's radius. That is a ray-versus-vertical-
	/// cylinder test written as two comparisons.</item>
	/// <item><b>Shields.</b> The facing is picked by which side of the machine the muzzle is on, and
	/// that facing absorbs up to what it holds — see <see cref="ShieldCharge.AbsorbDirectFire"/>.</item>
	/// <item><b>What is left goes to armour</b>, or would: see <see cref="PenetratingHits"/>.</item>
	/// </list>
	///
	/// <para>Note what the shield step does <i>not</i> do: a shot it absorbs entirely still counts as
	/// a hit and still stops the ray. Shields do not let fire through to whatever stands behind.</para>
	/// </summary>
	public override int DirectFireHitTest(WeaponShot shot) {
		var muzzle = new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z);
		if (WeaponShot.MuzzleClearance + shot.Distance + HitRadius < Position.ApproxDistanceTo(muzzle)) {
			return 0;
		}

		// Machine space to muzzle space, the two hops the original composes: this machine's own
		// world transform, then the world-to-muzzle one the raycast cached.
		var toMuzzleSpace = Transform3.Concat(WorldTransform, shot.MuzzleInverse);

		short shieldDamage = shot.DamageShield;
		int struckAt = ShieldAbsorbDirectFire(toMuzzleSpace, shot.Distance, ref shieldDamage);
		if (struckAt == 0) {
			return 0;
		}

		DamageTaken += shot.DamageShield - shieldDamage;

		if (shieldDamage > 0) {
			DamageTaken += shot.DamageArmor;
			PenetratingHits++;
		}

		return struckAt;
	}

	/// <summary>
	/// <c>Mech_ShieldAbsorb_DirectFire</c> (<c>00413cc4</c>) — the geometry and the facing choice, with
	/// <see cref="ShieldCharge.AbsorbDirectFire"/> doing the absorption itself.
	///
	/// <para>The returned distance is the original's own linearisation of where the ray enters the
	/// hit cylinder: <c>alongAxis - (radius - offAxis)</c>, floored at 1 so that a hit is never
	/// mistaken for a miss. It is what the raycast shortens the ray to.</para>
	/// </summary>
	/// <param name="toMuzzleSpace">This machine's frame expressed in the shot's.</param>
	/// <param name="range">The ray's remaining length.</param>
	/// <param name="shieldDamage">The shot's shield damage, reduced by what the struck facing took.</param>
	/// <returns>How far along the ray this machine was struck, or zero for a miss.</returns>
	private int ShieldAbsorbDirectFire(in Transform3 toMuzzleSpace, int range, ref short shieldDamage) {
		// The machine is tested by its centre of mass, not its origin: a beam passing over a HERC's
		// feet is a miss, and one through its torso is a hit, and the origin is at the feet.
		var center = toMuzzleSpace.TransformPoint(0, 0, _centerHeight);

		// Y is distance down the ray. The original's comparison is unsigned, which is what rejects
		// anything behind the muzzle without a second test.
		if ((uint)center.Y >= (uint)range) {
			return 0;
		}

		int offAxis = SimMath.FastMagnitude2D(center.X, center.Z);
		if (offAxis >= HitRadius) {
			return 0;
		}

		// Front or rear is decided by where the muzzle sits in the machine's frame, not by where the
		// machine sits in the shot's — so it is the shooter's bearing that picks the facing, which is
		// what makes turning your back on someone expose the rear array.
		bool front = toMuzzleSpace.Inverted().Y >= 1;
		Shields.AbsorbDirectFire(front, ref shieldDamage);

		int entry = center.Y - (HitRadius - offAxis);
		return entry < 1 ? 1 : entry + 1;
	}
}
