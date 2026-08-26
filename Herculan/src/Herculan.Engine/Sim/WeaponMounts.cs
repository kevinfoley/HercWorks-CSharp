using HercWorks.Core.Data.File.Dbsim;

namespace Herculan.Engine.Sim;

/// <summary>
/// A machine's weapon-mount manager, <c>mech+0x202</c> — the object
/// <c>MechLoadout_ConstructWeaponMounts</c> (<c>0040fff8</c>) builds and <c>FUN_004104ec</c> extends
/// for a locally-piloted machine. It owns the mounts, which one is armed, the three fire groups, and
/// the mounts' claim on the Master Energy Pool.
///
/// <para>The two classes in the original differ only in what a remote machine does not need: the
/// remote manager (<c>00499338</c>) is the bare mount array, the local one (<c>00499238</c>) adds
/// the selection and the groups. Both are modelled here as one type — a machine with no player in it
/// simply never has its selection read.</para>
/// </summary>
public sealed class WeaponMounts {
	/// <summary>How many fire groups the chain button cycles through — <c>FUN_004104ec</c> allocates exactly three.</summary>
	public const int GroupCount = 3;

	/// <summary><see cref="Selected"/> when the machine has nothing armed — the constructor's <c>0xff</c>.</summary>
	public const int NoSelection = -1;

	private readonly WeaponMount?[] _slots;
	private readonly bool[][] _groups;

	private WeaponMounts(WeaponMount?[] slots) {
		_slots = slots;
		_groups = new bool[GroupCount][];
		for (int group = 0; group < GroupCount; group++) {
			_groups[group] = new bool[slots.Length];
		}
	}

	/// <summary>A machine with no hardpoint list, no fit, or no readable weapon tables.</summary>
	public static WeaponMounts Empty { get; } = new(Array.Empty<WeaponMount?>());

	/// <summary>
	/// Every hardpoint the chassis has, in <c>.GL</c> file order, with an empty one left as a null
	/// hole rather than closed up. The holes matter: the armed-weapon index, the fire-group arrays
	/// and <see cref="WeaponMount.LinkPartnerOffset"/> are all positions in this array, so compacting
	/// it would silently renumber them.
	/// </summary>
	public IReadOnlyList<WeaponMount?> Slots => _slots;

	/// <summary>The fitted mounts, in <c>.GL</c> hardpoint order — see <see cref="WeaponMount.MountIndex"/>.</summary>
	public IEnumerable<WeaponMount> Mounts => _slots.Where(m => m != null)!;

	/// <summary>
	/// The armed mount's index, or <see cref="NoSelection"/>. <c>FUN_004104ec</c> starts it at the
	/// first mount that is not a pod, so a machine powers up with its first real weapon selected —
	/// which is a hardpoint-order first, not a cockpit-row first.
	/// </summary>
	public int Selected { get; private set; } = NoSelection;

	/// <summary>Which fire group the chain button has selected — <c>manager+0x1c</c>, zero at power-up.</summary>
	public int Group { get; private set; }

	/// <summary>
	/// <c>manager+0x18</c>. Set when the player armed a weapon by hand, cleared when the selection was
	/// stepped or advanced by the chain. While it is set, <see cref="PerFrameUpdate"/> leaves the
	/// selection alone however unready that weapon is — the manual's "select a weapon to single-fire";
	/// the original clears it again on the shot, and the chain resumes.
	/// </summary>
	public bool SingleFire { get; private set; }

	/// <summary>
	/// <c>manager+0x14</c>, the TRACK button's latch — automatic turret tracking. The console button
	/// sets and clears it (<c>FUN_00410f04</c> and <c>FUN_00410b40</c>'s own else branch), which is
	/// why TRACK is the one console button that stays lit. <b>Nothing acts on it yet:</b> turret
	/// tracking is unported, so this is the switch without the machinery behind it.
	/// </summary>
	public bool AutoTrack { get; set; }

	/// <summary>
	/// Whether cockpit weapon row <paramref name="mountIndex"/> draws as armed. The armed mount does,
	/// and so does the other half of a linked pair when its partner is the armed one — which is what
	/// makes linking visible: both rows light together. <c>FUN_00410b40</c> computes exactly this and
	/// pushes it to each row's gauge.
	/// </summary>
	public bool IsArmedRow(int mountIndex) {
		if (mountIndex == Selected) {
			return true;
		}

		return _slots.ElementAtOrDefault(mountIndex) is { Linked: true } mount
			&& mountIndex + mount.LinkPartnerOffset == Selected;
	}

