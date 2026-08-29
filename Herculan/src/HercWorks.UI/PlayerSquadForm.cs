using System.ComponentModel;
using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Editor for <c>data\player.mec</c> — the player's own squad: which machine the player pilots, which
/// wingmen come along, and each one's weapon fit. This is the file DBSIM reads for the player's
/// loadout; <c>script.dat</c> carries only the point the squad spawns at (its block 11 record 0),
/// which is why the mech and weapons cannot be edited from MissionScriptForm. Backed by
/// HercWorks.Core's MecFileTransformer; see MecFile for the format and the RE behind it.
///
/// <para><b>VSHELL regenerates this file from the .sav</b> every time a mission is launched from the
/// shell, so edits here apply to the next DBSIM run and are overwritten by going back through the
/// shell. Use CampaignResourcesForm's Herc Bay for changes that should survive that.</para>
///
/// <para>The layout is master-detail, like MissionScriptForm's Hercs tab: the squad roster on top,
/// and below it the selected entry's weapon slots one row each, with the weapon and (for launchers
/// only) the ammunition type picked by name — see <see cref="WeaponFitOption"/> and
/// <see cref="AmmoTypeOption"/>. Slots are added and removed there too, always to both arrays at
/// once, since their lengths are what set the record's own length.</para>
///
/// <para>Herc types are named, via <see cref="HercTypeOption"/>'s HercLUT-to-MECHS.NAM
/// equivalence; weapon names come from <c>WeaponLUT</c>, whose ids are the same ones these arrays
/// carry.</para>
/// </summary>
public partial class PlayerSquadForm : Form {
	private readonly MecFileTransformer _transformer = new();
	private readonly BindingList<PlayerSquadRow> _rows = new();
	private readonly BindingList<PlayerWeaponSlotRow> _slotRows = new();

	private MecFile? _loaded;
	private string? _loadedPath;

	/// <summary>Original VOL entry prefix, round-tripped on save — see VolEntryPrefixCodec.</summary>
	private byte? _originalCompressionType;
	private byte[]? _originalMagicPrefix;
	private bool _originalHadTrailingByte;

