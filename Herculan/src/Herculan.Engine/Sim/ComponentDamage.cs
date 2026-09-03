using HercWorks.Core.Data.File.Dbsim;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// The per-component health model a mech or a flyer carries — the header of three arrays the
/// original allocates at <c>mech+0x206</c> (<c>Component_AllocDamageArrays</c>, <c>0040d2cc</c>) and
/// the four functions that read and write it. Structures have their own, much simpler one; see
/// <see cref="BaseObject.ApplyDamage"/>.
///
/// <para><b>Three arrays, and they are damage taken rather than health left</b> — every entry
/// starts at zero and climbs, and <c>-1</c> is the destroyed sentinel:</para>
/// <list type="bullet">
/// <item><see cref="Damage"/>, one entry per <i>main</i> component — 29 for a mech, 1 for a flyer,
/// both hard-coded at the constructor's call site rather than taken from the file.</item>
/// <item>A parallel <i>dependent sub-piece</i> array — 22 for a mech, 1 for a flyer. These are the
/// finer parts a main component's reading aggregates: servos, the sensor array, the shield
/// generator, the engine.</item>
/// <item>An <i>active</i> flag per main component, cleared when that component is destroyed. It is
/// what the hit test reads to stop offering a lost component as a target.</item>
/// </list>
///
/// <para>The maxima are not here: they are the <c>dmg\&lt;NAME&gt;.DMG</c> file, which supplies both
/// an 18-byte record per main component (its armour, its bone group, its destruction flags, and its
/// weighted list of dependents) and a flat maximum per dependent. That file is
/// <see cref="HercSimDamage"/>, already parsed by HercWorks.Core.</para>
///
/// <para><b>Overflow spills sideways.</b> A hit that finishes a main component does not stop there:
/// the excess is handed to <see cref="SpillIntoDependents"/>, which picks one of the component's
/// live dependents at random — weighted by the per-dependent figure the <c>.DMG</c> calls a crit
/// chance — and pours the excess into it, repeating for as long as each spill destroys something.
/// That is how shooting a leg off takes the servo in it with it.</para>
/// </summary>
public sealed class ComponentDamage {
	/// <summary>
	/// The sentinel a destroyed entry holds. <c>FUN_0040d3ec</c> writes it instead of the maximum, so
	/// "destroyed" is a distinct state from "damaged to exactly its maximum", and every read
	/// substitutes the maximum back in.
	/// </summary>
	public const short Destroyed = -1;

	/// <summary>How many main component slots a mech gets — <c>Mech_Constructor</c>'s literal <c>0x1d</c>.</summary>
	public const int MechComponentCount = 29;

	/// <summary>And how many dependent sub-pieces — its literal <c>0x16</c>.</summary>
	public const int MechDependentCount = 22;

	/// <summary>
	/// A flyer's counts, both literal <c>1</c> in <c>FUN_004215f4</c>. A flyer is one component with
	/// one dependent under it, which is exactly what <c>SKIMMER.DMG</c> ships.
	/// </summary>
	public const int FlyerComponentCount = 1;

	/// <inheritdoc cref="FlyerComponentCount"/>
	public const int FlyerDependentCount = 1;

	/// <summary>The damage-finished-it flag <c>FUN_0040cf44</c> pours in on destruction — its own literal.</summary>
	private const short FinishOffDependents = 32000;

	private readonly HercSimDamage _model;
	private readonly short[] _damage;
	private readonly short[] _dependentDamage;
	private readonly bool[] _active;
	private readonly List<int> _pendingCascade = new();
	private readonly SimRandom _random;

	/// <param name="model">The type's <c>dmg\&lt;NAME&gt;.DMG</c>.</param>
	/// <param name="componentCount">
	/// Main component slots. <b>Not read from the file</b> — the original hard-codes 29 for a mech and
	/// 1 for a flyer at the allocation site, and a file that disagrees simply leaves slots unusable.
	/// </param>
	/// <param name="dependentCount">Dependent sub-piece slots, likewise hard-coded.</param>
	/// <param name="random">The simulation's generator, for the weighted spill pick.</param>
	public ComponentDamage(HercSimDamage model, int componentCount, int dependentCount, SimRandom random) {
		_model = model;
		_random = random;
		_damage = new short[componentCount];
		_dependentDamage = new short[dependentCount];
		_active = new bool[componentCount];
		Array.Fill(_active, true);
	}

