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
/// <para><b>The ids are the shell catalog's own.</b> The switch keys on the mount template's
/// <c>+0x56</c>, and <c>Weapons_LoadResourceTables</c> (<c>0040fc8c</c>) writes that field as the
/// record's own table index while loading <c>dat\WEAPONS.DAT</c> — so it is the same 0-32 weapon id
/// <c>SHELL0.VOL</c>'s <c>gam\WEAPONS.DAT</c> catalog and <c>player.mec</c>'s hardpoint list use.
/// Read against the retail catalog the five slots come out as ECM, TARG, SHLD, ENRG and TURB, which
/// is exactly the manual's pod list.</para>
///
/// <para><b>Slot order is not id order.</b> <c>FUN_0040fb2c</c>'s cases assign
/// <c>0x12→[0] 0x1d→[1] 0x1e→[2] 0x1f→[4] 0x20→[3]</c>, so ENRG and TURB are crossed relative to
/// their ids. The array indices are what the consumers use, and they are what the field names here
/// stand for; the crossing matters only if a future reader tries to derive one from the other.</para>
///
/// <para>The switch <i>assigns</i> rather than accumulates, so fitting the same pod twice fills one
/// slot and the second copy contributes nothing — the last mount in hardpoint order wins. That is
/// reproduced below.</para>
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

	/// <summary><c>FUN_0040fb2c</c>'s pass over the mount list, run against a fit's weapon ids.</summary>
	public static MechPods FromLoadout(MechLoadout loadout) {
		bool ecm = false, targeting = false, shieldPod = false, energyPod = false, turboPod = false;

		foreach (int id in loadout.WeaponIds) {
			switch (id) {
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
