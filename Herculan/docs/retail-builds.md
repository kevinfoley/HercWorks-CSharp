# Retail builds and languages

Two retail builds of Earthsiege 2 are in hand. They share the data formats every other doc describes, but differ in their launcher, their installer, their translations and a handful of missions. The rest of these docs describe v1.0 unless they say otherwise.

| | v1.0 | v1.10 |
|---|---|---|
| Source | the install under `ES2\` | the GoldGames freeware disc, `VER95\` and `VER31\` |
| `SIERRA.INF` `[Ident]` | `Version=100 Maverick JC` | `Version=110` |
| Windows | 95 | 95 (`VER95\`) or 3.1 with Win32s (`VER31\`), chosen by the installer; it refuses Windows NT |
| `ES.EXE` | 8,736 bytes | 11,808 (`VER95\`), 40,480 (`VER31\`) |
| `VSHELL.EXE` | 564,768 bytes ([Open](#open)) | 566,304 in both folders, different bytes |
| `DBSIM.EXE` | 724,512 bytes | 724,512 (`VER95\`, different bytes), 727,584 (`VER31\`) |
| Languages | English | English, French, German |

All four of v1.10's `VSHELL.EXE` and `DBSIM.EXE` carry v1.0's language strings — the `-f`/`-g` usage lines, the `eng\`/`fre\`/`ger\` folders, the `.eng`/`.fre`/`.ger` extensions, `simvoice`, `data\language.cfg` and the manual folders — and `VER95\VSHELL.EXE` adds a language-dependent folder for `campaign.str`. How else their code differs from v1.0's is [Open](#open).

## The installer

Both builds install through Sierra's `SETUP.EXE`, which runs the script in `SIERRA.INF`.

**The language is the setup program's own.** The script sets flags 1, 2 and 3 for English, French and German from three `LANGUAGE_EQ` tests. The `InstallLanguage` dialog that would ask the player is defined but its line in the script is commented out, in both builds.

**The size choice decides what is copied.** Minimum, Medium and Maximum copy more of the archives; whatever is left is read from the disc, which both programs find through `data\drive.cfg` ([`formats/vol-archive.md`](formats/vol-archive.md#which-archives-are-mounted)).

- v1.0 copies `SIMVOICE.VOL` at every size. The lines that would pick the French or German archive are commented out, and its `[Files]` list gives `SIMVOICF.VOL` and `SIMVOICG.VOL` as 4 bytes each.
- v1.10 copies the chosen language's voice archive and `README.WRI`, its `ERROR.*` as `ERROR.STR`, its `MISSION.*` as `DATA\MISSION.STR`, and under Windows 3.1 its `JCONFIG.*` as `JCONFIG.STR`. It adds `SHELL1.VOL` and `SIMLANG.VOL` to the archives.

**`BATCH.EXE` writes the configuration.** The script runs it as `BATCH.EXE <source> <install> <E|F|G> <0|1>`. It writes:

1. `data\drive.cfg`: the source directory and the install directory, one per line.
2. `data\language.cfg`: the letter, one byte.
3. A copy of `<install>\<LANGUAGE>\README.WRI`, under a name the two builds differ on ([`formats/winhelp.md`](formats/winhelp.md#macros)).
4. `data\prefs.cfg`, when `Sierra.ini`'s `[Config] VideoSpeed` is at most 1000 (v1.0) or 700 (v1.10): byte 4, the low-resolution option, set to 1, and in v1.10 also byte 47, which stops the shell's own `VideoSpeed` check from showing its `Performance Note` ([`shell/screen-layout.md`](shell/screen-layout.md#the-main-menu)).
5. With the last argument `1`, the Indeo codecs' registry entries. v1.0's returns before this step when `VideoSpeed` is above 1000; v1.10's does not. Both scripts also write the codecs' `SYSTEM.INI` entries themselves.

## How a language is chosen

The shell and the simulator each keep a language value, set only from their command lines ([`command-line.md`](command-line.md)):

| Program | Switches | Selects |
|---|---|---|
| VSHELL | `-f`, `-g`, either case | the `LANG0.VOL` folder of every `.BIN` table, the extension of a mission's text (`.eng`, `.fre`, `.ger`), and in v1.10 the folder of `campaign.str` |
| DBSIM | `-F`, `-G` (`-E` for Spanish) | the voice archive and its folder label, `SIMVOICE`/`SIMVOICF`/`SIMVOICG` ([`formats/audio.md`](formats/audio.md#speech-and-the-comm-portraits)), and the `st<letter>\` folder of its `.STR` tables |

The on-line manual is the exception: both programs open `<LANGUAGE>\es2guide.hlp` by `data\language.cfg` directly ([`formats/winhelp.md`](formats/winhelp.md)).

**v1.0's launcher passes neither switch**, so a v1.0 game always runs in English, whatever `language.cfg` says, and that file chooses only the manual and its readme.

**v1.10's launcher passes the installed language.** `VER95\ES.EXE` turns `language.cfg`'s letter into a switch on both command lines, so a French or German install runs both programs in its language ([`command-line.md`](command-line.md#v110s-language-switch)).

## What each build carries per language

| Resource | v1.0 | v1.10 |
|---|---|---|
| `SIMVOICF.VOL`, `SIMVOICG.VOL` | in the install, byte copies of `SIMVOICE.VOL` with its `SIMVOICE\` label | under `SIMVOICF\` and `SIMVOICG\`: the cockpit computer in French and German, the squadmates in English ([`formats/audio.md`](formats/audio.md#file-naming)) |
| Training instructor's loose clips | `SIMVOICE\` only, in the install | `SIMVOICE\`, `SIMVOICF\`, `SIMVOICG\`, 65 each, all three the English recordings |
| `LANG0.VOL` `FRE\`, `GER\` | copies of `ENG\` | translated, but for the pilot names and mission paths ([`formats/weapons-dat.md`](formats/weapons-dat.md#the-bin-string-tables)) |
| `SIMALERT.VOL` `STF\`, `STG\` | French and German text for an earlier design of the panels ([`simulation/alert-panels.md`](simulation/alert-panels.md#what-the-family-shares)) | translations of the shipped panels |
| `SIMLANG.VOL` | absent | `STF\` and `STG\` twins of every `str\` table in `SIMVOL0.VOL` but `PILOT0`, and of `TAPES\DEMOLIST.STR` |
| Mission text | `.ENG` only | `.FRE` and `.GER` beside every `.ENG` but `DEMO2`'s ([`formats/msn-mission-file.md`](formats/msn-mission-file.md#the-eng-string-table)) |
| Fonts | | six replaced, with more accented letters ([`formats/dfn-hfn-dci.md`](formats/dfn-hfn-dci.md#dfn--hfn--bitmap-font)) |
| On-line manual | the same three `ES2GUIDE.HLP` in both builds ([`formats/winhelp.md`](formats/winhelp.md)) | |

## Other content changes in v1.10

Every archive v1.0 has is in v1.10 with the same entries, except for the translations above and these:

**`SIMPATCH.VOL`** adds the two fonts above, a near-silent `battle1.wav` for the music catalog entries, and the low-memory bank's missing `explo5.wav` ([`formats/audio.md`](formats/audio.md#ids-0-9-are-music)).

**`ZONES.VOL`** changes twelve mission files:

| File | Change |
|---|---|
| `C2_01.ENG` | "You patrol zone" to "Your patrol zone" |
| `C2_04.ENG` | "an pair" to "a pair" |
| `C3_03.ENG` | the briefing loses its last sentence, that the listening post may be destroyed once its data is downloaded |
| `C4_09.ENG` | the briefing loses its last sentence |
| `C2_03.ENG`, `C2_03.MSN` | a second copy of the debrief's loss line, on twelve new row-1 conditions that repeat the first copy's tests, so the debrief reads the same ([`formats/msn-mission-file.md`](formats/msn-mission-file.md#the-debriefs-loss-line)) |
| `C2_05.MSN` | group 188's row-16 `0x08` from 25, the one non-zero value in any mission, to 0 ([`formats/msn-mission-file.md`](formats/msn-mission-file.md#row-16-field-decode--the-group-record-dat_0047065a-164-bytesrecord)) |
| `C2_06.MSN` | point 34 moves by (+19656, +5732) |
| `C5_05.MSN` | of the eight structures conditioned on campaign flags 821 to 828, the four on 821, 823, 825 and 827 are removed, with their conditions |
| `DEMO_01.MSN`, `DEMO_02.MSN` | two orders each take the next route: 46 to 47, and 29 to 30 |
| `TRAIN1.MSN` | two row-3 values, GUID 68 = 6 and GUID 71 = 37, which no record in the file names |

## Open

- **Unported:** French and German: taking the language from `data\language.cfg` as v1.10's launcher does, and reading the translated text, mission text and voice archives.
- **Open:** how v1.10's `VSHELL.EXE` and `DBSIM.EXE` differ from v1.0's beyond the language readers. `DBSIM.EXE`'s code section is `0x200` bytes longer, so a byte comparison says nothing, and neither v1.10 executable is in the Ghidra project.
- **Open:** where the analysed `ES2\VSHELL.EXE` (564,768 bytes) comes from, when the v1.0 `SIERRA.INF` beside it lists 563,232.
- **Open:** what `VER31\ES.EXE` does with `data\language.cfg`. It names the file; its code has not been read.- **Open:** what Sierra's `SETUP.EXE` tests `LANGUAGE_EQ` against.
