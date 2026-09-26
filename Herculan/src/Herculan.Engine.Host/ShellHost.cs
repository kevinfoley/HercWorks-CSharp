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
/// <para>The pointer is polled once per update, and each change in its position or in either button
/// becomes one of the events VSHELL's queue carries, which <see cref="ShellPointer"/> delivers as
/// <c>EventQueue_Pump</c> (<c>00469ba4</c>) does. Polling rather than queueing the device's own
/// events means two changes inside one update arrive together, move first; a press and release both
/// inside one update are lost.</para>
/// </summary>
static class ShellHost {
	/// <summary>
	/// Frames to let pass before <c>--screenshot</c> fires. The shell has nothing to settle — no
	/// simulation, no streaming — but the window manager can hand back a stale or part-sized
	/// framebuffer for the first frame or two, so the capture waits the same short beat the mission
	/// host waits.
	/// </summary>
	private const int ScreenshotFrame = 5;

	/// <summary>
	/// Slot 10, the current-game autosave, whose in-use byte (<c>00482a19</c>) gates CONTINUE GAME.
	/// </summary>
	private const int CurrentGameSlot = 10;

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
		var mainMenu = new ShellMainMenu(slots.ElementAtOrDefault(CurrentGameSlot)?.InUse == true);
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
		int loadedSlot = slots.ToList().FindIndex(s => s.InUse);
		var loadedGame = loadedSlot >= 0 ? ShellSaveSlots.LoadSave(installRoot, slots[loadedSlot].FileName) : null;
		var hangar = ShellHangar.From(loadedGame);
		if (loadedGame != null) {
			campaignStage = loadedGame.CampaignStage + 1;
			missionInStage = loadedGame.MissionInStage;
		}

		// The mission tab's briefing, objectives and intelligence report, assembled from the loaded slot's
		// career block and its own missn%d.str, and the screen that shows them, built once and kept.
		var missionTexts = ShellMissionTexts.Load(installRoot, loadedSlot, loadedGame);
		var missionScreen = new ShellMissionScreen(ShellMissionArt.Load(content));
		var missionViewUp = ShellMissionView.Map;

		// The art was loaded before the game, so a start on the mission tab took stage 1's palette.
		if (!string.Equals(PaletteFor(startTab), art.PaletteName, StringComparison.OrdinalIgnoreCase)
				&& ShellArt.Load(content, PaletteFor(startTab)) is { } staged) {
			art = staged;
		}

		var repairCosts = ShellRepairCosts.Load(content);
		var repairDiagrams = ShellRepairDiagrams.Load(content);
		// The armory's prices, names and stat lines, and the preferences byte that says whether weapons are
		// built by hand (prefs.cfg option 45), which gates CLEAR. The prices are also what the repair and
		// build screens deduct the save's build queue at. The screen is built on first entry and kept.
		var armoryCatalog = ShellArmoryCatalog.Load(content);
		// Option 44 is the repair mode the repair screen's readout names.
		var preferences = SimulatorPreferences.Load(Path.Combine(installRoot, "DATA"));
		bool manualWeaponBuild = preferences?[WeaponsBuildingOption] != 0;
		int repairMode = preferences?[RepairOption] ?? ShellRepairScreen.AutoRepairMode;
		ShellArmoryScreen? armoryScreen = null;

		var repairScreen = new ShellRepairScreen(hangar, repairCosts, startBay, repairDiagrams) {
			QueuedKilograms = armoryCatalog.QueuedTotal(hangar),
			RepairMode = repairMode,
		};
		Console.WriteLine($"Damage diagram layouts loaded for {repairDiagrams.LayoutCount} chassis.");

		// The squad panel's three-quarter view, which the crew tab shows (and WEAPONS and BUILD would), and
		// the crew screen's two portrait banks. The crew screen is built on first entry and kept, as the
		// original's widgets are; each entry re-runs its entry routine.
		var bayPictures = ShellBayPictures.Load(content);
		var crewPortraits = ShellCrewPortraits.Load(content);
		ShellCrewScreen? crewScreen = null;
		Console.WriteLine($"Bay picture layouts loaded for {bayPictures.LayoutCount} chassis.");

