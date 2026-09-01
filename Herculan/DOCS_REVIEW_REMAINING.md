# Documentation review — issues not fixed

Companion to [`DOCS_REVIEW.md`](DOCS_REVIEW.md), which lists all 113 findings. This file covers only
what was **not** fixed, and why. Finding numbers refer to `DOCS_REVIEW.md`.

Most of the 113 findings were applied, across 79 files. Every contradiction that could be settled
from the repo is fixed; what is listed below is what could not be, plus the duplication and prose
work not reached. Both solutions build with 0 errors / 0 warnings and all 271 tests pass.

Items are grouped by *why* they are outstanding, because that determines who can close them.

---

## 1. Blocked — needs Ghidra (not installed on this machine)

Two findings are disagreements about what the retail binary actually does. Neither can be settled by
reading this repo, and guessing would replace an honest contradiction with a confident error. Both
sites have been left exactly as they were.

| Finding | The disagreement | What settles it |
|---|---|---|
| **A2.37** | `TSGroup_RenderPolys` is `FUN_004758ce` in `docs/formats/distance-fog-and-sky.md:59` but `004758c8` in `docs/formats/dts-node-posing.md:12` and `Render/DtsMeshBuilder.cs:299`. One nibble apart, so one is a transcription slip — but which is unknown. | Look up both addresses in the `ES2Recon` project; whichever is the function entry point wins. |
| **A2.38** | Whether `grid+0x10c` is derived once at zone load or re-derived every frame. `terrain-heightmap.md:22` and `distance-fog-and-sky.md:28` say load time; `terrain-texturing.md:91-95` and `Terrain/TerrainZoneLoader.cs:140-148` say `Terrain_SetupVisibleRegion` re-reads it per frame from `DAT_004a0bcc[DAT_004d1fc3]`, with 10 only the retail default. | Decompile `Terrain_SetupVisibleRegion` (`0046ca98`) and confirm whether it writes `+0x10c` per frame. If it does, the engine's load-time derivation is a real divergence and belongs in `KNOWN_ISSUES.md`. |

`terrain-texturing.md` now records **both** readings side by side rather than silently picking one.

## 2. Blocked — needs retail game data (not present in this tree)

| Finding | Question | What settles it |
|---|---|---|
| **A2.45** | The throttle tick-nudge range: `Gau/GAUFile.cs` says −2..−4 for most hercs and +14/+17 for RAZOR/TOMAHAWK; `Gau/HThrottle.cs` says −4..+14. | Read the `int32` at file offset **1072** in each of the nine retail `<herc>.GAU` files. A note flagging the disagreement has been added at the `GAUFile.cs` site so nobody trusts either figure meanwhile. |
| **A2.47** | Does `MAGN` belong to bullet subtype 9? `docs/simulation/projectiles.md:42` lists "PLAS, MFAC, MAGN"; `Sim/BulletCatalog.cs:32` lists "PLAS, MFAC". | Read `BULLETS.DAT` and check which weapons carry subtype 9. Left as-is in both places rather than picking a winner. |
| — | `STRINGS0.STR` group counts for groups **14, 16 and 40**. The group table in `docs/formats/str-strings.md` was completed during this pass, but these three are recorded as `?` because no doc or code states them. | Walk `STRINGS0.STR`, or trace `SimStrings_LoadAll`'s registration sequence. |

## 3. Blocked — needs a decision from you

These are judgement calls about how you want the docs organised, not defects with an obvious fix.

- **B24 — the gap-list consolidation has no complete answer under the current split.**
  `KNOWN_ISSUES.md:18` explicitly says its engine section is *"not a todo list of features that
  haven't been tackled yet"*, so `planning.md`'s remaining gaps (flyer texture banks, SimRandom's
  seed table, AI/behaviour trees, `.SNC` audio, terrain swept-volume raycast, …) cannot move there.
  What I did instead: deleted the two entries that were provably closed, removed the section's
  self-disclaimer, and reframed it as an **index** that points at the owning doc. What is still open
  is where genuinely-unimplemented work should live — a third `KNOWN_ISSUES.md` section, a
  `ROADMAP.md`, or per-topic "Not ported" sections only. Pick one and the index can go.

- **E3 — partially done.** `Herculan/README.md` claimed `KNOWN_ISSUES.md` was "the canonical record"
  for Java-port bugs, which it never contained. I corrected the claim so it is true and states the
  two registers plainly. I did **not** move the ~10 Java-port bugs from `README.md:40-52, 92-106`
  into `KNOWN_ISSUES.md`, because they are embedded in the per-round porting narrative and lifting
  them is a structural change to both files. If you want that, they need a third heading such as
  "HercWorks toolkit — bugs inherited from the Java original".

- **E2 — no shared doc header convention.** Three incompatible forms are in use: status-in-title
  (5 docs), a status-and-port line (11 docs), and neither (8 docs); the Ghidra provenance boilerplate
  is verbatim in five files and absent from a dozen. Standardising means picking one and touching all
  39 docs. I did remove the dated "solved on" prefixes where I edited a header anyway, but did not
  impose a convention.

- **E4 — `docs/engine/potential-modernization-features.md`.** The review suggested folding its two
  bullets into `planning.md` and deleting the file. **I left it entirely alone**: its first line is
  `NOTE TO CLAUDE: Ignore this file unless asked to read from or write to it.` Your call.