	/// <summary>
	/// The mount this one is link-fired with, or null when it is unlinked or its partner slot is
	/// empty. The pairing itself is the chassis': <see cref="WeaponMount.LinkPartnerOffset"/> is the
	/// <c>.GL</c> record's own signed neighbour offset.
	/// </summary>
	public WeaponMount? PartnerOf(WeaponMount mount) {
		int partner = mount.MountIndex + mount.LinkPartnerOffset;
		return mount.LinkPartnerOffset != 0 && partner >= 0 && partner < _slots.Length
			? _slots[partner]
			: null;
	}

	/// <summary>
	/// Whether the currently armed mount could be linked at all — what the LINK button and <c>[L]</c>
	/// need before they do anything. <c>FUN_00410f14</c>'s own two conditions: the armed mount has a
	/// partner hardpoint, and that hardpoint carries the same weapon. The manual states the same rule
	/// from the other side: "any two identical weapons mounted symmetrically on the HERC".
	/// </summary>
	public bool CanLink =>
		_slots.ElementAtOrDefault(Selected) is { } armed
		&& PartnerOf(armed) is { } partner
		&& partner.WeaponId == armed.WeaponId;

	/// <summary>Whether <paramref name="mountIndex"/> is in the currently selected fire group.</summary>
	public bool InCurrentGroup(int mountIndex) =>
		Group >= 0 && Group < _groups.Length && mountIndex >= 0 && mountIndex < _groups[Group].Length
			&& _groups[Group][mountIndex];

	/// <summary>The mount that owns cockpit weapon row <paramref name="gaugeSlot"/>, or null when none does.</summary>
	public WeaponMount? BySlot(int gaugeSlot) => Mounts.FirstOrDefault(m => m.GaugeSlot == gaugeSlot);

	/// <summary>
	/// <c>MechLoadout_ConstructWeaponMounts</c> followed by <c>FUN_004104ec</c>: walk the chassis'
	/// hardpoint list in file order, resolve each record's fit slot into a weapon id and a mount, and
	/// then work out the fire groups and the initial selection.
	///
	/// <para>The group pass is simple and worth stating plainly, because the cockpit's state boxes
	/// depend on it: <b>every non-pod mount goes into group I and nothing goes into II or III.</b>
	/// The constructor writes <c>group == wanted</c> into all three arrays with <c>wanted</c> fixed
	/// at 0 for a weapon and -1 for a pod, so the other two groups start empty and only the chain
	/// controls ever put anything in them.</para>
	/// </summary>
	/// <param name="hardpoints">The chassis' own <c>gl\&lt;HERC&gt;.GL</c>, or null if it has none.</param>
	/// <param name="loadout">The fit, with its two parallel arrays addressed by hardpoint slot.</param>
	/// <param name="catalog">The weapon tables, or null when they could not be read.</param>
	public static WeaponMounts Build(GunLayout? hardpoints, MechLoadout loadout, WeaponCatalog? catalog) {
		if (hardpoints?.Hardpoints is not { } records || catalog == null) {
			return Empty;
		}

		int total = Math.Min(hardpoints.TotalGuns, records.Length);
		var manager = new WeaponMounts(new WeaponMount?[total]);

		for (int i = 0; i < total; i++) {
			var record = records[i];

			// A negative id is clamped to zero and a zero id builds nothing, so the slot stays a hole
			// in the array rather than shifting everything after it up.
			int weaponId = Math.Max(loadout.WeaponAt(record.HardpointId), 0);
			if (weaponId == WeaponMount.EmptyWeaponId
				|| WeaponCatalog.Kind(weaponId) == WeaponMountKind.None) {
				continue;
			}

			manager._slots[i] = new WeaponMount(
				i, record, weaponId, loadout.SecondaryAt(record.HardpointId), catalog);
		}

		foreach (var mount in manager.Mounts) {
			if (mount.Kind == WeaponMountKind.Pod) {
				continue;
			}

			manager._groups[0][mount.MountIndex] = true;
			if (manager.Selected == NoSelection) {
				manager.Selected = mount.MountIndex;
			}
		}

		return manager;
	}

