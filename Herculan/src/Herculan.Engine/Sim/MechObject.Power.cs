using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// The reactor and the Master Energy Pool — <c>Mech_PerTickSystemsUpdate</c>'s (<c>0041aa5c</c>)
/// opening five statements, which are the whole of DBSIM's power model.
/// </summary>
public sealed partial class MechObject {
	/// <summary>
	/// Reactor output for an undamaged HERC with no Energy Pod, in pool units per unit time —
	/// <c>FUN_00417d08</c>'s bare <c>0x14</c>. It is a literal, not a per-type stat: every machine in
	/// the fleet, from the STINGRAY to the APOCALYPSE, runs the same reactor.
	/// </summary>
	public const short BaseReactorOutputRate = 0x14;

	/// <summary>
	/// The pool's ceiling, and the value a HERC powers up holding — <c>Mech_Constructor</c> writes
	/// 10000 to <c>mech+0x292</c> and the tick clamps back to it. Also the denominator the cockpit
	/// meter's fill fraction is taken against.
	/// </summary>
	public const short EnergyPoolMax = 10000;

	/// <summary>
	/// The slice of the pool nothing may spend. The tick offers consumers <c>pool - 500</c> and puts
	/// <c>leftover + 500</c> back, so 500 units are held out of every arbitration and the pool
	/// settles at that floor rather than at zero under sustained demand.
	///
	/// <para>It is a floor, not a battery: when the pool is already below it the budget goes
	/// negative, which the weapon mounts read as "give charge back" — a drained machine pulls energy
	/// out of its own capacitors to hold the reserve.</para>
	/// </summary>
	public const short EnergyPoolReserve = 500;

	/// <summary>
	/// How badly the reactor itself is hurt. <c>Mech_ComponentDamageWrite</c> (<c>00417de4</c>) sets
	/// one of two latching flags off the damage on sub-piece 5 of the 22-entry dependent array, and
	/// the same pair also cuts movement speed — power and mobility failing together is what
	/// identifies that sub-piece as the reactor.
	/// </summary>
	public enum ReactorCondition {
		/// <summary>Sub-piece 5 at 50% damage or less. No penalty.</summary>
		Intact,

		/// <summary><c>mech+0xaa</c>, set past 50% damage: output falls to <c>Q10(600)</c>, about 59%.</summary>
		Degraded,

		/// <summary><c>mech+0xab</c>, set at 75% damage or worse: output falls to <c>Q10(200)</c>, about 20%.</summary>
		Critical,
	}

	/// <summary>
	/// This machine's reactor output rate, computed once when its loadout was configured.
	///
	/// <para><b>It is not recomputed.</b> <c>FUN_00417d08</c> has exactly one reference in the whole
	/// of DBSIM — the tail of <c>Mech_ConfigureLoadout</c>, which itself is only reached on spawn —
	/// so every damage term it reads is sampled at that one moment and the number stands for the rest
	/// of the mission. A HERC whose reactor is shot to pieces mid-fight keeps generating exactly what
	/// it generated when it rolled out. The damage terms are ported anyway, because they are what the
	/// original computes and because a machine can spawn already damaged.</para>
	/// </summary>
	public short ReactorOutputRate { get; private set; }

	/// <summary>
	/// The Master Energy Pool, <c>mech+0x292</c> — the manual's "advanced capacitor" that the reactor
	/// fills and the weapons and shields draw down. Ranges 0 to <see cref="EnergyPoolMax"/>, starts
	/// full.
	/// </summary>
	public short EnergyPool { get; private set; } = EnergyPoolMax;

	/// <summary>This machine's shield array — the pool's sink once the weapon mounts have taken their share.</summary>
	public ShieldCharge Shields { get; private set; } = new(0);

	/// <summary>The pods this machine's fit gave it. Two of them feed the numbers on this page.</summary>
	public MechPods Pods { get; private set; }

	/// <summary>
	/// The pool as a fraction of full, Q10 over 0-1024 — what
	/// <c>Player_PerFrameCockpitUpdate</c> computes as <c>(pool &lt;&lt; 10) / 10000</c> and hands the
	/// cockpit's energy meter. The meter's own LED bar is built with a range of <c>0x400</c>, so this
	/// is the number it fills against.
	/// </summary>
	public int EnergyPoolFraction => ((int)EnergyPool << 10) / EnergyPoolMax;

