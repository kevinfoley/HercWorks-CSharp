using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dbsim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// One fitted hardpoint — DBSIM's weapon-mount object, built by
/// <c>MechLoadout_ConstructWeaponMounts</c> (<c>0040fff8</c>) from three things that have to be
/// joined: the machine type's own hardpoint list (<c>gl\&lt;HERC&gt;.GL</c>), the fit the mission
/// gave it (<c>player.mec</c> or <c>script.dat</c>), and the weapon id's template
/// (<see cref="WeaponCatalog"/>).
///
/// <para><b>The hardpoint list drives the join, not the fit.</b> The factory walks the <c>.GL</c>
/// records in file order and reads each one's byte at <c>+0x17</c> as an index into the fit's two
/// parallel arrays — so a machine's mounts are ordered by its own chassis, and the fit is addressed
/// through it rather than iterated. That is why the same <c>player.mec</c> entry produces a
/// different-looking weapon panel on two different HERCs, and why reading the fit array in order
/// gets the order wrong.</para>
///
/// <para><b>Fields are shared, not per-class.</b> <c>+0x7b</c> and <c>+0x7d</c> mean different
/// things in the two live classes: rounds for an ammunition mount, a charge target and a capacitor
/// level for an energy one. They are modelled here under the names each class gives them, with the
/// raw offsets noted, rather than as one abstract "level".</para>
/// </summary>
public sealed class WeaponMount {
	/// <summary>
	/// What an energy mount's capacitor holds at spawn, and the charge level it asks for while idle
	/// — <c>FUN_0040e074</c>'s <c>Q10Multiply(820, 1200)</c>, a literal pair that does not vary by
	/// weapon. Both <c>+0x7b</c> (the target) and <c>+0x7d</c> (the level) start here, so an energy
	/// weapon powers up already charged.
	/// </summary>
	public static readonly short EnergyCapacitorFull = (short)SimMath.Q10Multiply(0x334, EnergyChargeScale);

	/// <summary>
	/// The denominator the charge bar is drawn against — <c>FUN_0040f288</c> pushes
	/// <c>(charge &lt;&lt; 10) / 1200</c> to a widget whose LED bar has a range of 1024. It is not
	/// the capacitor's own capacity, which is why a fully charged weapon reads four-fifths of a bar
	/// rather than a full one: 960 out of 1200. Only a mount with a shot demanded of it raises its
	/// target to the full 1200 (<c>FUN_0040f4f0</c>).
	/// </summary>
	public const short EnergyChargeScale = 0x4b0;

	/// <summary>How much an energy mount draws per tick when it is the one being served — <c>+0x7f</c>, a flat 20.</summary>
	public const short EnergyChargeRate = 0x14;

	/// <summary>
	/// The charge level an idle energy mount asks for — <c>FUN_0040f4d8</c>'s literal <c>0x334</c>,
	/// the same 820 <see cref="EnergyCapacitorFull"/> is derived from. A mount with a shot demanded
	/// of it raises its target to <see cref="EnergyChargeScale"/> instead.
	/// </summary>
	public const short EnergyIdleTarget = 0x334;

	/// <summary>
	/// What a mount whose turn has passed bleeds back into the pool each tick, once some other mount
	/// has declared itself mid-charge — <c>FUN_0040f00c</c>'s floor of -5 on a negative deficit.
	/// </summary>
	public const short EnergyBleedBack = 5;

	/// <summary>
	/// Catalog id 25, <c>PLAS</c> — the one weapon <c>FUN_0040f00c</c> singles out by id. Its
	/// capacitor deficit counts double and only half of what it draws is stored, so it costs twice
	/// the pool for the same charge.
	/// </summary>
	public const int HalfEfficiencyWeaponId = 0x19;

	/// <summary>The catalog id of a mount the factory builds nothing for.</summary>
	public const int EmptyWeaponId = 0;

	private readonly Weapons.WeaponMountTemplate? _template;

