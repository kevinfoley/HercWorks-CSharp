# Documentation review — outcome

Outcome of the 2026-08-31 review of `Herculan/docs` against `HercWorks.Core` and `Herculan.Engine`,
which raised 113 findings across 79 files. What remains is one deferred structural move and one code
change that wants its own commit; everything else is closed and is recorded below. Both solutions
build with 0 errors / 0 warnings and all 271 tests pass.

The review document itself has been deleted — its line numbers were captured before the fixes and
now point at the wrong places in 25+ edited files. It is in git history at `13e6734`
(`git show 13e6734:Herculan/DOCS_REVIEW.md`) if the full text of a finding is ever wanted; the
`A1.11`-style ids below are its numbering, kept as stable labels.

---

## 1. Deferred

- **E3 — partially done, rest deferred.** `Herculan/README.md`'s claim that `KNOWN_ISSUES.md` was
  "the canonical record" for Java-port bugs was corrected, since it never contained them. The ~10
  Java-port bugs at `README.md:40-52, 92-106` were **not** moved into `KNOWN_ISSUES.md`: they are
  embedded in the per-round porting narrative and lifting them is a structural change to both files.
  If you want that later, they need a third heading such as "HercWorks toolkit — bugs inherited from
  the Java original".

## 2. Not done — a code change with behaviour risk

- **A1.11 follow-up.** `Data/File/Dat/Sim/Weapons.cs`'s `Tail` is 48 raw bytes that
  `Sim/WeaponMount.cs` reads with ten hand-rolled `BitConverter.ToInt16(tail, …)` calls. The
  **comment** is fixed (it claimed only one field in the tail was decoded; it now names all ten with
  their offsets). Promoting them to named properties on `WeaponMountTemplate` is the real fix, but
  that is a code change touching the round-trip writer, so it wants its own commit and a round-trip
  test rather than riding along with a documentation pass.

---

## 3. Closed since the first pass

### Settled from the binary

- **A2.37** — `TSGroup_RenderPolys`'s entry point is `004758c8`. `known_symbols.json` already records
  that `FUN_004758ce` is the same function reached mid-prologue, so the one-nibble disagreement was a
  transcription slip in `distance-fog-and-sky.md`, which now names the symbol and its entry address.
- **A2.38** — `grid+0x10c` is written **every frame**, not derived at zone load. Confirmed by
  decompiling `Terrain_SetupVisibleRegion` (`0046ca98`), which does
  `*(int *)(param_1 + 0x10c) = DAT_004a0bcc[DAT_004d1fc3]` and then `>>= (cellShift - 14)`.
  `terrain-texturing.md` already carried this as the settled reading and the other two docs only
  reference it, so no live contradiction was left to fix. The engine's load-time derivation is
  **not** a divergence worth listing in `KNOWN_ISSUES.md`: the only input that can change per frame
  is the detail setting, which the engine has no UI for.

### Settled from the retail data

- **A2.45** — the throttle tick nudge, `int32` at content offset 1072 of each `<herc>.GAU` (file
  offset 1081, past the 9-byte VOL entry prefix): **-2** APOCA/OGRE, **-3** COLOSSUS/MAVERICK/
  OUTLAW/RAPTOR2, **-4** SAMSON, **+14** TOMAHAWK, **+17** RAZOR. `GAUFile.cs` was right and
  `HThrottle.cs`'s "-4 to +14" missed RAZOR; both now carry the measured values.
- **A2.47** — `MAGN` and `MFAC` are **the same weapon**, id 28: `MAGN` is DBSIM's own name for it and
  `MFAC` is the shell catalog's. Parsing `WEAPONS.DAT` shows ids 25 and 28 as the only two carrying
  `ProjDatIndex` 22, and `PROJ.DAT` record 22 is `type=2 missileId=9`. So bullet subtype 9 is reached
  by two weapons, and `projectiles.md`'s "PLAS, MFAC, MAGN" was counting one of them twice.
- **`STRINGS0.STR` groups 14, 16 and 40** — walked the file (41 groups, 2885 content bytes, zero
  slack). Group 14 is 19 entries (6 filled, flyer structure names, index-compatible with group 13),
  group 16 is 12 (group 15 with wing servos), group 40 is 8 (`ATTACK`, `TRAVEL`, `PATROL`,
  `FORM UP`, `GUARD`, `FLEE`, `DEAD`, `IMMOBILE`).

### Duplication collapsed (docs canonical, code keeps constants plus a pointer)