	/// <summary>How many main component slots this object has.</summary>
	public int Count => _damage.Length;

	/// <summary>Damage taken by each main component. <see cref="Destroyed"/> for one that has been lost.</summary>
	public IReadOnlyList<short> Damage => _damage;

	/// <summary>
	/// Whether a main component is still standing — the original's active-flag array, which the hit
	/// test consults before it will even test that component's spheres.
	/// </summary>
	public bool IsActive(int index) => index >= 0 && index < _active.Length && _active[index];

	/// <summary>
	/// Clears one slot's active flag on its own, leaving its damage reading alone — the bare
	/// <c>mech+0x20e[i] = 0</c> write <c>Mech_ApplyDirectFireDamage</c> makes when a weapon-mount
	/// component is knocked out. It is not <see cref="DestroyAndCascade"/>: the slot stops being
	/// shootable and stops taking further writes through the damage pathways, but nothing under it or
	/// mounted on it comes apart.
	/// </summary>
	public void Deactivate(int index) {
		if (index >= 0 && index < _active.Length) {
			_active[index] = false;
		}
	}

	/// <summary>The <c>.DMG</c> record for one main component, or null when the file has no such slot.</summary>
	public HercSimDamage.HercPiece? Piece(int index) {
		var pieces = _model.ComponentData;
		return pieces != null && index >= 0 && index < pieces.Length ? pieces[index] : null;
	}

	/// <summary>
	/// <c>Component_ReadDamagePercent</c> (<c>0040dbc0</c>) — one component's accumulated damage as a
	/// Q8 fraction, <b>0 pristine and 256 destroyed</b>. Not health — reading it as health inverts
	/// the meaning of every caller.
	///
	/// <para>It is an aggregate. The component's own damage and maximum are only the first term; the
	/// reading then folds in every dependent the component's record lists, each with its own damage
	/// and its own maximum, before dividing. So a leg reads as its own armour <i>plus</i> the servo
	/// inside it, which is why a component can show damage no shot ever put on it directly.</para>
	/// </summary>
	public int DamagePercent(int index) {
		if (Piece(index) is not { } piece) {
			return 0;
		}

		int maximum = piece.Armor;
		int taken = _damage[index] < 0 ? maximum : _damage[index];

		foreach (var dependent in piece.MappedInternals ?? Array.Empty<HercSimDamage.InternalsTarget>()) {
			int slot = dependent.InternalsId?.Id ?? -1;
			if (slot < 0 || slot >= _dependentDamage.Length) {
				continue;
			}

			short dependentMax = DependentMaximum(slot);
			taken += _dependentDamage[slot] < 0 ? dependentMax : _dependentDamage[slot];
			maximum += dependentMax;
		}

		// The original divides unguarded; a zero total is only reachable on hand-edited data, and
		// pristine is the honest answer for a component with nothing to lose.
		return maximum == 0 ? 0 : (taken << 8) / maximum;
	}

	/// <summary>
	/// <c>FUN_0040db2c</c> - the whole machine's damage as one Q8 fraction, 0 pristine and 256
	/// destroyed. It is the object's vtable <c>+0x40</c> for a HERC (<c>FUN_00415504</c>), and it is
	/// what the MFD status screen's structural-integrity readout prints.
	///
	/// <para>Every main component and every dependent slot is weighed once, each against its own
	/// maximum, and a destroyed one counts as its full maximum. Unlike <see cref="DamagePercent"/> a
	/// dependent is <b>not</b> also folded into its parent here, so nothing is double-counted.</para>
	/// </summary>
	public int OverallDamage {
		get {
			int taken = 0;
			int maximum = 0;

			for (int i = 0; i < _damage.Length; i++) {
				int armor = Piece(i)?.Armor ?? 0;
				taken += _damage[i] < 0 ? armor : _damage[i];
				maximum += armor;
			}

			for (int slot = 0; slot < _dependentDamage.Length; slot++) {
				short dependentMax = DependentMaximum(slot);
				taken += _dependentDamage[slot] < 0 ? dependentMax : _dependentDamage[slot];
				maximum += dependentMax;
			}

			return maximum == 0 ? 0 : (taken << 8) / maximum;
		}
	}