	/// <summary>
	/// Arm the mount that owns cockpit weapon row <paramref name="gaugeSlot"/> — what a left click on
	/// that row and the matching number key both do. The original reaches this by two different
	/// routes that meet in <c>FUN_004106ac</c>: a click goes through the row gadget
	/// (<c>FUN_00440ef0</c>/<c>FUN_004414b4</c> → <c>FUN_00432a50</c>), and a number key indexes the
	/// cockpit's own ten-gauge array at <c>CockpitViewInstance+0x70</c> and presses that gauge's
	/// select gadget.
	/// </summary>
	/// <returns>Whether anything was armed.</returns>
	public bool SelectBySlot(int gaugeSlot) =>
		BySlot(gaugeSlot) is { } mount && Select(mount.MountIndex);

	/// <summary>
	/// <c>FUN_004106ac</c>: arm one mount. Refused for a mount that is not
	/// <see cref="WeaponMount.Selectable"/> — a pod, or a weapon out of ammunition — which is why
	/// clicking a pod's row does nothing. A successful arm sets <see cref="SingleFire"/>.
	/// </summary>
	public bool Select(int mountIndex) {
		if (_slots.ElementAtOrDefault(mountIndex) is not { Selectable: true }) {
			return false;
		}

		SetSelected(mountIndex);
		SingleFire = true;
		return true;
	}

	/// <summary>
	/// <c>FUN_00410708</c>: the one place <see cref="Selected"/> is written.
	///
	/// <para>It normalises a linked pair to its first half — arming the right-hand weapon of a linked
	/// pair arms the left-hand one instead (the partner offset is negative on the second half). That
	/// is what keeps a pair's two rows agreeing about which of them is the armed one.</para>
	/// </summary>
	private void SetSelected(int mountIndex) {
		if (mountIndex == Selected) {
			return;
		}

		if (_slots.ElementAtOrDefault(mountIndex) is { Linked: true } mount && mount.LinkPartnerOffset < 0) {
			mountIndex += mount.LinkPartnerOffset;
		}

		Selected = mountIndex;
	}

	/// <summary>
	/// <c>FUN_0041074c</c>: step the armed mount one place through the current fire chain —
	/// <c>[W]</c> forward, <c>[Alt]+[W]</c> back. Wraps, and skips anything that is not selectable,
	/// not in the current chain, or the second half of a linked pair (a pair is armed by its first
	/// half only). Stepping clears <see cref="SingleFire"/>: the chain has the selection again.
	/// </summary>
	public void CycleSelection(int direction) {
		StepSelection(direction);

		// The original clears the flag at the key handler rather than inside the step, which is why
		// the two internal callers below — the chain switch and the per-frame advance — leave it
		// alone. Only the player stepping the weapon by hand gives the chain the selection back.
		SingleFire = false;
	}

	private void StepSelection(int direction) {
		if (Selected < 0 || _slots.Length == 0) {
			return;
		}

		int index = Selected;
		bool found = false;
		do {
			index += direction;
			if (index < 0) {
				index = _slots.Length - 1;
			} else if (index >= _slots.Length) {
				index = 0;
			}

			if (_slots[index] is { Selectable: true } mount
				&& (!mount.Linked || mount.LinkPartnerOffset > 0)) {
				found = InCurrentGroup(index);
			}
		} while (!found && index != Selected);

		SetSelected(index);
	}

	/// <summary>
	/// <c>FUN_004110ac</c> with <c>FUN_00410cd0</c>: add or remove the mount on cockpit weapon row
	/// <paramref name="gaugeSlot"/> from the <i>current</i> fire chain — a right click on the row, or
	/// <c>[Alt]</c> and its number key. It toggles membership rather than setting it, and it does not
	/// arm anything.
	///
	/// <para>A hardpoint whose weapon has no range is skipped outright, so the pods on the panel
	/// cannot be chained — see <see cref="WeaponMount.Range"/>.</para>
	/// </summary>
	/// <returns>Whether a mount was found to toggle.</returns>
	public bool ToggleChain(int gaugeSlot) {
		var mount = Mounts.FirstOrDefault(m => m.GaugeSlot == gaugeSlot && m.Range > 0);
		if (mount == null || Group < 0 || Group >= _groups.Length) {
			return false;
		}

		_groups[Group][mount.MountIndex] = !_groups[Group][mount.MountIndex];

		// The original follows the toggle by asking the mount for its arbitration priority and, when
		// that is zero, calling its "wake up" slot. Only an energy mount implements either, and only
		// one whose charge target had been zeroed is changed by it.
		if (mount.EnergyPriority == 0) {
			mount.WakeCapacitor();
		}

		return true;
	}

