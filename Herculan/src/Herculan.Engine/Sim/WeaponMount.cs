using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.Struct;
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
	/// rather than a full one: 960 out of 1200.
	///
	/// <para>What fills the last fifth is the <b>power-level keys</b> — see
	/// <see cref="AdjustPower"/>. <c>WeaponMount_DemandFullCharge</c> (<c>0040f4f0</c>) does the same
	/// thing in one step and was the obvious candidate, but its only caller (<c>FUN_00410d50</c>,
	/// "raise the armed mount to full and clear everyone else's mid-charge flag") has no reference of
	/// any kind anywhere in the image — neither a call nor a stored address — so nothing in the
	/// retail build ever reaches it.</para>
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

	/// <summary>
	/// The step one press of the manual's power-level keys moves an energy mount's charge target —
	/// <c>FUN_0040f48c</c>'s literal <c>0x50</c>, clamped to 0..<see cref="EnergyChargeScale"/>.
	/// </summary>
	public const short EnergyPowerStep = 0x50;

	/// <summary>
	/// The fixed-point scale on <see cref="RefireDelay"/> — <c>+0x63</c>, which the base mount
	/// constructor (<c>FUN_0040df30</c>) writes <c>0x400</c> into and nothing traced ever changes. A
	/// Q10 unit, so as things stand the delay a mount arms is the template's own figure exactly. It is
	/// modelled rather than folded away because it is a per-mount field, not a constant.
	/// </summary>
	public const short RefireScaleFull = 0x400;

	private readonly Weapons.WeaponMountTemplate? _template;
	private readonly GunLayout.HardpointEntry _hardpoint;
	private short _refireTimer;

	internal WeaponMount(int mountIndex, GunLayout.HardpointEntry hardpoint, int weaponId,
			short secondaryKey, WeaponCatalog catalog) {
		_hardpoint = hardpoint;
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
	/// their left and right mirror hardpoints with +1/-1. It is what pairs two mounts into one trigger
	/// pull — see <see cref="WeaponMounts.PartnerOf"/> and <see cref="WeaponMounts.FireTick"/>.
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
	/// <c>+0x31</c>, the refire countdown. Zero means the mount is out of its delay. A shot arms it
	/// with <see cref="RefireDelay"/> and the mount's own turn at the pool counts it down by the
	/// timestep, so it is the same clock everything else in the simulation runs on.
	/// </summary>
	public short RefireTimer => _refireTimer;

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
	/// <b>The weapon's range, in world units</b> — the template's int32 at <c>0x30</c>, which
	/// <c>WeaponMount_FireDispatch_GunBeam</c> hands straight to <c>Bullet_FireBurst</c> as the ray's
	/// length. That call is what settles the field: it was previously known only as the value
	/// <c>FUN_004110ac</c> requires to be positive before it will put a hardpoint into a fire chain,
	/// and was left undecoded because the manual's own 20 m figure for the ELF did not fit it.
	///
	/// <para>It does not fit that figure now either — ELF reads 20000 units, which is 120 m at the
	/// simulation's own scale — but the manual is not what identifies a field, and the fire path is.
	/// Retail values run 75000 (ATC20, 450 m) down to 15000 (ELF2, 90 m), descending with calibre
	/// across each family.</para>
	///
	/// <para>Zero for every pod, which is what still makes the chain gate work: a hardpoint with no
	/// range is not a weapon.</para>
	/// </summary>
	public int Range =>
		_template?.Tail is { Length: >= 0x12 } tail ? BitConverter.ToInt32(tail, 0x0e) : 0;

	/// <summary>
	/// What one shot takes out of the capacitor — the same template field at <c>0x38</c> that is the
	/// upper half of <see cref="ChargeThreshold"/>'s pair, read again by the beam dispatch as
	/// <c>min(cost, charge)</c>.
	///
	/// <para>The two shapes of that pair are two kinds of weapon. A laser reads the same number twice
	/// (LAS100 80/80): it fires at a fixed cost the moment it holds that much, so its shots are all
	/// identical. <c>PBEAM</c>, <c>EMP</c> and <c>PLAS</c> read a small low and a 10000 high (300 /
	/// 10000): the threshold is then whatever the mount is charging to, and the cost is the whole
	/// capacitor — a charge-up weapon whose shot is worth as much as the pilot let it accumulate. The
	/// manual's "power level" is that charge target, and the keys below are what move it.</para>
	/// </summary>
	public short ShotCost =>
		_template?.Tail is { Length: >= 0x18 } tail ? BitConverter.ToInt16(tail, 0x16) : (short)0;

	/// <summary>
	/// The refire delay a shot arms, in the same timer units <see cref="RefireTimer"/> counts down in
	/// — the template's <c>0x4c</c>, scaled by <see cref="RefireScaleFull"/>.
	///
	/// <para>At the simulation's 81-per-tick countdown, the retail 1200 that most weapons carry is
	/// about 15 ticks, or 0.6 s. <c>ELF</c> and <c>ELF2</c> carry <b>zero</b>, so they never have a
	/// delay at all — a continuous beam, held down and firing every tick the capacitor allows.</para>
	/// </summary>
	public short RefireDelay =>
		_template?.Tail is { Length: >= 0x2c } tail
			? (short)SimMath.Q10Multiply(RefireScaleFull, BitConverter.ToInt16(tail, 0x2a))
			: (short)0;

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
		if (Disabled) {
			return budget;
		}

		// FUN_0040ef94 — the refire countdown. It is the whole of an ammunition mount's turn at the
		// pool (that function *is* its vtable slot 0x34) and the first thing the energy class's own
		// slot does, so a mount's cooldown runs on the same pass that charges it and a destroyed
		// mount's does not run at all. Two muzzle-flash flag blocks the same function shuffles are
		// visual and are not modelled.
		if (Kind is WeaponMountKind.Energy or WeaponMountKind.Ammunition) {
			SimMath.CountdownTimerTick(ref _refireTimer);
		}

		if (Kind != WeaponMountKind.Energy) {
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

	/// <summary>
	/// Vtable slot <c>0x38</c>, <c>FUN_0040f48c</c> — the manual's power-level control, on
	/// <c>[-]</c>/<c>[=]</c> and the numeric keypad's <c>[-]</c>/<c>[+]</c>. Moves this mount's charge
	/// target by <see cref="EnergyPowerStep"/>, clamped to zero and <see cref="EnergyChargeScale"/>.
	/// Only the energy class implements it; the other two have a no-op in that slot.
	///
	/// <para>What it changes depends on which shape the weapon's threshold pair has — see
	/// <see cref="ShotCost"/>. A laser is unaffected in everything but its bar: its threshold and its
	/// cost are both fixed. A charge-up weapon's target <i>is</i> its shot strength, and turning it
	/// down is what makes one fire sooner and hit softer.</para>
	/// </summary>
	/// <param name="raise">True for the two "up" keys.</param>
	internal void AdjustPower(bool raise) {
		if (Kind != WeaponMountKind.Energy) {
			return;
		}

		ChargeTarget += raise ? EnergyPowerStep : (short)-EnergyPowerStep;
		ChargeTarget = Math.Clamp(ChargeTarget, (short)0, EnergyChargeScale);
	}

	/// <summary>
	/// Vtable slot <c>0x28</c>, the fire dispatch — <c>WeaponMount_FireDispatch_GunBeam</c>
	/// (<c>0040ea58</c>) for the energy class and <c>WeaponMount_FireDispatch_Missile</c>
	/// (<c>0040e964</c>) for the ammunition one. Both open with the same prologue
	/// (<c>FUN_0040e788</c>), which works out where the muzzle is and arms the refire delay, and then
	/// branch on the resolved <c>PROJ.DAT</c> record's own type.
	///
	/// <para><b>Only the beam branch is ported.</b> It spends <c>min(cost, charge)</c> out of the
	/// capacitor and resolves its hit synchronously, and that is the whole of it: every real
	/// <c>Beam</c> record carries <c>Speed == 0</c> and there is no travelling object to simulate.
	/// Every other branch — the gun dispatch's bullet, the ammunition dispatch's rocket and its own
	/// bullet fallback — builds a real object with flight time, and needs the projectile lifecycle
	/// that does not exist yet. So does the round the ammunition dispatch spends, which is why that
	/// is not taken either: a magazine that empties with nothing leaving the barrel would be worse
	/// than one that does not move.</para>
	///
	/// <para>The prologue runs regardless, as it does in the original, so an unported weapon still
	/// pays its refire delay — it fires blanks rather than free-running, and the fire chain advances
	/// past it exactly as it will once the shot is real.</para>
	/// </summary>
	internal void Fire(MechObject owner, SimWorld world) {
		var muzzle = PrepareShot(owner);

		if (Kind != WeaponMountKind.Energy
			|| Projectile is not { } projectile || projectile.Type != ProjectileType.Beam) {
			return;
		}

		// The cost is capped at what the capacitor actually holds, so a mount that somehow fires
		// under-charged fires a weaker shot rather than going negative. For a laser the two are the
		// same number every time; for a charge-up weapon the cost is larger than the capacitor can
		// ever hold, which is what makes the shot worth the whole of it.
		short power = Math.Min(ShotCost, (short)Charge);
		Charge -= power;
		world.FireBeam(new WeaponShot(muzzle, Range, projectile, power, owner));
	}

	/// <summary>
	/// <c>FUN_0040e788</c>, the shared fire prologue — where the shot comes from, which way it points,
	/// and the refire delay it costs.
	///
	/// <para>The frame is the firing hardpoint's own model bone, posed as it stands this tick and
	/// composed with the machine's world transform, so <b>a beam follows the torso because the gun
	/// bone does</b>: nothing here adds the twist or the pitch angle, and nothing needs to. The
	/// original also composes a per-hardpoint aim rotation over the top of it, but both angles are
	/// resolved from <c>.GL</c> fields that read -1 on every retail chassis, so that rotation is the
	/// identity throughout the retail fleet and is not modelled.</para>
	///
	/// <para>The muzzle point itself is three offsets summed in the bone's own space: the weapon
	/// template's, the hardpoint's, and a side offset the template holds separately and the hardpoint
	/// picks the sign of — see <see cref="MuzzleOffset"/>.</para>
	/// </summary>
	/// <returns>The shot's frame: the bone's world orientation, with the muzzle's world position in the translation.</returns>
	private Transform3 PrepareShot(MechObject owner) {
		var bone = owner.PartTransform(_hardpoint.BoneId);
		var offset = MuzzleOffset;
		var muzzle = bone.TransformPoint(offset.X, offset.Y, offset.Z);

		var shot = bone;
		shot.X = muzzle.X;
		shot.Y = muzzle.Y;
		shot.Z = muzzle.Z;

		_refireTimer = RefireDelay;
		return shot;
	}

	/// <summary>
	/// Where the muzzle sits in its bone's space — the template's own triple at <c>0x40</c>, the
	/// hardpoint's at <c>+0x10</c>, and <c>FUN_0040f904</c>'s side offset on top.
	///
	/// <para>That last one is what makes a mirrored pair of hardpoints fire from mirrored points off
	/// one template. The template carries a lateral figure at <c>0x46</c> and a vertical one at
	/// <c>0x4a</c>, and the hardpoint's own mounting code — the <c>.GL</c> byte at <c>+6</c>, which
	/// reads on top / underneath / left / right / invisible — selects one of them and its sign. Only
	/// one axis is ever used: a top or bottom mount takes the vertical figure and no lateral one, a
	/// side mount takes the lateral figure and no vertical one, and an invisible mount takes
	/// neither.</para>
	/// </summary>
	private Vec3i MuzzleOffset {
		get {
			int lateral = 0;
			int vertical = 0;

			if (_template?.Tail is { Length: >= 0x2a } tail) {
				switch (_hardpoint.AngleDirOption) {
					case 0:
						vertical = BitConverter.ToInt16(tail, 0x28);
						break;
					case 1:
						vertical = -BitConverter.ToInt16(tail, 0x28);
						break;
					case 2:
						lateral = -BitConverter.ToInt16(tail, 0x24);
						break;
					case 3:
						lateral = BitConverter.ToInt16(tail, 0x24);
						break;
				}

				return new Vec3i(
					BitConverter.ToInt16(tail, 0x1e) + _hardpoint.Offset[0] + lateral,
					BitConverter.ToInt16(tail, 0x20) + _hardpoint.Offset[1],
					BitConverter.ToInt16(tail, 0x22) + _hardpoint.Offset[2] + vertical);
			}

			return new Vec3i(_hardpoint.Offset[0], _hardpoint.Offset[1], _hardpoint.Offset[2]);
		}
	}
}