	/// <summary>
	/// One dependent sub-piece's damage on its own, as a Q8 fraction — <b>not</b> the aggregate
	/// <see cref="DamagePercent"/> computes. <c>Mech_ComponentDamageWrite</c> spells this reading out
	/// inline, by literal slot, four or six times over: the leg servos at 0 and 1 (plus 10 and 11 on
	/// a four-legged chassis), the shield generator at 4, the reactor at 5, and life support and the
	/// pilot at 8 and 9. A destroyed entry reads as a full 256.
	/// </summary>
	public int DependentPercent(int slot) {
		if (slot < 0 || slot >= _dependentDamage.Length) {
			return 0;
		}

		short maximum = DependentMaximum(slot);
		if (_dependentDamage[slot] < 0 || maximum == 0) {
			return _dependentDamage[slot] < 0 ? 0x100 : 0;
		}

		return (_dependentDamage[slot] << 8) / maximum;
	}

	/// <summary>
	/// <c>Component_FillDamageReadouts</c> (<c>004151a4</c>) — the display buffer every damage screen
	/// reads, in one pass, because that is how the original hands it to them: a flat array of Q8
	/// fractions the screens then index by fixed offset rather than calling back into the model.
	///
	/// <list type="bullet">
	/// <item><see cref="FirstArmorReadout"/>..+18: each of the first 19 main components against its
	/// own armour alone — <b>not</b> <see cref="DamagePercent"/>, which also folds in that
	/// component's dependents.</item>
	/// <item><see cref="FirstDependentReadout"/>..+11: the twelve dependents on their own, the same
	/// reading <see cref="DependentPercent"/> gives.</item>
	/// <item><see cref="FirstCombinedReadout"/>..+9: components 19-28 — the weapon mounts — each
	/// weighed together with the dependent at the matching offset, which is dependent 12 upward.
	/// Pairing is positional here, not through the component's own dependent list.</item>
	/// </list>
	///
	/// <para>Entry 0 is the type record's <c>+0x6c</c> (<c>Mech_ReadDamageReadouts</c>,
	/// <c>0041b4ec</c>) and stays zero: no screen's paint reads it.</para>
	/// </summary>
	public short[] ReadDamageReadouts() {
		var readouts = new short[ReadoutCount];

		for (int i = 0; i < ArmorReadoutCount; i++) {
			short armor = Piece(i)?.Armor ?? 0;
			readouts[FirstArmorReadout + i] = i >= _damage.Length ? (short)0
				: _damage[i] < 0 ? (short)0x100
				: armor == 0 ? (short)0
				: (short)((_damage[i] << 8) / armor);
		}

		for (int slot = 0; slot < DependentReadoutCount; slot++) {
			readouts[FirstDependentReadout + slot] = (short)DependentPercent(slot);
		}

		for (int i = 0; i < CombinedReadoutCount; i++) {
			int component = ArmorReadoutCount + i;
			int slot = DependentReadoutCount + i;
			int armor = Piece(component)?.Armor ?? 0;
			short dependentMax = DependentMaximum(slot);
			int total = armor + dependentMax;
			if (total == 0 || component >= _damage.Length || slot >= _dependentDamage.Length) {
				continue;
			}

			int taken = (_damage[component] < 0 ? armor : _damage[component])
				+ (_dependentDamage[slot] < 0 ? dependentMax : _dependentDamage[slot]);
			readouts[FirstCombinedReadout + i] = (short)((taken << 8) / total);
		}

		return readouts;
	}

	/// <summary>Entries in the buffer <see cref="ReadDamageReadouts"/> fills — the original's own 42.</summary>
	public const int ReadoutCount = 42;

	/// <summary>Where the 19 per-armour component readings start.</summary>
	public const int FirstArmorReadout = 1;

	/// <inheritdoc cref="FirstArmorReadout"/>
	public const int ArmorReadoutCount = 19;

	/// <summary>Where the 12 standalone dependent readings start.</summary>
	public const int FirstDependentReadout = 20;

	/// <inheritdoc cref="FirstDependentReadout"/>
	public const int DependentReadoutCount = 12;