	internal WeaponMount(int mountIndex, GunLayout.HardpointEntry hardpoint, int weaponId,
			short secondaryKey, WeaponCatalog catalog) {
		MountIndex = mountIndex;
		GaugeSlot = hardpoint.FireChainNumber;
		LoadoutSlot = hardpoint.HardpointId;
		LinkPartnerOffset = (sbyte)hardpoint.Unk7_val;
		WeaponId = weaponId;
		SecondaryKey = secondaryKey;
		Kind = WeaponCatalog.Kind(weaponId);
		Name = catalog.MountName(weaponId, secondaryKey);
		Projectile = catalog.Projectile(weaponId, secondaryKey);
		_template = catalog.Template(weaponId);

		switch (Kind) {
			case WeaponMountKind.Ammunition:
				// FUN_0040e140: the magazine size comes off the template and the mount powers up
				// holding a full one. The level is kept in 256ths of a round; the gauge prints
				// level >> 8.
				ChargeTarget = MagazineSize;
				Charge = MagazineSize << 8;
				break;

			case WeaponMountKind.Energy:
				ChargeTarget = EnergyCapacitorFull;
				Charge = EnergyCapacitorFull;
				ChargeRate = EnergyChargeRate;
				break;
		}

		// FUN_0040df30 sets +0x4c on every mount it builds; the pod base constructor
		// (FUN_0040e234) immediately clears it again, which is one of the two independent reasons a
		// pod can never be armed.
		Selectable = Kind != WeaponMountKind.Pod;
	}

	/// <summary>
	/// This mount's index in the machine's mount array — its position in the <c>.GL</c> file. It is
	/// what the selected-weapon index, the fire-group arrays and <see cref="LinkPartnerOffset"/> are
	/// all relative to, and it is the order the Heads-Down Display's weapon list prints in.
	/// </summary>
	public int MountIndex { get; }

	/// <summary>
	/// Which cockpit weapon row this mount owns — the <c>.GL</c> record's own byte at <c>+7</c>,
	/// which the mount hands to the gauge factory as a <c>.GAU</c> weapon-slot index. Row <c>n</c>
	/// prints the digit <c>n+1</c>, so this is the panel's numbering minus one. It is a different
	/// order from <see cref="MountIndex"/>.
	/// </summary>
	public int GaugeSlot { get; }

	/// <summary>
	/// Which slot of the fit's arrays this hardpoint draws from — the <c>.GL</c> record's byte at
	/// <c>+0x17</c>.
	/// </summary>
	public int LoadoutSlot { get; }

	/// <summary>
	/// The <c>.GL</c> record's signed byte at <c>+0x16</c>: how far away in the mount array this
	/// hardpoint's link partner sits, or zero for a hardpoint that has none. Retail chassis pair
	/// their left and right mirror hardpoints with +1/-1. Nothing reads it yet — the LINK button is
	/// a firing feature — but it is what pairs two mounts into one trigger pull.
	/// </summary>
	public int LinkPartnerOffset { get; }

	/// <summary>The fit's catalog weapon id for this hardpoint.</summary>
	public int WeaponId { get; }

	/// <summary>
	/// The fit's parallel second value for this hardpoint — the ammunition type a launcher is loaded
	/// with. Retail data puts 5 in every slot that is not a launcher.
	/// </summary>
	public short SecondaryKey { get; }

	/// <summary>Which mount class the factory built.</summary>
	public WeaponMountKind Kind { get; }

	/// <summary>
	/// The name this mount's gauge prints. A launcher is named by its loaded ammunition, everything
	/// else by its weapon id — see <see cref="WeaponCatalog.MountName"/>.
	/// </summary>
	public string Name { get; }

	/// <summary>The <c>PROJ.DAT</c> record this mount fires, or null for a pod and for <c>ECM</c>.</summary>
	public ProjectileData.Projectile? Projectile { get; }

	/// <summary>
	/// The magazine size — the template's field at <c>+0x3a</c>, which <c>FUN_0040e140</c> reads as
	/// both the round count a mount starts with and the count it is capped at. Zero for anything that
	/// is not an ammunition mount.
	/// </summary>
	public short MagazineSize =>
		Kind == WeaponMountKind.Ammunition && _template?.Tail is { Length: >= 0x1a } tail
			? BitConverter.ToInt16(tail, 0x18)
			: (short)0;