	/// <summary>
	/// <c>FUN_00410f14</c>: toggle link fire on the armed mount and its partner — the LINK button and
	/// <c>[L]</c>. Both halves flip together and only when <see cref="CanLink"/> holds, so a weapon
	/// with no symmetric twin, or one whose opposite hardpoint carries something else, simply cannot
	/// be linked.
	///
	/// <para>In the original one press runs this three times — once from the button's own click
	/// handler, once from the manager's next per-frame pass reading the button's latch byte, and once
	/// more as that pass writes the byte back and the widget notices it changed. Three flips of one
	/// bit is one flip, which is why it works; the net effect is what is reproduced here.</para>
	/// </summary>
	/// <returns>Whether the link state changed.</returns>
	public bool ToggleLink() {
		if (_slots.ElementAtOrDefault(Selected) is not { } armed
			|| PartnerOf(armed) is not { } partner
			|| partner.WeaponId != armed.WeaponId) {
			return false;
		}

		armed.Linked = !armed.Linked;
		partner.Linked = !partner.Linked;
		return true;
	}

	/// <summary>
	/// <c>FUN_00410ae4</c>: switch the chain the console's chain button names. Switching to a chain
	/// the armed weapon is not in steps the selection to one that is.
	/// </summary>
	public void SetGroup(int group) {
		if (group == Group || group < 0 || group >= _groups.Length) {
			return;
		}

		Group = group;
		if (Selected >= 0 && !InCurrentGroup(Selected)) {
			StepSelection(1);
		}
	}

	/// <summary>
	/// The manager's own per-frame pass, <c>FUN_00410b40</c> and <c>FUN_00410a3c</c>, minus the input
	/// block it reads (the host drives those directly) and the auto-fire it performs.
	///
	/// <list type="bullet">
	/// <item><b>The chain advances the armed weapon.</b> Unless <see cref="SingleFire"/> is set, a
	/// mount that could not fire hands the selection to the next one in the chain that could. With
	/// nothing firing yet every mount is ready, so in practice nothing moves — but this is the
	/// mechanism, and it is what makes a chain a chain.</item>
	/// <item><b>A destroyed mount breaks its link.</b> A linked pair whose half is destroyed or out
	/// of ammunition unlinks both halves and hands the selection to the partner.</item>
	/// </list>
	/// </summary>
	public void PerFrameUpdate() {
		foreach (var mount in Mounts.ToList()) {
			if (!mount.Linked || PartnerOf(mount) is not { } partner) {
				continue;
			}

			if (mount.Disabled || !mount.Selectable) {
				mount.Linked = false;
				partner.Linked = false;
				SetSelected(partner.MountIndex);
			}
		}

		if (Selected < 0 || SingleFire || CanFireNow(Selected)) {
			return;
		}

		int armed = Selected;
		for (int steps = 0; steps <= _slots.Length; steps++) {
			StepSelection(1);
			if (CanFireNow(Selected)) {
				return;
			}

			if (Selected == armed) {
				break;
			}
		}

		Selected = armed;
	}

	/// <summary>
	/// <c>FUN_00410970</c> reduced to what the engine models: the mount's own readiness, and for a
	/// linked pair, both halves'. The original also gates on the selected target's range and on the
	/// per-ammunition-type counters at <c>manager+0x0a</c>, neither of which is ported — see
	/// docs/simulation/weapon-mounts.md.
	/// </summary>
	private bool CanFireNow(int mountIndex) {
		if (_slots.ElementAtOrDefault(mountIndex) is not { } mount || !mount.CanFire) {
			return false;
		}

		return !mount.Linked || PartnerOf(mount) is not { } partner || partner.CanFire;
	}

