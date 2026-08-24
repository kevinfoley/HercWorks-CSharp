# Known Issues

## Earthsiege 2 (original retail game)

_Bugs listed in this section were tested on Windows 11. It's possible that some bugs would not occur on original 1990s hardware. Bugs in this section are reproduced as-is in HERCULAN Engine unless otherwise noted._

- Samson cockpit: several HUD elements are slightly misaligned (shield balance text is not centered horizontally with meter; firing chain, LINK, and TRACK buttons are slightly too high and too far outward)
- Player Herc acceleration/deceleration and turning are framerate-dependent (**fixed** in HERCULAN Engine)
- The "center legs" function moves the turret awkwardly and does not center the legs perfectly.
- Herc stats shown in the "Build" screen may be incorrect (the Outlaw is listed as having a top speed of 80 kph, but the manual and in-game readout show 100 kph).

## HERCULAN Engine

- Terrain textures are not mapped correctly.
- Claude used a separate camera for each cockpit panel, which causes a visible distortion in the side panels, particularly when looking downward at all. Will probably need to replace with a single camera covering the full width of the window (remember to maintain the same vertical FOV as the original game!). Or maybe we can crop the camera views by window frame rather than square, so that the seam isn't visible...
- Due to nearest-neighbor scaling, text often looks bad when window height isn't an integer multiple of 240.
- The `[V]` external view is placeholder geometry, not RE'd — see `docs/engine/planning.md`, "External view".
- Automatic Turret Tracking ([T]) and AI turret aiming are not ported yet.
- HERCULAN Engine is currently missing a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.