	/// <summary>
	/// <c>Mech_ConfigureLoadout</c>'s (<c>004175dc</c>) closing three calls, in its own order:
	/// file the pods out of the mount list, size the shield array and fill it, then work out the
	/// reactor rate. Everything damage-dependent in here is sampled now and not looked at again.
	/// </summary>
	/// <param name="bodyDamage">
	/// Damage on sub-piece 4, Q8 over 0-256 with 0 pristine — the chassis term in the shield-capacity
	/// formula. Zero until the component system lands.
	/// </param>
	/// <param name="shieldPodDamage">Damage on the Shield Pod's own component slot, same scale.</param>
	/// <param name="energyPodDamage">Damage on the Energy Pod's own component slot, same scale.</param>
	/// <param name="reactor">The reactor's condition as the damage endpoint's two flags describe it.</param>
	public void ConfigureLoadout(
			short bodyDamage = 0,
			short shieldPodDamage = 0,
			short energyPodDamage = 0,
			ReactorCondition reactor = ReactorCondition.Intact) {

		Pods = MechPods.FromLoadout(Loadout);

		Shields = new ShieldCharge(Type.ShieldCapacity);
		Shields.SetMax(ShieldCapacity(Type.ShieldCapacity, bodyDamage, Pods.ShieldPod, shieldPodDamage));
		Shields.RefillToBalance();

		ReactorOutputRate = ReactorRate(Pods.EnergyPod, energyPodDamage, reactor);
		EnergyPool = EnergyPoolMax;
	}

	/// <summary>
	/// <c>FUN_00417d08</c> — the reactor's output rate.
	///
	/// <para>A flat <see cref="BaseReactorOutputRate"/>, replaced outright (not scaled) by a much
	/// smaller figure when either reactor-damage flag is up, plus an Energy Pod's contribution. The
	/// pod's term is worth a second <see cref="BaseReactorOutputRate"/> at full — it <i>doubles</i>
	/// reactor output — and falls away in five steps as the pod itself is chewed up, contributing
	/// nothing at all once it passes 88% damage. The manual sells the Energy Pod as doubling the
	/// pool's <i>capacity</i>; the code doubles its <i>recharge rate</i> and leaves
	/// <see cref="EnergyPoolMax"/> alone, so on that point the manual is wrong and its own
	/// afterthought — "a modest increase in the Pool recharge rate" — is the real effect.</para>
	/// </summary>
	/// <param name="energyPod">Whether slot 3 is filled.</param>
	/// <param name="podDamage">The pod's component damage, Q8 over 0-256 with 0 pristine.</param>
	/// <param name="reactor">Which of the two reactor-damage flags, if either, is up.</param>
	public static short ReactorRate(bool energyPod, short podDamage, ReactorCondition reactor) {
		short rate = reactor switch {
			ReactorCondition.Critical => (short)SimMath.Q10Multiply(200, BaseReactorOutputRate),
			ReactorCondition.Degraded => (short)SimMath.Q10Multiply(600, BaseReactorOutputRate),
			_ => BaseReactorOutputRate,
		};

		if (energyPod && DamageScale(podDamage) is { } scale) {
			rate += (short)SimMath.Q10Multiply(scale, BaseReactorOutputRate);
		}

		return rate;
	}

	/// <summary>
	/// <c>FUN_00417bec</c> — the shield array's total capacity, the sibling of
	/// <see cref="ReactorRate"/> and built the same way.
	///
	/// <para>Heavy chassis damage scales the base capacity down: past 50% damage it drops in five
	/// steps to half. A Shield Pod then adds up to a second full base capacity on top — the manual's
	/// "doubles the effective size of your shield reserves", exactly — degrading with the pod's own
	/// damage on the same five-step curve the Energy Pod uses. The pod's share is a fraction of the
	/// <i>undamaged</i> base, not of the chassis-reduced figure, which is the original's own
	/// arithmetic and means a battered machine still gets the full pod bonus.</para>
	/// </summary>
	public static short ShieldCapacity(short baseCapacity, short bodyDamage, bool shieldPod, short podDamage) {
		short capacity = baseCapacity;

		// Chassis wear, on its own curve: 25-damage steps against a 102/1024 penalty each, so the
		// worst case is 514/1024 of base rather than zero.
		if (bodyDamage > 0x7f) {
			capacity = (short)SimMath.Q10Multiply(baseCapacity, ((bodyDamage - 0x80) / 0x19) * -0x66 + 0x400);
		}

		if (shieldPod && DamageScale(podDamage) is { } scale) {
			capacity += (short)SimMath.Q10Multiply(scale, baseCapacity);
		}

		return capacity;
	}

	/// <summary>
	/// The five-step curve both pods degrade on, shared verbatim between <c>FUN_00417d08</c> and
	/// <c>FUN_00417bec</c>: <c>1024 - 204 * (damage / 51)</c>, Q10, gated off entirely at 225 damage
	/// out of 256. Null means the pod is too far gone to contribute — which is not the same as a
	/// scale of zero, and the original's gate is why the last step is 208/1024 rather than a smooth
	/// fade to nothing.
	/// </summary>
	private static short? DamageScale(short damage) =>
		damage < 0xe1 ? (short)((damage / 0x33) * -0xcc + 0x400) : null;

