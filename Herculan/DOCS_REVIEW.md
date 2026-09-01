# Documentation review — `Herculan/docs` vs `HercWorks.Core` / `Herculan.Engine`

Review date: 2026-08-31. Scope: all 37 files under `Herculan/docs`, plus `Herculan/README.md`,
`Herculan/KNOWN_ISSUES.md` and the repo-root `README.MD`, compared against the comments in
`HercWorks.Core` and `Herculan.Engine`.

Line numbers are from the working tree at review time. Findings are grouped by the four problem
classes requested, most-important first within each group.

**Totals:** 58 contradictions/stale claims · 25 duplications · 6 rambling passages ·
16 journal-style / append-only-rot items · 8 structural issues.

The dominant pattern, by a wide margin: **the docs and comments are append-only.** New findings are
added as a new paragraph, a "Since:" entry, or a dated correction, while the earlier text that the
finding invalidates is left in place. Roughly two thirds of the contradictions below are a stale
"not ported" / "unresolved" / "no consumer located" claim sitting next to working code that does the
thing. A second pattern: the same reverse-engineering evidence is written out in full in both the
doc and the code comment, and the two copies have already drifted apart in at least six places.

---

## A. Contradictions and stale information

### A1. Status claims contradicted by the code

1. **`weapon-firing.md`'s "Not ported" list describes a build three milestones old.**
   `docs/simulation/weapon-firing.md:213-219`. It says "**Structures and aircraft.** Both have their
   own vtable `+0x20`; neither is ported, so beams pass through them" and "**Component damage.** …
   the 29-slot component health array … does not exist. Damage past shields is counted, not
   applied." Both are false: `src/Herculan.Engine/Sim/BaseObject.cs:196` and
   `src/Herculan.Engine/Sim/FlyerObject.cs:135` are real overrides, and
   `src/Herculan.Engine/Sim/MechObject.Combat.cs:332-411` writes per-component health via
   `ComponentDamage`. `docs/simulation/hit-detection.md:8-11` already says "All three vtable `+0x20`
   implementations are ported."
   *Fix:* delete both bullets; keep only Sound and the ELF/ELF2 jagged branch.