	public PlayerSquadForm() {
		InitializeComponent();
		_squadGrid.DataSource = _rows;
		_loadoutGrid.DataSource = _slotRows;
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	/// <summary>
	/// Distinct per-form/file-type identity so Windows remembers this dialog's last-visited folder
	/// separately from every other Open/Save dialog in the app — see CampaignResourcesForm's
	/// DialogClientGuid for the full explanation.
	/// </summary>
	private static readonly Guid DialogClientGuid = new("2d84f7a0-5b93-4e18-9a6c-8f3d1c07b562");

	/// <summary>
	/// The copy DBSIM actually reads, relative to the game directory — opened automatically so the
	/// editor starts on the live squad rather than an empty grid.
	/// </summary>
	private static readonly string[] DefaultFile = { "DATA", "PLAYER.MEC" };

	protected override void OnLoad(EventArgs e) {
		base.OnLoad(e);

		if (GamePaths.Resolve(DefaultFile) is { } path) {
			LoadFile(path);
		}
	}

	private void OnOpen(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Player squad files (*.mec)|*.mec|All files (*.*)|*.*",
			Title = "Open player squad file",
			ClientGuid = DialogClientGuid,
			InitialDirectory = GamePaths.InitialDirectoryFor("DATA")
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		LoadFile(dialog.FileName);
	}

	private void LoadFile(string path) {
		try {
			byte[] rawBytes = File.ReadAllBytes(path);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);
			var squad = (MecFile?)_transformer.Parse(prefix.Content);

			if (squad == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_loaded = squad;

			_rows.Clear();
			_slotRows.Clear();
			BindTypeOptions(squad);
			for (int i = 0; i < squad.Entries.Length; i++) {
				_rows.Add(new PlayerSquadRow { Index = i, Source = squad.Entries[i] });
			}

			BindLoadout();
			UpdatePlayerSlotRange();
			_playerSlotInput.Value = Math.Clamp(squad.PlayerEntryIndex, _playerSlotInput.Minimum, _playerSlotInput.Maximum);

			_loadedPath = path;
			_originalCompressionType = prefix.HadPrefix ? prefix.CompressionType : null;
			_originalMagicPrefix = prefix.MagicPrefix;
			_originalHadTrailingByte = prefix.HadTrailingByte;

			string prefixNote = prefix.HadPrefix ? " (VOL entry prefix detected — will be preserved on save)" : "";
			_statusLabel.Text =
				$"Loaded {Path.GetFileName(path)} — {squad.Entries.Length} entries, " +
				$"player pilots #{squad.PlayerEntryIndex}.{prefixNote}";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	/// <summary>
	/// Rebuilds the three dropdowns for the file being loaded, before any row is bound — a combo
	/// column rejects a value it has no item for, so the unnamed types, weapon ids and ammunition
	/// values the file actually carries have to be in the lists first.
	///
	/// <para>The weapon list is built without script.dat's <c>-1</c> empty-slot entry: this format
	/// spells an empty slot <c>NONE</c> (id 0), and its slot list is only as long as the machine's
	/// real slot count.</para>
	/// </summary>
	private void BindTypeOptions(MecFile squad) {
		_hercTypeColumn.DataSource = HercTypeOption.Build(squad.Entries.Select(entry => entry.MechType));
		_slotWeaponColumn.DataSource =
			WeaponFitOption.Build(squad.Entries.SelectMany(entry => entry.WeaponRefs), includeEmptySlot: false);
		_slotAmmoColumn.DataSource = AmmoTypeOption.Build(squad.Entries.SelectMany(entry => entry.WeaponAmmoTypes));
	}

	/// <summary>
	/// Points the loadout grid at the selected entry's weapon slots. The panel is disabled rather
	/// than left showing a stale fit when nothing is selected.
	/// </summary>
	private void BindLoadout() {
		var entry = _squadGrid.CurrentRow?.DataBoundItem as PlayerSquadRow;

		_slotRows.Clear();
		if (entry != null) {
			for (int slot = 0; slot < entry.Source.WeaponRefs.Length; slot++) {
				_slotRows.Add(new PlayerWeaponSlotRow { Source = entry.Source, Slot = slot });
			}
		}

		_loadoutGroupBox.Enabled = entry != null;
		_loadoutGroupBox.Text = entry == null ? "Weapon fit" : $"Weapon fit — entry {entry.Index}";
	}

	/// <summary>The loadout panel edits whichever entry the roster is on.</summary>
	private void OnSquadSelectionChanged(object? sender, EventArgs e) => BindLoadout();

	/// <summary>
	/// Appends a slot to both arrays at once — <c>NONE</c> and the filler 5, which is what retail
	/// writes for a slot carrying no launcher. Growing them together is what keeps the record's
	/// declared length and its two lists in agreement.
	/// </summary>
	private void OnAddSlot(object? sender, EventArgs e) {
		if (_squadGrid.CurrentRow?.DataBoundItem is not PlayerSquadRow row) {
			return;
		}

		ResizeSlots(row.Source, row.Source.WeaponRefs.Length + 1);
		RefreshAfterSlotChange(row);
	}

	/// <summary>Drops the selected slot from both arrays, or the last one if none is selected.</summary>
	private void OnRemoveSlot(object? sender, EventArgs e) {
		if (_squadGrid.CurrentRow?.DataBoundItem is not PlayerSquadRow row) {
			return;
		}

		var entry = row.Source;
		if (entry.WeaponRefs.Length == 0) {
			return;
		}

		int slot = (_loadoutGrid.CurrentRow?.DataBoundItem as PlayerWeaponSlotRow)?.Slot
			?? entry.WeaponRefs.Length - 1;

		entry.WeaponRefs = RemoveAt(entry.WeaponRefs, slot);
		entry.WeaponAmmoTypes = RemoveAt(entry.WeaponAmmoTypes, slot);
		RefreshAfterSlotChange(row);
	}

	/// <summary>
	/// Grows both arrays to <paramref name="length"/>, filling new slots with the empty weapon and
	/// the filler ammunition value.
	/// </summary>
	private static void ResizeSlots(MecEntry entry, int length) {
		int wasWeapons = entry.WeaponRefs.Length;
		int wasAmmo = entry.WeaponAmmoTypes.Length;

		short[] weapons = entry.WeaponRefs;
		short[] ammo = entry.WeaponAmmoTypes;
		Array.Resize(ref weapons, length);
		Array.Resize(ref ammo, length);

		for (int i = wasWeapons; i < length; i++) {
			weapons[i] = (short)WeaponLUT.None.Id;
		}
		for (int i = wasAmmo; i < length; i++) {
			ammo[i] = AmmoTypeOption.Filler;
		}

		entry.WeaponRefs = weapons;
		entry.WeaponAmmoTypes = ammo;
	}

	private static short[] RemoveAt(short[] values, int index) =>
		index >= 0 && index < values.Length
			? values.Where((_, i) => i != index).ToArray()
			: values;

	/// <summary>
	/// The slot rows carry their position, and the roster's Slots and fit columns are derived, so
	/// both grids have to be rebuilt rather than repainted after slots are added or removed.
	/// </summary>
	private void RefreshAfterSlotChange(PlayerSquadRow row) {
		BindLoadout();
		_squadGrid.InvalidateRow(row.Index);
	}

	/// <summary>
	/// A combo cell normally only commits when focus leaves it, which would leave the ammunition
	/// column and the roster's fit summary a step behind the weapon just picked.
	/// </summary>
	private void OnLoadoutCellDirtyStateChanged(object? sender, EventArgs e) {
		if (_loadoutGrid.IsCurrentCellDirty) {
			_loadoutGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
		}
	}

	/// <summary>Ammunition is a launcher-only field — see WeaponFitOption.IsLauncher.</summary>
	private void OnLoadoutCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e) {
		if (e.ColumnIndex == _slotAmmoColumn.Index && SlotAt(e.RowIndex) is { IsLauncher: false }) {
			e.Cancel = true;
		}
	}

	/// <summary>
	/// Greys the ammunition cell of every slot that is not a launcher, so a value that has no effect
	/// does not read as one that does. The value itself is still shown rather than blanked — it is
	/// real data in the file, and retail's own filler 5 is what belongs there.
	/// </summary>
	private void OnLoadoutCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e) {
		if (e.ColumnIndex == _slotAmmoColumn.Index && e.CellStyle is { } style
			&& SlotAt(e.RowIndex) is { IsLauncher: false }) {
			style.ForeColor = SystemColors.GrayText;
			style.BackColor = SystemColors.Control;
		}
	}

	private PlayerWeaponSlotRow? SlotAt(int rowIndex) =>
		rowIndex >= 0 && rowIndex < _loadoutGrid.Rows.Count
			? _loadoutGrid.Rows[rowIndex].DataBoundItem as PlayerWeaponSlotRow
			: null;

	/// <summary>
	/// Both the roster's fit summary and the ammunition cell's own enabled look are derived from the
	/// weapon just picked, and neither is a bound property that would repaint on its own.
	/// </summary>
	private void OnLoadoutCellValueChanged(object? sender, DataGridViewCellEventArgs e) {
		if (e.RowIndex < 0) {
			return;
		}

		_loadoutGrid.InvalidateRow(e.RowIndex);

		if (_squadGrid.CurrentRow is { } row) {
			_squadGrid.InvalidateRow(row.Index);
		}
	}

	/// <summary>
	/// PlayerEntryIndex names one of the entries, so its range has to follow the roster as rows are
	/// added and removed.
	/// </summary>
	private void UpdatePlayerSlotRange() {
		_playerSlotInput.Maximum = Math.Max(0, _rows.Count - 1);
	}

	/// <summary>Slots is derived from the weapon list's length, so it has to follow the edit.</summary>
	private void OnSquadCellChanged(object? sender, DataGridViewCellEventArgs e) {
		if (e.RowIndex >= 0) {
			_squadGrid.InvalidateRow(e.RowIndex);
		}
	}

	/// <summary>
	/// Rejected cell edits surface here — the row property setters throw FormatException for a
	/// malformed list. Report and keep the old value rather than letting WinForms rethrow into an
	/// unhandled crash.
	/// </summary>
	private void OnGridDataError(object? sender, DataGridViewDataErrorEventArgs e) {
		e.ThrowException = false;
		e.Cancel = true;
		MessageBox.Show(this, e.Exception?.Message ?? "That value could not be applied.",
			"Invalid value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
	}

	/// <summary>
	/// Adds a wingman by cloning the selected entry (or the first one). The three trailing spans are
	/// copied wholesale into DBSIM's mech record and nothing is known about their contents, so an
	/// entry built from scratch would be guesswork — cloning a real one keeps them valid.
	/// </summary>
	private void OnAddEntry(object? sender, EventArgs e) {
		if (_loaded == null || _rows.Count == 0) {
			MessageBox.Show(this, "Open a player.mec file first — new entries are cloned from an existing one.",
				"Nothing to clone", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		var template = (_squadGrid.CurrentRow?.DataBoundItem as PlayerSquadRow ?? _rows[0]).Source;
		var clone = new MecEntry {
			Unk00 = template.Unk00,
			Unk02 = template.Unk02,
			MechType = template.MechType,
			SlotCount = template.SlotCount,
			WeaponRefs = (short[])template.WeaponRefs.Clone(),
			WeaponAmmoTypes = (short[])template.WeaponAmmoTypes.Clone(),
			Unk3A = template.Unk3A,
			BlockA = (byte[])template.BlockA.Clone(),
			BlockB = (byte[])template.BlockB.Clone(),
			BlockC = (byte[])template.BlockC.Clone()
		};

		_rows.Add(new PlayerSquadRow { Index = _rows.Count, Source = clone });
		UpdatePlayerSlotRange();
		BindLoadout();
	}

	private void OnRemoveEntry(object? sender, EventArgs e) {
		if (_squadGrid.CurrentRow?.DataBoundItem is not PlayerSquadRow row) {
			return;
		}

		if (_rows.Count == 1) {
			MessageBox.Show(this, "The squad needs at least one entry — it is the machine the player pilots.",
				"Cannot remove", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		_rows.Remove(row);
		for (int i = 0; i < _rows.Count; i++) {
			_rows[i].Index = i;
		}

		UpdatePlayerSlotRange();
		_squadGrid.Refresh();
		BindLoadout();
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_loaded == null) {
			MessageBox.Show(this, "Open a player.mec file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		// Grid edits write straight through to the model, but only once the cell is committed.
		_squadGrid.EndEdit();
		_loadoutGrid.EndEdit();

		// The writer emits SlotCount as a field and then writes both arrays at their actual length,
		// so a mismatch does not just mis-describe this entry — it shifts every entry after it. The
		// loadout panel only ever grows and shrinks the two together, so this catches a file that
		// arrived mismatched rather than an edit made here.
		var mismatched = _rows
			.Where(r => r.Source.WeaponRefs.Length != r.Source.WeaponAmmoTypes.Length)
			.Select(r => $"Entry {r.Index}: {r.Source.WeaponRefs.Length} weapon ids vs {r.Source.WeaponAmmoTypes.Length} paired values.")
			.ToList();

		if (mismatched.Count > 0) {
			MessageBox.Show(this,
				"Each entry needs exactly one paired value per weapon id — the two lists set the record's " +
				"length together, so a mismatch corrupts every entry after it.\n\n" + string.Join("\n", mismatched),
				"Cannot save", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}

		_loaded.Entries = _rows.Select(r => r.Source).ToArray();
		foreach (var entry in _loaded.Entries) {
			entry.SlotCount = (short)entry.WeaponRefs.Length;
		}
		_loaded.PlayerEntryIndex = (short)_playerSlotInput.Value;

		using var dialog = new SaveFileDialog {
			Filter = "Player squad files (*.mec)|*.mec|All files (*.*)|*.*",
			Title = "Save player squad file",
			FileName = _loadedPath == null ? "PLAYER.MEC" : Path.GetFileName(_loadedPath),
			ClientGuid = DialogClientGuid,
			InitialDirectory = _loadedPath == null
				? GamePaths.InitialDirectoryFor("DATA")
				: Path.GetDirectoryName(_loadedPath)!
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] content = _transformer.Write(_loaded)!;
			byte[] outBytes;
			string formatNote;

			if (_originalCompressionType.HasValue && _originalMagicPrefix != null) {
				outBytes = VolEntryPrefixCodec.Wrap(
					content, _originalCompressionType.Value, _originalMagicPrefix, _originalHadTrailingByte);
				formatNote = "retail-compatible format — the original VOL entry prefix (compression type, magic) was preserved, with the size field updated for the edited content";
			} else {
				outBytes = content;
				formatNote = "content-only format — this file wasn't loaded with a VOL entry prefix to preserve, so no prefix could be reconstructed for this export";
			}

			File.WriteAllBytes(dialog.FileName, outBytes);

			// Same preallocated-buffer situation as script.dat: the retail file carries stale bytes
			// past its last declared entry, which DBSIM never reads.
			MessageBox.Show(this,
				$"Saved in {formatNote}.\n\n" +
				$"Written as {outBytes.Length:N0} bytes — the game's own file carries stale trailing data " +
				"past its last entry, which DBSIM stops short of and ignores.",
				"Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to save file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}
}
