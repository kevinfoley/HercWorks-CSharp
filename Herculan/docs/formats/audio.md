# Audio

DBSIM's sound is three stacked layers:

| Layer | What it is |
|---|---|
| Backend | HMI **Sound Operating System** (SOS) 9503, bound at runtime out of `sos9503.dll`, plus Win32 `mciSendCommand` for CD audio |
| `SFX` | A general resource/voice manager: named samples, handles, a memory budget, priority eviction |
| `Sound_*` | The game's own layer: a 57-entry catalog keyed by integer id, 3D placement, and a separate five-slot speech channel |

The first two layers, the effects half of the third and the whole of the computer's message channel —
text as well as voice — are ported; see [Engine coverage](#engine-coverage). CD music and squad
speech are not.

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

`0x33` is not the flamer: it is the burning-object loop, started by the first live
[`FireEffect`](../simulation/destruction-effects.md#fire) and stopped by the last, and kept
positioned on whichever fire is nearest the camera.

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
| `Widget_ClickSound` (`00438e2c`) | `0x11` `gm_69`, the console click. |

The mode-change tone is the [R] path only. The scanner screen's PASS/ACTIVE buttons write
`mech+0x96` directly and play nothing. The radar toggle also announces the new mode in the
computer's voice — see [The computer's messages](#the-computers-messages).

`Widget_ClickSound` is the whole of the click: `push 0x11; call Sound_Play; ret`, and it is the
image's only reference to that id. Nothing calls it directly — it sits in **fifteen widget
vtables**, so a widget clicks because of what kind of widget it is and not because its handler did
anything. That is why a button wired to nothing still clicks.

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
`PILOT2.STR`, `PILOT4.STR`. `SIMVOICE.VOL` holds 147 `P*_*.WAV` and 66 `CVM_*.WAV`, the cockpit
computer's own lines.

The three name templates live together in DATA as literals the loader patches digits into:
`BC_00000`, `TMx_0000`, `CVM_0000`.

The archive is chosen by `Voice_ArchiveName` (`0045ef68`), which patches the last character of the
literal `simvoice` with the language byte — `SIMVOICE` / `SIMVOICF` / `SIMVOICG`. All three are the
same size, carry the same `SIMVOICE` folder label inside, and differ only in their recordings.

## The computer's messages

`str\SYSTEM.STR` is what the cockpit computer can say: 63 lines, and for each the recording that
reads it. Two `.STR` groups of 40 and 23 — but **the grouping means nothing**. Every call site
passes one number, counted straight through both groups, and that same number is in each entry's own
attribute byte 0.

Eight attribute bytes, all read by `MessagePort_Enqueue` (`00434e8c`) into the queued record:

| Byte | Record | Meaning |
|---|---|---|
| 0 | `+0x00` | The message id, which is also the entry's flat position |
| 1 | `+0x16` | A digit the pilot channel patches into its `.WAV` filename. Zero throughout |
| 2 | `+0x17` | Queue priority: the insert stops at the first queued entry of strictly higher value, so low sorts first. Zero throughout |
| 3 | `+0x1c` | `minTime` — shortest time on screen |
| 4 | `+0x20` | `maxTime` — longest time on screen |
| 5 | `+0x24` | `minWait` — delay before it may be shown |
| 6 | `+0x28` | `maxWait` — after this it is dropped unshown |
| 7 | `+0x2c` | Which `CVM_nnnn.WAV` reads the line, one-based |

The four timings are stored as `byte * 0x3c` coarse ticks, so at 16 ms a tick their units read as
seconds. Every line carries `3, 6, 0, 0x14` — up for 3 to 6 seconds, no delay, gone in 20 if it
never got its turn — except `TRANSFERRING DATA`, which carries `0x0a, 0x14, 0, 0x14`. The names come
from the port's own trace string, and the enqueue settles the order: bytes 3 and 4 stay durations
until the message is shown and have the show tick added in then, while 5 and 6 have the post tick
added immediately.

The record reads four bytes past the eight the file supplies. As with `SOUNDS.STR`, attribute blobs
point into the loaded file buffer, so those four overlap the next entry — and nothing reads them
back.

Byte 7 is a field and not an offset from the id: the numbering runs 1 to 66 across the 63 messages,
skipping 0x1c, 0x2d and 0x2f, and the archive holds exactly 66 clips — so three are recorded lines
no message claims.

`SystemMessages_Index` (`00435970`) is what keys the set. It scatters each string into a table at
`base + attr[0] * 9` — a `{ char *text; byte count; byte *attributes }` triple — and counts how many
landed in each slot, so **two entries sharing an id would become variants of one message** and the
post would roll between them (`MessagePort_PickVariant` (`00436a3c`), the same roll `SOUNDS.STR`
byte 6 drives). All 63 retail ids are distinct, so every count is one and no variant exists.

### The port

Messages reach the **cockpit's message port** (`PMSGPORT.BND`) through a vtable call. The cockpit
view holds two instances of the same class: the computer's ticker at `view+0x20b` and the pilot and
squad channel at `view+0x207`. Each is a queue of ten records plus one lifecycle, and the
preferences screen's COMPUTER MESSAGE and PILOT MESSAGE settings are their two enable bytes —
`DAT_004d1fbf` and `DAT_004d1fbe`, entries 3 and 2 of one four-byte array at `DAT_004d1fbc`, offered
as OFF / TEXT ONLY / VOICE ONLY / TEXT-VOICE.

The byte gates the two halves separately: the display runs when it is not 1 and the voice when it is
not 0. That is three behaviours, not four, so which label carries which value is not settled by the
code. With the display off the port still runs the whole lifecycle and only skips the drawing —
`port+0x4d2`, the suppression flag every paint entry point tests alongside `port+0x49e`, "a line is
up".

Two further gates sit on the display half, both fields of the object `FUN_00429820` returns
(`DAT_004cfa20`). Its `+0x14` is a mode enum: the show refuses to display in mode 4 and suppresses
the line exactly as TEXT OFF does, lifecycle and all. Its `+0x1c` is a byte the paint tests first and
returns on. Neither field's owner is decoded.

Both boxes are the herc's own, the last two fields of its `.GAU`: the pilot channel's at content
offset 1668 (full screen width, ten units tall) and the ticker's at 1684, `100,y - 220,y+9` — a
120x9 box centred horizontally, at `y = 34` in seven cockpits, 43 in APOCA's and 100 in RAZOR's.
Both are coordinate-shifted into device pixels by the `.GAU` loader's caller
(`Gau_BuildCockpitWidgets`, `00431bf8`) before the constructor sees them.

`MessagePort_Tick` (`00435610`) is the whole lifecycle, and it runs on four latches:

| Latch | Meaning |
|---|---|
| `+0x4c9` | Due — `minWait` has passed and the message is waiting to go up |
| `+0x4ca` | Ready — set by whichever paint entry point runs next, which is what puts one frame between due and shown |
| `+0x4cb` | Cancelled |
| `+0x49e` | A line is up |

Each tick first drops every *queued* message past index 0 whose `maxWait` has passed, then takes the
front of the queue as current if there is nothing current, then:

- **not ready and not cancelled** — if `maxWait` has passed, drop it unshown; otherwise once
  `minWait` has, mark it due and run the port's begin callbacks (`+0x4b9`, up to two, registered
  through `MessagePort_AddBeginCallback`, `004355a8`). Only the pilot channel registers any: the
  comm box installs `CommBox_OnMessageBegin` (`0044b4ec`), which is what starts that speaker's
  `.wav` and `.SNC` portrait and plays the `whitenz` static under it, and `CommBox_OnMessageEnd`
  (`0044b5c0`) on the matching end hook. The computer's port has none.
- **not due, or cancelled** — take the line down when it is cancelled, when `maxTime` has passed,
  when `minTime` has passed *and the queue holds more than one message*, or when the player's
  machine is dead (`LocalPlayerMech + 0x99`). That middle clause is the whole of the port's
  preemption: a message with the screen to itself keeps it for its maximum and gives it up at its
  minimum only when there is a successor.
- **due and ready** — show it, and on success add the current tick into `minTime` and `maxTime`,
  turning both from durations into deadlines.

`MessagePort_Show` (`00436abc`) is the show. It swallows a repeat of the same id inside 300 coarse
ticks (about 4.8 s), and a swallowed repeat *refreshes* that window rather than leaving it, so a
stream of them stays silent for as long as it keeps coming. Otherwise it latches the line
(`strncpy`, 0x50 characters), restores the box to its authored rect, publishes the scroll origin,
repaints, and plays an alert tone picked by a switch on the id:

| Ids | Tone |
|---|---|
| `0x00` `INTERNAL DAMAGE`, `0x13` `STRUCTURAL FAILURE IMMINENT` | `0x19` `strcfail` |
| `0x0c`, `0x0f`, `0x10` (shield generator / powerplant / weapon destroyed), `0x14` `SHIELDS LOW`, `0x15` `SHIELDS CRITICAL` | `0x18` `wrnwoop2` |
| everything else | `0x1a` `gnract` |

The switch names twelve further ids explicitly — `0x17`, `0x19`, `0x1d`-`0x1f`, `0x2a`-`0x2f` and
`0x34` — and gives every one of them the same `gnract` its default arm gives, so it is wider than its
behaviour. Speech goes last, and only then: the voice is
a consequence of the line going up, not a separate event.

`MessagePort_Withdraw` (`00435ac8`) is the withdraw. It sets the cancel latch on the current message
only if that message is not yet due — a line already on screen is left to run out its display time —
and otherwise removes a match from the queue. It matches on the id **and** the record's `+0x02`
subject pointer, so the same message about two machines is two entries.

`MessagePort_Pause` (`00435b58`) / `MessagePort_Resume` (`00435b80`) are the pause pair: the second
shifts every deadline in the queue, the current message's two display deadlines and the scroll's
publish time forward by however long the pause lasted.

### The ticker

`MessageTicker_Paint` (`00436cec`) paints it: the box flooded with `COLORS.DAT` id 19 (black), a
one-pixel frame in id 9 (red) — the fill brush's style 4, which `Raster_FillRect` (`004865f8`)
implements as four line draws round the rect — and the line in `ColorSchemePanels[2]`, `CPRED`. The
clip rect is then narrowed by `3 << VideoMode_XCoordShift` on each side before the glyphs go down,
which is what makes the text slide under the frame rather than past it.

The text **scrolls**. `MessageTicker_ScrollText` (`00436f70`) recomputes its x every frame as
`port+0x4af - (0x23 << VideoMode_XCoordShift) * elapsed / 0x3c` — starting at the box's right edge
and travelling left at `0x23` authored units every `0x3c` ticks — about 36 units, 73 device pixels,
a second in the 640-wide mode. There is no wrap: a line that outlives its own width simply leaves,
and against a 120-unit box the 3-to-6-second display time is matched so a long line crosses about
once.

`TRANSFERRING DATA` (`0x36`) is the one exception, and the only reason the port tests a message id
outside the tone switch: it is centred in the box instead of scrolling, and it blinks on
`Time_GetCoarseTicks() & 0x20`. Its 10-and-20-second timings are what make that readable.

Vertically the line is centred by cell rather than by ink: the paint anchors at `((height -
cellHeight) >> 1) + inkHeight + 1` and the glyph blitter (`HudFont_DrawGlyph`, `00482428`) subtracts
`inkHeight` straight back off. That is **not** `Label_SetRect`'s rule (see
[`mfd.md`](mfd.md#label-placement)), which centres `inkHeight`; the ticker is not a label.

### Posters

| Poster | Messages |
|---|---|
| `Cockpit_PowerUpTick` (`00432924`) | Once `200 <` coarse ticks have passed since the sequence began, it walks the ten heads-down gauges (`FUN_0041b514`, then `Damage_ToConditionState` (`00438700`) under `0x5a`) and posts `0x22` `POWERUP INITIATED. INTERNAL DAMAGE DETECTED.` if any is under, else `0x21` `... ALL SYSTEMS NOMINAL.` |
| `Mech_ToggleRadarMode` (`0041b468`) | Withdraws **both** `0x2c` `ACTIVE RADAR MODE` and `0x2d` `PASSIVE RADAR MODE`, then posts the one the mode just became — so flipping twice quickly announces where it ended up rather than reading out the sequence |
| `ConsoleButtons_ToggleAutoTrack` (`00441f7c`) | The same shape with `0x26` `AUTO TRACKING ENGAGED` and `0x27` `AUTO TRACKING DISABLED` |

At 16 ms a coarse tick the power-up announcement lands 3.2 s in, inside `start3`'s five seconds
rather than after them.

The pilot channel's voice dispatch is `PilotMessagePort_Speak` (`00435d9c`), which builds its `P*_*`
filename out of the message id and the record's `+0x16` digit the same way `CommBox_BeginMessage`
does — the way in to squad speech.

## `.SNC` — portrait lip-sync scripts

**`.SNC` is not an audio format.** It is the frame timeline that animates the talking pilot portrait
in the heads-down display's comm box while the matching `.wav` plays.

556 files in `snc\` (in both `SIMVOL0.VOL` and `SIMSOUND.VOL`): twelve speakers `PA`-`PL` times
46-47 messages. **The twelve copies of a message are byte-identical** apart from their `.VOL`
timestamps — the per-speaker naming exists only because the loader builds the name from the speaker
letter.

After the 9-byte `.VOL` entry prefix:

```
int32  length            -- bytes that follow
length/2 x {
    int8  frame          -- index into the pilot<n>.DBA portrait bank
    int8  delta          -- coarse ticks until the NEXT event
}
```

The `0xff` terminator is **not in the file** — `Snc_Load` (`00463270`) reads the declared length
into the slot's 100-byte buffer and appends `0xff` itself. With no script at all the buffer is just
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
with the attribute layout above, `SoundBank` picks the `HMI`/`HMX` folder and decodes the samples
out of `SIMSOUND.VOL`, and `SoundDirector` is the `Sound_*` layer — one voice per catalog id, the
variation roll, the category split, the throttle, `Sound_Place`'s rolloff and pan, and
suspend/resume. `OpenAlBackend` stands in for HMI SOS; `NullAudioBackend` runs the same rules
silently. `GameAudio` is the host-facing bundle and is itself the `ISoundSink` the simulation
reaches through `SimWorld.Sounds`, with `PlayTableSound` applying the `+ 10` bias for `PROJ.DAT`,
`ROCKETS.DAT` and `EXPLOS.DAT` ids.

The computer's channel is complete. `SystemMessages` parses `SYSTEM.STR` and flattens it to the ids
the call sites use; `MessagePort` is the port — the ten-slot queue, the four timings, the four
latches, the repeat suppression, the preemption and the pause — and it drives both halves, raising
one event for the speech and another for the alert tone rather than reaching into either. Speech is
`ComputerVoice`, which opens `CVM` clips out of `SIMVOICE.VOL` on first use and keeps them rather
than running the original's five-slot LRU. The display is `MessageTickerLayout` plus
`Overlay2DRenderer.AddMessageTicker`: the herc's own `.GAU` box (surfaced as
`GAUFile.MessageTicker`), the black fill and red frame, the scrolling `CPRED` line, and
`TRANSFERRING DATA`'s centred blink.

Three things differ. The port's clock is wall time accumulated by `GameAudio` in 16 ms units rather
than `GetTickCount`, and it stops across `Suspend`/`Resume`, which is what the original's pause pair
achieves by shifting every deadline instead. The text is clipped per glyph in geometry rather than
by a raster clip rect, so the whole cockpit panel stays one draw. And the display's two further
gates — the show's mode-4 refusal and the paint's `+0x1c` byte, both on the object `FUN_00429820`
returns — are not reproduced,
because neither field's owner is decoded.

The pilot and squad channel, the port's second instance, is not ported: it needs squad speech, and
nothing posts to it. `PilotMessagePort_WrapText` (`00436318`) is its word wrap and
`PilotMessagePort_Paint` (`0043660c`) its paint.

Triggers ported so far: the beam report, the two table-driven fire sounds and the impact sound (with
the ground hit's suppression), footfalls, the console click, the radar mode tone and its spoken
announcement, the lock/acquire/loss tones, the power-up with its announcement and its flyer hum, and
the missile-inbound warning. Only the power-up and the radar toggle post to the message port; the
other 60 lines have no poster yet because the state they report on does not exist.

The power-up always announces the nominal line: the gauge reading its alternative is chosen by is
not decompiled, and a machine taken at the start of a mission is undamaged and gets the nominal line
either way.

**The memory budget is not reproduced.** `SoundBank` decodes every sample the catalog names at
startup instead of honouring the preload attribute and caching the rest on demand, so none of
[Memory budget and eviction](#memory-budget-and-eviction) exists here — no cap, no refcount, no
victim scoring. The whole `hmi` bank is about 1.5 MB of 8-bit PCM against the original's own
2,000,000-byte cap, so there is nothing for the eviction machinery to do; it would only start to
matter for a bank the retail game does not ship.

Not ported: CD music through MCI, the `.hmp` MIDI path, and squadmate and commander speech with its
`.SNC` portrait scripts. `HercWorks.Core` has `Data/File/Cfg/SoundCfg.cs`, a `SOUND.CFG` key holder
with no reader.
