# Mission difficulty

One number, `0`-`3`, chosen in VSHELL and carried to DBSIM in `data\script.dat`'s header. It is the **player's own pilot skill** in a campaign and a separate single-mission setting outside one; the simulator knows it only as `DAT_004a9ee0` and reads it in four places.

The four levels are named by `estext.bin` `0x35`-`0x38` — `ROOKIE`, `REGULAR`, `VETERAN`, `ELITE` — wherever they are shown, which is the same run the pilot roster prints a squadmate's skill from.

## Where the number comes from — VSHELL

`MsnGen_LoadMission` (`0041c73d`) fills the header globals immediately before it calls `WriteScriptDatFile`, and branches on `DAT_0048260c`, the campaign/training mode flag ([`../formats/save-games.md`](../formats/save-games.md)):

| Mode | Difficulty ← | Invulnerable ← | Unlimited ← |
|---|---|---|---|
| 1, campaign | `DAT_00482aa1` — the player pilot record's skill | forced 0 | forced 0 |
| 0, training / single mission | `prefs.cfg` option `0x27` | `prefs.cfg` option `0x26` | `prefs.cfg` option `0x25` |

`MsnGen_ParseMsnFile` (`00417b67`) zeroes all ten header globals as it starts, so **no `.msn` file carries a difficulty**; the mission never has an opinion about it.

### In a campaign it is the player's pilot skill

`00482aa1` is the player structure's embedded pilot record (`00482a7c`) at `+0x25`, the skill field every pilot has — see [`../formats/save-games.md`](../formats/save-games.md#pilot-record--59-bytes-0x3b-in-memory). It is set once, on the registration screen: both registration panels give it a row (`FUN_0043bd15` and `FUN_0043c01d`) that steps a shared global `DAT_004761ac` modulo 4 and prints `estext.bin 0x35 + value`, and `Game_NewCareer` (`0040e2ed`) passes that global into `Pilot_Init` (`0040fcd8`), which writes it to the player pilot's `+0x25`.

