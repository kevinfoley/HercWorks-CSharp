using Herculan.Engine.Content;
using Herculan.Engine.Shell;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Host;

/// <summary>
/// Runs the front end instead of a mission — <c>--shell</c>. The same thin-host arrangement the
/// mission loop uses (docs/engine/planning.md, "Engine internal architecture"): everything here is
/// wiring, and every rule about what the shell looks like and where its widgets are lives in
/// <c>Herculan.Engine.Shell</c>.
///
/// <para>It is a separate entry point rather than a mode of the mission loop because the two share
/// nothing: the shell mounts different archives, loads no zone, runs no simulation and needs no fixed
/// timestep. In the retail game they are two executables for the same reason
/// (docs/shell/campaign-loop.md).</para>
///
/// <para>The pointer is polled once per update rather than queued from the device's own events, which
/// is the opposite of what the cockpit does. The cockpit's queue is reproducing DBSIM's own deferred
/// mouse handling (docs/formats/cockpit-input.md); nothing has been read yet that says VSHELL does
/// the same, so this stays the simple thing until it is.</para>
/// </summary>
static class ShellHost {
	/// <summary>
	/// Frames to let pass before <c>--screenshot</c> fires. The shell has nothing to settle — no
	/// simulation, no streaming — but the window manager can hand back a stale or part-sized
	/// framebuffer for the first frame or two, so the capture waits the same short beat the mission
	/// host waits.
	/// </summary>
	private const int ScreenshotFrame = 5;

