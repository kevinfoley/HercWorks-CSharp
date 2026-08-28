using System.ComponentModel;
using HercWorks.Core.Data.File.Sav;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Herc;
using HercWorks.Core.Data.Struct.Vshell.Sav;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Editor for a player's .sav file. Covers Salvage Total, the 5 Workshop Slots, Herc Unlocks,
/// Squadmates + Player Pilot, Weapon Inventory, and Herc Bay (flat fields directly, per-part
/// health/equipped-weapons via HercBayEditorForm — too deeply nested for a flat grid row).
/// Follows the same pattern as HercStatsForm/WeaponStatsForm: works against a loose .sav file,
/// uses the shared VolEntryPrefixCodec so exports stay retail-compatible, and keeps layout in
/// CampaignResourcesForm.Designer.cs for the WinForms visual designer.
/// </summary>
public partial class CampaignResourcesForm : Form {
	private readonly PlayerSaveTransform _transformer = new();

	private readonly BindingList<HercUnlockRow> _hercUnlockRows = new();
	private readonly BindingList<SquadmateRow> _squadmateRows = new();
	private readonly BindingList<InventoryRow> _inventoryRows = new();
	private readonly BindingList<HercBayRow> _hercBayRows = new();

	private PlayerSave? _loadedSave;
	private string? _loadedPath;

	/// <summary>Original VOL entry prefix, round-tripped on save — see VolEntryPrefixCodec.</summary>
	private byte? _originalCompressionType;
	private byte[]? _originalMagicPrefix;
	private bool _originalHadTrailingByte;

	public CampaignResourcesForm() {
		InitializeComponent();

		foreach (var combo in WorkshopCombos) {
			combo.Items.AddRange(WeaponLUT.Values().ToArray());
		}

		_sqRankColumn.Items.AddRange(PilotRank.Values().Select(r => r.Label).Cast<object>().ToArray());
		_hercBayHercColumn.Items.AddRange(HercLUT.Values().Cast<object>().ToArray());

		_hercUnlocksGrid.DataSource = _hercUnlockRows;
		_squadmatesGrid.DataSource = _squadmateRows;
		_inventoryGrid.DataSource = _inventoryRows;
		_hercBayGrid.DataSource = _hercBayRows;
	}

	private ComboBox[] WorkshopCombos => new[] {
		_workshopSlot1Combo, _workshopSlot2Combo, _workshopSlot3Combo, _workshopSlot4Combo, _workshopSlot5Combo
	};

	private void OnClose(object? sender, EventArgs e) => Close();

	/// <summary>
	/// Distinct per-form/file-type identity so Windows remembers this dialog's last-visited folder
	/// separately from every other Open/Save dialog in the app — without an explicit ClientGuid,
	/// the common file dialog falls back to a shared default identity and all such dialogs end up
	/// remembering the same last folder. Shared between Open and Save As here since both deal with
	/// the same .sav file type, so remembering the same folder for both is the expected behavior.
	/// </summary>
	private static readonly Guid DialogClientGuid = new("9b52ab66-f6b6-4d5f-b24d-9640df321083");

	private void OnOpen(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Save files (*.sav)|*.sav|All files (*.*)|*.*",
			Title = "Open player save file",
			ClientGuid = DialogClientGuid
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] rawBytes = File.ReadAllBytes(dialog.FileName);
			var prefix = VolEntryPrefixCodec.StripIfPresent(rawBytes);
			var save = (PlayerSave?)_transformer.Parse(prefix.Content);

