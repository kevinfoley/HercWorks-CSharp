using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// Taking fire and giving it: the trigger path (<c>FUN_00415608</c> → <c>FUN_00410dbc</c>), the
/// mech's own direct-fire hit test (<c>Mech_DirectFireHitTest</c>, <c>00418ba8</c>) and the two
/// functions below it that turn a struck component into damage
/// (<c>Mech_ApplyDirectFireDamage</c> <c>004188c8</c>, <c>Mech_ComponentDamageWrite</c>
/// <c>00417de4</c>).
/// </summary>
public sealed partial class MechObject {
	/// <summary>
	/// The two cockpit component slots. <c>Mech_ComponentDamageWrite</c> checks them by literal index
	/// as the machine's death gate — losing either one, with everything inside it, kills the pilot
	/// outright regardless of what else is still standing.
	/// </summary>
	private const int CockpitFrontComponent = 0;

	/// <inheritdoc cref="CockpitFrontComponent"/>
	private const int CockpitRearComponent = 1;

	/// <summary>
	/// Dependent sub-piece slots the damage endpoint reads by literal offset. The leg servos are the
	/// front pair, joined by <see cref="RearLegServoDependents"/> on a four-legged chassis; life
	/// support and the pilot are the machine's other two death gates; the reactor drives the two
	/// output-damage flags.
	/// </summary>
	private static readonly int[] FrontLegServoDependents = { 0, 1 };

	/// <inheritdoc cref="FrontLegServoDependents"/>
	private static readonly int[] RearLegServoDependents = { 10, 11 };

	/// <inheritdoc cref="FrontLegServoDependents"/>
	private const int ShieldGeneratorDependent = 4;

	/// <inheritdoc cref="FrontLegServoDependents"/>
	private const int ReactorDependent = 5;

	/// <inheritdoc cref="FrontLegServoDependents"/>
	private const int LifeSupportDependent = 8;

	/// <inheritdoc cref="FrontLegServoDependents"/>
	private const int PilotDependent = 9;

	/// <summary>
	/// Where the legs count as crippled — <c>Mech_ComponentDamageWrite</c>'s own <c>0x8d</c>, Q8 over
	/// 256 with 0 pristine. Its second band at <c>0x50</c> only raises an alert for the pilot, which
	/// is not ported, so there is nothing to key off it here.
	/// </summary>
	private const int LegsCrippledDamage = 0x8d;

	/// <summary>Reactor-damage thresholds, from the same function: <c>0xc0</c> and <c>0x80</c>.</summary>
	private const int ReactorCriticalDamage = 0xc0;

	/// <inheritdoc cref="ReactorCriticalDamage"/>
	private const int ReactorDegradedDamage = 0x80;

	/// <summary>
	/// The Q8 reading that means a part is gone. Both <see cref="ComponentDamage.DamagePercent"/> and
	/// <see cref="ComponentDamage.DependentPercent"/> saturate here.
	/// </summary>
	private const int FullyDamaged = 0x100;

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
	/// <c>mech+0x1a4</c> — the machine's selected target, and the field the whole of homing hangs
	/// off: <c>Bullet_FirePowered</c> reads it to give a plasma round something to chase and
	/// <c>Rocket_Fire</c> reads it to give a missile a lock, so before anything wrote it every guided
	/// weapon in the game flew straight.
	///
	/// <para><b>Nothing in the simulation writes it for the player's machine.</b> The selection is
	/// made in the cockpit and copied here once a frame — see <see cref="TargetSelection"/>, which is
	/// where the RE for that lives. An AI machine's own writer (<c>FUN_0041c0f4</c>) is a separate
	/// function and is not ported, so an AI machine currently never selects anything.</para>
	///
	/// <para>The setter carries the two pieces of bookkeeping every writer of the field in the
	/// original performs, both of which live outside the machine that made the change: the old
	/// target's <see cref="SimObject.TargetedBy"/> count goes down and the new one's goes up, and
	/// <see cref="TargetChanged"/> is raised.</para>
	/// </summary>
	public SimObject? Target {
		get => _target;
		set {
			if (ReferenceEquals(_target, value)) {
				return;
			}

			if (_target != null) {
				_target.TargetedBy--;
			}

			_target = value;

			if (_target != null) {
				_target.TargetedBy++;
			}

			TargetChanged = true;
		}
	}

	private SimObject? _target;

	/// <summary>
	/// <c>mech+0x9d</c> — raised whenever <see cref="Target"/> changes and never cleared by the write
	/// itself. In the original it gates the AI's per-tick weapon arbitration (a machine that has just
	/// switched target does not shoot on that tick) and it is what tells the cockpit to reset the
	/// gunsight's lock state. Nothing consumes it yet; it is set because the setter is the only place
	/// that can, and leaving it out would mean revisiting the setter later.
	/// </summary>
	public bool TargetChanged { get; set; }