**The player's skill never moves afterwards**, because the debrief's promotion pass jumps clear of the skill ladder for the player ([`../shell/campaign-loop.md`](../shell/campaign-loop.md#pilot-progression)). So one choice at registration fixes the difficulty of the whole career, and it rides in the save file like any other pilot field.

A squadmate's skill is a separate, rising number and reaches the simulator not at all.

### Outside a campaign it is a `prefs.cfg` byte

VSHELL carries **the same 54-byte option array DBSIM does** — `DAT_004824b8`, its load-time shadow at `004824ee` and its handler table at `00482524`, loaded from and written back to `data\prefs.cfg` by `ShellOptions_Load` (`0040d6a3`) and `ShellOptions_SaveAll` (`0040d752`), with the same step/step-back/commit trio (`ShellOptions_StepOption`, `ShellOptions_StepOptionBack`, `ShellOptions_Commit`). An option's index is its byte offset, exactly as in [`preferences.md`](preferences.md#dataprefscfg--the-option-array).

The single-mission setup screen owns five of them, each row stepping its option with its own modulus and printing an `estext.bin` run:

| Option | Modulus | Strings | Becomes |
|---|---|---|---|
| `0x25` | 2 | `0x12a` `Limited` / `Unlimited` | header `+0x0a` — unlimited ammunition and energy |
| `0x26` | 2 | `0x128` `Vulnerable` / `Invulnerable` | header `+0x0c` — the player takes no damage |
| `0x27` | 4 | `0x35` `ROOKIE` … `ELITE` | header `+0x0e` — **the difficulty** |
| `0x28` | 9 | `0x6e` `Outlaw` … `Razor` | the chassis the player flies; not a header field |
| `0x29` | 2 | `0x12c` `Day` / `Night` | header `+0x12`, the theater variant — which is what that bit selects |

The screen's launch handler (`FUN_0044c396`) saves the array before starting the mission, so these persist between runs. The difficulty byte is passed to `Game_NewCareer` as well as to the header, so the `TRAINEE` pilot the training career creates carries it as their skill too.

**DBSIM never reads options `0x25`-`0x29`.** They are VSHELL's half of a shared file; what reaches the simulator is the header, not the option.

## How it reaches DBSIM — the `script.dat` header

`WriteScriptDatFile` (`0041ac54`) writes the ten header shorts from `00485446` upward, and `DBSim_LoadScriptDat` (`00424308`) reads all 20 bytes into `DAT_004a9ed2` in one call. A field's address is therefore `0x4a9ed2 + offset`:

| Offset | Global | Is |
|---|---|---|
| `+0x0a` | `DAT_004a9edc` | unlimited ammunition/energy when 1 |
| `+0x0c` | `DAT_004a9ede` | player invulnerable when 1 |
| `+0x0e` | `DAT_004a9ee0` | the difficulty |

See [`../formats/script-dat.md`](../formats/script-dat.md#header-format) for the rest of the header.

Nothing in DBSIM writes `DAT_004a9ee0` — the file read is its only writer, and it has four readers.

## What the difficulty changes

Four tables, one per consumer, all four entries wide and all indexed directly by the value:

| Consumer | Table | Entries | Effect |
|---|---|---|---|
| `Damage_ScaleByDifficulty` (`00426b04`), side 0 | `0049a73c` | 3500, 2800, 2100, 1400 | Q10 — a **human** shot does 3.42x down to 1.37x |
| the same, any other side | `0049a744` | 300, 600, 800, 1000 | a **Cybrid** shot does 0.29x up to 0.98x |
| `Ai_FireAtPoint` (`0041f5a0`) | `0049a30c` | 1000, 800, 400, 200 | how far a Cybrid machine's aim is thrown off — see [`ai-weapons.md`](ai-weapons.md#the-fire-decision--ai_fireatpoint-0041f5a0) |
| `Mech_CollisionTest` (`00418f74`) | `0049a058` | 400, 800, 1200, 1600 | the scale on the damage a long slide does on landing, player only — see [`mech-locomotion.md`](mech-locomotion.md#collision) |

Every table moves monotonically against the player, and `0049a060` and `0049a31c` are unrelated data, so four entries is the width rather than a guess — the same range the two screens that pick the number step through.

### The damage scale reaches all direct fire, not just plasma

`Damage_ScaleByDifficulty` takes a damage figure by pointer and the **firing side**, read as `owner->group->side` (`owner+0x45`, `group+0x12`) at every call site. It has three:

- `Sim_RaycastObjectList` (`00426528`) scales **both** of the shot record's damage figures — `+0x06` then `+0x04` ([`weapon-firing.md`](weapon-firing.md#the-shot-record)) — at the top of the sweep, whenever the record names an owner. Every beam and every bullet goes through it, and the record is rebuilt from the catalog each time, so nothing compounds.
- `Bullet_TickUpdate` (`0040b124`) scales the plasma round's blast figure separately, on the stashed copy. The plasma branch empties the shot record before the raycast, so the raycast's own scaling finds zeros there and the round is scaled exactly once.

So a difficulty step changes what **every weapon in the game** does, on both sides, in opposite directions — not only the one round with a blast.

### The two sibling cheats

`DAT_004a9edc` and `DAT_004a9ede` ride in the same header and are gated by `DAT_004a9ed6`, header `+0x04`, which `DBSim_LoadScriptDat` **sets to zero immediately after reading it** (`004246bb`) — every retail file carries 1 there and no other code writes it. With it held at 0:

- `Sim_DamageToPlayerDisabled` (`004240f4`) — which gates the first line of the player's own component damage — answers `DAT_004a9ede == 1`. Invulnerability is live; the `DAT_004a9ee0 == 0` arm beside it is the one the zeroing takes out, so **`ROOKIE` is not an invulnerability setting**.
- `WeaponMounts_ArbitrateEnergy` (`004107e4`) and `WeaponMounts_FireTrigger` (`00410dbc`) build their free-shot flag as `DAT_004a9edc == 1`, refunding energy and ammunition.

Both are `== 1`, not "nonzero", and both are the header field alone: `prefs.cfg`'s options `0x25` and `0x26` are what VSHELL steps to *write* those fields, and DBSIM never opens that half of the file.

Each reaches exactly one mechanism, and both are the player's machine alone:

- **Invulnerability takes out the whole damage write.** The gate is at `Mech_ComponentDamageWrite`'s entry, so none of that function's consequences — the shield-capacity recompute, the leg grading, the death gate, the reactor latches, the mounts' condition pass — run for the machine either ([`damage-system.md`](damage-system.md#the-component-damage-system)). **Shields are not covered**: absorption happens a function further out, before the write, so the array still drains and still recharges.
- **Unlimited ammunition is one free-shot flag and one refund.** The flag is the third argument every fire dispatch takes and only the ammunition class reads, where it skips the round spend ([`weapon-firing.md`](weapon-firing.md#the-ammunition-dispatch)); energy is not skipped but refunded, by the arbitration returning the budget it was called with ([`reactor-energy-pool.md`](reactor-energy-pool.md#weapon-energy-arbitration--weaponmounts_arbitrateenergy-004107e4)).

`Ai_FireAtPoint` (`0041f5a0`) pushes a literal 0 into that dispatch slot, so no AI machine can reach either half of the free shot. `WeaponMounts_FireTrigger` builds the flag from the globals without testing the owner, because `Mech_PlayerFireTick` (`00415608`) is its only caller; the arbitration, which every machine runs, tests `owner+0xa3` itself.

## Engine port

`ScriptDatHeader` decodes all three fields and `MissionScene` hands them to `SimWorld.Difficulty`, `SimWorld.PlayerInvulnerable` and `SimWorld.UnlimitedAmmunition`.

The difficulty is indexed by three things: `SimWorld.DamageScaleFor` holds both damage tables, `WeaponShot.ApplyDifficultyScale` applies it to a shot's two figures at the top of `SimWorld.Raycast`, `Projectile.Detonate` applies it to the plasma blast, and `MechObject.AiAimScatter` is read where the AI aims.

The two cheats sit where the original puts them: `MechObject.ComponentDamageWrite` returns at its head for an invulnerable locally piloted machine, `WeaponMount.Fire` takes the free-shot flag and `WeaponMount.FireAmmunition` is the only branch that reads it, and `WeaponMounts.ChargeTick` returns its incoming budget unspent.

Three deliberate differences:

- **The header value is clamped to 0-3 on the way in.** The original indexes four-entry tables with whatever the file says and reads past them; a hand-edited `script.dat` is held to the four levels instead.
- **The damage scale reads the side off `SimObject.Side`** where the original reaches it through the group pointer. The side is copied onto the object at spawn and nothing changes it mid-mission, so the two are the same byte, and reading it off the object removes an unguarded dereference. The aim scatter still reads `Group.Side`, which is the same value by a longer path.
- **`WeaponMounts.FireTick` tests the owner** where `WeaponMounts_FireTrigger` relies on its caller being the player's poll. The engine runs the trigger path for every machine, so the test restores what that caller guarantees.

The fourth table, `0049a058`, is `MechObject.SlideDamageScale`, read by `MechObject.SlideLandingDamage` where a slide ends, which also raises the cockpit shake the original raises beside the damage — see [`mech-locomotion.md`](mech-locomotion.md#the-landing).

**Nothing in the engine writes any of the three fields.** They arrive only from a `script.dat` on disk: the MDK's mission-script Header tab exposes theater, zone and variant alone (`MissionScriptForm.ApplyHeader`), and the shell's single-mission setup screen — the one place retail sets them — is not ported. An editor toggle or a host flag would make them reachable.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Difficulty runs 0-4 | Three of the four tables have a zero or an unrelated value in what would be slot 4, which reads as a fifth level. `0049a73c` and `0049a744` are 8 bytes apart and admit no fifth entry, both screens that set the number step it modulo 4, and the skill ladder the campaign feeds it from caps at 3 |
| Difficulty is a property of the mission | `MsnGen_ParseMsnFile` zeroes the header global before parsing and only `MsnGen_LoadMission` fills it. A `.msn` file cannot carry one |
| The difficulty scale applies to plasma blast damage only | That is one of `Damage_ScaleByDifficulty`'s three call sites. The other two are in `Sim_RaycastObjectList`, on the two damage figures of every direct-fire shot |
| `DAT_004a9ee0 == 0` makes the player invulnerable | It is one arm of `Sim_DamageToPlayerDisabled`, and the arm the loader's zeroing of `DAT_004a9ed6` makes unreachable. Invulnerability is its own header field |
| `prefs.cfg` options `0x25`/`0x26` are the two cheats DBSIM reads | They are VSHELL's half of a file the two programs share. The simulator reads neither: it tests the `script.dat` header fields against `== 1`. The option bytes are the shell's record of what the player asked for, not the switch |
