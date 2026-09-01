namespace Herculan.Engine.Sim;

/// <summary>
/// The five non-firing "pods" a HERC can carry, resolved out of its hardpoint fit.
///
/// <para>DBSIM does not model these as a separate equipment category: a pod is an ordinary weapon
/// mount, built from an ordinary hardpoint by the same factory every gun goes through, and it
/// occupies a hardpoint like any other. What sets them apart is one pass at the end of
/// <c>Mech_ConfigureLoadout</c> (<c>004175dc</c>): <c>FUN_0040fb2c</c> walks the finished mount list
/// and files five specific weapon ids into a five-pointer array at <c>mech+0x307</c>, one slot per
/// id. Everything that asks "does this machine have a Shield Pod" is really asking whether that
/// slot is non-null.</para>
///
/// <para>The slot/id table, why the ids are the shell catalog's, why slot order is not id order, and
/// the shared pod damage curve are in docs/simulation/reactor-energy-pool.md, "Equipment pods". The
/// constants below are the ids that document names; the last mount in hardpoint order wins a slot,
/// which is reproduced here.</para>
/// </summary>
/// <param name="Ecm">Slot 0, <c>mech+0x307</c> — the ECM pod (catalog id 18).</param>
/// <param name="Targeting">Slot 1, <c>mech+0x30b</c> — the targeting computer (id 29).</param>
/// <param name="ShieldPod">
/// Slot 2, <c>mech+0x30f</c> — the Shield Pod (id 30). Read by <c>FUN_00417bec</c>, which adds its
/// bonus to shield capacity; see <see cref="ShieldCharge"/>.
/// </param>
/// <param name="EnergyPod">
/// Slot 3, <c>mech+0x313</c> — the Energy Pod (id 32). Read by <c>FUN_00417d08</c>, which adds its
/// bonus to reactor output; see <see cref="MechObject.ReactorOutputRate"/>.
/// </param>
/// <param name="TurboPod">Slot 4, <c>mech+0x317</c> — the Turbo Pod (id 31), read by the locomotion tick.</param>
public readonly record struct MechPods(
	bool Ecm,
	bool Targeting,
	bool ShieldPod,
	bool EnergyPod,
	bool TurboPod) {

	/// <summary>Catalog id 18 — <c>ECM</c>, slot 0.</summary>
	public const int EcmWeaponId = 0x12;

	/// <summary>Catalog id 29 — <c>TARG</c>, slot 1.</summary>
	public const int TargetingWeaponId = 0x1d;

	/// <summary>Catalog id 30 — <c>SHLD</c>, slot 2.</summary>
	public const int ShieldPodWeaponId = 0x1e;

	/// <summary>Catalog id 31 — <c>TURB</c>, slot 4.</summary>
	public const int TurboPodWeaponId = 0x1f;

	/// <summary>Catalog id 32 — <c>ENRG</c>, slot 3.</summary>
	public const int EnergyPodWeaponId = 0x20;

	/// <summary>A machine carrying no pods at all.</summary>
	public static MechPods None => default;

	/// <summary>
	/// <c>FUN_0040fb2c</c>'s pass over the finished mount list. It runs against the mounts, not the
	/// raw fit: a weapon id sitting in a fit slot no hardpoint addresses builds no mount and so fits
	/// no pod.
	/// </summary>
	public static MechPods FromLoadout(WeaponMounts mounts) {
		bool ecm = false, targeting = false, shieldPod = false, energyPod = false, turboPod = false;

		foreach (var mount in mounts.Mounts) {
			switch (mount.WeaponId) {
				case EcmWeaponId: ecm = true; break;
				case TargetingWeaponId: targeting = true; break;
				case ShieldPodWeaponId: shieldPod = true; break;
				case TurboPodWeaponId: turboPod = true; break;
				case EnergyPodWeaponId: energyPod = true; break;
			}
		}

		return new MechPods(ecm, targeting, shieldPod, energyPod, turboPod);
	}
}