	/// <summary>
	/// The reactor/energy half of <c>Mech_PerTickSystemsUpdate</c> — its first five statements, in
	/// order, and the only place the pool moves.
	///
	/// <list type="number">
	/// <item>The reactor's rate is integrated over this tick and added to the pool.</item>
	/// <item>The weapon mounts are offered <c>pool - 500</c> and hand back what they did not take.</item>
	/// <item>The shields are offered that remainder and hand back what <i>they</i> did not take.</item>
	/// <item>The reserve is added back and the result becomes the new pool, <b>overwriting</b> it —
	/// consumption is not a subtraction, it is that the pool is rebuilt from whatever survived the
	/// pass.</item>
	/// <item>The pool is clamped to 0..<see cref="EnergyPoolMax"/>.</item>
	/// </list>
	///
	/// <para>The ordering is the answer to the manual's claim that energy goes to "movement, shields,
	/// and weapons, in that order". It is weapons and then shields, and movement takes nothing at all
	/// — the locomotion tick never reads the pool. What the manual gets right is the consequence:
	/// because weapons are served first and the shields only ever get the leftovers, a machine firing
	/// hard stops replenishing its shields long before it stops shooting.</para>
	///
	/// <para>All of it is 16-bit, and deliberately so — the original's adds are word-sized and wrap.
	/// Nothing in the real ranges gets anywhere near an overflow, but the arithmetic is kept faithful
	/// rather than widened.</para>
	/// </summary>
	private void PowerTick() {
		EnergyPool = unchecked((short)(EnergyPool + SimMath.IntegrateRateOverTick(ReactorOutputRate)));

		short budget = unchecked((short)(EnergyPool - EnergyPoolReserve));
		budget = ChargeWeapons(budget);
		budget = Shields.RechargeTick(budget);

		EnergyPool = unchecked((short)(budget + EnergyPoolReserve));
		EnergyPool = Math.Clamp(EnergyPool, (short)0, EnergyPoolMax);
	}

	/// <summary>
	/// <b>Not a ported mechanic — a test seam.</b> Empties the Master Energy Pool down to
	/// <see cref="EnergyPoolReserve"/>, the floor the tick holds back and the lowest a running
	/// machine settles at. The host's debug panel is the only caller.
	///
	/// <para>Nothing in the engine spends the pool hard enough to reach the floor yet — energy
	/// weapons are a later milestone and the shields alone draw less than the reactor makes — so this
	/// is how the refill is watched: from 500 the pool climbs back at the reactor's rate, minus
	/// whatever the shields are still taking.</para>
	/// </summary>
	public void DrainEnergyPoolForTest() => EnergyPool = EnergyPoolReserve;

	/// <summary>
	/// The weapon mounts' claim on the pool — vtable slot 0 of the mount-manager object at
	/// <c>mech+0x202</c>, which is <c>FUN_004107e4</c> for every machine.
	///
	/// <para><b>Nothing is claimed yet</b>, because no weapon exists to claim it; weapon systems are
	/// their own milestone. The seam is here rather than inlined into <see cref="PowerTick"/> because
	/// the original's arbitration is already understood and this is where it goes:</para>
	///
	/// <list type="bullet">
	/// <item>Mounts are served one at a time, highest priority first. Priority is the mount's own
	/// <c>+0x7b</c>, except that a mount already mid-charge (<c>+0x43</c>) reports 10000 and jumps the
	/// queue — so a weapon that started charging finishes charging.</item>
	/// <item>The player's currently selected mount is served before the ranking is consulted at all.
	/// The AI passes -1 for that and goes straight to the ranking.</item>
	/// <item>Each mount takes <c>min(its charge rate, what is left, its own deficit)</c> and passes
	/// the remainder down the chain (<c>FUN_0040f00c</c>).</item>
	/// <item>Once any mount reports itself mid-charge, every mount after it is told to target zero
	/// instead, and <i>bleeds its capacitor back into the pool</i> at 5 units a tick. One energy
	/// weapon charges at a time and the rest give way to it.</item>
	/// <item>Ammunition mounts consume nothing: their slot-0x34 override returns the budget
	/// untouched, which is why a HERC full of autocannons never troubles its reactor.</item>
	/// <item>PLAS (catalog id 25) is special-cased to half efficiency — its deficit counts double and
	/// only half of what it draws reaches its capacitor.</item>
	/// </list>
	/// </summary>
	private short ChargeWeapons(short budget) => budget;
}