			if (save == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_loadedSave = save;
			_salvageInput.Value = Math.Clamp(save.SalvageTotal, (int)_salvageInput.Minimum, (int)_salvageInput.Maximum);

			var combos = WorkshopCombos;
			for (int i = 0; i < combos.Length; i++) {
				combos[i].SelectedItem = save.WorkshopSlots[i];
			}

			_hercUnlockRows.Clear();
			foreach (var herc in HercLUT.Values()) {
				if (herc.Id >= HercLUT.Mongoose.Id) {
					continue;
				}
				short val = save.UnlockedHercs.TryGetValue(herc, out var v) ? v : (short)0;
				_hercUnlockRows.Add(HercUnlockRow.FromLut(herc, val));
			}

			_squadmateRows.Clear();
			foreach (var squadmate in save.Squadmates ?? Array.Empty<PilotEntry>()) {
				_squadmateRows.Add(SquadmateRow.FromEntry(squadmate, isPlayer: false));
			}
			if (save.PlayerPilot != null) {
				_squadmateRows.Add(SquadmateRow.FromEntry(save.PlayerPilot, isPlayer: true));
			}

			_inventoryRows.Clear();
			foreach (var item in save.Inventory?.Items ?? Array.Empty<Inventory.InventoryItem>()) {
				_inventoryRows.Add(new InventoryRow {
					WeaponId = item.Id,
					Buildable = item.UnlockFlag != 0,
					Quantity = item.Quantity
				});
			}

			_hercBayRows.Clear();
			foreach (var kv in save.HercBay) {
				_hercBayRows.Add(new HercBayRow {
					BayId = kv.Key,
					Herc = kv.Value.Id,
					BuildPercent = kv.Value.BuildPercent,
					BuildStepNum = kv.Value.BuildStepNum,
					HardpointMax = kv.Value.HardpointMax,
					ActiveSocketCount = kv.Value.Weapons.Count,
					Entry = kv.Value
				});
			}

			_loadedPath = dialog.FileName;
			_originalCompressionType = prefix.HadPrefix ? prefix.CompressionType : null;
			_originalMagicPrefix = prefix.MagicPrefix;
			_originalHadTrailingByte = prefix.HadTrailingByte;

			string prefixNote = prefix.HadPrefix ? " (VOL entry prefix detected — will be preserved on save)" : "";
			_statusLabel.Text = $"Loaded {Path.GetFileName(dialog.FileName)}.{prefixNote}";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void OnHercBayCellClick(object? sender, DataGridViewCellEventArgs e) {
		if (e.RowIndex < 0 || e.ColumnIndex != _hercBayEditColumn.Index) {
			return;
		}

		var row = _hercBayRows[e.RowIndex];
		using var editor = new HercBayEditorForm(row.Entry, $"Edit Herc Bay {row.BayId} — {row.Herc?.Name ?? "(unassigned)"}");
		if (editor.ShowDialog(this) == DialogResult.OK) {
			row.ActiveSocketCount = row.Entry.Weapons.Count;
			_hercBayGrid.InvalidateRow(e.RowIndex);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_loadedSave == null) {
			MessageBox.Show(this, "Open a save file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "Save files (*.sav)|*.sav|All files (*.*)|*.*",
			Title = "Save player save file",
			FileName = _loadedPath == null ? "PLAYER.SAV" : Path.GetFileName(_loadedPath),
			ClientGuid = DialogClientGuid
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			_loadedSave.SalvageTotal = (int)_salvageInput.Value;

			var combos = WorkshopCombos;
			for (int i = 0; i < combos.Length; i++) {
				if (combos[i].SelectedItem is WeaponLUT selected) {
					_loadedSave.WorkshopSlots[i] = selected;
				}
			}

			// Recalculate WorkshopSpace to match the edited slots (occupied slots are anything
			// other than WeaponLUT.None) — previously this was left at whatever value the file had
			// on load, so changing a slot's contents without this could leave WorkshopSpace
			// inconsistent with the slots actually written out.
			int occupiedSlots = _loadedSave.WorkshopSlots.Count(w => w.Id != WeaponLUT.None.Id);
			_loadedSave.WorkshopSpace = (short)(_loadedSave.WorkshopSlots.Length - occupiedSlots);

			foreach (var row in _hercUnlockRows) {
				var herc = HercLUT.GetById(row.HercId);
				if (herc != null) {
					_loadedSave.UnlockedHercs[herc] = row.Unlocked ? (short)1 : (short)0;
				}
			}

			var squadRows = _squadmateRows.Where(r => !r.IsPlayer).ToArray();
			if (_loadedSave.Squadmates != null) {
				int count = Math.Min(squadRows.Length, _loadedSave.Squadmates.Length);
				for (int i = 0; i < count; i++) {
					squadRows[i].ApplyTo(_loadedSave.Squadmates[i]);
				}
			}
			var playerRow = _squadmateRows.FirstOrDefault(r => r.IsPlayer);
			if (playerRow != null && _loadedSave.PlayerPilot != null) {
				playerRow.ApplyTo(_loadedSave.PlayerPilot);
			}

			if (_loadedSave.Inventory?.Items != null) {
				int count = Math.Min(_inventoryRows.Count, _loadedSave.Inventory.Items.Length);
				for (int i = 0; i < count; i++) {
					ApplyInventoryRow(_inventoryRows[i], _loadedSave.Inventory.Items[i]);
				}
			}

			foreach (var row in _hercBayRows) {
				row.Entry.Id = row.Herc;
				row.Entry.BuildPercent = row.BuildPercent;
				row.Entry.BuildStepNum = row.BuildStepNum;
				row.Entry.HardpointMax = row.HardpointMax;
			}

			byte[] content = _transformer.Write(_loadedSave)!;
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

			MessageBox.Show(this,
				$"Saved in {formatNote}.",
				"Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to save file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	/// <summary>
	/// Applies an edited quantity by resizing the per-copy ShellWeaponEntry array: existing copies
	/// (and their individually-varying armor health / missile type) are preserved, new copies
	/// default to full health / no missile (see InventoryRow's doc comment), and a shrink just
	/// truncates from the end.
	/// </summary>
	private static void ApplyInventoryRow(InventoryRow row, Inventory.InventoryItem item) {
		item.UnlockFlag = row.Buildable ? (short)1 : (short)0;

		var oldData = item.Data ?? Array.Empty<ShellWeaponEntry>();
		int newQty = Math.Max((short)0, row.Quantity);
		var newData = new ShellWeaponEntry[newQty];

		for (int q = 0; q < newQty; q++) {
			if (q < oldData.Length) {
				newData[q] = oldData[q];
			} else {
				newData[q] = new ShellWeaponEntry {
					Id = row.WeaponId ?? item.Id,
					NameId = oldData.Length > 0 ? oldData[0].NameId : (short)0,
					HealthArmor = 100,
					HealthInteral = 100,
					MissileType = MissileType.None
				};
			}
		}

		item.Data = newData;
		item.Quantity = (short)newQty;
	}
}
