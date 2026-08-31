# Known Issues

## Earthsiege 2 (original retail game)

_Bugs listed in this section were tested on Windows 11. It's possible that some bugs would not occur on original 1990s hardware. Bugs in this section are reproduced as-is in HERCULAN Engine unless otherwise noted._

- Samson cockpit: several HUD elements are slightly misaligned (shield balance text is not centered horizontally with meter; firing chain, LINK, and TRACK buttons are slightly too high and too far outward)
- Player Herc acceleration/deceleration and turning are framerate-dependent (**fixed** in HERCULAN Engine)
- The "center legs" function moves the turret awkwardly and does not center the legs perfectly.
- Herc stats shown in the VSHELL "Build" screen may be incorrect (the Outlaw is listed as having a top speed of 80 kph, but the manual and in-game readout show 100 kph).
- Some buildings' collision volumes are shorter than the visible mesh, so shots pass through the top of the building.
- Turning with the keyboard turns at half of the speed of turning with the joystick.
- The HUD speed readout does not reflect speed accurately. Hercs have two strides, "walking" and "running". In the walking stride, the Herc moves much more slowly than the speed gauge indicates.
- The Range readout of the MFD TARGET tab (F5) prints raw world units — 1 unit = 6 mm, so 103050 is about 618 m. Every other distance readout in the cockpit converts to metres first (`Hud_WorldUnitsToMetres`); this one does not.

## HERCULAN Engine

_Note to Claude: This section is for listing features which have been implemented but behave differently than retail or otherwise incorrectly, not a todo list of features that haven't been tackled yet._

- Terrain textures are not mapped correctly.
- Gouraud-shaded structures band differently from retail. On the type-15 octagonal tower, retail draws six narrow bands (~4 px, ramp-8 entries 9 down to 4) then four wide ones (28, 29, 29, 59 px, entries 3 down to 0); the engine cannot produce the wide dark bands, because `Light_ComputeShadeForFace` is negative at both corners of the away-facing facet and so must flat-fill entry 0 there. The sun direction, the light intensity and the ramp entry sequence are each excluded as the cause. See "Unresolved: type-15 band widths" in `docs/formats/dts-texture-binding.md`. Reference captures: `Reference/Gouraud_shading_comparison_2.png`, `Reference/Scramble_Training_Base_4.png`.
- Lighting on HERCs may not be correct (needs review) — re-check: HERCs are almost entirely `TSShadedPoly`, so the shade-ramp and away-facing-light fixes changed their colouring too.
- When projectiles hit buildings, many hit effects seem to clip inside the building - I don't observe this in retail. **Hit geometry ruled out**, and **building LOD ruled out** (the engine draws maximum detail, which is what retail's screenshots were taken at). Most likely a draw order issue.
- Claude used a separate camera for each cockpit panel, which causes a visible distortion in the side panels, particularly when looking downward at all. Will probably need to replace with a single camera covering the full width of the window (remember to maintain the same vertical FOV as the original game!). Or maybe we can crop the camera views by window frame rather than square, so that the seam isn't visible...
- Due to nearest-neighbor scaling, text often looks bad when window height isn't an integer multiple of 240.
- The `[V]` external view is placeholder geometry, not RE'd — see `docs/engine/planning.md`, "External view".
- Automatic Turret Tracking ([T]) and AI turret aiming are not ported yet.
- Currently missing is a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.
- Similarly to the previous, currently missing is an animation where weapon buttons wink on one-at-a-time when the simulation first starts.
- In the Scramble practice mission while piloting an Apocalypse, a Particle Beam Weapon is equipped to slot 8. In HERCULAN, when this PBW is fired the beam visibly clips off near the corner of the screen. This may be a camera near-clip plane issue.
- TextureAtlas.AverageColor() sounds like a hack (needs investigation) — it was one, and it is now only the fallback for a theater palette with no shade-ramp table. Retail has no such palette, so nothing reaches it. It is still what `HercWorks.UI`'s `Model3DViewerControl` uses.
- The `[P]` pause is a placeholder, not RE'd: it just stops the fixed-timestep tick loop. Retail DBSIM's own pause has not been traced.
- Targeting range seems to be much lower than retail, even with active radar. Possibly because there's currently no AI, so the enemies never switch their radar on.