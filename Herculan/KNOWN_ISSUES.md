# Known Issues

## Earthsiege 2 (original retail game)

_Bugs listed in this section are reproduced as-is in HERCULAN Engine unless otherwise noted._

- Samson cockpit: several HUD elements are slightly misaligned (shield balance text is not centered horizontally with meter; firing chain, LINK, and TRACK buttons are slightly too high and too far outward)
- Player Herc acceleration/deceleration and turning are framerate-dependent (**fixed** in HERCULAN Engine)

## HERCULAN Engine

- Terrain textures are not mapped correctly.
- Claude used a separate camera for each cockpit panel, which causes a visible distortion in the side panels, particularly when looking downward at all. Will probably need to replace with a single camera covering the full width of the window (remember to maintain the same vertical FOV as the original game!)
- Due to nearest-neighbor scaling, text often looks bad when window height isn't an integer multiple of 240.
- The `[V]` external view is placeholder geometry, not RE'd — see `docs/engine/planning.md`, "External view".
- Leg animations are not drawn at all: `DtsMeshBuilder` bakes the rest pose and each object draws with one matrix. `Esc` opens a debug panel whose skeleton view shows the pose the sim is actually holding.