2. **`planning.md` says DBSIM's trig table was never located; `SimTrig` says all four were located
   and verified entry-by-entry.**
   `docs/engine/planning.md:171-172` ("**DBSIM's sine/cosine table isn't located.**") and
   `src/Herculan.Engine/Numerics/BinaryAngle.cs:10-20` ("a search of the current symbol set turns up
   no trig function at all … treat any result that depends on exact trig parity as provisional")
   against `src/Herculan.Engine/Numerics/SimTrig.cs:11-23`, which names `DAT_004a25dc`,
   `DAT_004a1c4c`, `DAT_004a1e54` and `DAT_004a05d4` and reports 1020/1024 cosine entries exact and
   the other three tables exact. *(Independently verified.)*
   *Fix:* delete the `planning.md` bullet; rewrite `BinaryAngle`'s paragraph to say the vanilla
   coarse table is `SimTrig` and this one is engine-side only.

3. **`planning.md` says `typeRecord+0x1a` "isn't mapped"; it is mapped, and tabulated in another
   doc.** `docs/engine/planning.md:173-175` against
   `src/Herculan.Engine/Sim/MechTypeRecord.cs:129-139` (`public short HitRadius =>
   Data.AiAimTargOffset;`) and `docs/simulation/damage-system.md:122` (`| +0x1a | 24 | hit radius
   (AiAimTargOffset) | 2500 heavy, 1500 medium, 1000 SPIDER |`). *(Independently verified.)*
   *Fix:* delete the bullet. See also D2 — the whole list disclaims its own currency.

4. **`Rocket.cs` and `Projectile.cs` state that nothing homes, directly above the homing code.**
   `src/Herculan.Engine/Sim/Rocket.cs:36`, `:289-296` ("**Nothing homes** … A rocket therefore flies
   where it was pointed") and `src/Herculan.Engine/Sim/Projectile.cs:250-254` — but the next lines
   steer toward `Target.AimPoint`, and `src/Herculan.Engine/Sim/SimWorld.cs:338-341, 373-377` say
   `TargetSelection` "now fills in" the target. `Rocket.cs:289` also says three gates "are
   consequently not reachable either, and are recorded here rather than written as branches" while
   the emission gate *is* a branch at `Rocket.cs:322`.
   *Fix:* rewrite both summaries to describe shipped homing; keep only the genuinely unported items
   (`+0x5a` node handle, ECM wobble, player steer).

5. **`TargetSelection.cs` says the HUD target box "was never traced".**
   `src/Herculan.Engine/Sim/TargetSelection.cs:33-35` against
   `src/Herculan.Engine/Content/TargetBox.cs:1-25` (child 5 reverse-engineered in full) and
   `src/Herculan.Engine.Host/Program.cs:938` (draw gated on `IndicatorArmed`).
   *Fix:* delete the "Not ported" paragraph, point at `Content.TargetBox`.

6. **`ShieldCharge.cs` says absorption is not modelled in the file that implements it.**
   `src/Herculan.Engine/Sim/ShieldCharge.cs:35-38` ("**Damage absorption is not modelled here.**")
   and `:193-200` (`Empty()` "exists because nothing in the engine damages shields yet") against
   `AbsorbDirectFire` at `:226-241`, called every hit by `MechObject.ShieldAbsorbDirectFire`.
   *Fix:* name `AbsorbDirectFire` as ported; leave only `FUN_00413c68` (explosions) outstanding.

7. **Two `WeaponMount`/`WeaponMounts` comments say nothing fires and no magazine empties.**
   `src/Herculan.Engine/Sim/WeaponMount.cs:256-262` ("Nothing empties a magazine yet") against
   `FireAmmunition` twelve lines below at `:630-634`; `WeaponMounts.cs:386-388` ("With nothing firing
   yet every mount is ready, so in practice nothing moves") against `FireTick` at `:460-485`.
   *Fix:* strike both "yet" clauses.

8. **`impact-effects.md` says group 2 is never selected because there is no component health array.**
   `docs/simulation/impact-effects.md:116-118` against
   `src/Herculan.Engine/Sim/MechObject.Combat.cs:339-347`, which measures
   `_damage.DamagePercent(componentIndex) >> 5` either side of the write and picks
   `ImpactFxGroup.Armor` on a band change; `src/Herculan.Engine/Sim/WeaponShot.cs:186-190` already
   says "both branches are reachable."
   *Fix:* replace with the still-true note that retail data makes the two arrays identical.

9. **`manager+0x0a` is called "per-ammunition-type counters with no traced writer" in three places
   the missile-lock work superseded.** `docs/simulation/weapon-mounts.md:155` (table row) versus its
   own `:249-252` ("**`manager+0x0a` is solved**"); `src/Herculan.Engine/Sim/Rocket.cs:346-350`;
   `src/Herculan.Engine/Sim/WeaponMounts.cs:425-428` versus the correct `MissileLock` property at
   `:74-100` in the same file. The writer is `MechObject.MissileLockTick`.
   *Fix:* "per-subtype missile-lock flags" in all three; fold the Open bullet into the table.

10. **`mech+0x30b` is called "unidentified" while `MechPods.cs` identifies it.**
    `docs/simulation/missile-lock.md:53-56` and `src/Herculan.Engine/Sim/MechObject.Lock.cs:238-240`
    against `src/Herculan.Engine/Sim/MechPods.cs:31` ("Slot 1, `mech+0x30b` — the targeting computer
    (id 29)"); `docs/simulation/rockets.md:118-119` already words the condition correctly.
    *Fix:* say it is the TARG pod's mount; note only that `+0x7f` on a pod mount is untested.

11. **`Weapons.cs` claims the 48-byte tail has exactly one decoded field.**
    `src/HercWorks.Core/Data/File/Dat/Sim/Weapons.cs:80-82` against
    `docs/formats/weapons-dat-sim.md:57-71` and the ten tail fields `WeaponMount.cs:286-315`,
    `:669-674`, `:727-755` reads with raw `BitConverter.ToInt16(tail, …)` calls (range `0x30`,
    thresholds `0x36`/`0x38`, magazine `0x3a`, barrels `0x3c`, muzzle triple `0x40`–`0x44`, side
    offsets `0x46`/`0x4a`, refire `0x4c`).
    *Fix:* promote the decoded tail fields to named properties and update the summary.

12. **`ProjectileData.cs` and `Weapons.cs` say the launcher secondary key is not visible to this
    project.** `src/HercWorks.Core/Data/File/Dat/Sim/ProjectileData.cs:22-23` and `Weapons.cs:99-103`
    against `docs/formats/weapons-dat-sim.md:109`, `docs/simulation/weapon-mounts.md:49-60` and
    `src/Herculan.Engine/Sim/WeaponCatalog.cs:107-118`. The key is the mission's second loadout array
    (`MecEntry.WeaponAmmoTypes` / `script.dat` block 7 offset `0x72`).
    *Fix:* replace both passages with the resolved answer and a pointer.

13. **`terrain-texturing.md`'s "Question 3 — still open" was solved and never retracted.**
    `docs/formats/terrain-texturing.md:107-119` ("No writer of the diagonal-selector's bit 1 was
    found … the writer, if one exists in DBSIM at all, is somewhere else again") against
    `docs/formats/terrain-heightmap.md:43-46` ("**The selector and both normals are written by the
    same function**, `Terrain_BuildCellSurface` (`0046bed8`)") and
    `src/Herculan.Engine/Terrain/HeightGrid.cs:174-213`, which ports it.
    *Fix:* delete the section; replace with a pointer to the selector table.

14. **`HeightGrid`'s class comment says 14 of the 16 cell bytes are undecoded and never touched.**
    `src/Herculan.Engine/Terrain/HeightGrid.cs:9-14` against
    `docs/formats/terrain-heightmap.md:31-41`, which decodes `+0x1..+0x6` and `+0x7..+0xc` as the
    near/far face normals and `+0xd`/`+0xe` as baked shade bytes — and this same class builds
    `_normals` for them in its constructor.
    *Fix:* restate as "the parallel arrays hold what DBSIM keeps in `+0x1..+0xe`".

15. **Diagonal selector 1 is documented as having "no observed producer" in two files that produce
    it.** `src/Herculan.Engine/Terrain/HeightGrid.cs:319-322` and
    `src/Herculan.Engine/Render/TerrainMeshBuilder.cs:76-78` against `HeightGrid.cs:198`, which
    writes `_diagonals[cell] = 1` for every coplanar quad.
    *Fix:* change both comments to name selector 3 only.

16. **`TSSurfaceEntry`'s comment denies the frame-index resolution the engine implements.**
    `src/HercWorks.Core/Data/File/Dts/TSSurfaceEntry.cs:5-17` ("reads FrontColor as an index into a
    per-surface runtime lookup record (**not** a direct DBA-frame index)") against
    `docs/formats/dts-texture-binding.md:139` and
    `src/Herculan.Engine/Render/DtsMeshBuilder.cs:820-831`
    (`atlas.Frame(surfaces[surfaceIndex].FrontColor)`).
    *Fix:* replace with the four-line field meaning plus a link.

17. **`TextureAtlas.AverageColor`'s doc states a reading the format doc lists as disproven.**
    `src/Herculan.Engine/Render/TextureAtlas.cs:90-100` ("the exe resolves a flat face's `FrontColor`
    as a frame index into the mesh's own bound DBA and uses that frame's pixel data as a per-pixel
    dithered shading swatch") against `docs/formats/dts-texture-binding.md:450-451`, whose
    Rejected-readings table has exactly that as row 2 and rejects the average-colour reading in row 3.
    *Fix:* describe it as the fallback used when the theater has no shade-ramp table.

18. **`TextureAtlas` and `SceneModelLibrary` disagree about cutout frames.**
    `src/Herculan.Engine/Render/TextureAtlas.cs:122-126` ("a mesh texture frame has no transparent
    index") against `src/Herculan.Engine/Scene/SceneModelLibrary.cs:462-481`, which passes
    `transparentBank: true` for both structure paths; `docs/formats/dts-texture-binding.md:414` sides
    with `SceneModelLibrary`.
    *Fix:* correct `TextureAtlas`; restate `SceneModelLibrary`'s paragraph as a plain statement.

19. **`DtsMeshBuilder`'s summary says it produces untextured flat-shaded triangles.**
    `src/Herculan.Engine/Render/DtsMeshBuilder.cs:13-14` against the same comment block's `:24-42`,
    which describes texture resolution through the DBA chain and three ramp-shading mechanisms.
    *Fix:* change the opening line; also move the block onto the class (it currently documents
    `MeshSegment` — see A3.8).

20. **`TerrainMeshBuilder.SurfaceColor` says terrain texturing is not implemented.**
    `src/Herculan.Engine/Render/TerrainMeshBuilder.cs:124-128` ("textured-rendering work the first
    milestone deliberately leaves out") against `:41-43, 63`, where `Build` takes a
    `TerrainTextureBank` and calls `bank.CellRect(...)` per cell.
    *Fix:* reword to "the fallback when a cell's material or frame does not resolve".

21. **`TSBitmapPart` still carries the pre-`ModelSkinId` guesswork about bank naming.**
    `src/HercWorks.Core/Data/File/Dts/Part/TSBitmapPart.cs:10-16` ("per user domain knowledge … not
    one uniform naming rule … except a 'certain mechs use NEWHERCS.DBA instead' exception") against
    `docs/formats/dts-texture-binding.md:114-136`, which has the rule byte-verified via
    `HercSimDat.ModelSkinId` (file offset 148) into a 7-entry table.
    *Fix:* replace with "the bank is chosen by `HercSimDat.ModelSkinId`".

22. **`TSCellAnimPart` carries a `FIXME` for a format that is fully solved.**
    `src/HercWorks.Core/Data/File/Dts/Part/TSCellAnimPart.cs:5-10` ("FIXME (carried over from Java) …
    Will test other files") against `docs/formats/dts-billboards.md:22-42`, which has
    `TSCellAnimPart_Render` (`004767e4`) decompiled and `AnimSequence` located at `part+0x12`.
    *Fix:* replace with the field meaning and a link.

23. **`DefaultShapeColors` is listed as a disproven reading but is still the model viewer's live
    colour path.** `docs/formats/dts-texture-binding.md:402-403, 452` calls it "a 13-entry guess
    table … Mostly clamps to cyan" and claims `Model3DViewerControl` implements the averaged-colour
    path — but `src/HercWorks.UI/DtsGeometryBuilder.cs:503` colours every surface with
    `DefaultShapeColors.Color(...)` and never averages, and
    `src/HercWorks.Core/Data/File/Dts/DefaultShapeColors.cs:5-9` still says "Can't figure out the
    pattern".
    *Fix:* correct the doc's status line and mark the class as a superseded stand-in.

24. **`Overlay2DRenderer`'s comments describe an engine three milestones out of date.**
    `src/Herculan.Engine/Render/Overlay2DRenderer.cs:13-14, 30-32` ("flat-color placeholder shapes",
    "Frame selection is fixed at each bank's first frame … nothing here animates") against `AddMfd`
    at `:1297` picking `LitFrame`/`UnlitFrame`. `AddMfd`'s own list item says "MFD frame 0" while the
    code calls `MfdLayout.BackgroundFrame` and both `MfdLayout.cs:76-77` and `docs/formats/mfd.md:173`
    state "**Frame 0 is never used as a background**". `:1237-1239` says SCANNER and TARGET STATUS
    "draw their screen and buttons only", but the switch at `:1311-1320` drives both from live state.
    *Fix:* rewrite all four passages against what the methods now do.

25. **`GAUFile.cs` still describes the throttle block at 1016 as a track rect plus four detent
    points.** `src/HercWorks.Core/Data/File/Gau/GAUFile.cs:47-54` against
    `docs/formats/cockpit-hud.md:507-518` ("handed `.GAU` offset **1000**, not 1016"; ints `[8..15]`
    are "**two rects, not four points**") and `src/HercWorks.Core/Data/File/Gau/HThrottle.cs:16-25`,
    which calls the old reading superseded.
    *Fix:* rewrite the 1000/1016 entries in `GAUFile.cs`; delete the detent-point language.

26. **`str-strings.md`'s group table omits about ten groups the other docs index against.**
    `docs/formats/str-strings.md:47-68` is headed "Groups referenced by decoded code" but lists none
    of 2 (`OFFLINE`), 3 (`" POD"`), 9 (XMIT/CANCEL/EXIT), 11 (MAP/DAMAGE), 12, 13-16, 19 (`NO TARGET
    SELECTED`), 33 or 40 — all named in `docs/formats/cockpit-hud.md`,
    `docs/formats/heads-down-display.md`, `src/Herculan.Engine/Content/HddLayout.cs:130-146` and
    `src/Herculan.Engine/Content/WeaponRowState.cs:58-71`.
    *Fix:* add the missing rows; this file is the index the others point at.

27. **`ScriptDat`'s block-10 comment says DBSIM discards a block the engine's loader depends on.**
    `src/HercWorks.Core/Data/File/Msn/Script/ScriptDat.cs:310-313` ("**DBSIM reads and fully discards
    every instance of this block.**") against `docs/formats/script-dat.md:222` ("pass 1 reads all 14B
    and discards it; **pass 2 resolves it**"), the same file's own class comment at `:76-83`, and
    `src/Herculan.Engine/World/MissionLoader.cs:501-524`, which reads `LinkedRefs22[..].RefRow8` to
    build every group's route.
    *Fix:* "pass 1 discards it; pass 2 resolves it into the group's route link".

28. **`ScriptDat`'s header comment still calls the zone index unconfirmed.**
    `src/HercWorks.Core/Data/File/Msn/Script/ScriptDat.cs:26-29` ("**Mostly unconfirmed** … mission/
    chapter id? a checksum?") against `src/Herculan.Engine/World/ScriptDatHeader.cs:15-19`, which
    says it "**resolves** script-dat.md's open question", and `docs/formats/script-dat.md:255-263`,
    which carries the decoded table (0 = theater, 2 = zone, 18 = variant).
    *Fix:* point at `ScriptDatHeader`; delete the guess.

29. **`msn-mission-file.md`'s "How to apply" is a to-do list for work already done.**
    `docs/formats/msn-mission-file.md:5, 406, 408` ("`MissionFileTransformer.cs`'s `TRAIN5.MSN`-
    hardcoded layout is wrong and **must be replaced**"; "**Most have no current C# model**"; "**Row
    #4 is fully wrong** — the existing `UnitInfo` hypothesis … **build this one fresh**") against
    `src/HercWorks.Core/Io/Transform/Common/MissionFileTransformer.cs:5-52`, which implements the
    17-row table including skip-row #5 and row #8's nested entries. Every row has a model under
    `Data/File/Msn/`, and `RewardPackage144.cs` replaced `UnitInfo`, which no longer exists in the
    tree.
    *Fix:* replace the section with a pointer to the implementation.

30. **`torso-aim.md` blocks a feature on something that now exists.**
    `docs/simulation/torso-aim.md:196-197` ("**Automatic Turret Tracking** ([T]) … **Needs target
    selection.**") against `src/Herculan.Engine/Sim/TargetSelection.cs:1-40` and
    `src/Herculan.Engine/Sim/MechObject.Combat.cs:102`. The feature is still unported; the stated
    blocker is gone.
    *Fix:* name what is actually missing (the input path's third turret branch feeding a snap target
    from `Mech_TargetRelativeToPilot`).

31. **`SimMath.Q10Multiply` is documented as having no traced caller; five call sites are ported.**
    `src/Herculan.Engine/Numerics/SimMath.cs:42-48` ("**no caller has been traced to it yet**, so
    which unit domain it serves is still open") against `MechObject.cs:539`,
    `MechObject.Locomotion.cs:111, 176, 336`, `MechObject.Power.cs:150-156`, and the callers named in
    `docs/simulation/mech-locomotion.md:18, 118, 159` and
    `docs/simulation/reactor-energy-pool.md:37-39`. The same stale hedge for Q14 sits at
    `docs/simulation/dbsim-physics-notes.md:45-48` ("no caller confirms that"), now contradicted by
    `SimTrig`/`Transform3`.
    *Fix:* state the confirmed domains (Q10 = speed/throttle/reactor scalars, Q14 = rotation
    matrices) with one call site each.

32. **The locomotion comment states the opposite sense of the damage terms.**
    `src/Herculan.Engine/Sim/MechObject.Locomotion.cs:124-126` ("Three of the original's terms are
    omitted because they are all **exactly zero at full health** … and the `mech+0x317` subsystem's
    **throttle runaway**") against `docs/simulation/mech-locomotion.md:319-321` ("the sense of the
    first term was corrected 2026-08-23 and is no longer 'zero at full health' — it is **maximal** at
    full health"). The omission is therefore not harmless at full health, and the term is a speed
    bonus, not a runaway. See also D1.
    *Fix:* restate as "the Turbo Pod's speed bonus (maximal at full health) and the two damage-gated
    flat penalties are not modelled yet."

### A2. Numeric and symbol disagreements

33. **`SplashFactor` is Q8 in four places and Q10 in three; the code executes Q10.**
    Q8: `docs/simulation/weapon-firing.md:121` and `:137`,
    `src/Herculan.Engine/Sim/WeaponShot.cs:137`,
    `src/HercWorks.Core/Data/File/Dat/Sim/ProjectileData.cs:79`.
    Q10: `docs/simulation/damage-system.md:483-485` ("earlier passes of this doc called it Q8"),
    `docs/engine/handoff-weapon-effects.md:30`,
    `src/Herculan.Engine/Sim/MechObject.Combat.cs:314`.
    The executed line is `MechObject.Combat.cs:338`:
    `SimMath.Q10Multiply(shot.SplashFactor, armorDamage)`. *(Independently verified.)*
    *Fix:* change all four Q8 sites to Q10 and delete the "an earlier note said Q8" asides.

34. **Sphere-radius range: 40–300 in one file, 40–600 in two others.**
    `src/Herculan.Engine/Sim/CollisionModel.cs:57-58` ("spheres 40 to 300 world units across")
    against `docs/simulation/hit-detection.md:128` and
    `src/HercWorks.Core/Data/File/Dbsim/ColliderModel.cs:5` (both 40–600 radii).
    *Fix:* make `CollisionModel.cs` read 40–600 radii.

35. **`Weapons.cs` cites three collision reader symbols that `hit-detection.md` renamed.**
    `src/HercWorks.Core/Data/File/Dat/Sim/Weapons.cs:17, 59, 66` still name
    `Collision_LoadSubSphereFlag` / `Collision_LoadSubMeshIndices`;
    `docs/simulation/hit-detection.md:73-76` records the rename to `Collision_ReadCluster`
    (`0040cc14`) / `Collision_ReadSphereArray` (`0040c7c4`).
    *Fix:* rename the three citations; then `docs/formats/weapons-dat-sim.md:19`'s parenthetical
    about the rename can go too.

36. **Two addresses for `Light_CreateMissionSun` inside one file.**
    `src/Herculan.Engine/Render/MissionSun.cs:8` (`004614fc`'s callee) versus `:73` (`00461240`);
    `docs/formats/terrain-lighting.md:55` and `docs/formats/dts-texture-binding.md:325` both give
    `00461240`.
    *Fix:* use `00461240` in both places.

37. **`TSGroup_RenderPolys`' address differs by one nibble between docs.**
    `docs/formats/distance-fog-and-sky.md:59` gives `FUN_004758ce`;
    `docs/formats/dts-node-posing.md:12` and `src/Herculan.Engine/Render/DtsMeshBuilder.cs:299` give
    `004758c8`.
    *Fix:* verify in Ghidra and make the three agree.

38. **`+0x10c`: load-time constant or per-frame detail-table read?**
    `docs/formats/terrain-heightmap.md:22` and `docs/formats/distance-fog-and-sky.md:28` say the
    `10 >> (cellShift - 14)` derivation is the zone loader's, "at load time";
    `docs/formats/terrain-texturing.md:91-95` and
    `src/Herculan.Engine/Terrain/TerrainZoneLoader.cs:140-148` say `Terrain_SetupVisibleRegion`
    re-derives it every frame from `DAT_004a0bcc[DAT_004d1fc3]`, with 10 only the retail default.
    *Fix:* state the per-frame source once in `terrain-texturing.md`; the other two reference it.

39. **`+0x10c` is claimed to have exactly one reader, next to a list of four.**
    `docs/formats/distance-fog-and-sky.md:18-20` ("this is it, and it is **the only reader**") against
    `docs/formats/terrain-texturing.md:91-105`, which names `Terrain_SetupVisibleRegion` (`0046ca98`),
    `Terrain_BuildDrawRegionQuad` (`0046d220`) and `maybe_Terrain_ComputeViewDistance` (`00470910`).
    `src/Herculan.Engine/Terrain/HeightGrid.cs:67-74` repeats "its one consumer is
    `Terrain_DrawCellQuad`".
    *Fix:* cross-reference the consumer list from both places.

40. **Theater index range: 0–2 in one doc, 0–4 in another — and the first contradicts its own table.**
    `docs/formats/terrain-texturing.md:47-54` ("theater index (**0, 1, or 2**)") sits directly under
    its own table at `:41-45` listing "**Five theaters**, two variants each";
    `docs/formats/script-dat.md:254-263` gives "theater index, **0-4**".
    *Fix:* delete `terrain-texturing.md`'s header subsection and link `script-dat.md`.

41. **The LED gauge's fill colours are grey in one comment and blue in the doc — and the same code
    file contradicts itself.** `src/Herculan.Engine/Content/HudColorTable.cs:40-42` says ids 6/5
    "resolve to near-identical greys (112,112,112 and 100,100,100)"; `:77-78` in the same file says id
    6 is "palette 98, the same blue an LED bar's even columns use";
    `docs/formats/cockpit-hud.md:492-493` gives palette 98/97/16 = `(0,116,204)`, `(0,40,160)`.
    `docs/formats/cockpit-hud.md:683` and `src/Herculan.Engine/Content/CockpitArt.cs:300` both repeat
    the superseded "a capacitor bar is blue where the energy meter is grey".
    *Fix:* settle on 98/97; delete the greys sentence and reword the two "blue where grey" lines.

42. **`FUN_00444d5c` is `ShieldsGauge`'s constructor in code and `EnergyPoolGauge_Ctor` in the doc.**
    `src/Herculan.Engine/Content/HudColorTable.cs:35-36, 56-57` against
    `docs/formats/cockpit-hud.md:492, 496-498`, which explicitly rules the other reading out ("the
    binary's own class-name table pairs `EnergyPoolGauge` with `LEDBarGraphV` … `ShieldsGauge` is a
    different class entirely"). The real `ShieldsGauge_Ctor` is `004434fc`, which
    `Overlay2DRenderer.cs:1192` uses correctly.
    *Fix:* rename both mentions in `HudColorTable.cs`.

43. **`HddLayout.DamageFooter` states a box size its own expression does not produce.**
    `src/Herculan.Engine/Content/HddLayout.cs:415-418` says "centred in a 80x14 device box"; the rect
    it documents, `(X0+56, Y1-18)-(X0+136, Y1-4)`, is 81x15 inclusive, which is what
    `docs/formats/heads-down-display.md:267` says.
    *Fix:* change to 81x15.

44. **`HudSpriteSheet` names a clip-region folder that does not exist.**
    `src/Herculan.Engine/Content/HudSpriteSheet.cs:27-28` lists the `d`/`h` split as "… `dmg`/`hdg`";
    the literals are `"edg"`/`"hdg"` per `docs/formats/cockpit-hud.md:280` and
    `src/Herculan.Engine/Content/CockpitClipRegions.cs:25-26`.
    *Fix:* `dmg` → `edg`.

45. **Two comments give different ranges for the throttle tick nudge.**
    `src/HercWorks.Core/Data/File/Gau/GAUFile.cs:61-63` ("-2 to -4 for most hercs, +14/+17 for
    RAZOR/TOMAHAWK") against `src/HercWorks.Core/Data/File/Gau/HThrottle.cs:61-62` ("Small and
    per-herc (-4 to +14)"). `docs/formats/cockpit-hud.md:522` states the role but not the range, so
    nothing arbitrates.
    *Fix:* measure once, state it in `HThrottle.cs` only.

46. **The shield-capacity function has two names inside one doc.**
    `docs/simulation/reactor-energy-pool.md:50` calls it `Mech_ComputeShieldCapacity`; the same doc's
    pod table at `:78` and `src/Herculan.Engine/Sim/MechObject.Power.cs:163` call it `FUN_00417bec`.
    *Fix:* one name.

47. **Bullet subtype 9's weapon list has already drifted.**
    `docs/simulation/projectiles.md:42` lists "PLAS, MFAC, MAGN";
    `src/Herculan.Engine/Sim/BulletCatalog.cs:32` lists "PLAS, MFAC".
    *Fix:* settle whether MAGN belongs, then keep one copy (see B4).

48. **Three different name sets for the same `.MSN` rows.**
    `docs/formats/msn-mission-file.md:231, 320, 351, 380` uses `Heading10`, `EntitySpawn164`,
    `EntityTemplate144`, `UnitSpawn58`; `docs/formats/script-dat.md:31-41` uses `Flag10`,
    `UnkEntity164Bytes`, "(144B type)", `LinkedRef58`; the classes are `Flag10.cs`,
    `UnkEntity164Bytes.cs`, `SpawnRecord144.cs`, `LinkedRef58.cs`. `msn-mission-file.md` also
    disagrees with itself — `:109` calls row #16 `UnkEntity164Bytes`, `:320` calls it
    `EntitySpawn164`. `script-dat.md:214` additionally re-derives row #7's heading meaning as a new
    finding when `msn-mission-file.md:231` already titles it `Heading10`.
    *Fix:* adopt the class names as canonical everywhere; cut the re-derivation to a cross-reference.

### A3. Broken and wrong cross-references

49. **Two code comments link a doc path that does not exist.**
    `src/HercWorks.Core/Data/File/Dbsim/TerrainRampFile.cs:18` and
    `src/HercWorks.Core/Data/File/Dbsim/WorldData.cs:49` both cite
    `docs/simulation/distance-fog-and-sky.md`; the file is `docs/formats/distance-fog-and-sky.md`.
    *(Independently verified — the only broken path among 23 doc references in code.)*

50. **Three code comments cite sections and symbols that do not exist.**
    `src/Herculan.Engine/Render/DtsMeshBuilder.cs:99` and
    `src/Herculan.Engine/Render/TextureAtlas.cs:25` cite `dts-texture-binding.md`'s "UV-generation
    formula — FOUND"; the heading is "Render path and UV generation".
    `src/Herculan.Engine/Render/SpriteRenderer.cs:193` cites a constant `UpAxisProbeLength` that
    exists nowhere in the repo. `src/Herculan.Engine/Sim/Anim/SkeletonPose.cs:10`,
    `DtsMeshBuilder.cs:53` and `docs/formats/dts-node-posing.md:61` all name
    `AnimationThread.NodeTransform`; the method is `ShapeInstance.NodeTransform`
    (`src/Herculan.Engine/Sim/Anim/ShapeInstance.cs:55`).
    *Fix:* repoint each; define `UpAxisProbeLength = 0x800` in `SpriteRenderer` or drop the citation.

51. **`TextureAtlas.AverageColor` cites a `"Flat-shaded lighting"` section that does not exist** in
    `dts-texture-binding.md` (`src/Herculan.Engine/Render/TextureAtlas.cs:90-100`).

52. **Two comments link the wrong repo.**
    `src/HercWorks.Core/Data/File/Dts/TSSurfaceEntry.cs:5-17` and
    `src/HercWorks.Core/Data/File/Dts/Part/TSBitmapPart.cs:10-16` link
    `HercWorksMDK-CSharp-port/docs/…`, which is not this repo's layout.

53. **`Herculan/README.md:38` cites `docs/engine/planning.md`'s "Known technical debt" section;**
    `planning.md` has no such section.

54. **`docs/formats/msn-mission-file.md:80` says "see 'How this was verified' below";** the nearest
    section is `### Verification note` at `:116`.

55. **`docs/formats/script-dat.md:280` says "(see rule 5 above)" for the player's squad;** that is
    rule 6 at `:125`.

56. **`Overlay2DRenderer.Draw`'s doc refers to a `<paramref name="widgets"/>` that no longer
    exists** — the parameter is `hud` (`src/Herculan.Engine/Render/Overlay2DRenderer.cs:96-97`).

57. **`planning.md:51` hardcodes the repo as `E:\ES2Stuff`,** which is not where it is checked out.
    *Fix:* drop the absolute path.

58. **Four XML doc comments are bound to the wrong member** because a second `<summary>` was inserted
    below an existing one, silently attaching the first block to the preceding member and leaving the
    intended one undocumented: `src/Herculan.Engine/Content/CockpitArt.cs:388-396`
    (`ReadColorSchemeIndex`'s doc sits on `LoadPaperDoll`), `CockpitArt.cs:474-482` (`LoadFrame`'s doc
    sits on the private `_shieldRingColors` field), `src/Herculan.Engine/Content/MfdLayout.cs:350-365`
    (`IntegrityReadout`'s doc sits on `DistanceReadout`),
    `src/Herculan.Engine/Content/HddLayout.cs:306-330` (`WidgetVisible`'s doc sits on `IsLatching`).
    The same defect affects `src/Herculan.Engine/Render/DtsMeshBuilder.cs:13-42` (see A1.19) and
    `src/Herculan.Engine/Render/TextureAtlas.cs:99-100` (a dangling `TODO` between two `<summary>`
    blocks).
    *Fix:* move each orphaned block onto the member it describes.

---

## B. Duplicated information

Each entry lists every copy found. In six cases the copies have already drifted (marked **drifted**).

1. **The world-scale derivation, in full, twice.**
   `docs/engine/planning.md:107-159` and `src/Herculan.Engine/Render/WorldScale.cs:14-65` carry the
   same `Hud_WorldUnitsToMetres` (`00434228`) derivation, the same three call sites, the same
   COLOSSUS `-400`/`UnitOffsetYAdjust` argument, the same OUTLAW 1500-against-1700 argument, the same
   10.2m–15.5m vs manual 6.1/10.4m reconciliation, and the same `MechType_InitOne` note.
   **Drifted:** both still gloss `AiAimTargOffset` as "how high up a target the AI aims", which
   `damage-system.md:126` and `MechTypeRecord.cs:136` retired. *(Independently verified.)*
   *Fix:* keep the derivation in `planning.md`; reduce `WorldScale.cs` to the constants and a link.

2. **The raycast sweep's four properties, four times.**
   `docs/simulation/damage-system.md:37-57`, `docs/simulation/weapon-firing.md:194-211`,
   `docs/simulation/hit-detection.md:13-22`, `src/Herculan.Engine/Sim/SimWorld.cs:200-236`.
   Terrain-first, ray shortening ("a candidate found later but nearer wins", verbatim in two), the
   500-unit early break and the undeployed-group filter appear in all four; `hit-detection.md` and
   `SimWorld.cs` share the same example sentence about "seven objects in three overlapping pairs".
   `weapon-firing.md:198` even says "documented in damage-system.md" and then documents it anyway.
   *Fix:* `damage-system.md` canonical; one-line references elsewhere.

3. **The shield system, twice at equal length.**
   `docs/simulation/damage-system.md:194-286` and `src/Herculan.Engine/Sim/ShieldCharge.cs:5-73,
   116-130`: the five-field `+0x222` layout, "there is one pool, not two", the fleet-wide 3500 at file
   offset 190, 5-per-tick / 700-tick / 28-second, the `±0x66` balance step, the "readouts always sum
   to 200" trap. **Drifted** — see A1.6.
   *Fix:* RE evidence in the doc; layout table plus link in the code.

4. **Retail `BULLETS.DAT` and `ROCKETS.DAT` tables, twice each.**
   `docs/simulation/projectiles.md:23-42` vs `src/Herculan.Engine/Sim/BulletCatalog.cs:22-35`;
   `docs/simulation/rockets.md:23-44` vs `src/Herculan.Engine/Sim/RocketCatalog.cs:26-49`.
   **Drifted** — see A2.47.

5. **The shot record layout, twice with different field sets.**
   `docs/simulation/weapon-firing.md:112-134` (two tables) and
   `docs/simulation/damage-system.md:456-468` (annotated pseudo-C), the latter linking to the former
   in the sentence directly above. **Drifted** — this is where A2.33's Q8/Q10 split lives.

6. **The mission sun's derivation, three times.**
   `docs/formats/dts-texture-binding.md:298-338`, `docs/formats/terrain-lighting.md:24-64`,
   `src/Herculan.Engine/Render/MissionSun.cs:13-55, 70-85`: the same `FUN_0048c060` vs
   `Light_ComputeShadeForFace` (`0048bedc`) comparison, the same `(dot - 0x400000) >> 1` derivation,
   the same middle-column argument, the same `(±0.758, -0.359, -0.544)` / horizontal 0.839 / vertical
   0.544 result. **Drifted** — see A2.36.
   *Fix:* `terrain-lighting.md` canonical.

7. **`ScalePerTickStep`'s deviation, six times.**
   `docs/simulation/dbsim-physics-notes.md:37-41` *and again* at `:111-117` in the same file,
   `docs/simulation/mech-locomotion.md:206-213`, `src/Herculan.Engine/Numerics/SimMath.cs:96-116`,
   `src/Herculan.Engine/Sim/SimWorld.cs:83-88`, `src/Herculan.Engine/Sim/MechTypeRecord.cs:85-95`.
   *Fix:* full rationale in `SimMath.ScalePerTickStep` only.

8. **The mission deployment mechanism, three times.**
   `docs/simulation/mission-deployment.md:16-30, 64-114`,
   `src/Herculan.Engine/World/MissionLoader.cs:53-86`, `src/Herculan.Engine/Sim/SimObject.cs:234-259`.
   Three copies of the verb list, the 150000/90000 distances, the ±90°/±22.5° bearings and the
   70000-95000 drop height.

9. **The `.HFN` header layout and the label-placement formula, three times.**
   Header: `docs/formats/dfn-hfn-dci.md:88-148` vs `src/Herculan.Engine/Content/HudFont.cs:28-54`.
   Placement formula: `docs/formats/mfd.md:195-204`, `docs/formats/dfn-hfn-dci.md:140-145` (which
   then says "Full placement formula … see mfd.md"), and `HudFont.cs:84-96, 127-146`.

10. **The `COCKPIT.DPL` palette-window derivation, twice** —
    `docs/formats/cockpit-hud.md:311-360` vs `src/Herculan.Engine/Content/CockpitPalette.cs:6-46`
    (the `Palette_InstallRange(0x2a, 0x18, …)` call, slots 42-65, the nine-herc permutation, the APOCA
    `i-10` / COLOSSUS `i+14` corroboration, the heading-tape-74 / hazard-stripe-13 consequences) —
    **and the shield ring ramp again**, `cockpit-hud.md:603-624` vs `CockpitPalette.cs:120-143`.

11. **The `.HD*`/`.ED*` clip-region format, twice.**
    `docs/formats/cockpit-hud.md:216-270` vs `src/Herculan.Engine/Content/CockpitClipRegions.cs:12-59`
    — layout block, the `ActiveScanlineClipSpans` chain, the `APOCA.HD0` "row 204 +168 twice"
    observation, the rows-0-371 verification, and the whole `piVar4[1] = piVar1[3]` divergence
    paragraph.

12. **The sky gradient and fog colour, twice at full length.**
    `src/Herculan.Engine/Content/SkyGradient.cs:6-29` and
    `src/Herculan.Engine/Content/ShadeRamp.cs:168-185` vs
    `docs/formats/distance-fog-and-sky.md:78-106` — the same two reference screenshots, the same
    `#D4D0D4 #D4D0D8 #D8D0D8` run, `#985C20`→`#747060`, the 6-rows-at-480 band height, the
    `FUN_00401d94`/`004a80d0` uninitialised-callback reason, the "entry 222 or 223 so the horizon has
    no seam" argument.

13. **`ShadeRamp`'s "Who gets faded" paragraph is `distance-fog-and-sky.md`'s "What gets faded"
    section.** `src/Herculan.Engine/Content/ShadeRamp.cs:37-45` vs
    `docs/formats/distance-fog-and-sky.md:47-76` — same three callers, same `0042xxxx`-family
    argument, same type-3/`FUN_00428c60` trace, same closing sentence.

14. **`grid+0x10c`, explained independently three times.**
    `docs/formats/terrain-heightmap.md:22`, `docs/formats/terrain-texturing.md:87-105`,
    `docs/formats/distance-fog-and-sky.md:9-29`. **Drifted** — see A2.38 and A2.39.

15. **`DtsMeshBuilder.ResolveSolidColors` restates the `TSSolidPoly` doc section.**
    `src/Herculan.Engine/Render/DtsMeshBuilder.cs:984-1032` vs
    `docs/formats/dts-texture-binding.md:200-220` — same pseudocode, same `FUN_0048d518` / mode-4 /
    `iVar11 == 4` outline explanation, same retail counts (12 in `BULLETS.DTS`, 57 in `ROCKETS.DTS`,
    73 fleet-wide, 1227 of APOCA's 1368, 2049 of `BASES_AN`'s). See also C5.

16. **`SpriteRenderer`'s class doc re-derives all of `dts-billboards.md`'s render section.**
    `src/Herculan.Engine/Render/SpriteRenderer.cs:32-59` vs `docs/formats/dts-billboards.md:45-90` —
    same four steps, the `(radius * 4 << focalShift) / depth` scale, the "one bitmap pixel is
    `radius / 64` world units" conclusion, the `(0, 0, 0x800)` probe, the
    `cols + (rows - cols) * measured / 0x800` squash, the EMP round's `(45, 45)` against 40x30 frames.

17. **`SurfaceShading.GouraudColor` reproduces the doc's `TSGouraudPoly` section, including its
    evidence hex run.** `src/Herculan.Engine/Render/SurfaceShading.cs:57-75` vs
    `docs/formats/dts-texture-binding.md:248-271` — including the identical eleven-colour capture
    `#9090a4 … #343444`.

18. **`HercSimDat.ModelSkinId`'s comment reproduces the whole skin-to-bank table.**
    `src/HercWorks.Core/Data/File/Dat/Sim/HercSimDat.cs:142-157` vs
    `docs/formats/dts-texture-binding.md:118-136` — all seven groups with full mech rosters.

19. **`Cam.cs`'s class comment restates `bnd-notes.md`.**
    `src/HercWorks.Core/Data/File/Bnd/Cam.cs:3-26` vs `docs/formats/bnd-notes.md:7-22, 52-56, 64-70`
    — the 9-byte envelope byte by byte, the 21-of-22 Java field match, the `Unknown7` "50 vs 80"
    hex-transcription theory, the trailing byte, the build-time-only conclusion with its
    ROCKET/PWEAPONS evidence.

20. **The `.STR` layout and verification prose, verbatim twice.**
    `docs/formats/str-strings.md:20-43` vs `src/Herculan.Engine/Content/SimStringTable.cs:13-37`.

21. **The pod slot/id table, twice.**
    `docs/simulation/reactor-energy-pool.md:67-91` vs `src/Herculan.Engine/Sim/MechPods.cs:3-40` —
    same slot/offset/id/name/effect table, same `0x1f→[4] 0x20→[3]` crossing, same "assigns rather
    than accumulates" note.

22. **The three-thread override rule and per-HERC node lists, three times.**
    `docs/simulation/torso-aim.md:114-127`, `src/Herculan.Engine/Sim/Anim/ShapeInstance.cs:70-83`,
    and a partial third at `src/Herculan.Engine/Sim/MechObject.cs:80-85`.

23. **The `DAT_0049a06e` "not a gear selector" argument, three times.**
    `docs/simulation/mech-locomotion.md:124-138`, `src/Herculan.Engine/Sim/MechControls.cs:27-43`,
    `src/Herculan.Engine/Sim/MechObject.Locomotion.cs:40-52`.

24. **Three gap lists maintained in parallel.**
    `docs/engine/planning.md:161-204`, `docs/engine/handoff-weapon-effects.md:61-105`,
    `Herculan/KNOWN_ISSUES.md:16-33`. The structures-clip item appears in all three; the FOV guess in
    two (`planning.md:200-201`, `handoff:95-99`); the external view in two (`planning.md:187-189`,
    `KNOWN_ISSUES.md:26`); `planning.md`'s "Combat gaps" bullet is a compression of the handoff's
    "Also outstanding", itself a compression of each topic doc's "Not ported".
    *Fix:* one register — `KNOWN_ISSUES.md` for behavioural divergences, per-topic "Not ported" for
    unimplemented work — and delete both aggregates.

25. **The same "Rejected reading" row in two tables.**
    `docs/formats/dts-texture-binding.md:457` and `docs/formats/terrain-lighting.md:114` — "A per-row
    brightness multiplier over an expanded RGB texel, in place of the indexed lookup", near-verbatim.

---

## C. Rambling and low-value prose

1. **`GAUFile.cs`'s class comment is a 130-line investigation log.**
   `src/HercWorks.Core/Data/File/Gau/GAUFile.cs:6-137`. Dated entries ("Confirmed 2026-08-09",
   "Corrected 2026-08-17"), method narration ("a user-supplied pixel measurement … then confirmed
   decisively by overlaying"), the same decision justified five times ("this app has no HUD renderer
   to visually confirm a guess against … rather than force-fitting an unverified model"), and
   instructions to future sessions at `:127-130` ("a future session shouldn't re-run the same
   string/constructor search without a genuinely new angle"). None of it is layout information.
   *Fix:* reduce to the offset table plus the three load-bearing facts (round-trips byte-exact,
   `Remainder` written verbatim, NAVBAR a confirmed negative).

2. **`msn-mission-file.md`'s fourteen "Model:" lines restate the table directly above them.**
   `docs/formats/msn-mission-file.md:139, 155, 178, 194, 216, 229, 243, 258, 272, 295, 317, 349, 377,
   398`. Typical: after a table listing GUID, four fields marked "**dead** — always `-1`", and X/Y/Z
   int32s — "**Model:** Flat position record. Four dead fields are declared but never used; model as
   GUID + X/Y/Z + opaque padding." Row #7's is "**Model:** Minimal {GUID, heading}" under a table
   saying exactly that. One of them (row #12) adds only a contradiction — see D5.
   *Fix:* delete all fourteen; fold the two that carry a real fact (row #8's inheritance rule, row
   #16's payload discriminator) into the tables' notes column.

3. **`damage-system.md`'s "Port notes" re-narrates the whole document.**
   `docs/simulation/damage-system.md:591-621` (31 lines). Point 1 repeats the shield hard cap already
   stated at `:88-89` and `:234-236`; point 2 repeats the direct-vs-explosive contrast from `:186-191`;
   point 4 repeats the recharge constants from `:243-257`; point 5 repeats `PROJ.DAT`'s two damage
   fields from `:478-480`. Paired with inline editorialising already present at `:188-191` ("Using
   the AoE formula for a laser would make it behave like a mini-explosion…").
   *Fix:* cut to a five-line checklist of the non-obvious traps, or delete.

4. **The verification caveat and the fast-magnitude bias, each stated four times.**
   `docs/simulation/dbsim-physics-notes.md:5-10` sets out "checked against raw disassembly, not just
   decompiler output", then repeats it per item at `:45-46` and `:71-75`, then again in
   `src/Herculan.Engine/Numerics/SimMath.cs:35-39, 167-177`. The ~3.4%-low conclusion itself appears
   at `dbsim-physics-notes.md:64-76`, `:86-90`, `:118-120` and `SimMath.cs:167-177`.
   *Fix:* verification method once in the header; the 3.4% figure once, in the toolkit entry.

5. **`ResolveSolidColors` argues for a change that already landed.**
   `src/Herculan.Engine/Render/DtsMeshBuilder.cs:984-1032`, last two paragraphs — "Which is why this
   correction is small and safe as well as right… An autocannon round is meant to be gold",
   "Corroborated across the install: … all 1517".
   *Fix:* cut to the two-line rule (`rampRow(0x80)[Front]`, outline when the ramped line byte
   differs).

6. **`terrain-texturing.md` is organised around questions it never states.**
   `docs/formats/terrain-texturing.md:5` ("Questions 1 and 2 are answered; Question 3 remains open")
   with headings `## Question 2 — ANSWERED: the LOD field` and `## Question 3 — still open`. The
   questions are listed nowhere and there is no Question 1 heading at all.
   *Fix:* retitle by subject; delete line 5.

---

## D. Journal-style writing and append-only rot

1. **`mech+0x317` is documented as three different things in one file, and a fourth in the code.**
   *(The clearest example of the pattern in the whole set.)*
   `docs/simulation/mech-locomotion.md:55` — field table: "Subsystem object; **damage causes throttle
   runaway**".
   `:322-331` — "`mech+0x317` is the **Turbo Pod** (`TURB`, catalog id 31) … A speed bonus that
   degrades, **not a throttle runaway**", with a blockquote at `:329-331` explaining that the earlier
   reading was an artifact of a misnamed function.
   `:436-439` — "## Outstanding — `mech+0x317` subsystem identity", i.e. the identity `:322` states.
   `src/Herculan.Engine/Sim/MechObject.Locomotion.cs:124-126` still calls it the throttle runaway
   (see A1.32). Correctly identified only at `src/Herculan.Engine/Sim/MechPods.cs:40`.
   *Fix:* rewrite line 55, delete the Outstanding bullet, drop the blockquote, fix the code comment.

2. **`planning.md`'s gap list openly disclaims its own currency instead of being maintained.**
   `docs/engine/planning.md:164-165`: "Check the linked topic doc before assuming one of these is
   still current — this list is not re-verified on every edit." At least two of its items are closed
   (A1.2, A1.3) and one rests on a retired field identification (D3).
   *Fix:* a list that disclaims its own accuracy should be replaced by links to each topic doc's own
   Open section.

3. **`planning.md`'s world-scale argument rests on a field identification that was retired.**
   `docs/engine/planning.md:141` and `src/Herculan.Engine/Render/WorldScale.cs:49` both argue from
   "`AiAimTargOffset` (how high up a target the AI aims) tracks model height across the fleet".
   `docs/simulation/damage-system.md:126` and `src/Herculan.Engine/Sim/MechTypeRecord.cs:136` both say
   that name "was a guessed name" and the field is the hit radius. The conclusion may survive — a hit
   radius tracks size too — but the stated argument does not. *(Independently verified.)*
   *Fix:* re-word using the hit-radius reading, or lean on the COLOSSUS `UnitOffsetYAdjust` evidence
   beside it.

4. **`handoff-weapon-effects.md` is a letter to the next session, not a reference document.**
   `docs/engine/handoff-weapon-effects.md:1-63`. "Rewritten 2026-08-28", "## Where this left off",
   then four consecutive paragraphs each opening "**Since:**" and closing "Closed with them:" /
   "Corrected on the way:". Every fact in lines 20-60 is already in the topic docs those same
   paragraphs link to.
   *Fix:* delete `:18-63`; keep "Also outstanding" (`:64-105`), which is the only reference content,
   and rename the file accordingly.

5. **`msn-mission-file.md` row #12's summary contradicts the table three lines above it — and the
   stale wording has already propagated into code.**
   `:365-366` decodes `0x32–0x44` as "**weapon fit**, 10 slots … DBSIM hands this array straight to
   `Mech_ConfigureLoadout`" and `0x46` as "**not dead** — this is the spawn-position override".
   `:377` then says "real payload is **unresolved** 10-slot array (**possibly weapons/items,
   unconfirmed**)". `src/HercWorks.Core/Data/File/Msn/Script/ScriptDat.cs:192-194` quotes the stale
   line back: "the 10-slot array `msn-mission-file.md` row #12 calls the 'unresolved 10-slot array …
   domain unknown'".
   *Fix:* rewrite the Model line from its own table; drop the quotation in `ScriptDat.cs`.

6. **`dgs-hd0-notes.md` is now mostly a record of what it used to say.**
   `docs/formats/dgs-hd0-notes.md:9-10, 24, 27-31, 61-71` — of 84 lines. "(an earlier read of this
   doc's own guess, now corrected)"; "An earlier read of this doc called all three 'id fields'";
   "**Corrects this doc's earlier reading** of steps 5–6 … that walk consumed exactly the same bytes,
   so every retail record parsed correctly while all of it was named wrongly"; "Two earlier readings
   in this file were wrong and are corrected there". The `.HD0` half (`:51-71`) contains no format
   detail at all — it defers entirely to `cockpit-hud.md`. The habit leaks into code at
   `src/HercWorks.Core/Data/File/Dgs/BaseShapeLibrary.cs:64`.
   *Fix:* delete the `.HD*` section, strip the four self-corrections; ~35 lines of real `.DGS`
   container doc remain.

7. **`dbsim-physics-notes.md`'s "Rocket physics" section contains no physics — only past mistakes.**
   `docs/simulation/dbsim-physics-notes.md:96-108`: "Moved to `rockets.md`, which supersedes what was
   here: the earlier reading of `ROCKETS.DAT`'s fields … and of `Rocket_BallisticSteer` … were all
   wrong", followed by a negative result about `fire.cpp`, a file the doc does not otherwise cover.
   *Fix:* delete; the pointer at `:12` already does the job.

8. **`ProjectileData.cs`'s comment is a session-by-session log duplicating `damage-system.md`.**
   `src/HercWorks.Core/Data/File/Dat/Sim/ProjectileData.cs:11-118`, overlapping
   `docs/simulation/damage-system.md:443-544`. "An initial pass guessed …", "**SOLVED 2026-08-11**",
   "Follow-up (2026-08-09, same session)", "Second follow-up (2026-08-09, same session)", "already
   suspected, two sessions ago". It has already drifted twice — A2.33 and A1.12.
   *Fix:* cut to the retail index table plus one link.

9. **`script-dat.md`'s formation section is written as a fix narrative.**
   `docs/formats/script-dat.md:158` — heading "**A base formation slot also turns the structure —
   SOLVED.**"; `:172-173` — "**This is why one structure of a group could stand in the right place
   facing the wrong way**", a fixed bug narrated as current; `:182-193` — "**not implemented;
   reverted after a real regression (2026-08-15)** … A port was attempted, decompiled and
   formula-matched … **reverted same day**", which buries an actionable warning in a blow-by-blow.
   *Fix:* drop "SOLVED" and the pre-fix symptom; reduce the grid-snap bullet to formula + failure
   mode + one-line warning.

10. **"Open" bullets that answer themselves in the same sentence.**
    `docs/formats/cockpit-input.md:319-323` — "…not checked for the throttle or for ordinary MFD/HDD
    leaf buttons themselves. **Settled since** for `ConsoleButton` (`FUN_00442dc8`) and
    `WeaponSelectGadget` (`FUN_00442458`): both take `OnClick` at `+8`…".
    `docs/simulation/weapon-mounts.md:250-252` — "- **`manager+0x0a` is solved** — it is the
    per-subtype missile-lock state".
    *Fix:* move each finding into the body; leave only the unresolved residue in Open.

11. **Cross-references to a document state that no longer exists.**
    `docs/formats/mfd-scanner.md:37-38` and `src/Herculan.Engine/Content/MfdScanner.cs:87-89` both
    describe frames 14-18 as "the five that `mfd.md` listed as having no located consumer" —
    `docs/formats/mfd.md:342-344` has already been updated to "**Frames 14-18 do**". Same defect at
    `docs/formats/distance-fog-and-sky.md:17-19` ("`terrain-heightmap.md` **previously recorded**
    `+0x10c` as a load-time LOD value whose consumer had not been located") against
    `docs/formats/terrain-heightmap.md:22`, which already carries the corrected text. A reader chases
    the reference and finds nothing there.
    *Fix:* drop the "previously listed" clauses.

12. **Corrections narrated in place instead of applied — across twelve docs and six code files.**
    Docs: `cockpit-input.md:152-157` ("Corrected 2026-08-21; **this section previously stated** that
    no widget ever enters capture, on a survey that had covered only the buttons and the shield
    facings") and `:176-177`; `damage-system.md:321-322` ("Renamed 2026-08-23; **the earlier
    `Component_ReadHealthPercent`** had the sense inverted") and `:483-485`;
    `terrain-heightmap.md:36` ("**Recorded here as unwritten until 2026-08-26**");
    `terrain-texturing.md:107-119` ("**now disproved** (2026-08-13)"); `beam-visuals.md:151-155`;
    `reactor-energy-pool.md:50-52` ("**An earlier pass of this doc recorded it as spawn-only.**");
    `weapons-dat-sim.md:19` and `:89-91`; `cockpit-hud.md:347-348` ("This supersedes the earlier
    'assembled from two `.DPL` files' model"), `:507` ("used to start it at 628 … corrected
    2026-08-17") and `:645-647`; `hit-detection.md:74-80` and `:136-141`.
    Code: `CockpitPalette.cs:12-13, 24-25, 27` ("which is what this class previously assumed and got
    backwards"); `CockpitArt.cs:37, 41-44` ("(An earlier revision of this comment called it a
    rear/overhead equipment-bay view; it is not.)"); `HudFont.cs:93-94`;
    `SimWorld.cs:100-103` ("**This replaces an earlier provisional pairing of 256 at 30 Hz**") beside
    `:76` and `:95`, which already say the figure is recovered; `TSSurfaceEntry.cs:5-17` ("an earlier
    version of this comment wrongly claimed…"); `BaseShapeLibrary.cs:64`.
    All three of `cockpit-hud.md`, `cockpit-input.md` and `script-dat.md` carry a "NOTE TO CLAUDE:
    This should be a reference document, not a personal journal" header and then do exactly this.
    *Fix:* delete each retraction. Keep only the ones that warn against a *live* wrong artifact — the
    misnamed symbol still in the Ghidra project, the `HercCollider` reading still in
    `HercWorks.Core` — and move those to the relevant "Rejected readings" table.

13. **Dated "solved on" headers and "why this wasn't obvious at first" asides.**
    `docs/simulation/weapon-mounts.md:3` ("Loadout and naming solved 2026-08-23; selection, chaining
    and linking 2026-08-24"); `docs/simulation/weapon-firing.md:3`;
    `docs/simulation/damage-system.md:551-562` ("**Why this wasn't obvious at first:** … conflating
    the two makes the search for a 'separate beam mechanism' look necessary when it isn't").
    *Fix:* state the finding; drop the date and the account of the wrong turn that preceded it.

14. **`reactor-energy-pool.md` tracks an open issue in prose while saying it is tracked elsewhere.**
    `docs/simulation/reactor-energy-pool.md:131-134` narrates a cosmetic mismatch and then says
    `KNOWN_ISSUES.md` is where it is tracked.
    *Fix:* move it there.

15. **`SimWorld.TickDelta` records a superseded guess beside two statements that it is recovered.**
    `src/Herculan.Engine/Sim/SimWorld.cs:100-103` vs `:76` ("the original's own, recovered
    2026-08-21") and `:95` ("**Recovered, no longer a guess.**").
    *Fix:* keep the `40 * 256 / 125 = 81` derivation; delete all three "no longer a guess" clauses.

16. **`docs/formats/mfd.md`'s scanner section was appended in the wrong place.**
    `## Paint order` at `:311` is followed by `### MFDRadar — mode 3` at `:318`, making the scanner a
    subsection of paint order rather than the sixth entry under `## Screens` beside `### MFDStatus`,
    `### MFDFlashComm` and `### MFDMap`.
    *Fix:* move it up with its siblings.

---

## E. Structural issues

1. **Ten files carry an in-band "NOTE TO CLAUDE" instruction; twenty-nine do not.**
   Line 3 of `cockpit-hud.md`, `cockpit-input.md`, `dfn-hfn-dci.md`, `dts-texture-binding.md`,
   `heads-down-display.md`, `msn-mission-file.md`, `dbsim-physics-notes.md`, `mech-locomotion.md`;
   `potential-modernization-features.md:1`; `KNOWN_ISSUES.md:18`. Worst case:
   `docs/formats/script-dat.md:3`, where "NOTE TO CLAUDE: This should be a reference document, not a
   personal journal." is appended to the end of the doc's own format-summary sentence, mid-paragraph.
   *Fix:* move to a `CLAUDE.md` / docs style note; fix `script-dat.md`'s mangled opening regardless.

2. **No shared header convention across the set.** Three incompatible forms:
   status-in-title (`terrain-heightmap.md:1`, `terrain-texturing.md:1`, `terrain-lighting.md:1`,
   `bnd-notes.md:1`, `dgs-hd0-notes.md:5`); a status-and-port line
   (`distance-fog-and-sky.md`, `dts-billboards.md`, `projectiles.md`, `rockets.md`,
   `impact-effects.md`, `weapon-firing.md`, `weapon-mounts.md`, `reactor-energy-pool.md`,
   `mission-deployment.md`, `missile-lock.md`, `target-selection.md`); and neither
   (`cockpit-hud.md`, `mfd.md`, `hit-detection.md`, `beam-visuals.md`, `torso-aim.md`,
   `hud-target-indicator.md`, `dts-node-posing.md`, `str-strings.md`). The Ghidra/symbols provenance
   boilerplate is verbatim in five docs and absent from a dozen.
   *Fix:* standardise on the status-line form, require an "Engine implementation" line, and move the
   boilerplate to one place.

3. **`Herculan/README.md` names `KNOWN_ISSUES.md` as canonical for a list it does not contain.**
   `README.md:8-11`: "**See `KNOWN_ISSUES.md` for a consolidated list of every bug/quirk found in the
   original Java source during porting.** … that file is the canonical record". `KNOWN_ISSUES.md`
   contains only retail-game bugs and engine divergences — not one Java-port bug. The README then
   lists ~10 such bugs itself at `:40-52` and `:92-106`.
   *Fix:* move the Java-port list into `KNOWN_ISSUES.md` under its own heading, or drop the claim.

4. **`potential-modernization-features.md` is a 5-line stub whose content already has homes.**
   Both bullets exist elsewhere: the light-source gap at `handoff-weapon-effects.md:83-88`, the
   tick-rate caveat at `mech-locomotion.md:210-213`.
   *Fix:* delete the file; fold the two bullets into `planning.md`'s "Vanilla by default" section as
   the opt-in examples that principle already promises.

5. **Mechanical rendering breakages.**
   - `docs/simulation/weapon-mounts.md:158-166` — a blank line at `:160` splits the manager field
     table, orphaning the `+0x14` and `+0x18` rows into a second headerless table.
   - `docs/formats/dts-texture-binding.md:175-176` — `## Poly types and their colour mechanisms` has
     no blank line before it and renders inside the preceding paragraph.
   - `docs/formats/msn-mission-file.md:399-403` — five consecutive blank lines.
</content>
</invoke>