## 4. Not done — a code change with behaviour risk

- **A1.11 follow-up.** `Data/File/Dat/Sim/Weapons.cs`'s `Tail` is 48 raw bytes that
  `Sim/WeaponMount.cs` reads with ten hand-rolled `BitConverter.ToInt16(tail, …)` calls. I fixed the
  **comment** (it claimed only one field in the tail was decoded; it now names all ten with their
  offsets). Promoting them to named properties on `WeaponMountTemplate` is the real fix, but that is
  a code change touching the round-trip writer, so it wants its own commit and a round-trip test
  rather than riding along with a documentation pass.

## 5. Not done — safe and mechanical, just not reached

Nothing blocks these. They are listed so the remaining work is visible.

**Duplication still to collapse** (docs canonical, code keeps constants plus a link):

| Finding | Copies |
|---|---|
| **B3** | The shield system, twice at equal length — `docs/simulation/damage-system.md:194-286` and `Sim/ShieldCharge.cs` (the `+0x222` layout, the fleet-wide 3500, the 5-per-tick/700-tick/28s figure, the `±0x66` balance step, the "readouts sum to 200" trap). The stale half was fixed; the duplication was not. |
| **B4** | `BULLETS.DAT` / `ROCKETS.DAT` retail tables duplicated between `projectiles.md` / `rockets.md` and `Sim/BulletCatalog.cs` / `Sim/RocketCatalog.cs`. Note this pair has **already drifted** — see A2.47 above. |
| **B5** | The shot record layout — `weapon-firing.md:112-134` (tables) and `damage-system.md:456-468` (pseudo-C), the latter linking to the former in the sentence above it. |
| **B8** | The mission deployment mechanism ×3 — `mission-deployment.md:16-30, 64-114`, `World/MissionLoader.cs:53-86`, `Sim/SimObject.cs:234-259` (verb list, 150000/90000 distances, ±90°/±22.5° bearings, 70000-95000 drop height). |
| **B21** | The pod slot/id table — `reactor-energy-pool.md:67-91` and `Sim/MechPods.cs:3-40`. |
| **B22** | The three-thread override rule and per-HERC node lists — `torso-aim.md:114-127`, `Sim/Anim/ShapeInstance.cs:70-83`, and a partial third at `Sim/MechObject.cs:80-85`. |
| **B23** | The `DAT_0049a06e` "not a gear selector" argument — `mech-locomotion.md:124-138` and `Sim/MechControls.cs:27-43`. |

**Prose still to trim:**

- **C2** — `docs/formats/msn-mission-file.md`'s fourteen "**Model:**" lines restate the table directly
  above each of them. I rewrote **row #12's** (it contradicted its own table — finding D5) and left
  the other thirteen. They can simply be deleted, except row #8's inheritance rule and row #16's
  payload discriminator, which carry a fact the tables do not and should be folded into the notes
  column first.
- **C4** — the verification caveat is stated four times (`dbsim-physics-notes.md:5-10`, `:45-46`,
  `:71-75`, plus `Numerics/SimMath.cs` twice) and the ~3.4%-low fast-magnitude bias four times
  (`dbsim-physics-notes.md` ×2 remaining, `SimMath.cs` ×1). One statement of each is enough — the
  method in the doc header, the bias in the toolkit entry.

**Naming still inconsistent:**

- **A2.48** — the same `.MSN` rows carry different names in `msn-mission-file.md`, `script-dat.md`
  and the C# classes (`Heading10`/`Flag10`, `EntitySpawn164`/`UnkEntity164Bytes`,
  `EntityTemplate144`/`SpawnRecord144`, `UnitSpawn58`/`LinkedRef58`), and `msn-mission-file.md`
  disagrees with itself between `:109` and `:320`. The C# class names are the sensible canonical set.
  This is a cross-file rename over two docs and their headings — safe, but wide, so it wants to be
  its own commit where it can be reviewed as a rename rather than buried in a content diff.

**Remaining narrated corrections (D12):** most were removed, but sites in
`docs/simulation/beam-visuals.md:151-155` and `docs/formats/weapons-dat-sim.md:89-91` were not
reached.

---

## 6. Resolved after the first pass

- **E1 — the in-band `NOTE TO CLAUDE:` lines.** The nine reading *"This should be a reference
  document, not a personal journal"* have been removed from `cockpit-hud.md`, `cockpit-input.md`,
  `dfn-hfn-dci.md`, `dts-texture-binding.md`, `heads-down-display.md`, `msn-mission-file.md`,
  `script-dat.md`, `dbsim-physics-notes.md` and `mech-locomotion.md`. Two notes are deliberately
  **kept**: `docs/engine/potential-modernization-features.md:1` (a file-handling instruction, not a
  style note) and `KNOWN_ISSUES.md:18` (which scopes that section against becoming a todo list).

## Note on one finding that changed shape

**A1.17 / A3.51** — the review reported `Render/TextureAtlas.cs`'s `AverageColor` as stating a
disproven reading. On inspection `AverageColor` **no longer exists anywhere in the repo**; the
passage was an orphaned `<summary>` sitting above `FrameSize`. Deleting it resolved A1.17, A3.51 and
part of A3.58 together. A knock-on: `dts-texture-binding.md`'s "Fallbacks" bullet was citing that
dead symbol, and now points at `DtsMeshBuilder.FallbackColor`, which is what the code actually falls
back to.
