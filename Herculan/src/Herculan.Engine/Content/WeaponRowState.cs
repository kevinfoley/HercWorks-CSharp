using Herculan.Engine.Sim;

namespace Herculan.Engine.Content;

/// <summary>
/// One row of the cockpit's weapon panel, as the three weapon-gauge classes draw it. A row is a
/// <c>.GAU</c> weapon slot, and which mount owns it is the mount's own
/// <see cref="WeaponMount.GaugeSlot"/> — the <c>.GL</c> record's byte at <c>+7</c>, not its position
/// in the fit. A slot no mount claims draws its plate and number and nothing else.
///
/// <para>The three classes differ only past the name: an energy mount's row
/// (<c>FUN_00440a68</c>) carries an LED charge bar, an ammunition mount's (<c>FUN_00440f78</c>)
/// carries a round count, and a pod's (<c>FUN_00441524</c>) carries neither and instead widens its
/// name label across the whole row.</para>
/// </summary>
/// <param name="Name">
/// What the row prints — see <see cref="Build"/> for the pod suffix and the destroyed-mount
/// substitution.
/// </param>
/// <param name="Kind">Which gauge class this row is.</param>
/// <param name="Selected">
/// Whether this row draws as armed — the row plate lights and its text brightens. Both halves of a
/// linked pair draw armed together; see <see cref="WeaponMounts.IsArmedRow"/>.
/// </param>
/// <param name="InGroup">Whether the mount is in the current fire group. With <paramref name="Selected"/>
/// clear too, the state box is not drawn at all — which is what a pod's row always looks like.</param>
/// <param name="Ready">Whether the mount could fire right now — the state box's lit frame rather than its dark one.</param>
/// <param name="Rounds">Rounds remaining, for an ammunition row.</param>
/// <param name="ChargeMeter">
/// An energy row's bar value over the LED bar's own 0-1024 range. A full capacitor reads about
/// four-fifths of the bar, not all of it — see <see cref="WeaponMount.EnergyChargeScale"/>.
/// </param>
public readonly record struct WeaponRowState(
	string Name,
	WeaponMountKind Kind,
	bool Selected,
	bool InGroup,
	bool Ready,
	int Rounds,
	int ChargeMeter) {

	/// <summary>A <c>.GAU</c> slot with no mount on it.</summary>
	public static WeaponRowState Empty { get; } =
		new(string.Empty, WeaponMountKind.None, false, false, false, 0, 0);

	/// <summary>The longest name a weapon gauge's own buffer holds — <c>strncpy(gauge+0xb1, name, 12)</c>.</summary>
	public const int NameLength = 12;

	/// <summary>
	/// A pod row's name buffer is one shorter, because <c>FUN_00441524</c> seeds it with a leading
	/// space and then appends with <c>strncat(dest, text, 11 - strlen(dest))</c>.
	/// </summary>
	public const int PodNameLength = 11;

	/// <summary>The space <c>FUN_00441524</c> seeds a pod row's name with, before the weapon's own name.</summary>
	public const string PodNamePrefix = " ";

	/// <summary>
	/// Which <c>STRINGS0.STR</c> group holds the label a destroyed mount's row prints in place of its
	/// name — the third registration in <c>SimStrings_LoadAll</c> (<c>00437598</c>), a single-entry
	/// group reading <c>OFFLINE</c>.
	/// </summary>
	public const int OfflineStringGroup = 2;

	/// <summary>
	/// And the fourth, also a single entry: the <c>" POD"</c> a pod row's name ends with. Both are
	/// string-table text rather than literals in the executable, which is why <c>SHIELD</c> reads
	/// <c>SHIELD POD</c> in the cockpit but plain <c>SHIELD</c> in the Heads-Down Display's weapon
	/// list.
	/// </summary>
	public const int PodSuffixStringGroup = 3;

	/// <summary>
	/// Lays out one machine's weapon panel: <paramref name="slots"/> rows, each filled by whichever
	/// mount claims it.
	///
	/// <para>Naming follows the gauge constructors exactly. An energy or ammunition row takes the
	/// mount's name as it is; a pod row is built as <c>" " + name</c> capped at
	/// <see cref="PodNameLength"/> and then <c>" POD"</c> appended into whatever room is left, so
	/// <c>SHIELD</c> becomes <c>" SHIELD POD"</c> and <c>ENERGY</c> becomes <c>" ENERGY POD"</c>
	/// exactly filling the buffer. A destroyed mount prints <c>OFFLINE</c> instead of any of it.</para>
	/// </summary>
	/// <param name="mounts">The machine's mounts.</param>
	/// <param name="slots">How many weapon rows this herc's <c>.GAU</c> declares.</param>
	/// <param name="strings">
	/// <c>STRINGS0.STR</c>, for the two labels above. Without it a pod row prints its bare weapon name
	/// and a destroyed one prints nothing — neither is invented.
	/// </param>
	public static IReadOnlyList<WeaponRowState> Build(WeaponMounts mounts, int slots,
			SimStringTable? strings) {
		string offline = strings?.Text(OfflineStringGroup, 0) ?? string.Empty;
		string podSuffix = strings?.Text(PodSuffixStringGroup, 0) ?? string.Empty;

		var rows = new WeaponRowState[Math.Max(slots, 0)];
		for (int slot = 0; slot < rows.Length; slot++) {
			if (mounts.BySlot(slot) is not { } mount) {
				rows[slot] = Empty;
				continue;
			}

			string name = mount.Disabled
				? offline
				: mount.Kind == WeaponMountKind.Pod
					? PodName(mount.Name, podSuffix)
					: Truncate(mount.Name, NameLength);

			rows[slot] = new WeaponRowState(
				name,
				mount.Kind,
				mounts.IsArmedRow(mount.MountIndex),
				mounts.InCurrentGroup(mount.MountIndex),
				mount.CanFire,
				mount.Rounds,
				mount.ChargeMeterValue);
		}

		return rows;
	}

	private static string PodName(string name, string suffix) {
		string built = Truncate(PodNamePrefix + name, PodNameLength);
		return built + Truncate(suffix, PodNameLength - built.Length);
	}

	private static string Truncate(string text, int length) =>
		length <= 0 ? string.Empty : text.Length <= length ? text : text[..length];
}