	public static int Run(string installRoot, string? paletteName, string? screenshotPath = null,
			ShellCampaignMode mode = ShellCampaignMode.Campaign, int startTab = ShellScreen.MainMenuTab,
			int startBay = 0) {
		var content = GameContent.Mount(GameInstall.ArchiveDirectory(installRoot), ShellArt.Archives);
		Console.WriteLine($"Mounted archives: {string.Join(", ", content.MountedArchives)}");

		// The mission tab's palette depends on the campaign stage, counted from one, and on which of its
		// views the tab opens: the map while the campaign map's once-per-load flag (DAT_004778aa) is
		// clear and the mission-within-stage counter is zero, the briefing otherwise
		// (TabHandler_Mission, 0043a6ca). All three come from the loaded game below.
		int campaignStage = 1;
		int missionInStage = 0;
		bool missionMapShown = false;

		// Reassigned when a tab switches palette, since the art is decoded through one palette at load
		// rather than re-mapped per frame — see SwitchPalette.
		string startPalette = PaletteFor(startTab);
		if (ShellArt.Load(content, startPalette) is not { } loaded) {
			Console.Error.WriteLine(
				$"Could not load the shell's art from {GameInstall.ArchiveDirectory(installRoot)}.\n" +
				$"It needs {string.Join(" and ", ShellArt.Archives)}, a "
				+ $"dpl\\{startPalette}.DPL palette and "
				+ $"dbm\\{ShellArt.BackdropName}.DBM.");
			return 1;
		}

		var art = loaded;

		Console.WriteLine(
			$"Shell art loaded — {ShellArt.BackdropName} backdrop {art.Backdrop.Width}x{art.Backdrop.Height}, "
			+ $"palette {art.PaletteName}. "
			+ (art.Sprites is { } sheet
				? $"Banks and fonts: {string.Join(", ", sheet.BankNames)} in a "
				  + $"{sheet.Atlas.Width}x{sheet.Atlas.Height} atlas."
				: "No sprite banks or fonts could be loaded — backdrop only."));

		// The save screen reads real files: sav\GAMEFILE.STR for the slot list and each GAME_?.SAV it
		// says is in use for that slot's summary. Both are loose files beside the VOL folder rather than
		// archive entries, so they are read from the install root and not through GameContent.
		var slots = ShellSaveSlots.Load(installRoot, art.Text);
		var saveScreen = new ShellSaveScreen(slots);
		var contentSurface = new ShellSurface();
		Console.WriteLine(slots.Count > 0
			? $"Save slots: {slots.Count(s => s.InUse)} of {slots.Count} in use — "
			  + string.Join(", ", slots.Take(ShellSaveScreen.RowCount).Select(s => s.Label.Trim()))
			: $"No {ShellSaveSlots.DirectoryFileName} in {ShellSaveSlots.Directory(installRoot)} — "
			  + "the save screen draws its furniture and no rows.");

		// The repair screen works over a loaded game, which the original only has once one is started or
		// restored. Until the save screen's RESTORE replaces it, this opens the first slot the directory
		// marks in use — enough to put a real machine's damage on the screen, and stated rather than
		// hidden.
		var loadedGame = slots.FirstOrDefault(s => s.InUse) is { } inUse
			? ShellSaveSlots.LoadSave(installRoot, inUse.FileName) : null;
		var hangar = ShellHangar.From(loadedGame);
		if (loadedGame != null) {
			campaignStage = loadedGame.CampaignStage + 1;
			missionInStage = loadedGame.MissionInStage;
		}

		// The art was loaded before the game, so a start on the mission tab took stage 1's palette.
		if (!string.Equals(PaletteFor(startTab), art.PaletteName, StringComparison.OrdinalIgnoreCase)
				&& ShellArt.Load(content, PaletteFor(startTab)) is { } staged) {
			art = staged;
		}

		var repairCosts = ShellRepairCosts.Load(content);
		var repairDiagrams = ShellRepairDiagrams.Load(content);
		var repairScreen = new ShellRepairScreen(hangar, repairCosts, startBay, repairDiagrams);
		Console.WriteLine($"Damage diagram layouts loaded for {repairDiagrams.LayoutCount} chassis.");

		// The squad panel's three-quarter view, which the crew tab shows (and WEAPONS and BUILD would), and
		// the crew screen's two portrait banks. The crew screen is built on first entry and kept, as the
		// original's widgets are; each entry re-runs its entry routine.
		var bayPictures = ShellBayPictures.Load(content);
		var crewPortraits = ShellCrewPortraits.Load(content);
		ShellCrewScreen? crewScreen = null;
		Console.WriteLine($"Bay picture layouts loaded for {bayPictures.LayoutCount} chassis.");
		Console.WriteLine(repairCosts == null
			? $"No gam\\{ShellRepairCosts.ValuesResourceName} or gam\\{ShellRepairCosts.ChassisResourceName}"
			  + " — the repair screen draws its labels and no cost figures."
			: $"Repair costs loaded for {repairCosts.ChassisCount} chassis.");

		for (int bay = 0; bay < ShellHangar.BayCount; bay++) {
			if (hangar.Bay(bay) is not { } machine) {
				continue;
			}

			Console.WriteLine($"  bay {bay}: chassis type {machine.ChassisType}, "
				+ $"{machine.MountCapacity} hardpoints, {machine.BuildPercent}% built"
				+ (machine.IsFlightworthy ? string.Empty : ", not flightworthy")
				+ $", rebuild {repairCosts?.HercCost(machine) ?? 0} kg");
		}

		Console.WriteLine(repairScreen.SelectedBay >= 0
			? $"Repair opens on bay {repairScreen.SelectedBay}, "
			  + $"{repairScreen.AvailableKilograms} kg available."
			: "No built machine in any hangar bay — the repair screen draws empty rows.");

		var screen = ShellScreen.CreateFrame(art.Text, startTab, mode);
		EnterTab(screen.SelectedTab);

		Console.WriteLine(art.Text != null
			? $"Tabs: {string.Join(", ", screen.Buttons.Where(b => b.Id < ShellLayout.TabCount && b.Caption != null).Select(b => b.Caption))}"
			: "No estext.bin — the tabs draw their plates and no captions.");
		Console.WriteLine(mode == ShellCampaignMode.Training
			? "Training campaign: REPAIR, BUILD and ARMORY are gated off, as the strip refresh gates them."
			: "Campaign: every tab is live.");
		Console.WriteLine("SAVE, REPAIR and CREW are the tabs with a screen behind them. Click a save slot "
			+ "row, or a repair list row, a part of the damage diagram or a Squad Inventory row, to select "
			+ "it and the panels beside it follow. On CREW, click a row to select it, then a squad portrait "
			+ "to put that pilot in the row, a Squad Inventory row to give the row's pilot that bay, or "
			+ "CLEAR to empty the row. The other five tabs latch and show the frame. The save screen hides "
			+ "the strip, as the original's does: leave it with EXIT, or RESTORE a slot to load it into the "
			+ "repair screen. Close the window to quit.");
		Console.WriteLine(paletteName != null
			? $"Palette pinned to {art.PaletteName} on every tab."
			: "Each tab installs its own palette, as the original's do.");

		using var window = new EngineWindow("HERCULAN Engine — shell");

		ShellRenderer? renderer = null;
		GL? gl = null;
		IMouse? mouse = null;
		bool pointerHeld = false;
		int framesRendered = 0;

		window.Load += (loadedGl, input) => {
			gl = loadedGl;
			renderer = new ShellRenderer(loadedGl, art);
			mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
			RepaintContent();
		};

		window.Update += _ => {
			if (mouse == null) {
				return;
			}

			// The pointer reports window-client pixels while the canvas is placed in framebuffer pixels,
			// which differ on a scaled display — the same correction the mission host makes.
			var client = window.ClientSize;
			var framebuffer = window.FramebufferSize;
			float windowX = mouse.Position.X * framebuffer.X / Math.Max(client.X, 1);
			float windowY = mouse.Position.Y * framebuffer.Y / Math.Max(client.Y, 1);

			var layout = ShellScreenLayout.Create(framebuffer.X, framebuffer.Y);
			var (canvasX, canvasY) = layout.WindowToCanvas(windowX, windowY);

			bool held = mouse.IsButtonPressed(MouseButton.Left);
			if (held && !pointerHeld) {
				screen.PointerDown(canvasX, canvasY);
			} else if (!held && pointerHeld) {
				// The strip gets first refusal: it is drawn over the content and its buttons are the only
				// ones ShellScreen tracks. A release the strip does not claim falls through to whatever
				// tab is up.
				if (screen.PointerUp(canvasX, canvasY) is { } activated) {
					Activate(activated);
				} else {
					ClickContent(canvasX, canvasY);
				}
			} else {
				screen.PointerMoved(canvasX, canvasY);
			}

			pointerHeld = held;
		};

		window.Render += (_, frameGl) => {
			// Black behind the canvas: the shell is a fixed 640x480 layout scaled to the window, so a
			// window that is not 4:3 has margin left over and the original has nothing to put in it.
			frameGl.ClearColor(0f, 0f, 0f, 1f);
			frameGl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			var framebuffer = window.FramebufferSize;
			renderer?.Draw(ShellScreenLayout.Create(framebuffer.X, framebuffer.Y), screen);

			framesRendered++;
			if (screenshotPath != null && framesRendered == ScreenshotFrame) {
				Screenshot.Capture(frameGl, framebuffer.X, framebuffer.Y, screenshotPath);
				window.Close();
			}
		};

		window.Closing += () => {
			renderer?.Dispose();
			renderer = null;
		};

		window.Run();
		return 0;

		void Activate(int id) {
			if (id == ShellScreen.MenuButtonId) {
				Console.WriteLine("Menu button — no screen behind it yet.");
				return;
			}

			// Clicking the tab you are already on is a no-op in the original: every handler returns
			// early when DAT_0047581c already holds its own index, before the teardown, the palette and
			// the click sound.
			if (id == screen.SelectedTab) {
				return;
			}

			// Tab 1's handler writes where EXIT goes before it enters the screen.
			if (id == ShellScreen.SaveTab) {
				saveScreen.ExitTarget = ShellSaveExitTarget.TabStrip;
			}

			screen.SelectTab(id);
			Console.WriteLine($"Tab {id}"
				+ (screen.Button(id)?.Caption is { } caption ? $" ({caption})" : string.Empty)
				+ (HasScreen(id) ? "." : " — no screen behind it yet."));

			SwitchPalette(id);
			EnterTab(id);
			RepaintContent();
		}

		// What a tab's entry does beyond showing it. The mission tab's map view sets the campaign map's
		// once-per-load flag (maybe_Mission_Show, 004441e3), so the next visit opens the briefing.
		void EnterTab(int id) {
			if (id == ShellScreen.CrewTab) {
				EnterCrew();
			} else if (id == ShellScreen.MissionTab && MissionView() == ShellMissionView.Map) {
				missionMapShown = true;
			}
		}

		// A click the strip did not take, handed to the tab that is up.
		void ClickContent(float canvasX, float canvasY) {
			if (screen.SelectedTab == ShellScreen.RepairTab) {
				ClickRepair(canvasX, canvasY);
				return;
			}

			if (screen.SelectedTab == ShellScreen.CrewTab) {
				ClickCrew(canvasX, canvasY);
				return;
			}

			if (screen.SelectedTab != ShellScreen.SaveTab) {
				return;
			}

			if (saveScreen.RowAt(canvasX, canvasY) is { } slot) {
				// Clicking the row already selected is a no-op, the same early return the original's
				// selection move opens with.
				if (slot == saveScreen.SelectedSlot) {
					return;
				}

				saveScreen.SelectSlot(slot);
				RepaintContent();
				Console.WriteLine($"Slot {slot + 1}: "
					+ (saveScreen.Slots.ElementAtOrDefault(slot) is { InUse: true, Summary: { } summary }
						? $"{summary.PilotName}, sector {summary.Sector}, mission {summary.Mission + 1}, "
						  + $"{summary.SalvageKilograms} kg salvage"
						: "empty."));
				return;
			}

			switch (saveScreen.ButtonAt(canvasX, canvasY)) {
				case ShellSaveButton.Restore:
					RestoreSelectedSlot();
					break;
				case ShellSaveButton.Exit:
					LeaveSaveScreen();
					break;
				case { } button:
					Console.WriteLine($"{button} — the button is live and its action is not ported yet.");
					break;
			}
		}

		// RESTORE, SaveScreen_OnRestore (00437d03): load the selected slot, then leave exactly as EXIT does on the tab-strip
		// path, whichever way the screen was entered. The original also writes the loaded game straight
		// back out as the slot-10 autosave and clears the campaign map's intro flag (DAT_004778aa); this
		// engine has no save writer and no campaign map yet, so neither has a counterpart here.
		void RestoreSelectedSlot() {
			int slot = saveScreen.SelectedSlot;
			if (saveScreen.Slots.ElementAtOrDefault(slot) is not { InUse: true } entry
					|| ShellSaveSlots.LoadSave(installRoot, entry.FileName) is not { } restored) {
				Console.WriteLine($"Slot {slot + 1} could not be read — nothing restored.");
				return;
			}

			hangar = ShellHangar.From(restored);
			campaignStage = restored.CampaignStage + 1;
			missionInStage = restored.MissionInStage;
			missionMapShown = false;
			repairScreen = new ShellRepairScreen(hangar, repairCosts, startBay, repairDiagrams);
			saveScreen.CanSave = true;
			Console.WriteLine($"Restored slot {slot + 1} ({entry.FileName}): "
				+ (ShellSaveSummary.From(restored) is { } summary
					? $"{summary.PilotName}, sector {summary.Sector}, mission {summary.Mission + 1}."
					: "no pilot record.")
				+ (repairScreen.SelectedBay >= 0
					? $" Repair opens on bay {repairScreen.SelectedBay}."
					: " No built machine in any hangar bay."));

			saveScreen.Leave();
			ReturnToFrame();
		}

		// EXIT, SaveScreen_OnExit (00437d94): the teardown, then wherever the handler that entered the
		// screen said to go.
		void LeaveSaveScreen() {
			saveScreen.Leave();
			if (saveScreen.ExitTarget == ShellSaveExitTarget.MainMenu) {
				screen.SelectTab(ShellScreen.MainMenuTab);
				Console.WriteLine("Exit — main menu.");
				RepaintContent();
				return;
			}

			ReturnToFrame();
		}

		void ReturnToFrame() {
			screen.ReturnToFrame(mode);
			Console.WriteLine("Back to the tab strip, no tab up.");
			RepaintContent();
		}

		// The repair screen's own clicks: a row or a hotspot moves the selection and the detail panel
		// follows, which is the whole of what the original's Repair_SelectHotspot (00433eb9) does before its own refill; a
		// roster row moves the bay, Squad_SelectBay (0043d64d)'s repair-tab arm.
		void ClickRepair(float canvasX, float canvasY) {
			if (ShellSquadPanel.RowAt(canvasX, canvasY) is { } bay) {
				if (repairScreen.SelectBay(bay)) {
					RepaintContent();
					Console.WriteLine($"Bay {bay}: chassis type {repairScreen.Machine?.ChassisType}.");
				}

				return;
			}

			if (repairScreen.RowAt(canvasX, canvasY) is { } cell) {
				// A hardpoint row past the machine's capacity, or one holding no weapon, refuses the
				// selection outright — nothing moves and nothing repaints.
				if (!repairScreen.Select(cell.Column, cell.Row)) {
					return;
				}

				RepaintContent();
				var category = ShellRepairScreen.CategoryOf(cell.Column, cell.Row);
				Console.WriteLine($"{category} {ShellRepairScreen.IndexOf(category, cell.Row)}: "
					+ $"condition {repairScreen.SelectionCondition}, "
					+ $"{repairScreen.SelectionCost} kg to repair one level.");
				return;
			}

			if (repairScreen.ButtonAt(canvasX, canvasY) is { } repairButton) {
				Console.WriteLine($"{repairButton} — the button is live and its action is not ported yet.");
			}
		}

		// The crew screen's own clicks. A row, or the portrait inside it, selects the row; a squad portrait,
		// a Squad Inventory row and CLEAR assign against the selected row. Every one of them repaints.
		void ClickCrew(float canvasX, float canvasY) {
			if (crewScreen == null) {
				return;
			}

			if (ShellSquadPanel.RowAt(canvasX, canvasY) is { } bay) {
				if (!crewScreen.ClickRoster(bay)) {
					return;
				}
			} else if (ShellCrewScreen.PortraitAt(canvasX, canvasY) is { } member) {
				crewScreen.ClickPortrait(member);
			} else if (ShellCrewScreen.IsClearAt(canvasX, canvasY)) {
				crewScreen.Clear();
			} else if (ShellCrewScreen.RowAt(canvasX, canvasY) is { } row) {
				crewScreen.SelectRow(row);
			} else {
				return;
			}

			RepaintContent();
			LogCrew();
		}

		void LogCrew() {
			if (crewScreen == null) {
				return;
			}

			var rows = Enumerable.Range(0, ShellCrewScreen.RowCount).Select(row =>
				crewScreen.RowPilot(row) is { } pilot ? $"{row}: {pilot.Name} in bay {pilot.Bay}" : $"{row}: empty");
			Console.WriteLine($"Crew row {crewScreen.SelectedRow}, bay {crewScreen.SelectedBay} — "
				+ string.Join("; ", rows) + $". {hangar.MachinesOnStrength} on strength.");
		}

		// Tab 6's entry. The bay it starts from is the one the previous tab left selected, DAT_00482ae5,
		// which here only the repair screen tracks; the entry then moves it.
		void EnterCrew() {
			if (crewScreen == null) {
				crewScreen = new ShellCrewScreen(hangar, repairScreen.SelectedBay, bayPictures, crewPortraits);
			} else {
				crewScreen.Enter(hangar, repairScreen.SelectedBay);
			}

			var player = hangar.Player;
			Console.WriteLine($"Crew: {hangar.SquadPositions} squad positions in play, "
				+ $"{hangar.SquadMembers.Count} squad members; "
				+ (player != null ? $"player {player.Name} in bay {player.Bay}; " : "no player record; ")
				+ $"the entry leaves bay {crewScreen.SelectedBay} selected.");
		}

		// Rasterizes the current tab's content and hands it to the renderer. Called on a state change
		// rather than per frame: it resolves a whole canvas of palette indices and uploads a texture,
		// which is the same "repaint only what moved" the original's widget paints are driven by.
		void RepaintContent() {
			if (renderer == null) {
				return;
			}

			contentSurface.Clear();

			// Tabs 2-7 switch palette through the scope, whose paint blacks out everything below the
			// strip; the tab's screen, if one is ported, draws over that.
			bool filled = ShellPalette.FillsScope(screen.SelectedTab);
			if (filled) {
				ShellPalette.PaintScope(contentSurface);
			}

			switch (screen.SelectedTab) {
				case ShellScreen.RepairTab:
					repairScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.SaveTab:
					saveScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.CrewTab when crewScreen != null:
					crewScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				default:
					if (!filled) {
						renderer.SetContent(null);
						return;
					}

					break;
			}

			renderer.SetContent(contentSurface);
		}

		// Which palette a tab is drawn through: Shell_SelectTabPalette (0043b162)'s, unless --shell-palette
		// pins one entry everywhere. A tab with no screen ported shows only the strip over the scope's black
		// fill, so its palette colours the strip alone, as retail's does.
		string PaletteFor(int tab) {
			if (paletteName != null) {
				return paletteName;
			}

			return ShellPalette.ForTab(tab, MissionView(), campaignStage) is { } index
				&& ShellPalette.Name(index) is { } name
				? name : ShellArt.DefaultPaletteName;
		}

		ShellMissionView MissionView() =>
			!missionMapShown && missionInStage == 0 ? ShellMissionView.Map : ShellMissionView.Briefing;

		// The original writes an index into the palette widget and shows it; here the whole of the art
		// is decoded through one palette at load, so a change means loading it again and rebuilding the
		// renderer's textures. That is a few milliseconds on a click, and it happens only when the
		// palette actually changes.
		void SwitchPalette(int tab) {
			string name = PaletteFor(tab);
			if (gl == null || string.Equals(name, art.PaletteName, StringComparison.OrdinalIgnoreCase)) {
				return;
			}

			if (ShellArt.Load(content, name) is not { } reloaded) {
				Console.WriteLine($"Palette {name} could not be loaded — keeping {art.PaletteName}.");
				return;
			}

			art = reloaded;
			renderer?.Dispose();
			renderer = new ShellRenderer(gl, art);

			// The new renderer has no content texture, and the old one's was resolved through the old
			// palette anyway — so the tab's content is rasterized again through the palette it is now
			// being drawn in. Activate calls RepaintContent after this returns.
			Console.WriteLine($"Palette dpl\\{name}.DPL.");
		}
	}

	/// <summary>The tabs this engine has a screen behind.</summary>
	private static bool HasScreen(int tab) =>
		tab is ShellScreen.SaveTab or ShellScreen.RepairTab or ShellScreen.CrewTab;
}
