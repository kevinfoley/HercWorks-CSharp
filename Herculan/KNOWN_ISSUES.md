# Known Issues

## Earthsiege 2 (original retail game)

_Bugs listed in this section were tested on Windows 11. It's possible that some bugs would not occur on original 1990s hardware. Bugs in this section are reproduced as-is in HERCULAN Engine unless otherwise noted._

- Samson cockpit: several HUD elements are slightly misaligned (shield balance text is not centered horizontally with meter; firing chain, LINK, and TRACK buttons are slightly too high and too far outward)
- Player Herc acceleration/deceleration and turning are framerate-dependent (**fixed** in HERCULAN Engine)
- The "center legs" function moves the turret awkwardly and does not center the legs perfectly.
- Herc stats shown in the "Build" screen may be incorrect (the Outlaw is listed as having a top speed of 80 kph, but the manual and in-game readout show 100 kph).
- Some buildings' collision volumes are shorter than the visible mesh, so shots pass through the top of the building (verified against the retail data: type 3 stops at 2225 against a 6756 mesh, type 22 at 9400 against 18300). Reproduced as-is.

## HERCULAN Engine

- Terrain textures are not mapped correctly.
- Lighting on HERCs may not be correct (needs review).
- Turning movement is not correct (most obvious when turning while throttle is at 0). Possibly missing some root motion.
- Claude used a separate camera for each cockpit panel, which causes a visible distortion in the side panels, particularly when looking downward at all. Will probably need to replace with a single camera covering the full width of the window (remember to maintain the same vertical FOV as the original game!). Or maybe we can crop the camera views by window frame rather than square, so that the seam isn't visible...
- Due to nearest-neighbor scaling, text often looks bad when window height isn't an integer multiple of 240.
- The `[V]` external view is placeholder geometry, not RE'd — see `docs/engine/planning.md`, "External view".
- Automatic Turret Tracking ([T]) and AI turret aiming are not ported yet.
- Currently missing is a quirk from retail where the player's shield meter fills in over ~10 seconds at the start of a mission. Claude says there's no explanation for this in the shield code, where the shields start out at full charge, and would take ~30 seconds to fully charge from empty. The fade-in-over-10-seconds may be a HUD animation that hasn't been discovered during RE yet.
- Similarly to the previous, currently missing is an animation where weapon buttons wink on one-at-a-time when the simulation first starts.
- In the Scramble practice mission while piloting an Apocalypse, a Particle Beam Weapon is equipped to slot 8. In HERCULAN, when this PBW is fired the beam visibly clips off near the corner of the screen. This may be a camera near-clip plane issue.
- TextureAtlas.AverageColor() sounds like a hack (needs investigation)
- Impact effects have no sound. The two arrays that pick which effect a hit on armour draws are now distinguished correctly (they key on a component health-band change), though all 27 retail records carry identical arrays for the two - see `docs/simulation/impact-effects.md`.
- Missile launchers do not home: `Rocket_Fire` attaches the firing machine's selected target and there is no target selection, so a missile flies where it was pointed — see `docs/simulation/rockets.md`. The electro-optical missile's nose-camera view is unported for the same reason.
- Mission deployment is gated but not implemented: a group waiting on a mission action is correctly held out of the world, but nothing ever fires the trigger, so it never arrives. Drop pods (the falling `METEOR` that delivers Cybrid reinforcements), walk-on arrivals and the trigger evaluator are all missing — see `docs/simulation/mission-deployment.md`, which has the full RE.
- Mission group orders are not ported: nothing follows a route, so deployed AI units stand still.
- The `[P]` pause is a placeholder, not RE'd: it just stops the fixed-timestep tick loop. Retail DBSIM's own pause has not been traced.
- When projectiles hit buildings, many hit effects seem to clip inside the building - I don't observe this in retail. **Hit geometry ruled out**: every static type's collision grid is larger than its drawn mesh (it rounds out to whole 512-unit cells), and the five functions in the volume and sphere paths all re-check as faithful ports. Remaining suspect is render layering. See `docs/simulation/hit-detection.md`, "Measured: hit geometry versus the drawn mesh".
- Structures never come apart visually: a destroyed component's sub-shape should stop being drawn, and its destruction effect should play. Neither is ported - see `docs/simulation/hit-detection.md`.
- Weapon mounts are never destroyed: components 19-28 are the machine's mounts and a health-band change rolls to knock one out, which needs the mount manager's own destroy path - see `docs/simulation/damage-system.md`.
