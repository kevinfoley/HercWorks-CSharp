using System.ComponentModel;
using HercWorks.Core.Data.File.Msn.Script;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.UI;

/// <summary>
/// Editor for <c>data\script.dat</c> — the file VSHELL writes after parsing a mission's <c>.msn</c>
/// and DBSIM reads to actually build the world, so this is the handoff that decides what a mission
/// contains and where it stands. Backed by HercWorks.Core's ScriptDatTransformer (byte-exact
/// round-trip verified against all 10 real sample files); see docs/formats/script-dat.md for the
/// format itself. Follows the same shape as CampaignResourcesForm: a tab per block, loose-file
/// Open/Save As, shared VolEntryPrefixCodec handling, layout in MissionScriptForm.Designer.cs.
///
/// <para><b>Records are edited in place, not added or removed.</b> Every block past the first two
/// addresses the others by array index — a group's member list indexes the Herc roster, a roster
/// slot's position indexes the point table — so inserting or deleting a record would silently
/// repoint every ref after it. The one exception is the Unlocks block, which nothing references and
/// which is therefore rebuilt wholesale from its grid.</para>
///
/// <para>Herc types are named, via <see cref="HercTypeOption"/>'s HercLUT-to-MECHS.NAM equivalence.
/// Flyer and base types stay raw indexes: their names live in <c>nam\FLYERS.NAM</c> and
/// <c>dat\BASES.DAT</c> inside the game's VOLs, which this form has no loaded VOL to resolve
/// against, and no equivalent hardcoded LUT exists for them.</para>
/// </summary>
public partial class MissionScriptForm : Form {
	private readonly ScriptDatTransformer _transformer = new();

	private readonly BindingList<ScriptPointRow> _pointRows = new();
	private readonly BindingList<ScriptHeadingRow> _headingRows = new();
	private readonly BindingList<ScriptRouteRow> _routeRows = new();
	private readonly BindingList<ScriptLinkRewardRow> _linkRows = new();
	private readonly BindingList<ScriptActionRow> _actionRows = new();
	private readonly BindingList<ScriptActionPairRow> _actionPairRows = new();
	private readonly BindingList<ScriptMechRow> _mechRows = new();
	private readonly BindingList<ScriptFlyerRow> _flyerRows = new();
	private readonly BindingList<ScriptBaseRow> _baseRows = new();
	private readonly BindingList<ScriptRouteLinkRow> _routeLinkRows = new();
	private readonly BindingList<ScriptGroupRow> _groupRows = new();
	private readonly BindingList<ScriptEntityLinkRow> _entityLinkRows = new();
	private readonly BindingList<ScriptUnlockRow> _unlockRows = new();

	private ScriptDat? _loaded;
	private string? _loadedPath;

	/// <summary>Original VOL entry prefix, round-tripped on save — see VolEntryPrefixCodec.</summary>
	private byte? _originalCompressionType;
	private byte[]? _originalMagicPrefix;
	private bool _originalHadTrailingByte;

	public MissionScriptForm() {
		InitializeComponent();

		_pointsGrid.DataSource = _pointRows;
		_headingsGrid.DataSource = _headingRows;
		_routesGrid.DataSource = _routeRows;
		_linksGrid.DataSource = _linkRows;
		_actionsGrid.DataSource = _actionRows;
		_actionPairsGrid.DataSource = _actionPairRows;
		_mechsGrid.DataSource = _mechRows;
		_flyersGrid.DataSource = _flyerRows;
		_basesGrid.DataSource = _baseRows;
		_routeLinksGrid.DataSource = _routeLinkRows;
		_groupsGrid.DataSource = _groupRows;
		_entityLinksGrid.DataSource = _entityLinkRows;
		_unlocksGrid.DataSource = _unlockRows;
	}

	private void OnClose(object? sender, EventArgs e) => Close();

	/// <summary>
	/// Distinct per-form/file-type identity so Windows remembers this dialog's last-visited folder
	/// separately from every other Open/Save dialog in the app — see CampaignResourcesForm's
	/// DialogClientGuid for the full explanation. Shared between Open and Save As here since both
	/// deal with the same script.dat file type.
	/// </summary>
	private static readonly Guid DialogClientGuid = new("6c1e0d54-3a7b-4f92-8c1d-2f5b7a9e4d31");

	/// <summary>
	/// The copy DBSIM actually reads, relative to the game directory — opened automatically so the
	/// editor starts on the live mission rather than an empty grid.
	/// </summary>
	private static readonly string[] DefaultFile = { "DATA", "SCRIPT.DAT" };