		// The build screen's chassis figures and prices, and the same body layouts the repair diagram
		// draws, which its blueprints reuse. Built on first entry and kept, as the crew screen is.
		var chassisCatalog = ShellBuildScreen.LoadCatalog(content);
		ShellBuildScreen? buildScreen = null;

		// The WARNING dialog both SCRAP buttons open, and its twin the armory's Scrap opens, each built once
		// as the original builds them at startup. At most one is ever up.
		var scrapDialog = ShellScrapDialog.Herc();
		var weaponScrapDialog = ShellScrapDialog.Weapons();

		// The weapons screen's pictures and prose, and the screen itself, built on first entry and kept.
		var weaponsArt = ShellWeaponsArt.Load(content);
		ShellWeaponsScreen? weaponsScreen = null;
		Console.WriteLine($"Weapon pictures loaded for {weaponsArt.WeaponCount} weapons.");
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
		var pointer = new ShellPointer(screen);
		EnterTab(screen.SelectedTab);

		Console.WriteLine(art.Text != null
			? $"Tabs: {string.Join(", ", screen.Buttons.Where(b => b.Id < ShellLayout.TabCount && b.Caption != null).Select(b => b.Caption))}"
			: "No estext.bin — the tabs draw their plates and no captions.");
		Console.WriteLine(mode == ShellCampaignMode.Training
			? "Training campaign: REPAIR, BUILD and ARMORY are gated off, as the strip refresh gates them."
			: "Campaign: every tab is live.");
		Console.WriteLine("Every tab has a screen behind it but MISSION's map view. On the "
			+ "main menu, SAVE/RESTORE opens the save screen, whose EXIT comes back to the menu. Click a save "
			+ "slot row, or a repair list row, a part of the damage diagram or a Squad Inventory row, to "
			+ "select it and the panels beside it follow; REPAIR lifts the selected part one level, REPAIR ALL "
			+ "rebuilds the machine, and CANCEL undoes both since the bay was selected. On BUILD, click a chassis to see its blueprint and "
			+ "figures, or a Squad Inventory row to pick the bay SCRAP and BUILD are gated on. On WEAPONS, click an "
			+ "inventory row to see the weapon, and on a missile rack a guidance button to see that kind. BUILD orders "
			+ "the chassis into an empty bay, and SCRAP, there or on REPAIR, asks before it scraps the bay's machine. "
			+ "On ARMORY, click a row to see the weapon and its figures; with weapons built by hand, click the lit row "
			+ "again to queue one, right-click it to take one off, or CLEAR to take them all off; SCRAP sells the lit "
			+ "weapon's whole stock. On CREW, click "
			+ "a row to select it, then a squad portrait to put that pilot in the row, a Squad Inventory row "
			+ "to give the row's pilot that bay, or CLEAR to empty the row. MISSION shows the briefing once a "
			+ "stage is under way: its three text buttons switch the summary and the arrows beside it page "
			+ "through it; its map view is not ported. The square button latches and shows the frame. The save screen hides "
			+ "the strip, as the original's does: leave it with EXIT, or RESTORE a slot to load it into the "
			+ "repair screen. Close the window to quit.");
		Console.WriteLine(paletteName != null
			? $"Palette pinned to {art.PaletteName} on every tab."
			: "Each tab installs its own palette, as the original's do.");

		using var window = new EngineWindow("HERCULAN Engine — shell");

		ShellRenderer? renderer = null;
		GL? gl = null;
		IMouse? mouse = null;
		bool leftHeld = false;
		bool rightHeld = false;

		// Which button the event being delivered is, for the handlers that tell them apart: an armory
		// row's thunk calls one function on the left release and another on the right.
		var eventButton = ShellMouseButton.Left;
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

			// The mission tab's arrows draw a lit face while pressed, so a change in what the pointer has lit
			// repaints that tab; the other screens draw no pressed state.
			var litBefore = pointer.Lit;

			// The pointer reports window-client pixels while the canvas is placed in framebuffer pixels,
			// which differ on a scaled display — the same correction the mission host makes.
			var client = window.ClientSize;
			var framebuffer = window.FramebufferSize;
			float windowX = mouse.Position.X * framebuffer.X / Math.Max(client.X, 1);
			float windowY = mouse.Position.Y * framebuffer.Y / Math.Max(client.Y, 1);