	/// <summary>
	/// <c>+0x7b</c>. An ammunition mount keeps its remaining round count here; an energy mount keeps
	/// the charge level it is asking the pool for, which doubles as its priority in the arbitration.
	/// </summary>
	public short ChargeTarget { get; internal set; }

	/// <summary>
	/// <c>+0x7d</c>. An ammunition mount's rounds in 256ths; an energy mount's capacitor level in
	/// pool units.
	/// </summary>
	public int Charge { get; internal set; }

	/// <summary>
	/// <c>+0x7f</c>. How much an energy mount takes per tick when it is served; zero for the other
	/// classes, which take nothing.
	/// </summary>
	public short ChargeRate { get; internal set; }

	/// <summary>
	/// <c>+0x43</c>. Set while this mount is the one drawing on the pool. Every mount served after it
	/// this tick is told to target zero instead and gives its own charge back.
	/// </summary>
	public bool Charging { get; internal set; }

	/// <summary>
	/// <c>+0x49</c>. A destroyed mount: it charges nothing, fires nothing, and its cockpit row prints
	/// <c>OFFLINE</c> in place of the weapon's name. Nothing damages a mount yet.
	/// </summary>
	public bool Disabled { get; internal set; }

	/// <summary>
	/// <c>+0x31</c>, the refire countdown. Zero means the mount is out of its delay. Nothing arms it
	/// yet — firing is a later milestone — but the ready test reads it, so it is modelled rather than
	/// silently assumed zero.
	/// </summary>
	public short RefireTimer { get; internal set; }

	/// <summary>
	/// <c>+0x4c</c>. Whether this mount can be armed at all. Clear for a pod from construction, and
	/// cleared on an ammunition mount the moment its magazine runs out
	/// (<c>WeaponMount_FireDispatch_Missile</c>) — an empty weapon drops out of the selection cycle
	/// rather than staying armed. Nothing empties a magazine yet.
	/// </summary>
	public bool Selectable { get; internal set; }

	/// <summary>
	/// <c>+0x4b</c>. Whether this mount is link-fired with its <see cref="LinkPartnerOffset"/>
	/// partner. Both halves of a pair carry it, and it is always set and cleared as a pair — see
	/// <see cref="WeaponMounts.ToggleLink"/>.
	/// </summary>
	public bool Linked { get; internal set; }

	/// <summary>
	/// The template's int32 at <c>0x30</c>, which <c>FUN_004110ac</c> requires to be positive before
	/// it will put a hardpoint into a fire chain. Every real firing weapon carries a large positive
	/// value and every pod carries zero, so in practice this is the "is a weapon at all" gate; the
	/// field's own meaning is not decoded. Descending with calibre across the autocannon and laser
	/// families, which looks like a range but does not survive the manual's own figures for the ELF.
	/// </summary>
	public int TemplateGate =>
		_template?.Tail is { Length: >= 0x12 } tail ? BitConverter.ToInt32(tail, 0x0e) : 0;

	/// <summary>Whether the pool arbitration treats this mount as half-efficient — <c>PLAS</c> alone.</summary>
	public bool HalfEfficiency => WeaponId == HalfEfficiencyWeaponId;

	/// <summary>
	/// Vtable slot <c>0x3c</c>, <c>FUN_0040f4d8</c>: put an energy mount's charge target back to its
	/// idle level. Only the energy class implements it — the other two have a no-op in that slot.
	/// </summary>
	internal void WakeCapacitor() {
		if (Kind == WeaponMountKind.Energy && !Disabled) {
			ChargeTarget = EnergyIdleTarget;
		}
	}

	/// <summary>Rounds remaining, as the ammunition gauge prints them — <c>FUN_0040f330</c>'s <c>+0x7d &gt;&gt; 8</c>.</summary>
	public int Rounds => Charge >> 8;

	/// <summary>
	/// The charge bar's value, over the 0-1024 range its LED bar was built with —
	/// <c>FUN_0040f288</c>'s <c>(charge &lt;&lt; 10) / 1200</c>.
	/// </summary>
	public int ChargeMeterValue => (Charge << 10) / EnergyChargeScale;