	protected override void OnLoad(EventArgs e) {
		base.OnLoad(e);

		if (GamePaths.Resolve(DefaultFile) is { } path) {
			LoadFile(path);
		}
	}

	private void OnOpen(object? sender, EventArgs e) {
		using var dialog = new OpenFileDialog {
			Filter = "Mission script files (script*.dat)|script*.dat|DAT files (*.dat)|*.dat|All files (*.*)|*.*",
			Title = "Open mission script file",
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
			var script = (ScriptDat?)_transformer.BytesToObject(prefix.Content);

			if (script == null) {
				MessageBox.Show(this, "File was empty or could not be parsed.", "Error",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			_loaded = script;
			Populate(script);

			_loadedPath = path;
			_originalCompressionType = prefix.HadPrefix ? prefix.CompressionType : null;
			_originalMagicPrefix = prefix.MagicPrefix;
			_originalHadTrailingByte = prefix.HadTrailingByte;

			string prefixNote = prefix.HadPrefix ? " (VOL entry prefix detected — will be preserved on save)" : "";
			_statusLabel.Text =
				$"Loaded {Path.GetFileName(path)} — {script.Coordinates.Length} points, " +
				$"{script.SpawnRecords.Length} hercs, {script.Entities102.Length} flyers, " +
				$"{script.MiscEntities.Length} bases, {script.Entities164.Length} groups.{prefixNote}";
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void Populate(ScriptDat script) {
		_theaterInput.Value = Clamp(ReadHeaderShort(script, 0), _theaterInput);
		_zoneInput.Value = Clamp(ReadHeaderShort(script, 2), _zoneInput);
		_variantInput.Value = Clamp(ReadHeaderShort(script, 18), _variantInput);
		_headerRawText.Text = string.Join(" ", script.HeaderBytes.Select(b => b.ToString("X2")));
		UpdateWorldLabel();

		Refill(_pointRows, script.Coordinates, (src, i) => new ScriptPointRow { Index = i, Source = src });
		Refill(_headingRows, script.Headings, (src, i) => new ScriptHeadingRow { Index = i, Source = src });
		Refill(_routeRows, script.WaypointGroups, (src, i) => new ScriptRouteRow { Index = i, Source = src });
		Refill(_linkRows, script.LinksOrRewards, (src, i) => new ScriptLinkRewardRow { Index = i, Source = src });
		Refill(_actionRows, script.Actions, (src, i) => new ScriptActionRow { Index = i, Source = src });
		Refill(_actionPairRows, script.ActionPairs, (src, i) => new ScriptActionPairRow { Index = i, Source = src });
		// A combo column rejects a value it has no item for, so any type the file carries that
		// MECHS.NAM has no name for needs an entry in the list first. Order matters both ways: the
		// rows still bound here are the previously loaded file's, whose types the new list may not
		// cover, so they have to go before the swap.
		_mechRows.Clear();
		_mechTypeColumn.DataSource = HercTypeOption.Build(script.SpawnRecords.Select(r => r.SmallDiscrete));

		Refill(_mechRows, script.SpawnRecords, (src, i) => new ScriptMechRow { Index = i, Source = src });
		Refill(_flyerRows, script.Entities102, (src, i) => new ScriptFlyerRow { Index = i, Source = src });
		Refill(_baseRows, script.MiscEntities, (src, i) => new ScriptBaseRow { Index = i, Source = src });
		Refill(_routeLinkRows, script.LinkedRefs22, (src, i) => new ScriptRouteLinkRow { Index = i, Source = src });
		Refill(_groupRows, script.Entities164, (src, i) => new ScriptGroupRow { Index = i, Source = src });
		Refill(_entityLinkRows, script.LinkedRefs58, (src, i) => new ScriptEntityLinkRow { Index = i, Source = src });

		_unlockRows.Clear();
		foreach (short value in script.UnlockedLutRefs) {
			_unlockRows.Add(new ScriptUnlockRow { Value = value });
		}
	}

	private static void Refill<TSource, TRow>(BindingList<TRow> rows, TSource[] source, Func<TSource, int, TRow> makeRow) {
		rows.Clear();
		for (int i = 0; i < source.Length; i++) {
			rows.Add(makeRow(source[i], i));
		}
	}

	private static short ReadHeaderShort(ScriptDat script, int offset) =>
		script.HeaderBytes.Length >= offset + 2 ? BitConverter.ToInt16(script.HeaderBytes, offset) : (short)0;

	private static decimal Clamp(short value, NumericUpDown input) =>
		Math.Clamp(value, input.Minimum, input.Maximum);

	/// <summary>
	/// The theater/variant pair selects a world file by <c>theater * 2 + variant</c> — showing the
	/// resolved name makes an edit here checkable against the texture bank it actually picks.
	/// </summary>
	private void OnWorldSelectionChanged(object? sender, EventArgs e) => UpdateWorldLabel();

	private void UpdateWorldLabel() =>
		_worldValueLabel.Text = $"wld\\world{(int)_theaterInput.Value * 2 + (int)_variantInput.Value}.wld";

	/// <summary>Waypoint lists are variable-length, so the count column has to follow the edit.</summary>
	private void OnRouteCellChanged(object? sender, DataGridViewCellEventArgs e) {
		if (e.RowIndex >= 0) {
			_routesGrid.InvalidateRow(e.RowIndex);
		}
	}

	/// <summary>
	/// Rejected cell edits surface here — the row property setters throw FormatException for a
	/// malformed or wrong-length ref list. Report and keep the old value rather than letting
	/// WinForms rethrow into an unhandled crash.
	/// </summary>
	private void OnGridDataError(object? sender, DataGridViewDataErrorEventArgs e) {
		e.ThrowException = false;
		e.Cancel = true;
		MessageBox.Show(this, e.Exception?.Message ?? "That value could not be applied.",
			"Invalid value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
	}

	private void OnAddUnlock(object? sender, EventArgs e) => _unlockRows.Add(new ScriptUnlockRow());

	private void OnRemoveUnlock(object? sender, EventArgs e) {
		if (_unlocksGrid.CurrentRow?.DataBoundItem is ScriptUnlockRow row) {
			_unlockRows.Remove(row);
		}
	}

	private void OnSaveAs(object? sender, EventArgs e) {
		if (_loaded == null) {
			MessageBox.Show(this, "Open a script.dat file first.", "Nothing to save",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		// Commit whatever cell is still being edited — grid edits write straight through to the
		// model, but only once the cell is committed.
		_tabs.SelectedTab?.Controls.OfType<DataGridView>().ToList()
			.ForEach(grid => grid.EndEdit());

		ApplyHeader(_loaded);
		_loaded.UnlockedLutRefs = _unlockRows.Select(r => r.Value).ToArray();

		var warnings = Validate(_loaded);
		if (warnings.Count > 0 && !ConfirmDespiteWarnings(warnings)) {
			return;
		}

		using var dialog = new SaveFileDialog {
			Filter = "Mission script files (*.dat)|*.dat|All files (*.*)|*.*",
			Title = "Save mission script file",
			FileName = _loadedPath == null ? "SCRIPT.DAT" : Path.GetFileName(_loadedPath),
			ClientGuid = DialogClientGuid,
			InitialDirectory = _loadedPath == null
				? GamePaths.InitialDirectoryFor("DATA")
				: Path.GetDirectoryName(_loadedPath)!
		};

		if (dialog.ShowDialog(this) != DialogResult.OK) {
			return;
		}

		try {
			byte[] content = _transformer.ObjectToBytes(_loaded)!;
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

			// The retail file is a fixed 13,520-byte preallocated buffer whose tail is stale
			// leftovers; DBSIM stops at block 13's declared end and ignores the rest, so the shorter
			// unpadded write is correct — worth saying, since the size differing from retail's is
			// otherwise an alarming thing to notice.
			MessageBox.Show(this,
				$"Saved in {formatNote}.\n\n" +
				$"Written as {outBytes.Length:N0} bytes — the game's own files are padded out to a fixed " +
				"13,520-byte buffer with stale trailing data, which readers stop short of and ignore.",
				"Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
		} catch (Exception ex) {
			MessageBox.Show(this, $"Failed to save file:\n{ex.Message}", "Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	/// <summary>
	/// Writes the three decoded header fields back into the raw 20 bytes, leaving every other short
	/// exactly as loaded (they are constant across the whole real corpus and undecoded).
	/// </summary>
	private void ApplyHeader(ScriptDat script) {
		WriteHeaderShort(script, 0, (short)_theaterInput.Value);
		WriteHeaderShort(script, 2, (short)_zoneInput.Value);
		WriteHeaderShort(script, 18, (short)_variantInput.Value);
		_headerRawText.Text = string.Join(" ", script.HeaderBytes.Select(b => b.ToString("X2")));
	}

	private static void WriteHeaderShort(ScriptDat script, int offset, short value) {
		if (script.HeaderBytes.Length >= offset + 2) {
			BitConverter.GetBytes(value).CopyTo(script.HeaderBytes, offset);
		}
	}

	/// <summary>
	/// Cross-block ref sanity check. Every ref is an index into another block's array (or -1 for
	/// "unset"), and an out-of-range one is exactly the kind of edit that produces a mission DBSIM
	/// reads off the end of its own tables — but this stays advisory rather than blocking, since
	/// nothing here has been proven to be the game's own validation rule.
	/// </summary>
	private static List<string> Validate(ScriptDat script) {
		var warnings = new List<string>();

		for (int i = 0; i < script.WaypointGroups.Length; i++) {
			CheckRefs(warnings, $"Route {i} waypoints", script.WaypointGroups[i].Waypoints, script.Coordinates.Length, "points");
		}

		for (int i = 0; i < script.Actions.Length; i++) {
			CheckRefs(warnings, $"Action {i} link refs", script.Actions[i].RefsRow9, script.LinksOrRewards.Length, "links/rewards");
		}

		for (int i = 0; i < script.ActionPairs.Length; i++) {
			var pair = script.ActionPairs[i];
			CheckRef(warnings, $"Action pair {i} action ref", pair.PrimaryActionRef, script.Actions.Length, "actions");
			CheckRefs(warnings, $"Action pair {i} sequence refs", pair.SequenceRefs, script.Actions.Length, "actions");
		}

		for (int i = 0; i < script.SpawnRecords.Length; i++) {
			CheckRef(warnings, $"Herc {i} point ref", script.SpawnRecords[i].PositionRef, script.Coordinates.Length, "points");
			CheckRef(warnings, $"Herc {i} heading ref", script.SpawnRecords[i].HeadingRef, script.Headings.Length, "headings");
		}

		for (int i = 0; i < script.Entities102.Length; i++) {
			CheckRef(warnings, $"Flyer {i} point ref", script.Entities102[i].PositionRef, script.Coordinates.Length, "points");
			CheckRef(warnings, $"Flyer {i} heading ref", script.Entities102[i].HeadingRef, script.Headings.Length, "headings");
		}

		for (int i = 0; i < script.MiscEntities.Length; i++) {
			CheckRef(warnings, $"Base {i} point ref", script.MiscEntities[i].PositionRef, script.Coordinates.Length, "points");
			CheckRef(warnings, $"Base {i} heading ref", script.MiscEntities[i].HeadingRef, script.Headings.Length, "headings");
		}

		for (int i = 0; i < script.Entities164.Length; i++) {
			var group = script.Entities164[i];
			CheckRef(warnings, $"Group {i} point ref", group.RefRow6, script.Coordinates.Length, "points");
			CheckRef(warnings, $"Group {i} heading ref", group.RefRow7, script.Headings.Length, "headings");
			CheckRef(warnings, $"Group {i} route ref", group.RefRow8, script.WaypointGroups.Length, "routes");
			CheckRef(warnings, $"Group {i} action ref", group.RefRow10, script.Actions.Length, "actions");
			CheckRefs(warnings, $"Group {i} route link refs", group.Row15Refs, script.LinkedRefs22.Length, "route links");

			// Record 0 is the player squad placeholder: DBSIM never reads its member list (it fills
			// the squad from data\player.mec instead), so whatever indexes it carries are inert.
			if (i == 0) {
				continue;
			}

			(int rosterCount, string rosterName) = group.Discriminator switch {
				0 => (script.SpawnRecords.Length, "hercs"),
				1 => (script.Entities102.Length, "flyers"),
				2 => (script.MiscEntities.Length, "bases"),
				_ => (-1, "")
			};

			if (rosterCount < 0) {
				warnings.Add($"Group {i} roster is {group.Discriminator} — only 0 (hercs), 1 (flyers) and 2 (bases) exist.");
				continue;
			}

			CheckRefs(warnings, $"Group {i} member slots", group.DiscriminatedRefs, rosterCount, rosterName);
		}

		return warnings;
	}

	private static void CheckRefs(List<string> warnings, string label, short[] refs, int count, string target) {
		foreach (short value in refs) {
			CheckRef(warnings, label, value, count, target);
		}
	}

	private static void CheckRef(List<string> warnings, string label, short value, int count, string target) {
		if (value >= count) {
			warnings.Add($"{label}: {value} is past the end of the {count} {target}.");
		} else if (value < -1) {
			warnings.Add($"{label}: {value} is not a valid index (-1 means unset).");
		}
	}

	private bool ConfirmDespiteWarnings(List<string> warnings) {
		const int shown = 12;
		string detail = string.Join("\n", warnings.Take(shown));
		if (warnings.Count > shown) {
			detail += $"\n… and {warnings.Count - shown} more.";
		}

		return MessageBox.Show(this,
			$"{warnings.Count} reference(s) point outside the block they index:\n\n{detail}\n\nSave anyway?",
			"Reference check", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
	}
}