			var layout = ShellScreenLayout.Create(framebuffer.X, framebuffer.Y);
			var (canvasX, canvasY) = layout.WindowToCanvas(windowX, windowY);

			pointer.Move(HitAt(canvasX, canvasY));
			ButtonEdge(mouse.IsButtonPressed(MouseButton.Left), ref leftHeld, ShellMouseButton.Left);
			ButtonEdge(mouse.IsButtonPressed(MouseButton.Right), ref rightHeld, ShellMouseButton.Right);
			if (pointer.Lit != litBefore && screen.SelectedTab == ShellScreen.MissionTab) {
				RepaintContent();
			}
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
		// once-per-load flag (Mission_Show, 004441e3), so the next visit opens the briefing.
		void EnterTab(int id) {
			// The repair and build screens quote the pool net of the queue, which the armory tab changes.
			repairScreen.QueuedKilograms = armoryCatalog.QueuedTotal(hangar);

			if (id == ShellScreen.RepairTab) {
				repairScreen.Enter();
			} else if (id == ShellScreen.CrewTab) {
				EnterCrew();
			} else if (id == ShellScreen.WeaponsTab) {
				EnterWeapons();
			} else if (id == ShellScreen.BuildTab) {
				EnterBuild();
			} else if (id == ShellScreen.ArmoryTab) {
				EnterArmory();
			} else if (id == ShellScreen.MissionTab) {
				EnterMission();
			}
		}

		// The widget under a canvas point. The strip is drawn over the content and hit first; below it,
		// whichever tab is up.
		ShellHit? HitAt(float canvasX, float canvasY) {
			if (scrapDialog.IsOpen) {
				return scrapDialog.HitAt(canvasX, canvasY);
			}

			if (weaponScrapDialog.IsOpen) {
				return weaponScrapDialog.HitAt(canvasX, canvasY);
			}

			if (screen.HitAt(canvasX, canvasY) is { } strip) {
				return strip;
			}

			return screen.SelectedTab switch {
				ShellScreen.MainMenuTab => mainMenu.HitAt(canvasX, canvasY),
				ShellScreen.SaveTab => saveScreen.HitAt(canvasX, canvasY),
				ShellScreen.RepairTab => ShellSquadPanel.HitAt(canvasX, canvasY)
					?? repairScreen.HitAt(canvasX, canvasY),
				ShellScreen.BuildTab when buildScreen != null => ShellSquadPanel.HitAt(canvasX, canvasY)
					?? buildScreen.HitAt(canvasX, canvasY),
				ShellScreen.WeaponsTab when weaponsScreen != null => ShellSquadPanel.HitAt(canvasX, canvasY)
					?? weaponsScreen.HitAt(canvasX, canvasY),
				ShellScreen.CrewTab when crewScreen != null => ShellSquadPanel.HitAt(canvasX, canvasY)
					?? ShellCrewScreen.HitAt(canvasX, canvasY),
				ShellScreen.ArmoryTab when armoryScreen != null => armoryScreen.HitAt(canvasX, canvasY),
				ShellScreen.MissionTab when missionViewUp == ShellMissionView.Briefing => missionScreen.HitAt(canvasX, canvasY),
				_ => null,
			};
		}

		// A button changing state since the last update, delivered as the press or release it is.
		void ButtonEdge(bool held, ref bool wasHeld, ShellMouseButton button) {
			if (held == wasHeld) {
				return;
			}

			wasHeld = held;
			eventButton = button;
			if (held) {
				pointer.Press(button, Fire);
			} else {
				pointer.Release(button, Fire);
			}
		}