| Finding | What moved |
|---|---|
| **B3** | `Sim/ShieldCharge.cs`'s class doc dropped the `+0x222` layout table, the fleet-wide 3500 derivation and the "sums to 200" trap; `damage-system.md` keeps them. The 28 s refill figure now appears once in the file instead of twice. |
| **B4** | `Sim/BulletCatalog.cs` and `Sim/RocketCatalog.cs` dropped their retail tables and field maps; `projectiles.md` and `rockets.md` keep them, and are the fuller version of both. |
| **B5** | `damage-system.md`'s pseudo-C shot record deleted; it already linked to `weapon-firing.md`'s tables one sentence above. |
| **B8** | `World/MissionLoader.cs`'s per-verb arrival list and `Sim/SimObject.cs`'s three-gate list both reduced to pointers at `mission-deployment.md`. |
| **B21** | `Sim/MechPods.cs` dropped the slot/id derivation; `reactor-energy-pool.md` gained the one fact only the code had (last mount in hardpoint order wins a slot). |
| **B22** | `Sim/Anim/ShapeInstance.cs` dropped the per-HERC node lists, keeping the first-thread-wins rule the method implements. |
| **B23** | `Sim/MechControls.cs`'s `DAT_0049a06e` argument reduced to the conclusion plus the clamp behaviour the port depends on. |

### Organisation (decided 2026-09-01)

- **B24** — unimplemented work now lives in a new [`ROADMAP.md`](ROADMAP.md), the third register
  beside `KNOWN_ISSUES.md` (implemented but wrong) and the README (Java-port bugs). `planning.md`'s
  "Known open RE gaps" section moved there wholesale and now points at it; `KNOWN_ISSUES.md`'s
  self-disclaimer names it, and its three todo-style entries (external view, turret tracking, pause)
  moved across.
- **E2** — deliberately **not** standardising doc headers. Three forms stay in use.
- **E4** — `docs/engine/potential-modernization-features.md` stays exactly as it is, ignore note
  included.

### Prose and naming

- **C2** — the thirteen redundant "**Model:**" lines in `msn-mission-file.md` are gone. Row #16's
  `0x2E` → rows #12/#13/#14 mapping and row #9's verb correlation were folded into the table and the
  lead paragraph first. Row #12's line stays: it carries the payload description, not a restatement.
- **C4** — the verification caveat now appears once, in `dbsim-physics-notes.md`'s header. The
  ~3.4%-low bias appears once in the doc, in the `Math_FastMagnitude3D` toolkit entry, with the two
  later mentions referring to it rather than restating the figure.
- **A2.48** — the four disputed `.MSN` row names are unified on the descriptive set. Note the review
  had this backwards: the **docs** already used `Heading10`/`EntitySpawn164`/`EntityTemplate144`/
  `UnitSpawn58` and the **C#** carried `Flag10`/`UnkEntity164Bytes`/`SpawnRecord144`/`LinkedRef58`.
  The classes were renamed to match the docs (56 sites, 11 files, plus four file renames); both
  solutions build clean and all tests pass. It is still a self-contained rename if you want it as its
  own commit.
- **D12** — the last two narrated corrections, in `beam-visuals.md` and `weapons-dat-sim.md`, are
  removed.

### Earlier

- **E1 — the in-band `NOTE TO CLAUDE:` lines.** The nine reading *"This should be a reference
  document, not a personal journal"* were removed from `cockpit-hud.md`, `cockpit-input.md`,
  `dfn-hfn-dci.md`, `dts-texture-binding.md`, `heads-down-display.md`, `msn-mission-file.md`,
  `script-dat.md`, `dbsim-physics-notes.md` and `mech-locomotion.md`. Two notes are deliberately
  **kept**: `docs/engine/potential-modernization-features.md:1` (a file-handling instruction, not a
  style note) and `KNOWN_ISSUES.md:18` (which scopes that section against becoming a todo list).
- **A1.17 / A3.51** — the review reported `Render/TextureAtlas.cs`'s `AverageColor` as stating a
  disproven reading. `AverageColor` no longer exists anywhere in the repo; the passage was an
  orphaned `<summary>` sitting above `FrameSize`. Deleting it resolved A1.17, A3.51 and part of A3.58
  together, and `dts-texture-binding.md`'s "Fallbacks" bullet now points at
  `DtsMeshBuilder.FallbackColor`, which is what the code actually falls back to.