	/// <summary>
	/// <c>FUN_00410dbc</c> — the manager's fire entry, and the whole of what pulling the trigger does.
	///
	/// <para>Its order is worth keeping: <b>both halves of a linked pair are tested before either
	/// fires</b>, so a pair whose second half is still charging does not fire its first half alone.
	/// The same goes for the trigger itself, which is asked of each mount separately (vtable
	/// <c>+0x30</c>) even though every mount answers from the same device byte.</para>
	///
	/// <para>Firing clears <see cref="SingleFire"/> the moment the armed mount is no longer ready,
	/// which is what "once you fire, the current firing chain will resume" means in the manual: a
	/// weapon armed by hand keeps the selection until its shot leaves it unready, and then the chain
	/// takes over again on the next <see cref="PerFrameUpdate"/>.</para>
	///
	/// <para>Two things in the original are not here. It passes the mounts a flag off a pair of
	/// globals which only the ammunition class reads, as its "this shot is free" gate; and it asks the
	/// armed mount for its ammunition type and raises an alert flag for type 3, which an energy mount
	/// can never report.</para>
	/// </summary>
	/// <param name="owner">The machine firing, which the shot's geometry and the raycast both need.</param>
	/// <param name="world">The world the shot is resolved against.</param>
	/// <param name="triggerHeld">The device's fire byte — see <see cref="MechControls.Fire"/>.</param>
	/// <returns>Whether anything fired.</returns>
	public bool FireTick(MechObject owner, SimWorld world, bool triggerHeld) {
		if (_slots.ElementAtOrDefault(Selected) is not { } armed) {
			return false;
		}

		var partner = armed.Linked ? PartnerOf(armed) : null;

		if (!armed.CanFire || (partner != null && !partner.CanFire)) {
			return false;
		}

		if (!triggerHeld) {
			return false;
		}

		armed.Fire(owner, world);
		partner?.Fire(owner, world);

		// The original re-tests the armed mount after each of the two shots, and the partner's shot
		// cannot change the armed mount's readiness, so the two tests are one.
		if (!armed.CanFire) {
			SingleFire = false;
		}

		return true;
	}

	/// <summary>
	/// <c>WeaponMounts_HandleCommand</c>'s <c>0x0c</c>/<c>0x0d</c>/<c>0x4a</c>/<c>0x4e</c> cases — the
	/// <c>[-]</c> and <c>[=]</c> keys and the keypad's <c>[-]</c> and <c>[+]</c>, which reach the armed
	/// mount's vtable <c>+0x38</c> with the "up" half of the pair as a flag. Only an energy mount has
	/// anything in that slot; see <see cref="WeaponMount.AdjustPower"/> for what it does.
	/// </summary>
	/// <returns>Whether a mount took the command.</returns>
	public bool AdjustPower(bool raise) {
		if (_slots.ElementAtOrDefault(Selected) is not { } armed) {
			return false;
		}

		armed.AdjustPower(raise);
		return true;
	}

	/// <summary>
	/// The mounts' claim on the Master Energy Pool — vtable slot 0 of the manager,
	/// <c>FUN_004107e4</c>, called from <c>Mech_PerTickSystemsUpdate</c> between the reactor's
	/// contribution and the shields'.
	///
	/// <para>Mounts are served one at a time, and the order is not the mount order:</para>
	/// <list type="number">
	/// <item>The armed mount goes first, before the ranking is consulted at all. An AI machine
	/// passes -1 for it and starts straight at the ranking.</item>
	/// <item>Every pass after that picks the highest-priority mount not yet served — priority being
	/// <see cref="WeaponMount.EnergyPriority"/>, which a mount mid-charge reports as 10000 so that it
	/// finishes before anything else starts.</item>
	/// <item>Once any mount reports itself mid-charge, every mount served after it this tick is told
	/// to target zero instead and bleeds its own capacitor back into the pool.</item>
	/// </list>
	///
	/// <para>The original also fires any mount that reports itself due to auto-fire in this same
	/// loop; that is a firing mechanic and is not here.</para>
	/// </summary>
	/// <param name="budget">The pool less its reserve.</param>
	/// <param name="selected">
	/// The armed mount to serve first, or <see cref="NoSelection"/> to go straight to the ranking.
	/// </param>
	/// <returns>What the mounts did not take.</returns>
	public short ChargeTick(short budget, int selected) {
		if (_slots.Length == 0) {
			return budget;
		}

		var served = new bool[_slots.Length];
		bool yieldToOther = false;

		for (int pass = 0; pass < _slots.Length; pass++) {
			WeaponMount? next;

			if (selected >= 0 && selected < served.Length && !served[selected]) {
				next = _slots[selected];
			} else {
				next = null;
				for (int i = 0; i < _slots.Length; i++) {
					if (_slots[i] is { } candidate && !served[i]
						&& (next == null || candidate.EnergyPriority > next.EnergyPriority)) {
						next = candidate;
					}
				}
			}

			if (next == null) {
				break;
			}

			served[next.MountIndex] = true;
			budget = next.ChargeTick(budget, yieldToOther);
			yieldToOther |= next.Charging;
		}

		return budget;
	}
}