		// Runs a widget's click handler — Widget_DispatchCallback (0041f5d4) calling what the builder
		// passed the widget.
		void Fire(ShellWidget widget) {
			switch (widget.Kind) {
				case ShellWidgetKind.StripButton:
					Activate(widget.Index);
					break;
				case ShellWidgetKind.MainMenuButton:
					ClickMainMenuButton((ShellMainMenuButton)widget.Index);
					break;
				case ShellWidgetKind.SaveRow:
					SelectSaveSlot(widget.Index);
					break;
				case ShellWidgetKind.SaveButton:
					ClickSaveButton((ShellSaveButton)widget.Index);
					break;
				case ShellWidgetKind.RepairRow or ShellWidgetKind.RepairHotspot:
					SelectRepair(widget.Index, widget.Sub);
					break;
				case ShellWidgetKind.RepairButton when (ShellRepairButton)widget.Index == ShellRepairButton.Scrap:
					OpenScrapDialog(repairScreen.SelectedBay);
					break;
				case ShellWidgetKind.RepairButton:
					ClickRepairButton((ShellRepairButton)widget.Index);
					break;
				case ShellWidgetKind.SquadRow:
					ClickRoster(widget.Index);
					break;
				case ShellWidgetKind.BuildChassisRow:
					SelectChassis(widget.Index);
					break;
				case ShellWidgetKind.BuildButton:
					ClickBuildButton((ShellBuildButton)widget.Index);
					break;
				case ShellWidgetKind.WeaponsRow:
					SelectWeaponsRow(widget.Index);
					break;
				case ShellWidgetKind.WeaponsButton:
					ClickWeaponsButton((ShellWeaponsButton)widget.Index);
					break;
				case ShellWidgetKind.WeaponsHotspot:
					SelectHardpoint(widget.Index);
					break;
				case ShellWidgetKind.ArmoryRow:
					ClickArmoryRow(widget.Index);
					break;
				case ShellWidgetKind.ArmoryButton:
					ClickArmoryButton((ShellArmoryButton)widget.Index);
					break;
				case ShellWidgetKind.ScrapDialogButton:
					ClickScrapDialogButton((ShellScrapDialogButton)widget.Index);
					break;
				case ShellWidgetKind.MissionButton:
					ClickMissionButton((ShellMissionButton)widget.Index);
					break;
				case ShellWidgetKind.MissionArrow:
					ClickMissionArrow((ShellMissionArrow)widget.Index);
					break;
				default:
					ClickCrew(widget);
					break;
			}
		}

		// SAVE/RESTORE, 00431498: hide the menu, set the campaign mode to 1 (FUN_0040e69e), point the
		// save screen's EXIT back here, and enter it. The other nine buttons' actions are not ported.
		void ClickMainMenuButton(ShellMainMenuButton button) {
			if (button != ShellMainMenuButton.SaveRestore) {
				Console.WriteLine($"{button} — the button is live and its action is not ported yet.");
				return;
			}

			mode = ShellCampaignMode.Campaign;
			saveScreen.ExitTarget = ShellSaveExitTarget.MainMenu;
			screen.SelectTab(ShellScreen.SaveTab);
			Console.WriteLine("Save/Restore — the save screen, with EXIT back to the main menu.");
			RepaintContent();
		}

		// A save row's handler, SaveScreen_SelectSlot (0043795f). Clicking the row already selected is a
		// no-op, the same early return the original's selection move opens with.
		void SelectSaveSlot(int slot) {
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
		}