	/// <summary>Where the 10 combined weapon-mount readings start.</summary>
	public const int FirstCombinedReadout = 32;

	/// <inheritdoc cref="FirstCombinedReadout"/>
	public const int CombinedReadoutCount = 10;

	/// <summary>
	/// <c>FUN_0040d9f8</c> — whether a main component is destroyed <b>and</b> every dependent under it
	/// is too (<c>FUN_0040cf10</c>). It is the stricter of the two "is this gone" questions, and the
	/// one the mech's death test asks of its two cockpit sections.
	/// </summary>
	public bool FullyDestroyed(int index) {
		if (Piece(index) is not { } piece || index >= _damage.Length || _damage[index] >= 0) {
			return false;
		}

		foreach (var dependent in piece.MappedInternals ?? Array.Empty<HercSimDamage.InternalsTarget>()) {
			int slot = dependent.InternalsId?.Id ?? -1;
			if (slot >= 0 && slot < _dependentDamage.Length && _dependentDamage[slot] >= 0) {
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// <c>Component_ApplyDamageAndCascade</c> (<c>0040da38</c>) — the write half, and the endpoint
	/// both damage pathways converge on.
	///
	/// <list type="number">
	/// <item>Add the damage to the component. Under its maximum, that is the whole of it.</item>
	/// <item>At or over, the component is destroyed (<see cref="Destroyed"/>) and the
	/// <i>excess</i> carries on into its dependents — see <see cref="SpillIntoDependents"/>.</item>
	/// <item>If the spill ran out of live dependents <b>and</b> the record's destruction flags say so,
	/// the component cascades: it comes apart, and so does everything mounted on it.</item>
	/// </list>
	///
	/// <para><b>The active flag is not this function's gate.</b> It gates
	/// <see cref="DestroyAndCascade"/> and each pathway's own entry point
	/// (<c>Mech_ComponentDamageWrite</c> tests it and returns before reaching here); the write itself
	/// runs on an inactive slot. That distinction is what lets the weapon-mount destruction roll
	/// clear the slot's flag and <i>then</i> finish the component off with a flat 10000 without the
	/// bone group coming apart with it — see <see cref="Deactivate"/>.</para>
	/// </summary>
	/// <returns>Whether the component cascaded — which is what a caller reads as "this thing is gone".</returns>
	public bool ApplyDamage(int index, short damage) {
		if (Piece(index) is not { } piece) {
			return false;
		}

		if (!AddDamage(ref _damage[index], piece.Armor, ref damage)) {
			return false;
		}

		if (!SpillIntoDependents(piece, damage) || (piece.DestructionFlags & 1) == 0) {
			return false;
		}

		DestroyAndCascade(index);

		// The original queues the bone-group siblings on a shared list and drains it here rather
		// than recursing, so a cycle in the data cannot blow the stack. Same shape, own list.
		while (_pendingCascade.Count > 0) {
			int next = _pendingCascade[^1];
			_pendingCascade.RemoveAt(_pendingCascade.Count - 1);
			DestroyAndCascade(next);
		}

		return true;
	}

	/// <summary>
	/// <c>Component_DestroyAndCascade</c> (<c>0040d434</c>) — takes one component out: the entry goes
	/// to <see cref="Destroyed"/>, the active flag clears, everything under it is finished off, and
	/// every <i>other</i> component whose record names this one as its bone group is queued to follow.
	///
	/// <para><b>The <c>.DMG</c>'s <c>BoneId</c> is a parent component index</b>, not a model bone: the
	/// original compares it against the index of the component that just died. That is the whole of
	/// the dependency graph between main components — losing a shoulder takes the arm on it.</para>
	///
	/// <para>Not ported, all of it visual: the debris and destruction effect the record's own
	/// <c>DebrisFlags</c> selects, the HUD slot it marks, and the sub-shape it hides — the record's
	/// <c>+3</c> byte names a cell-animation sequence, and the original steps it to its blank third
	/// cell. See docs/formats/mech-shape-drawing.md.</para>
	/// </summary>
	private void DestroyAndCascade(int index) {
		if (Piece(index) is not { } piece || !IsActive(index)) {
			return;
		}

		_damage[index] = Destroyed;
		_active[index] = false;

		// Whatever is left under it goes with it. The original's literal 32000 is simply more than
		// any dependent's maximum.
		SpillIntoDependents(piece, FinishOffDependents);

		// The field is a signed byte in the original and 0xff means "no parent", so it is read as one
		// here: as an unsigned byte the sentinel would simply never match, which is the right answer
		// by accident rather than the right comparison.
		var pieces = _model.ComponentData ?? Array.Empty<HercSimDamage.HercPiece>();
		for (int i = 0; i < _active.Length && i < pieces.Length; i++) {
			if (_active[i] && (sbyte)pieces[i].BoneId == index) {
				_pendingCascade.Add(i);
			}
		}
	}

	/// <summary>
	/// <c>FUN_0040cf44</c> — pours a component's overflow damage into its dependents, one at a time.
	///
	/// <para>The pick is <b>weighted and random</b>: each live dependent contributes the record's own
	/// per-dependent figure (<see cref="HercSimDamage.InternalsTarget.CritChance"/>, 20 on most mech
	/// pieces and 150 on the skimmer's one) to a total, a number is drawn under that total, and the
	/// entry whose band it lands in takes the hit. If that spill destroys the dependent, its own
	/// excess goes round again — so one large hit can strip several internals in sequence.</para>
	/// </summary>
	/// <returns>
	/// Whether the component has no live dependents left. It is that answer, not the damage, that the
	/// caller turns into a cascade — a component only comes apart once everything inside it has.
	/// </returns>
	private bool SpillIntoDependents(HercSimDamage.HercPiece piece, short damage) {
		var dependents = piece.MappedInternals ?? Array.Empty<HercSimDamage.InternalsTarget>();

		while (true) {
			int total = 0;
			foreach (var dependent in dependents) {
				int slot = dependent.InternalsId?.Id ?? -1;
				if (slot >= 0 && slot < _dependentDamage.Length && _dependentDamage[slot] >= 0) {
					total += dependent.CritChance;
				}
			}

			if (total == 0) {
				return true;
			}

			// The original walks the list subtracting each live entry's weight from the draw and stops
			// on the first that takes it below zero, falling off the end onto the last entry when the
			// weights do not cover the draw. Reproduced, landing included.
			int draw = _random.NextBelow((short)total);
			int chosen = -1;
			foreach (var dependent in dependents) {
				int slot = dependent.InternalsId?.Id ?? -1;
				chosen = slot;
				if (slot < 0 || slot >= _dependentDamage.Length || _dependentDamage[slot] < 0) {
					continue;
				}

				draw -= dependent.CritChance;
				if (draw < 0) {
					break;
				}
			}

			if (chosen < 0 || chosen >= _dependentDamage.Length) {
				return false;
			}

			if (!AddDamage(ref _dependentDamage[chosen], DependentMaximum(chosen), ref damage)) {
				return false;
			}
		}
	}

	/// <summary>
	/// <c>FUN_0040d3ec</c> — adds damage to one entry, capping it at the maximum.
	///
	/// <para>Two things it does that a plain accumulate would not. An entry already holding
	/// <see cref="Destroyed"/> absorbs nothing at all, so a lost part cannot be shot again. And an
	/// entry the damage finishes stores the sentinel rather than the maximum and <b>writes the excess
	/// back into <paramref name="damage"/></b>, which is the mechanism the whole spill is built
	/// on.</para>
	/// </summary>
	/// <returns>Whether the entry is now destroyed.</returns>
	private static bool AddDamage(ref short entry, short maximum, ref short damage) {
		if (entry < 0) {
			return true;
		}

		int total = entry + damage;
		if (total < maximum) {
			entry = (short)total;
		} else {
			damage = (short)(total - maximum);
			entry = Destroyed;
		}

		return entry < 0;
	}

	/// <summary>
	/// One dependent's maximum, out of the <c>.DMG</c>'s flat leading array. Every retail HERC states
	/// 22 of these and the skimmer one, matching the slot counts the constructors allocate.
	/// </summary>
	private short DependentMaximum(int slot) {
		var internals = _model.Internals;
		return internals != null && slot >= 0 && slot < internals.Length
			? internals[slot]?.Armor ?? 0
			: (short)0;
	}
}