	/// <summary>
	/// Total damage this machine has taken, <c>mech+0x288</c> — the running sum the original keeps of
	/// everything both shields and armour have absorbed.
	/// </summary>
	public int DamageTaken { get; private set; }

	/// <summary>
	/// How many shots have got past this machine's shields. Not part of the original — a plain
	/// counter kept alongside the real per-component health in <see cref="Damage"/>, which is what
	/// those shots actually go into.
	/// </summary>
	public int PenetratingHits { get; private set; }

	/// <summary>
	/// <c>mech+0x99</c> — whether the machine is dead. Set by <see cref="ComponentDamageWrite"/> when
	/// either cockpit section is gone or life support or the pilot has been destroyed; see there for
	/// the whole of the test.
	/// </summary>
	public bool Destroyed { get; private set; }

	/// <summary>
	/// <c>mech+0xa4</c> — whether enough legs are gone that the machine cannot walk. Latched, never
	/// cleared, and in the original it also clears the machine's target and fires its mission action.
	/// </summary>
	public bool Immobilised { get; private set; }

	/// <summary>
	/// <c>mech+0xa9</c> — the softer of the two leg states: the legs are past
	/// <see cref="LegsCrippledDamage"/> but not enough of them are actually destroyed.
	/// </summary>
	public bool LegsCrippled { get; private set; }

	/// <summary>
	/// The reactor's condition as the two latching flags <c>mech+0xaa</c> and <c>mech+0xab</c>
	/// describe it. It feeds <see cref="ReactorRate"/>, which the original computes <b>once, at
	/// spawn</b> — so a reactor wrecked mid-mission latches the flag without changing the rate. That
	/// is the original's own behaviour and is reproduced: nothing recomputes the rate from here.
	/// </summary>
	public ReactorCondition Reactor { get; private set; } = ReactorCondition.Intact;

	/// <summary>Who landed the shot that killed it, for the kill credit the original hands back.</summary>
	public SimObject? LastAttacker { get; private set; }