		void ClickSaveButton(ShellSaveButton button) {
			switch (button) {
				case ShellSaveButton.Restore:
					RestoreSelectedSlot();
					break;
				case ShellSaveButton.Exit:
					LeaveSaveScreen();
					break;
				default:
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
			missionTexts = ShellMissionTexts.Load(installRoot, slot, restored);
			campaignStage = restored.CampaignStage + 1;
			missionInStage = restored.MissionInStage;
			missionMapShown = false;
			repairScreen = new ShellRepairScreen(hangar, repairCosts, startBay, repairDiagrams) {
				QueuedKilograms = armoryCatalog.QueuedTotal(hangar),
				RepairMode = repairMode,
			};
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

		// A repair row or hotspot's handler, Repair_SelectHotspot (00433eb9): the selection moves and the
		// detail panel follows. A hardpoint row past the machine's capacity, or one holding no weapon,
		// refuses the selection outright — nothing moves and nothing repaints.
		void SelectRepair(int column, int row) {
			if (!repairScreen.Select(column, row)) {
				return;
			}

			RepaintContent();
			var category = ShellRepairScreen.CategoryOf(column, row);
			Console.WriteLine($"{category} {ShellRepairScreen.IndexOf(category, row)}: "
				+ $"condition {repairScreen.SelectionCondition}, "
				+ $"{repairScreen.SelectionCost} kg to repair one level.");
		}

		// REPAIR (00434b2d), REPAIR ALL (00434c59) and CANCEL (00434d73). Each refills the rows and the readout
		// panels after it, which the repaint does here.
		void ClickRepairButton(ShellRepairButton button) {
			switch (button) {
				case ShellRepairButton.Repair:
					int cost = repairScreen.Repair();
					Console.WriteLine($"Repaired {repairScreen.SelectedCategory} {repairScreen.SelectedIndex} to "
						+ $"{repairScreen.SelectionCondition} for {cost} kg.");
					break;
				case ShellRepairButton.RepairAll:
					Console.WriteLine($"Repaired bay {repairScreen.SelectedBay} to 100 for {repairScreen.RepairAll()} kg.");
					break;
				case ShellRepairButton.Cancel:
					repairScreen.Cancel();
					Console.WriteLine($"Repairs to bay {repairScreen.SelectedBay} undone.");
					break;
				default:
					return;
			}

			Console.WriteLine($"{hangar.SalvageKilograms} kg in the pool, {repairScreen.AvailableKilograms} kg available.");
			RepaintContent();
		}

		// A Squad Inventory row's handler, Squad_SelectBay (0043d64d), whose arm is the tab that is up.
		void ClickRoster(int bay) {
			if (screen.SelectedTab == ShellScreen.CrewTab) {
				if (crewScreen?.ClickRoster(bay) == true) {
					RepaintContent();
					LogCrew();
				}

				return;
			}

			if (screen.SelectedTab == ShellScreen.BuildTab) {
				if (buildScreen?.ClickRoster(bay) == true) {
					RepaintContent();
					LogBuild();
				}

				return;
			}

			if (screen.SelectedTab == ShellScreen.WeaponsTab) {
				if (weaponsScreen?.ClickRoster(bay) == true) {
					RepaintContent();
					LogWeapons();
				}

				return;
			}

			if (repairScreen.SelectBay(bay)) {
				RepaintContent();
				Console.WriteLine($"Bay {bay}: chassis type {repairScreen.Machine?.ChassisType}.");
			}
		}

		// The crew panel's handlers. A row, or the portrait inside it, selects the row; a squad portrait
		// and CLEAR assign against the selected row. Every one of them repaints.
		void ClickCrew(ShellWidget widget) {
			if (crewScreen == null) {
				return;
			}

			switch (widget.Kind) {
				case ShellWidgetKind.CrewRow or ShellWidgetKind.CrewRowPortrait:
					crewScreen.SelectRow(widget.Index);
					break;
				case ShellWidgetKind.CrewSquadPortrait:
					crewScreen.ClickPortrait(widget.Index);
					break;
				case ShellWidgetKind.CrewClear:
					crewScreen.Clear();
					break;
				default:
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

		// A chassis row's handler, Build_SelectChassis (00446c3b); the chassis already selected is a no-op.
		void SelectChassis(int chassis) {
			if (buildScreen?.SelectChassis(chassis) == true) {
				RepaintContent();
				LogBuild();
			}
		}

		void LogBuild() {
			if (buildScreen == null) {
				return;
			}

			Console.WriteLine($"Build: chassis {buildScreen.SelectedChassis}"
				+ (buildScreen.SelectedEntry is { } entry ? $" ({entry.SalvageReq} tons)" : string.Empty)
				+ $", bay {buildScreen.SelectedBay}, {buildScreen.AvailableKilograms} kg available; "
				+ $"SCRAP {(buildScreen.IsEnabled(ShellBuildButton.Scrap) ? "live" : "dead")}, "
				+ $"BUILD {(buildScreen.IsEnabled(ShellBuildButton.Build) ? "live" : "dead")}.");
		}

		// BUILD's handler (00446f3e) orders the chassis; SCRAP's (00446ee0) puts the dialog up.
		void ClickBuildButton(ShellBuildButton button) {
			if (buildScreen == null) {
				return;
			}

			if (button == ShellBuildButton.Scrap) {
				OpenScrapDialog(buildScreen.SelectedBay);
				return;
			}

			buildScreen.Build();
			Console.WriteLine($"Built chassis {buildScreen.SelectedChassis} into bay {buildScreen.SelectedBay}.");
			RepaintContent();
			LogBuild();
		}

		// Both SCRAP handlers, the build tab's (00446ee0) and the repair tab's (00434d15): 00447711 quotes
		// the selected bay's machine and shows the dialog.
		void OpenScrapDialog(int bay) {
			scrapDialog.Open(bay, (repairCosts?.ScrapValue(hangar.Bay(bay)) ?? 0) / ShellRepairCosts.KilogramsPerTon);
			Console.WriteLine($"Scrap bay {bay}? It will yield {scrapDialog.YieldTons} tons.");
			RepaintContent();
		}

		// CANCEL (00447c38) only takes the dialog down. ACCEPT (00447c96) takes it down, scraps the bay
		// (0040e757), and on the repair tab moves to the first bay holding a finished machine; the build
		// tab keeps the bay, now empty, and regates.
		void ClickScrapDialogButton(ShellScrapDialogButton button) {
			if (weaponScrapDialog.IsOpen) {
				ClickWeaponScrapDialogButton(button);
				return;
			}

			scrapDialog.Close();
			if (button == ShellScrapDialogButton.Accept) {
				int value = hangar.Scrap(scrapDialog.Subject, repairCosts);
				Console.WriteLine($"Scrapped bay {scrapDialog.Subject} for {value} kg; {hangar.SalvageKilograms} kg in the pool.");
				if (screen.SelectedTab == ShellScreen.RepairTab) {
					repairScreen.SelectBay(hangar.FirstBuiltBay());
				}
			}

			RepaintContent();
		}

		// The weapon dialog's CANCEL (00447d59) only takes it down. ACCEPT (WeaponScrapDialog_OnAccept,
		// 00447db7) takes it down, sells the whole stock (Armory_ScrapWeapons, 0040e7b2), trims or refills
		// the queue by the build mode (Armory_RefreshQueue, 00412413), and refreshes the rows and readout.
		void ClickWeaponScrapDialogButton(ShellScrapDialogButton button) {
			weaponScrapDialog.Close();
			if (button == ShellScrapDialogButton.Accept && armoryScreen != null) {
				int weapon = weaponScrapDialog.Subject;
				hangar.ScrapStock(weapon, armoryCatalog.ScrapValueTons(hangar, weapon));
				armoryCatalog.RefreshQueue(hangar, manualWeaponBuild);
				armoryScreen.RefreshAfterScrap();
				Console.WriteLine($"Scrapped weapon {weapon}'s stock; {hangar.SalvageKilograms} kg in the pool.");
				LogArmory();
			}

			RepaintContent();
		}

		// Tab 4's entry, from the bay the repair screen has selected, as the crew tab's is.
		void EnterBuild() {
			if (buildScreen == null) {
				buildScreen = new ShellBuildScreen(hangar, repairScreen.SelectedBay, chassisCatalog, repairDiagrams,
					bayPictures);
			} else {
				buildScreen.Enter(hangar, repairScreen.SelectedBay);
			}

			buildScreen.QueuedKilograms = armoryCatalog.QueuedTotal(hangar);

			LogBuild();
		}

		// Tab 2's entry, from the bay the repair screen has selected, as the build and crew tabs' are.
		void EnterWeapons() {
			if (weaponsScreen == null) {
				weaponsScreen = new ShellWeaponsScreen(hangar, repairScreen.SelectedBay, weaponsArt, bayPictures);
			} else {
				weaponsScreen.Enter(hangar, repairScreen.SelectedBay);
			}

			LogWeapons();
		}

		// An inventory row's handler, one of the thunks from 00440300: Arming_SelectRow (0043f71c) with the
		// fit armed, so with a hardpoint selected the row's weapon goes into it.
		void SelectWeaponsRow(int row) {
			if (weaponsScreen?.SelectRow(row, fit: true) == true) {
				RepaintContent();
				LogWeapons();
			}
		}

		// A hotspot over the bay picture, Arming_SelectHardpoint (0043dbb2).
		void SelectHardpoint(int hardpoint) {
			if (weaponsScreen?.SelectHardpoint(hardpoint) == true) {
				RepaintContent();
				LogWeapons();
			}
		}

		// The four guidance buttons show their kind and write it to the mount, the rack's own button
		// selects its row again without fitting it (0044012a), and the steppers move the hardpoint.
		void ClickWeaponsButton(ShellWeaponsButton button) {
			if (weaponsScreen == null) {
				return;
			}

			bool changed = button switch {
				ShellWeaponsButton.Arm or ShellWeaponsButton.Arh or ShellWeaponsButton.Sarh or ShellWeaponsButton.Eo =>
					ShowWeaponsGuidance((int)button),
				ShellWeaponsButton.Weapon => weaponsScreen.SelectRow(weaponsScreen.SelectedRow, fit: false),
				ShellWeaponsButton.PreviousHardpoint => weaponsScreen.PreviousHardpoint(),
				_ => weaponsScreen.NextHardpoint(),
			};

			if (changed) {
				RepaintContent();
				LogWeapons();
			}
		}

		bool ShowWeaponsGuidance(int button) {
			weaponsScreen!.ShowGuidance(button);
			return true;
		}

		void LogWeapons() {
			if (weaponsScreen == null) {
				return;
			}

			int weapon = weaponsScreen.SelectedWeapon;
			int hardpoint = weaponsScreen.SelectedHardpoint;
			var mount = weaponsScreen.Machine?.Mount(hardpoint);
			Console.WriteLine($"Weapons: bay {weaponsScreen.SelectedBay}, row {weaponsScreen.SelectedRow} "
				+ $"({art.Text?.Text(0x7e + weapon) ?? $"weapon {weapon}"}, {hangar.WeaponsOwned(weapon)} held)"
				+ (hardpoint == -1 ? ", no hardpoint"
					: $", hardpoint {hardpoint + 1} holding " + (mount == null ? "nothing"
						: $"weapon {mount.WeaponId} at {weaponsScreen.Machine!.Condition(ShellRepairCategory.Hardpoint, hardpoint)}%, guidance {mount.Guidance}"))
				+ (weaponsScreen.ShowingGuidance ? $", showing guidance kind {weaponsScreen.ShownGuidance}." : "."));
		}

		// Tab 5's entry, Armory_Enter (004494f7). The armory has no squad panel and no bay.
		void EnterArmory() {
			if (armoryScreen == null) {
				armoryScreen = new ShellArmoryScreen(hangar, manualWeaponBuild, armoryCatalog, weaponsArt);
			} else {
				armoryScreen.Enter(hangar, manualWeaponBuild);
			}

			Console.WriteLine($"Armory: weapons built {(manualWeaponBuild ? "by hand" : "automatically")}, "
				+ $"{hangar.QueueFreeSlots} of {ShellHangar.QueueSlots} queue slots free, "
				+ $"{armoryScreen.AllocatedKilograms} kg allocated, {armoryScreen.AvailableKilograms} kg available.");
			LogArmory();
		}

		// A row's release: Armory_ClickRow (0044969f) for the left button, Armory_RightClickRow (004499de)
		// for the right.
		void ClickArmoryRow(int row) {
			if (armoryScreen == null) {
				return;
			}

			bool changed = eventButton == ShellMouseButton.Left
				? armoryScreen.ClickRow(row)
				: armoryScreen.RightClickRow(row);
			if (!changed) {
				return;
			}

			RepaintContent();
			LogArmory();
		}

		// Clear (00449ef4) takes the lit weapon's units off the queue. Scrap (Armory_OnScrap, 00449e78) puts
		// the weapon scrap dialog up on the lit weapon's stock.
		void ClickArmoryButton(ShellArmoryButton button) {
			if (armoryScreen == null) {
				return;
			}

			if (button == ShellArmoryButton.Scrap) {
				if (armoryScreen.SelectedRow != -1) {
					int weapon = armoryScreen.SelectedWeapon;
					weaponScrapDialog.Open(weapon, armoryCatalog.ScrapValueTons(hangar, weapon));
					Console.WriteLine($"Scrap weapon {weapon}? {hangar.WeaponsOwned(weapon)} held will yield "
						+ $"{weaponScrapDialog.YieldTons} tons.");
					RepaintContent();
				}

				return;
			}

			armoryScreen.Clear();
			RepaintContent();
			LogArmory();
		}

		void LogArmory() {
			if (armoryScreen == null) {
				return;
			}

			int weapon = armoryScreen.SelectedWeapon;
			Console.WriteLine($"Armory: row {armoryScreen.SelectedRow} "
				+ $"({armoryCatalog.Names?.Text(weapon) ?? $"weapon {weapon}"}, {hangar.WeaponsOwned(weapon)} held, "
				+ $"{hangar.QueuedCount(weapon)} queued, {armoryCatalog.PriceKilograms(weapon)} kg); "
				+ $"{hangar.QueueFreeSlots} slots free, {armoryScreen.AllocatedKilograms} kg allocated.");
		}

		// Tab 7's entry, Mission_Show (004441e3), in the view the tab handler picks. Only the
		// briefing view has a screen here; the map view shows the frame.
		void EnterMission() {
			missionViewUp = MissionView();
			if (missionViewUp == ShellMissionView.Map) {
				missionMapShown = true;
				Console.WriteLine("Mission: the campaign map view, which is not ported — the tab shows the frame.");
				return;
			}

			missionScreen.EnterBriefing(missionTexts, art.Sprites?.Font(ShellArt.ScreenFont));
			LogMission();
		}

		// A text button shows its text; Rock & Roll's launch (00445509) is not ported.
		void ClickMissionButton(ShellMissionButton button) {
			if (button == ShellMissionButton.RockAndRoll) {
				Console.WriteLine("Rock & Roll — launching the mission is not ported yet.");
				return;
			}

			missionScreen.ShowText(button);
			RepaintContent();
			LogMission();
		}

		// The page arrows page the text that is up; the map's six move a map this engine does not draw.
		void ClickMissionArrow(ShellMissionArrow arrow) {
			if (arrow is not (ShellMissionArrow.PageUp or ShellMissionArrow.PageDown)) {
				Console.WriteLine($"{arrow} — the mission map is not ported yet.");
				return;
			}

			if (missionScreen.Page(arrow == ShellMissionArrow.PageDown)) {
				RepaintContent();
				LogMission();
			}
		}

		void LogMission() {
			var box = missionScreen.Box(missionScreen.ShownText);
			Console.WriteLine($"Mission: {missionScreen.ShownText}, {box.Lines.Count} lines, "
				+ $"page {box.Page + 1} of {box.PageCount}.");
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
				case ShellScreen.MainMenuTab:
					mainMenu.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.RepairTab:
					repairScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.SaveTab:
					saveScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.BuildTab when buildScreen != null:
					buildScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.WeaponsTab when weaponsScreen != null:
					weaponsScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.CrewTab when crewScreen != null:
					crewScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.ArmoryTab when armoryScreen != null:
					armoryScreen.Paint(contentSurface, art.Text, art.Sprites);
					break;
				case ShellScreen.MissionTab when missionViewUp == ShellMissionView.Briefing:
					missionScreen.Paint(contentSurface, art.Text, art.Sprites, pointer.Lit);
					break;
				default:
					if (!filled) {
						renderer.SetContent(null);
						return;
					}

					break;
			}

			scrapDialog.Paint(contentSurface, art.Text, art.Sprites);
			weaponScrapDialog.Paint(contentSurface, art.Text, art.Sprites);
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
		tab is ShellScreen.MainMenuTab or ShellScreen.SaveTab or ShellScreen.WeaponsTab or ShellScreen.RepairTab or ShellScreen.BuildTab
			or ShellScreen.ArmoryTab or ShellScreen.CrewTab or ShellScreen.MissionTab;

	/// <summary><c>prefs.cfg</c> option 45, VSHELL's <c>Weapons Building:</c> — 1 builds weapons by hand (docs/simulation/preferences.md).</summary>
	private const int WeaponsBuildingOption = 45;

	/// <summary><c>prefs.cfg</c> option 44, VSHELL's <c>Repair Options:</c> (docs/simulation/preferences.md).</summary>
	private const int RepairOption = 44;
}
