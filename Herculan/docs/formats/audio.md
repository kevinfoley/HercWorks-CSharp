# Audio

DBSIM's sound is three stacked layers:

| Layer | What it is |
|---|---|
| Backend | HMI **Sound Operating System** (SOS) 9503, bound at runtime out of `sos9503.dll`, plus Win32 `mciSendCommand` for CD audio |
| `SFX` | A general resource/voice manager: named samples, handles, a memory budget, priority eviction |
| `Sound_*` | The game's own layer: a 57-entry catalog keyed by integer id, 3D placement, and a separate five-slot speech channel |

The first two layers and the effects half of the third are ported; see [Engine coverage](#engine-coverage).
CD music and the speech channel are not.

## Backend

### HMI SOS

`Sos_BindLibrary` (`004957f1`) picks the DLL by `GetVersion()` — Win32s (high bit set, major <= 3)
gets `sos32s03.dll`, everything else `sos9503.dll` — then walks a self-describing binding table at
`004a6ab4`: `0x24`-byte records of `{ void **destination, char name[0x20] }`, terminated by a NULL
destination. **100 entry points** are bound this way: 44 `sosDIGI*`, 45 `sosMIDI*`, 8 `sosTIMER*`,
plus `sosGetErrorString`, `sosPrepare32Memory`, `sosUnPrepare32Memory`. It refcounts (`004a6ab0`),
so repeated calls bind once.

Only `sos9503.dll` ships. `sos32s03.dll` does not.

Digital output covers samples and `.hmp` MIDI songs; the `.hmp` path is present but no `.hmp` file
ships with DBSIM. VSHELL carries a hardcoded `.\sos\song.hmp`, and no `sos\` directory ships either.

### CD audio

Music is Red Book, driven straight through MCI on device `cdaudio` — not through SOS at all.

| Function | MCI |
|---|---|
| `Music_PlayTrack` (`00473b3c`) | `MCI_OPEN` type `cdaudio`; `MCI_SET` time format TMSF; `MCI_PLAY` `MCI_FROM｜MCI_TO｜MCI_NOTIFY`, from track *n* to that track's own length |
| `Music_Stop` (`00473af4`) | `MCI_STOP` then `MCI_CLOSE` |
| `Music_GetPosition` (`00473c38`) | `MCI_STATUS` item `MCI_STATUS_POSITION`, `MCI_WAIT` |
| `Music_ResumeAt` (`00473cc0`) | Same open/set/play, but `MCI_FROM` is a saved TMSF position rather than a track start |

The track loops because `sfxWndProc` re-issues `Music_PlayTrack` on `MM_MCINOTIFY` (`0x3b9`) with
`MCI_NOTIFY_SUCCESSFUL`. `Sim_InitMissionSession` writes the track number (`0049f914`) and the
enable byte (`0049f918`).

### `sfxWndProc` (`00462294`)

Registered through `WndProcHook_Register` — this is one of the four `MainWndProc` filters mentioned
in [`cockpit-input.md`](cockpit-input.md). It handles exactly two messages: `WM_TIMER` (`0x113`),
pumped into the SOS/MME service routine, and the `MM_MCINOTIFY` loop above.

### `DATA\SOUND.CFG`

Plain INI, read with `GetPrivateProfileString` by `Sfx_ReadConfig` (`00463698`) into the manager's
config block at `+0x24`:

| Key | Values | Stored |
|---|---|---|
| `Driver` | `MME` (default) or `DirectSound` | `+0x2a` = 1 or 2 |
| `Buffers` | 1-64, else 5 | `+0x30` |
| `Rate` | `11` gives `0x10`, anything else `0x20` | `+0x24` |
| `Width` | `Mono` gives 4, else 8 | `+0x28` |

`+0x26` is fixed at 1 and `+0x32` at `0x200`. `Buffers` only applies to the MME driver, per the
file's own comments.

## The `SFX` manager

One instance, `0x4c` bytes, at `0049f904`. Its method names survive as assertion strings in VSHELL
(DBSIM's copy is stripped down to `setVolume` and `cache`): `open`, `close`, `cache`, `play`,
`stop`, `stopAll`, `isDone`, `setVolume`, `setPan`, `setPitch`, `setPriority`, `setLooping`,
`setCallback`, `getAttributes`, `setAttributes`, `getSampleData`.

`Sfx_Init` (`00463590`) is called as `(memoryCap, 60, 90)` — **60 resource slots, 90 voice slots**.

| Field | Meaning |
|---|---|
| `+0x10` | resource slot count (60) |
| `+0x14` | voice slot count (90) |
| `+0x18` | memory cap in bytes |
| `+0x1c` | bytes currently cached |
| `+0x20` | handle generation counter |
| `+0x34` | resource table, stride `0x21c` |
| `+0x38` | voice table, stride `0x28` |
| `+0x3c` / `+0x40` | count of playing samples / playing songs |

**Handles are `generation << 16 | slotIndex`.** Every accessor re-reads the slot's own copy of the
handle and rejects a mismatch, which is what the `Sample handle is old and no longer valid`
assertions report. `0xffffffff` is the null handle.

A *resource* is one file; a *voice* is one playable instance bound to a resource. Two voices opened
on the same filename share the resource and bump its refcount, which is how the ten music ids and
their single file coexist.

### Resource record (`0x21c`)

```
+0x000  int32   handle
+0x004  char    name[0x200]        -- path as passed to open
+0x204  int32   user value (open's 4th argument)
+0x208  int32   refcount
+0x20c  int32   cached flag
+0x210  int32   byte size on disk
+0x214  void*   backend object (SOS sample, or MIDI song)
```

### Voice record (`0x28`)

```
+0x00  int32   handle
+0x04  int32   resource handle
+0x08  uint32  flags
+0x0c  int32   priority        default 5
+0x10  int32   callback        default 0
+0x14  int32   volume          default 100
+0x18  int32   loop count      default 1;  0 = forever, n = play n times
+0x1c  int32   pan             default 0x8000 (centre)
+0x20  int32   pitch           default 0x10000 (1.0 in 16.16)
+0x24  uint16  backend handle
```

Flag bits, all set by the setters as a side effect of a non-default value:

| Bit | Meaning |
|---|---|
| `0x0001` | resource is a `.hmp` MIDI song, not a sample |
| `0x0080` | looping (loop count is not 1) |
| `0x0100` | currently playing |
| `0x0400` | pitch is not 1.0 |
| `0x0800` | pan is not centre |
| `0x1000` | open type 2 — a third playback path, a file streamed by name through the window handle. No DBSIM caller found. |

`Sfx_Open` (`00463910`) chooses the path from its third argument: 0 = `.hmp` song, 1 = sample, 2 =
the streamed type. The caller decides by searching the filename for `.hmp` / `.wav`.

### Memory budget and eviction

`Sfx_Cache` (`00463c48`) is the load/unload call. Loading first stats the file, and if
`cached + size > cap` it calls `Sfx_EvictUntilFree` (`0046428c`) before committing. The victim
picker (`0046417c`) scores every live voice and takes the **lowest**:

```
score = (resource cached ? 100 : 0)
      + priority
      + (playing            ? 1000  : 0)
      + (playing && looping  ? 10000 : 0)
```

so an idle, uncached, low-priority voice goes first and a looping playing one goes last.

`Sound_Init` (`0046230c`) sets the cap to **2,000,000 bytes**, or **1,000,000** in the low-memory
mode (`CockpitArt_LoadOnDemand` — `-l`, or under 12 MB physical).

### Backend volume and panning

`Sos_ApplyVolume` (`004739e0`) converts the voice's 0-100 volume to SOS's range as
`volume * masterVolume * 0x7fff / 10000`, duplicated into both 16-bit halves for left and right, and
for a MIDI song as `volume * 0x7f / 100`. `masterVolume` (`004a0e48`) is a constant 100 — its setter
(`004739a8`) has no callers.

## The sound catalog — `str\SOUNDS.STR`

The game addresses sounds by a small integer, 0-56. The mapping lives in `SOUNDS.STR`, a `.STR`
string table (layout in [`str-strings.md`](str-strings.md)) whose single group of 57 entries pairs a
filename with a 7-byte attribute blob.

`SoundCatalog_Load` (`00462448`) walks the group into three parallel arrays — names (`004d2b0c`),
attribute pointers (`004d2bfc`), voice handles (`004d2cfc`) — and for each entry opens a voice, sets
priority 5, applies the attributes, then fixes up defaults.

The code treats the blob as **ten** bytes. The file supplies seven; the last three are runtime
scratch that the loader initialises in place.

| Byte | Meaning |
|---|---|
| 0 | loop count — `Sfx_SetLooping`. `0` = loop forever, `1` = once, `n` = n times |
| 1 | volume, 0-100, applied as `Math_Q16Multiply(v, 65000)` |
| 2 | preload — nonzero caches the sample at startup instead of on first play |
| 3 | throttle divisor (see below) |
| 4 | rolloff start distance, in units of 1024 world units. `0xff` becomes 5 |
| 5 | cutoff distance, same units. `0xff` becomes 100 |
| 6 | variation count — playing id *i* actually plays `i + rand(count)` when count > 1 |
| 7 | *runtime*: "was playing" flag, for suspend/resume |
| 8 | *runtime*: category volume percentage, initialised to 100 |
| 9 | *runtime*: throttle counter |

Because `.STR` attribute blobs point directly into the loaded file buffer, bytes 7-9 of one entry
overlap the next entry's length field and first name byte. That is inert — every pointer is
collected before the first write — and the four empty entries the file carries after the last real
sound give the last one its slack.

`0xff` in bytes 4 and 5 means "use the default", not "not positional".

### Ids 0-9 are music

`Sound_IsCategoryEnabled` (`00462680`) splits the catalog at 10: ids below 10 answer to the music
enable flag (`0049f90c`), ids 10 and up to the effects flag (`0049f910`). Every mute/unmute pair in
the module respects the same boundary.

All ten music entries name `battle1.wav`, and **no `battle1.wav` ships in any archive**, so the
digital-music path is dead in retail — music is the CD. `Sound_ShiftMusicSet` (`00462fbc`) offsets
one character of each of the ten filenames by a delta and re-opens them, which is how a different
set would have been selected.

### Sample banks

`Sound_ResolveSamplePath` (`00462238`) prefixes the catalog's filename with `HMI\` normally and
`HMX\` in the low-memory mode. `SIMSOUND.VOL` carries both: 43 files under `hmi\` and 42 under
`hmx\`, each `hmx\` file roughly half the size of its `hmi\` twin — the same content at half the
sample rate.

**`EXPLO5.WAV` exists only in `hmi\`.** Catalog id `0x22` names it, so in low-memory mode that one
sound fails to open.

### The catalog

`vol`, `pre`, `thr`, `min`, `max`, `var` are attribute bytes 1, 2, 3, 4, 5, 6; `loop` is byte 0.

| id | File | loop | vol | pre | thr | min | max | var |
|---|---|---|---|---|---|---|---|---|
| 0 | `battle1.wav` | forever | 100 | 1 | 0 | - | - | 1 |
| 1-9 | `battle1.wav` | forever | 100 | 0 | 0 | - | - | 1 |
| 0x0a | `laser3h.wav` | 1 | 70 | 0 | 5 | 5 | 40 | 1 |
| 0x0b | `laser1.wav` | 1 | 50 | 0 | 2 | 5 | 40 | 1 |
| 0x0c | `impacts2.wav` | 1 | 70 | 0 | 2 | 0 | 15 | **3** |
| 0x0d | `impacts3.wav` | 1 | 70 | 0 | 2 | 0 | 15 | 1 |
| 0x0e | `impacts5.wav` | 1 | 70 | 0 | 2 | 0 | 15 | 1 |
| 0x0f | `missle.wav` | 1 | 98 | 0 | 2 | - | - | 1 |
| 0x10 | `xplmlt2.wav` | 1 | 50 | 1 | 3 | - | - | 1 |
| 0x11 | `gm_69.wav` | 1 | 90 | 1 | 1 | - | - | 1 |
| 0x12 | `bacann4.wav` | 1 | 90 | 0 | 3 | 5 | 15 | 1 |
| 0x13 | `start3.wav` | 1 | 90 | 0 | 0 | - | - | 1 |
| 0x14 | `bptslct.wav` | 1 | 90 | 0 | 1 | - | - | 1 |
| 0x15 | `trgloc.wav` | 1 | 90 | 0 | 2 | - | - | 1 |
| 0x16 | `trgunloc.wav` | 1 | 90 | 0 | 2 | - | - | 1 |
| 0x17 | `warn1.wav` | 5 | 30 | 0 | 0 | - | - | 1 |
| 0x18 | `wrnwoop2.wav` | 5 | 30 | 0 | 0 | - | - | 1 |
| 0x19 | `strcfail.wav` | 5 | 30 | 0 | 0 | - | - | 1 |
| 0x1a | `gnract.wav` | 1 | 80 | 0 | 1 | - | - | 1 |
| 0x1b | `gnrdact.wav` | 1 | 80 | 0 | 1 | - | - | 1 |
| 0x1c | `whitenz.wav` | 1 | 80 | 1 | 0 | - | - | 1 |
| 0x1d | `foot2.wav` | 1 | 98 | 1 | 4 | 1 | 20 | 1 |
| 0x1e | `callpsa.wav` | 1 | 68 | 0 | 2 | - | - | 1 |
| 0x1f | `callpsb.wav` | 1 | 68 | 0 | 2 | - | - | 1 |
| 0x20 | `plasma.wav` | 1 | 60 | 0 | 4 | 5 | 75 | 1 |
| 0x21 | `explo4.wav` | 1 | 60 | 0 | 3 | - | - | 1 |
| 0x22 | `explo5.wav` | 1 | 60 | 0 | 3 | - | - | 1 |
| 0x23 | `plsmahit.wav` | 1 | 99 | 0 | 4 | 1 | 50 | 1 |
| 0x24 | `explo7.wav` | 1 | 60 | 0 | 3 | - | - | 1 |
| 0x25 | `explos1.wav` | 1 | 80 | 1 | 3 | - | - | 1 |
| 0x26 | `explos2.wav` | 1 | 80 | 0 | 3 | - | - | 1 |
| 0x27 | `xplmlt4.wav` | 1 | 50 | 1 | 3 | - | - | 1 |
| 0x28 | `explo1d.wav` | 1 | 70 | 0 | 3 | - | - | 1 |
| 0x29 | `explo2.wav` | 1 | 70 | 0 | 3 | - | - | 1 |
| 0x2a | `explo3.wav` | 1 | 70 | 0 | 3 | - | - | 1 |
| 0x2b | `lsrhit2.wav` | 1 | 8 | 0 | 4 | - | - | 1 |
| 0x2c | `throtl.wav` | 1 | 100 | 0 | 0 | - | - | 1 |
| 0x2d | `herceng1.wav` | forever | 50 | 1 | 5 | 5 | 50 | 1 |
| 0x2e | `shield1.wav` | forever | 80 | 1 | 4 | 0 | 25 | 1 |
| 0x2f | `podin2.wav` | 1 | 70 | 0 | 1 | - | - | 1 |
| 0x30 | `podland.wav` | 1 | 70 | 0 | 1 | - | - | 1 |
| 0x31 | `flyby1.wav` | 1 | 90 | 0 | 4 | - | - | 1 |
| 0x32 | `missin.wav` | 1 | 28 | 0 | 4 | 5 | 75 | 1 |
| 0x33 | `fire1a.wav` | forever | 40 | 0 | 4 | 0 | 25 | 1 |
| 0x34 | `ricup.wav` | 1 | 40 | 0 | 2 | 0 | 15 | 1 |
| 0x35-0x38 | *(empty)* | | | | | | | |

`-` is the authored `0xff`, i.e. the 5/100 defaults. The four empty entries have no attribute bytes
at all and are never opened.

This resolves the sound ids scattered through the other docs: `0x0b` is `laser1.wav`, the beam muzzle
sound of [`../simulation/weapon-firing.md`](../simulation/weapon-firing.md); `0x16` the target-lost
tone of [`../simulation/missile-lock.md`](../simulation/missile-lock.md); `0x21` the torso servo
loop of [`../simulation/torso-aim.md`](../simulation/torso-aim.md); `0x2f` and `0x30` the drop pod's
fall and landing in [`../simulation/mission-deployment.md`](../simulation/mission-deployment.md).
The `+ 10` seen at every data-driven call site — `record.SoundId + 10` in `PROJ.DAT`, `ROCKETS.DAT`,
`EXPLOS.DAT` — is exactly the music/effects split: those tables index the effects half of the
catalog from zero.

## Playing a sound

Two entry points, both taking a catalog id.

**`Sound_Play` (`0046272c`)** — non-positional. Applies the variation roll, sets volume to
`Q16Multiply(vol, 65000) * byte8 / 100` (or 0 if the category is muted), and plays.

**`Sound_PlayAt` (`004627dc`)** — positional, `(id, worldPoint)`. Applies the variation roll, then
`Sound_Place` (`00462898`) computes volume and pan; it plays only if the result is audible.

`Sound_Place` resets the model transform and pushes the world point through the current camera
transform, so the listener is the camera. Then, with `d = Math_FastMagnitude3D(view)`:

```
minRange = attr[4] * 1024
maxRange = attr[5] * 1024
if d > maxRange:            volume = 0        -- not played at all
else:
    volume = Q16Multiply(attr[1], 65000)
    if d >= minRange:       volume = (maxRange - d) * volume / maxRange
volume = volume * attr[8] / 100
```

The rolloff divides by `maxRange`, not by `maxRange - minRange`, so a sound at exactly `minRange` is
already attenuated rather than at full volume.

Pan comes from the horizontal bearing, `a = Math_Atan2Bam(viewX, viewY)`, as `(-2a) & 0xffff` for
`a < 0x8000` and `2a & 0xffff` otherwise — a full sweep of the pan range over half a turn, mirrored
front to back.

At 166.667 world units per metre ([`../engine/planning.md`](../engine/planning.md)), a `max` of 40
is about 245 m, and the largest — `herceng1`'s 50 — about 307 m.

### The throttle

`Sound_ThrottleCheck` (`004626c4`) exists so that a sound fired by many objects at once does not play
once per object:

```
interval = (2 - detailSetting) * attr[3]
if interval == 0:  play
else:              attr[9]++;  play only when attr[9] % interval == 0
```

`attr[9]` wraps at `0x0f`. `detailSetting` (`004d1fc7`) is an options-screen 0-2 value, so the
highest setting zeroes the interval and lets everything through, while the lowest doubles the
authored divisor.

### Mute, suspend and resume

`Sound_MuteMusic` / `Sound_MuteEffects` (`00462c74` / `00462cd8`) zero the volume of their half of
the catalog and clear the enable flag; the unmute pair restores each id's own
`Q16Multiply(vol, 65000) * byte8 / 100`. Music mutes by stopping the CD instead when a track is set.

`Sound_SuspendAll` (`00463078`) records which voices are playing into attribute byte 7, saves the CD
position, and stops everything. `Sound_ResumeAll` (`00463134`) replays exactly those and resumes the
CD from the saved TMSF position.

`Sound_SetCategoryVolume` (`00462f5c`) writes attribute byte 8, the per-sound category scale every
volume computation multiplies through.

### The cockpit power-up

`Cockpit_PowerUpSound` (`004328cc`) is what the player hears on taking a machine. Two sounds, and the
second is conditional:

```
if (cockpit+0x245 == 0):                 -- once per session
    cockpit+0x241 = Time_GetCoarseTicks()
    Sound_Play(0x13)                     -- start3, the start-up sequence
if (mech+0x1f2 -> +0x50 != 0):
    Sound_Play(0x2d)                     -- herceng1, looping forever
    Sound_SetPitch(0x2d, 42000)          -- 42000/65536, about 0.64
```

The engine hum is not started at its recorded rate: it is dropped to roughly two thirds of it
immediately, which is what turns the sample into a hum rather than a whine. It loops for the rest of
the mission — attribute byte 0 is 0 — and follows its machine through `Sound_UpdatePosition`.

**The hum belongs to the flyer, not to a HERC.** The gate is type record `+0x50`, which is file
offset 78, `InputFlagFlyer`, set on the RAZOR alone (see
[`../simulation/mech-locomotion.md`](../simulation/mech-locomotion.md)'s type-record table). A
walking HERC powers up with `start3` and nothing else; its running noise is its footsteps.

### Sounds a cockpit control makes

Two toggles play a confirmation directly rather than through any data table:

| Trigger | Sound |
|---|---|
| `Mech_ToggleRadarMode` (`0041b468`) | `0x1a` `gnract` going ACTIVE, `0x1b` `gnrdact` going PASSIVE. Not positional — the cockpit makes it, not the world. |
| Heads-down display transmit (`0044cc40`) | The same pair, reused as its accepted/rejected blip. |

The mode-change tone is the [R] path only. The scanner screen's PASS/ACTIVE buttons write
`mech+0x96` directly and play nothing.

## Speech and the comm portraits

Squadmate and commander speech does not go through the catalog. It has its own five-slot channel pool
allocated by `Sound_Init`: five records of `0x42` bytes plus a 5 x 100-byte script buffer.

```
+0x00  int32   SFX voice handle (-1 = free)
+0x04  uint32  next script event time
+0x08  int16   current portrait frame  (-1 = finished)
+0x0a  char*   script cursor
+0x0e  char    wav name[0x21]
+0x2f  char*   this slot's 100-byte script buffer
+0x33  char*   name pointer, for the by-name lookup
+0x37  uint32  last-use tick, for LRU
+0x3b  byte    in-use
+0x3c  uint16  catalog id owning the slot
+0x3e  uint32  SFX voice handle
```

`Voice_Acquire` (`00462a98`) looks the requested `.wav` name up across the five slots; a miss evicts
the least recently used one (`00462a2c`), copies the name in, opens an `SFX` voice at **priority
`0xff`** so the catalog's priority-5 voices can never evict it, caches it, and loads the matching
`.SNC` script. Speech is gated on its own enable flag (`0049f97e`).

### File naming

`CommBox_BeginMessage` (`0044afc8`) builds two names from the speaker's squad slot and the message
id:

```
suffix = "_" + 2-digit message id + 3-digit variant     e.g. "_01000"
wav    = "P" + voiceBank + suffix        in simvoice/simvoicf/simvoicg
snc    = "P" + ('A' + slot) + suffix     in snc/
```

`voiceBank` is `(slot >> 2) + 1`, with 3 remapped to 4 — so twelve squad slots share three recorded
voices, `P1_`, `P2_`, `P4_`. That is the same 1/2/4 grouping as `str\PILOT0.STR`, `PILOT1.STR`,
`PILOT2.STR`, `PILOT4.STR`. `SIMVOICE.VOL` holds 147 `P*_*.WAV` and 66 `CVM_*.WAV`, the commander's
own lines.

The archive is chosen by `Voice_ArchiveName` (`0045ef68`), which patches the last character of the
literal `simvoice` with the language byte — `SIMVOICE` / `SIMVOICF` / `SIMVOICG`. All three are the
same size and differ only in their recordings.

## `.SNC` — portrait lip-sync scripts

**`.SNC` is not an audio format.** It is the frame timeline that animates the talking pilot portrait
in the heads-down display's comm box while the matching `.wav` plays.

556 files in `snc\` (in both `SIMVOL0.VOL` and `SIMSOUND.VOL`): twelve speakers `PA`-`PL` times 46-47
messages. **The twelve copies of a message are byte-identical** apart from their `.VOL` timestamps —
the per-speaker naming exists only because the loader builds the name from the speaker letter.

After the 9-byte `.VOL` entry prefix:

```
int32  length            -- bytes that follow
length/2 x {
    int8  frame          -- index into the pilot<n>.DBA portrait bank
    int8  delta          -- coarse ticks until the NEXT event
}
```

The `0xff` terminator is **not in the file** — `Snc_Load` (`00463270`) reads the declared length into
the slot's 100-byte buffer and appends `0xff` itself. With no script at all the buffer is just
`0xff`, and the voice plays with the portrait held.

**Verified across all 556 files**: length always even, always `fileLength - 14`, never containing a
`0xff` byte, 2-28 pairs (so at most 61 bytes in the 100-byte buffer). Frame values are 0-23 —
matching the 24 same-sized frames at the head of a `pilot<n>.DBA` bank, described in
[`heads-down-display.md`](heads-down-display.md) — and deltas 2-74 ticks.

`Snc_Advance` (`004633ac`) reads pairs until the accumulated time passes now, publishes the frame at
slot `+0x08`, and re-inserts the slot into a small global event queue (`004d2efa`, 8-byte
`{ time, slot }` entries) that `Snc_ServiceQueue` (`004631c0`) drains. Reaching the `0xff` sets the
frame to `-1`, which is what tells `HddGauge_PaintPilotFrame` the message is over.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `.SNC` is an audio format | It carries no samples. It is a two-byte-per-event portrait animation script, and the audio beside it is an ordinary RIFF WAV. |
| Attribute byte 0 selects a mixer channel or category | Its three retail values (0, 1, 5) look like a small enum, but it is passed straight to `Sfx_SetLooping` as a repeat count — 0 means forever, which is why the music entries and `herceng1`/`fire1a` carry it. |
| Attribute byte 2 is "looping" | It is the preload flag; `Sfx_Cache` is a load call, not a play call. Looping is byte 0. |
| The `battle1.wav` entries are the real music | The file ships in no archive. The ten slots are a stub; music is Red Book CD audio through MCI. |
| A `.wav` name resolves under one directory | It resolves under `HMI\` or `HMX\` depending on the low-memory flag, and the two banks are not identical — `EXPLO5.WAV` is missing from `HMX\`. |
| `herceng1` is the HERC engine hum | The name says so and the sample is one, but the only thing that starts it gates on type record `+0x50` — `InputFlagFlyer`, the RAZOR. A walking HERC never plays it. |

## Engine coverage

`Herculan.Engine.Audio` covers the catalog and the effects path: `SoundCatalog` parses `SOUNDS.STR`
with the attribute layout above, `SoundBank` picks the `HMI`/`HMX` folder and decodes the samples out
of `SIMSOUND.VOL`, and `SoundDirector` is the `Sound_*` layer — one voice per catalog id, the
variation roll, the category split, the throttle, `Sound_Place`'s rolloff and pan, and
suspend/resume. `OpenAlBackend` stands in for HMI SOS; `NullAudioBackend` runs the same rules
silently. `GameAudio` is the host-facing bundle, and `SimWorld.Sounds` is how the simulation reaches
it, with `PlayTableSound` applying the `+ 10` bias for `PROJ.DAT`, `ROCKETS.DAT` and `EXPLOS.DAT`
ids.

Triggers ported so far: the beam report, the two table-driven fire sounds and the impact sound (with
the ground hit's suppression), footfalls, the radar mode tone, the lock/acquire/loss tones, the
power-up and its flyer hum, and the missile-inbound warning.

**The memory budget is not reproduced.** `SoundBank` decodes every sample the catalog names at
startup instead of honouring the preload attribute and caching the rest on demand, so none of
[Memory budget and eviction](#memory-budget-and-eviction) exists here — no cap, no refcount, no
victim scoring. The whole `hmi` bank is about 1.5 MB of 8-bit PCM against the original's own
2,000,000-byte cap, so there is nothing for the eviction machinery to do; it would only start to
matter for a bank the retail game does not ship.

Not ported: CD music through MCI, the `.hmp` MIDI path, and the five-slot speech channel with its
`.SNC` portrait scripts. `HercWorks.Core` has `Data/File/Cfg/SoundCfg.cs`, a `SOUND.CFG` key holder
with no reader.