	/// <summary>
	/// <c>Mech_DirectFireHitTest</c> (<c>00418ba8</c>), the mech's vtable <c>+0x20</c> — the hit test
	/// and the damage application in one call, exactly as the original has it.
	///
	/// <list type="number">
	/// <item><b>Reject by distance.</b> Muzzle to machine, against the ray's remaining length plus
	/// this machine's <see cref="MechTypeRecord.HitRadius"/> plus the shot's own
	/// <see cref="WeaponShot.Clearance"/>. A coarse first pass that keeps the transform work off
	/// everything nowhere near the shot.</item>
	/// <item><b>Geometry, in the shot's own frame.</b> The machine's hit centre is brought into
	/// muzzle space, where the ray is the Y axis: the hit needs the centre in front and within range,
	/// and its distance off the axis under this machine's radius. That is a ray-versus-vertical-
	/// cylinder test written as two comparisons.</item>
	/// <item><b>Shields.</b> The facing is picked by which side of the machine the muzzle is on, and
	/// that facing absorbs up to what it holds — see <see cref="ShieldCharge.AbsorbDirectFire"/>. A
	/// shot it absorbs entirely stops here: it still counts as a hit and still stops the ray, and it
	/// spawns only a shield flash.</item>
	/// <item><b>Component selection.</b> Anything that got through goes to the machine's real hit
	/// geometry — the <c>col\&lt;NAME&gt;.COL</c> sphere model, every cluster of which rides one of
	/// the shape's animated nodes, so which part is struck depends on where the legs and torso are
	/// right now. <b>Missing every sphere is a clean miss</b>: the cylinder is only a gate, and a
	/// shot through the gap under a HERC's torso passes on to whatever stands behind it.</item>
	/// </list>
	/// </summary>
	public override int DirectFireHitTest(SimWorld world, WeaponShot shot) {
		var muzzle = new Vec3i(shot.Muzzle.X, shot.Muzzle.Y, shot.Muzzle.Z);
		if (shot.Clearance + shot.Distance + Type.HitRadius < Position.ApproxDistanceTo(muzzle)) {
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

		if (shieldDamage == 0) {
			world.SpawnImpactEffect(
				world.PickImpactEffect(shot.ImpactFx(WeaponShot.ImpactFxGroup.Shield)),
				shot.Muzzle.TransformPoint(0, struckAt, 0));
			return struckAt;
		}

		var hit = CollisionModel.Test(
			_collision, toMuzzleSpace, shot.Distance, shot.Clearance, ComponentAlive, NodeFrame);

		if (hit is not { } struck) {
			return 0;
		}

		DamageTaken += shot.DamageArmor;
		PenetratingHits++;
		ApplyDirectFireDamage(world, struck.ComponentIndex, shot,
			shot.Muzzle.TransformPoint(0, struck.Distance, 0));

		return struck.Distance;
	}

	/// <summary>
	/// Whether one of this machine's components is still standing. A machine with no
	/// <c>dmg\&lt;NAME&gt;.DMG</c> has no components at all and nothing can hit it, which is what the
	/// original ends up with for a type whose files are missing.
	/// </summary>
	private bool ComponentAlive(int index) => _damage?.IsActive(index) ?? false;

	/// <summary>
	/// Where one of the shape's nodes stands right now, relative to this machine's own frame — the
	/// resolver <see cref="CollisionModel.Test"/> places node-mounted sphere clusters with, and the
	/// reason a HERC's hit volume walks with it.
	///
	/// <para>The <c>.COL</c> names a shape <i>part</i> id, which the original resolves through the
	/// shape to that part's transform slot (<c>Mech_ComponentGeometryTest_Candidate</c>, which falls
	/// back on an identity transform for a part the shape does not have) —
	/// <see cref="Anim.ShapeAnimation.TransformIdOfPart"/> is that lookup.</para>
	/// </summary>
	private Transform3? NodeFrame(short partId) {
		int transformId = Animation?.TransformIdOfPart(partId) ?? -1;
		return transformId < 0 || Shape == null ? null : Shape.NodeTransform(transformId);
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
		// The machine is tested by its hit centre, not its origin: a beam passing over a HERC's feet
		// is a miss, and one through its torso is a hit, and the origin is at the feet.
		var center = toMuzzleSpace.TransformPoint(0, 0, Type.HitCenterHeight);

		// Y is distance down the ray. The original's comparison is unsigned, which is what rejects
		// anything behind the muzzle without a second test.
		if ((uint)center.Y >= (uint)range) {
			return 0;
		}

		int offAxis = SimMath.FastMagnitude2D(center.X, center.Z);
		if (offAxis >= Type.HitRadius) {
			return 0;
		}

		// Front or rear is decided by where the muzzle sits in the machine's frame, not by where the
		// machine sits in the shot's — so it is the shooter's bearing that picks the facing, which is
		// what makes turning your back on someone expose the rear array.
		bool front = toMuzzleSpace.Inverted().Y >= 1;
		Shields.AbsorbDirectFire(front, ref shieldDamage);

		int entry = center.Y - (Type.HitRadius - offAxis);
		return entry < 1 ? 1 : entry + 1;
	}

	/// <summary>
	/// <c>Mech_ApplyDirectFireDamage</c> (<c>004188c8</c>) — what a named component does with the
	/// damage that reached it.
	///
	/// <para><b>The shot's splash fraction is taken off the top.</b>
	/// <see cref="WeaponShot.SplashFactor"/> is a Q10 multiplier, and the share it names is
	/// <i>diverted</i> away from the struck component into a small explosion of its own (the mech's
	/// vtable <c>+0x70</c>, a 500-unit blast) — the component gets only the remainder. Every retail
	/// beam states zero, so today the whole shot lands on the component; the blast half has nowhere
	/// to go until an explosive sweep exists.</para>
	///
	/// <para>The effect a hit spawns depends on whether the component's damage reading crossed one of
	/// its eight bands: it did not, and the shot draws from
	/// <see cref="WeaponShot.ImpactFxGroup.Ground"/>; it did, and the shot draws from
	/// <see cref="WeaponShot.ImpactFxGroup.Armor"/> instead. This is the branch that made the two
	/// arrays distinct, and it is now reachable — though on retail data all 27 projectile records
	/// carry byte-identical arrays for the two, so the same effect is drawn either way.</para>
	///
	/// <para>Not ported, and both wanting systems that are not here: the weapon-mount destruction roll
	/// a band change on a component past index 18 makes (those slots are the machine's mounts, indexed
	/// <c>component - 19</c> into the mount manager), and the pilot alerts the original plays
	/// throughout.</para>
	/// </summary>
	private void ApplyDirectFireDamage(SimWorld world, short componentIndex, WeaponShot shot, Vec3i hitPoint) {
		if (_damage == null) {
			return;
		}

		short armorDamage = shot.DamageArmor;
		short splash = (short)SimMath.Q10Multiply(shot.SplashFactor, armorDamage);
		int band = _damage.DamagePercent(componentIndex) >> 5;

		ComponentDamageWrite(componentIndex, (short)(armorDamage - splash), shot.Owner);

		var group = _damage.DamagePercent(componentIndex) >> 5 == band
			? WeaponShot.ImpactFxGroup.Ground
			: WeaponShot.ImpactFxGroup.Armor;

		world.SpawnImpactEffect(world.PickImpactEffect(shot.ImpactFx(group)), hitPoint);
	}

	/// <summary>
	/// <c>Mech_ComponentDamageWrite</c> (<c>00417de4</c>), the mech's vtable <c>+0x74</c> — the shared
	/// endpoint both damage pathways converge on, and where the consequences of losing a part are
	/// worked out.
	///
	/// <list type="number">
	/// <item><b>The write itself</b>, through <see cref="ComponentDamage.ApplyDamage"/>, which is what
	/// carries overflow into the component's internals and cascades the parts mounted on it.</item>
	/// <item><b>Shield capacity is recomputed</b>, because it is a function of the shield generator's
	/// own damage — shooting a machine's generator shrinks the array it can hold. The original calls
	/// <c>Mech_ComputeShieldCapacity</c> from here as well as from the spawn path.</item>
	/// <item><b>The legs are graded</b>, from the servo dependents rather than from the leg components
	/// — the front pair on a biped, averaged with the rear pair on the PITBULL. Lose half of them and
	/// the machine is <see cref="Immobilised"/>; short of that it is <see cref="LegsCrippled"/> or
	/// merely hurt. The RAZOR is skipped outright, as a chassis that does not walk.</item>
	/// <item><b>The death test.</b> Either cockpit section fully gone, or life support or the pilot
	/// destroyed, and the machine is dead — at which point the original re-enters this function with a
	/// flat 30000 on component 0 to finish everything else off, which is why a kill leaves a machine
	/// comprehensively wrecked rather than merely stopped.</item>
	/// <item><b>The reactor flags latch</b> off its own dependent. They never clear, and the check is
	/// gated on both being down, so once the first sets the second is only reachable by a single hit
	/// crossing both thresholds at once.</item>
	/// </list>
	///
	/// <para>Left out: every alert sound, the per-mount condition notification the original pushes
	/// through each mount's own <c>+0x68</c> slot before and after the write, and the debris the
	/// destruction path throws.</para>
	/// </summary>
	private void ComponentDamageWrite(short componentIndex, short damage, SimObject? attacker) {
		if (_damage == null || !_damage.IsActive(componentIndex)) {
			return;
		}

		_damage.ApplyDamage(componentIndex, damage);

		Shields.SetMax(ShieldCapacity(Type.ShieldCapacity,
			(short)_damage.DependentPercent(ShieldGeneratorDependent),
			Pods.ShieldPod, (short)0));

		if (!Type.IsFlyer) {
			GradeLegs(attacker);
		}

		if (!Destroyed && (_damage.FullyDestroyed(CockpitFrontComponent)
				|| _damage.FullyDestroyed(CockpitRearComponent)
				|| _damage.DependentPercent(PilotDependent) == FullyDamaged
				|| _damage.DependentPercent(LifeSupportDependent) == FullyDamaged)) {
			Destroyed = true;
			LastAttacker ??= attacker;

			// The original's own recursive finish-off, with no attacker so the kill is not credited
			// twice. Destroyed is already set, so this pass cannot re-enter the death branch.
			ComponentDamageWrite(CockpitFrontComponent, 30000, null);
		}

		if (Reactor == ReactorCondition.Intact) {
			int reactor = _damage.DependentPercent(ReactorDependent);
			Reactor = reactor > ReactorCriticalDamage ? ReactorCondition.Critical
				: reactor > ReactorDegradedDamage ? ReactorCondition.Degraded
				: ReactorCondition.Intact;
		}
	}

	/// <summary>
	/// The leg half of <c>Mech_ComponentDamageWrite</c>. The readings are the servo dependents' own,
	/// not the leg components': a HERC's legs are graded by what is inside them.
	/// </summary>
	private void GradeLegs(SimObject? attacker) {
		int legCount = Type.LegCount;
		if (_damage == null || legCount == 0 || Immobilised) {
			return;
		}

		var slots = legCount > 2
			? FrontLegServoDependents.Concat(RearLegServoDependents).ToArray()
			: FrontLegServoDependents;

		int destroyed = 0;
		foreach (int slot in slots.Take(legCount)) {
			if (_damage.DependentPercent(slot) == FullyDamaged) {
				destroyed++;
			}
		}

		if (destroyed >= legCount / 2) {
			Immobilised = true;
			LastAttacker ??= attacker;
			return;
		}

		// A four-legged chassis is graded on the average of each side's pair rather than on the front
		// pair alone — the original's own `(front + rear) >> 1`, per side.
		int left = _damage.DependentPercent(FrontLegServoDependents[0]);
		int right = _damage.DependentPercent(FrontLegServoDependents[1]);
		if (legCount == 4) {
			left = (left + _damage.DependentPercent(RearLegServoDependents[0])) >> 1;
			right = (right + _damage.DependentPercent(RearLegServoDependents[1])) >> 1;
		}

		if (left >= LegsCrippledDamage || right >= LegsCrippledDamage) {
			LegsCrippled = true;
		}
	}
}
