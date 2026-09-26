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
	private readonly BindingList<CampaignFlagRow> _flagRows = new();

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

		_sqSkillColumn.Items.AddRange(PilotSkill.Values().Select(s => s.Label).Cast<object>().ToArray());
		_sqRankColumn.Items.AddRange(PilotRank.Values().Select(r => r.Label).Cast<object>().ToArray());
		_hercBayHercColumn.Items.AddRange(HercLUT.Values().Cast<object>().ToArray());

		_hercUnlocksGrid.DataSource = _hercUnlockRows;
		_squadmatesGrid.DataSource = _squadmateRows;
		_inventoryGrid.DataSource = _inventoryRows;
		_hercBayGrid.DataSource = _hercBayRows;
		_flagsGrid.DataSource = _flagRows;
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

			LoadCareer(save);

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

	/// <summary>
	/// The career block's named fields, the two squad counts, the game state and the flag array. A
	/// save whose tail is too short for the flags (none in retail) leaves those disabled.
	/// </summary>
	private void LoadCareer(PlayerSave save) {
		_stageInput.Value = save.CampaignStage;
		_missionInput.Value = save.MissionInStage;
		_squadPositionsInput.Value = save.SquadPositionsInPlay;
		_onStrengthInput.Value = save.MachinesOnStrength;

		_careerTextBox.Text = string.Join(Environment.NewLine,
			$"Squad members (record index per squad): {save.SquadMemberIndex(0)}, {save.SquadMemberIndex(1)}, {save.SquadMemberIndex(2)}",
			"",
			"Briefing/debrief mission.str line indices (count: lines):",
			FormatCareerText(save.CareerTextA),
			FormatCareerText(save.CareerTextB),
			FormatCareerText(save.CareerTextC));

		_flagRows.Clear();
		bool hasFlags = save.HasCampaignState;
		_gameStateInput.Enabled = hasFlags;
		_flagsGrid.Enabled = hasFlags;
		if (hasFlags) {
			_gameStateInput.Value = save.GameState;
			for (int i = 0; i < PlayerSave.CampaignFlagCount; i++) {
				_flagRows.Add(new CampaignFlagRow { Index = i, Value = save.GetCampaignFlag(i) });
			}
		}
	}

	private static string FormatCareerText((short Count, short[] Lines) text) =>
		$"{text.Count}: {string.Join(", ", text.Lines)}";

	private void ApplyCareer(PlayerSave save) {
		save.CampaignStage = (short)_stageInput.Value;
		save.MissionInStage = (short)_missionInput.Value;
		save.SquadPositionsInPlay = (short)_squadPositionsInput.Value;
		save.MachinesOnStrength = (short)_onStrengthInput.Value;

		if (save.HasCampaignState) {
			save.GameState = (short)_gameStateInput.Value;
			foreach (var row in _flagRows) {
				save.SetCampaignFlag(row.Index, row.Value);
			}
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

	/// <summary>
	/// A new chassis brings its own mount capacity, which the save stores beside the type and the
	/// shell writer loops to — so it follows the type rather than keeping the old chassis's. It stays
	/// editable afterwards.
	/// </summary>
	private void OnHercBayCellValueChanged(object? sender, DataGridViewCellEventArgs e) {
		if (e.RowIndex < 0 || e.ColumnIndex != _hercBayHercColumn.Index) {
			return;
		}

		var row = _hercBayRows[e.RowIndex];
		if (row.Herc != null) {
			row.HardpointMax = row.Herc.HardpointMax;
			_hercBayGrid.InvalidateRow(e.RowIndex);
		}
	}

	/// <summary>
	/// Mounts at or past a bay's capacity: the shell's own save writer loops only to the capacity, so
	/// the game drops them the next time it saves.
	/// </summary>
	private List<string> MountsPastCapacity() =>
		_hercBayRows
			.SelectMany(row => row.Entry.Weapons.Keys
				.Where(socket => socket >= row.HardpointMax)
				.Select(socket => $"Bay {row.BayId}: weapon in socket {socket}, capacity {row.HardpointMax}."))
			.ToList();

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_loadedSave == null) {
			MessageBox.Show(this, "Open a save file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		_hercBayGrid.EndEdit();

		var pastCapacity = MountsPastCapacity();
		if (pastCapacity.Count > 0 && MessageBox.Show(this,
				"These weapons sit in sockets the machine does not have. The game will drop them the next " +
				"time it saves:\n\n" + string.Join("\n", pastCapacity) + "\n\nSave anyway?",
				"Mount capacity", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) {
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
			_flagsGrid.EndEdit();
			ApplyCareer(_loadedSave);

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
				// The record stores the type twice (+0x00, and +0x02 through VSHELL's identity map
				// over 0-8), and the shell takes the name and the stats from +0x02 — so a changed
				// chassis has to land in both.
				if (row.Herc != null) {
					row.Entry.NameId = row.Herc.Id;
				}
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