	/// <summary>
	/// The priority this mount reports to the arbitration — <c>WeaponMount_GetEnergyPriority</c>
	/// (<c>0040f504</c>) for an energy mount, a flat zero for every other class
	/// (<c>FUN_004111e2</c>). A mount already mid-charge reports 10000 and so is always served first,
	/// which is how one weapon finishes charging before another starts.
	/// </summary>
	public short EnergyPriority => Kind switch {
		WeaponMountKind.Energy => Charging ? (short)10000 : ChargeTarget,
		_ => 0,
	};

	/// <summary>
	/// Whether the mount could fire right now — the per-class test at vtable slot <c>0x2c</c>.
	///
	/// <list type="bullet">
	/// <item><b>Ammunition</b> (<c>FUN_0040ed6c</c>): not destroyed, out of its refire delay, and
	/// holding at least one round.</item>
	/// <item><b>Energy</b> (<c>FUN_0040ecdc</c>): not destroyed, out of its refire delay, and charged
	/// to at least the threshold below.</item>
	/// <item><b>Pods</b> have no such method — they never fire and are never in a fire group.</item>
	/// </list>
	/// </summary>
	public bool CanFire => Kind switch {
		WeaponMountKind.Ammunition => !Disabled && RefireTimer == 0 && ChargeTarget != 0,
		WeaponMountKind.Energy => !Disabled && RefireTimer == 0 && ChargeThreshold <= Charge,
		_ => false,
	};

	/// <summary>
	/// How much charge an energy mount needs before it will fire — <c>FUN_0040ecdc</c>'s own
	/// arithmetic over the template's two fields at <c>+0x36</c> and <c>+0x38</c>. When the first is
	/// below the second the threshold is the larger of it and the mount's current target; otherwise
	/// the second is used outright. Real templates carry both shapes: <c>EMP</c> reads (350, 10000),
	/// <c>ELF</c> reads (400, 70).
	/// </summary>
	private short ChargeThreshold {
		get {
			if (_template?.Tail is not { Length: >= 0x18 } tail) {
				return 0;
			}

			short low = BitConverter.ToInt16(tail, 0x14);
			short high = BitConverter.ToInt16(tail, 0x16);
			return low < high ? Math.Max(low, ChargeTarget) : high;
		}
	}

	/// <summary>
	/// This mount's turn at the Master Energy Pool — vtable slot <c>0x34</c>. An ammunition mount's
	/// override (<c>FUN_0040ef94</c>) hands the budget straight back; an energy mount runs
	/// <c>WeaponMount_ChargeCapacitor</c> (<c>0040f00c</c>):
	///
	/// <list type="number">
	/// <item>The deficit is the mount's target (or zero, once another mount has claimed the tick)
	/// minus its current level, doubled for <c>PLAS</c>.</item>
	/// <item>A positive deficit takes <c>min(charge rate, budget, deficit)</c> — so a mount can be
	/// starved by an empty pool as easily as by its own rate.</item>
	/// <item>A deficit of zero or less clears the mid-charge flag and gives back up to
	/// <see cref="EnergyBleedBack"/> a tick, which is what "targeting zero" means: the capacitor
	/// drains into the pool for someone else to use.</item>
	/// <item>Half of what <c>PLAS</c> draws is thrown away rather than stored.</item>
	/// </list>
	/// </summary>
	/// <param name="budget">What is left of the pool this tick.</param>
	/// <param name="yieldToOther">Whether some earlier mount has already declared itself mid-charge.</param>
	/// <returns>The budget with this mount's draw removed — negative draws put charge back.</returns>
	internal short ChargeTick(short budget, bool yieldToOther) {
		if (Kind != WeaponMountKind.Energy || Disabled) {
			return budget;
		}

		short deficit = (short)((yieldToOther ? 0 : ChargeTarget) - Charge);
		if (HalfEfficiency) {
			deficit *= 2;
		}

		short draw;
		if (deficit < 1) {
			Charging = false;
			draw = Math.Max(deficit, (short)-EnergyBleedBack);
		} else {
			draw = Math.Min(Math.Min(ChargeRate, budget), deficit);
		}

		Charge += draw;
		if (HalfEfficiency) {
			Charge -= draw >> 1;
		}

		return (short)(budget - draw);
	}
}
