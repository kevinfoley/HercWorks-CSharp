# Audio

DBSIM's sound is three stacked layers:

| Layer | What it is |
|---|---|
| Backend | HMI **Sound Operating System** (SOS) 9503, bound at runtime out of `sos9503.dll`, plus Win32 `mciSendCommand` for CD audio |
| `SFX` | A general resource/voice manager: named samples, handles, a memory budget, priority eviction |
| `Sound_*` | The game's own layer: a 57-entry catalog keyed by integer id, 3D placement, and a separate five-slot speech channel |

All three layers are ported except the `.hmp` MIDI path; see [Engine coverage](#engine-coverage) and [Open](#open). The message channels themselves — the computer's ticker and the pilot/squad comm boxes that ride on this layer's speech slots — are [`cockpit-messages.md`](cockpit-messages.md)'s.

## Backend

### HMI SOS

`Sos_BindLibrary` (`004957f1`) picks the DLL by `GetVersion()` — Win32s (high bit set, major <= 3) gets `sos32s03.dll`, everything else `sos9503.dll` — then walks a self-describing binding table at `004a6ab4`: `0x24`-byte records of `{ void **destination, char name[0x20] }`, terminated by a NULL destination. **100 entry points** are bound this way: 44 `sosDIGI*`, 45 `sosMIDI*`, 8 `sosTIMER*`, plus `sosGetErrorString`, `sosPrepare32Memory`, `sosUnPrepare32Memory`. It refcounts (`004a6ab0`), so repeated calls bind once.

Only `sos9503.dll` ships. `sos32s03.dll` does not.

Digital output covers samples and `.hmp` MIDI songs; the `.hmp` path is present but no `.hmp` file ships with DBSIM. VSHELL carries a hardcoded `.\sos\song.hmp`, and no `sos\` directory ships either.

### CD audio

Music is Red Book, driven straight through MCI on device `cdaudio` — not through SOS at all. The `Sound_*` layer reaches it through four `SFX`-level thunks (`Sfx_PlayMusicTrack` `00464754`, `Sfx_StopMusic` `00464770`, `Sfx_GetMusicPosition` `00464884`, `Sfx_ResumeMusicAt` `00464898`) rather than through the digital backend, so no part of music touches a voice record.

| Function | MCI |
|---|---|
| `Music_PlayTrack` (`00473b3c`) | `MCI_OPEN` type `cdaudio`; `MCI_SET` time format TMSF; `MCI_PLAY` `MCI_FROM｜MCI_TO｜MCI_NOTIFY`, from track *n* to that track's own length |
| `Music_Stop` (`00473af4`) | `MCI_STOP` then `MCI_CLOSE`, and `Music_MciDeviceId` (`004a0e44`) back to -1 |
| `Music_GetPosition` (`00473c38`) | `MCI_STATUS` item `MCI_STATUS_POSITION`, `MCI_WAIT` |
| `Music_GetTrackLength` (`00473c78`) | `MCI_STATUS` item `MCI_STATUS_LENGTH` with `MCI_TRACK`; the result is kept in `Music_TrackLength` (`006b5610`) |
| `Music_ResumeAt` (`00473cc0`) | Same open/set/play, but `MCI_FROM` is a saved TMSF position rather than a track start, and `MCI_TO` is whatever `Music_TrackLength` already holds — it never re-queries |
| `Music_IsIdle` (`00473b2c`) | No command; the device id against -1 |

The device is held open only while a track is sounding: every entry point opens it, and every failure path closes it again.

The track is meant to loop: `sfxWndProc` re-issues `Music_PlayTrack` on `MM_MCINOTIFY` (`0x3b9`) with `MCI_NOTIFY_SUCCESSFUL`. The notify is addressed to `Music_NotifyWindow` (`006b560c`), the handle `Sos_InitBackend` was given.

**On Windows 11 it plays once.** The `mcicda` driver never reports the end of a play: once the play head reaches `MCI_TO` the device goes on answering `MCI_MODE_PLAY` with the position frozen there, and no `MM_MCINOTIFY` is ever posted. So the restart never happens, and retail's mission music falls silent after one pass of its track.

#### The disc

The retail disc's table of contents, as `IOCTL_CDROM_READ_TOC` reports it:

| Track | Kind | Start (LBA) | Length |
|---|---|---|---|
| 1 | data | 0 | — |
| 2 | audio | 172283 | 2:24.87 |
| 3 | audio | 183148 | 2:45.27 |
| 4 | audio | 195543 | 2:45.24 |
| 5 | audio | 207936 | 2:54.20 |
| 6 | audio | 221001 | 2:53.40 |
| 7 | audio | 234006 | 2:26.92 |
| lead-out | | 245025 | |

Track 7 is music in its own right, distinct from the other five, and **the game never plays it**: the track formula below reaches 2 to 6 only.

#### Which track, and whether there is one

`Sim_InitMissionSession` (`004614fc`) writes `Music_CdTrack` (`0049f914`) and `Music_CdEnabled` (`0049f918`) and then calls `Sound_StartMissionMusic` (`00463038`), which plays only if `Sound_MusicEnabled` is also up.

```
Music_CdTrack = Music_TrackSelect % 5 + 2
```

`Music_TrackSelect` (`004d25f7`) is the `-R` command-line switch, parsed with `atol` at `0045e824`. **Nothing else in DBSIM picks a track**, and the switch defaults to 0. `ES.EXE` passes `-R<n>` with `n` counting the simulator launches of its own run from 0, so retail's missions play tracks 2, 3, 4, 5, 6, 2… in the order they are flown ([`../command-line.md`](../command-line.md#the-loop)). The remainder is a signed `IDIV`, so a negative `-R` would ask MCI for a track below 2.

The whole arm is skipped when `TrainingMissionNumber` (`004aa7ac`) is nonzero, so **a training mission runs without music**. That value is the copy of `script.dat` header offset 8 taken at the end of `DBSim_LoadScriptDat` (`00425321`); it also selects the larger pilot and squad message port and supplies the digit of the `TM<n>_` instructor voice template — see [`script-dat.md`](script-dat.md#header-format).

`Music_CdEnabled` has exactly one reader, `Sound_SuspendAll`, which is why a mute saves no position unless a track is set but a suspend tests both.

#### The mission session overrides the MUSIC preference

`Sim_InitMissionSession` ends with an unconditional `Sound_SetMusicEnabled(1)`, long after the arm above has already tested the flag. With MUSIC off in `prefs.cfg` no track starts — `Sound_StartMissionMusic` sees the flag down — but the flag is then raised behind it, so the next `Sound_ResumeAll` starts the music the player turned off. Alt-tabbing away and back is enough.

#### No drive is named

`Music_PlayTrack` opens the device type and nothing else: `MCI_OPEN_TYPE` with the string `cdaudio`, no `MCI_OPEN_ELEMENT`, so MCI answers with whichever CD drive it picks. Neither executable reads a drive letter from anywhere — there is no `GetDriveType`, no `GetLogicalDrives`, and no key for one in `SOUND.CFG` or any other configuration file. Nor is the disc checked: any audio CD in the drive plays.

### `sfxWndProc` (`00462294`)

Registered through `WndProcHook_Register` — this is one of the four `MainWndProc` filters mentioned in [`cockpit-input.md`](cockpit-input.md). It handles exactly two messages: `WM_TIMER` (`0x113`), pumped into the SOS/MME service routine, and the `MM_MCINOTIFY` loop above.

### `DATA\SOUND.CFG`

Plain INI, read with `GetPrivateProfileString` by `Sfx_ReadConfig` (`00463698`) into the manager's config block at `+0x24`:

| Key | Values | Stored |
|---|---|---|
| `Driver` | `MME` (default) or `DirectSound` | `+0x2a` = 1 or 2 |
| `Buffers` | 1-64, else 5 | `+0x30` |
| `Rate` | `11` gives `0x10`, anything else `0x20` | `+0x24` |
| `Width` | `Mono` gives 4, else 8 | `+0x28` |

`+0x26` is fixed at 1 and `+0x32` at `0x200`. `Buffers` only applies to the MME driver, per the file's own comments.

## The `SFX` manager

One instance, `0x4c` bytes, at `0049f904`. Its method names survive as assertion strings in VSHELL (DBSIM's copy is stripped down to `setVolume` and `cache`): `open`, `close`, `cache`, `play`, `stop`, `stopAll`, `isDone`, `setVolume`, `setPan`, `setPitch`, `setPriority`, `setLooping`, `setCallback`, `getAttributes`, `setAttributes`, `getSampleData`.

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

**Handles are `generation << 16 | slotIndex`.** Every accessor re-reads the slot's own copy of the handle and rejects a mismatch, which is what the `Sample handle is old and no longer valid` assertions report. `0xffffffff` is the null handle.

A *resource* is one file; a *voice* is one playable instance bound to a resource. Two voices opened on the same filename share the resource and bump its refcount, which is how the ten music ids and their single file coexist.

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
| `0x1000` | open type 2 — a third playback path, a file streamed by name through the window handle. See [Open](#open). |

### A repeated play layers; it does not restart

`Sfx_Play` (`00463f34`) never tests flag `0x100` before starting. It caches the resource if it has to and goes straight to `Sos_StartVoice` (`0047378c`), whose sample path zeroes the voice record's backend handle (`+0x24`) and hands that field to `sosDIGIStartSample` as an out-parameter. Every call is therefore given a **new** SOS handle, and the one it replaces is neither stopped nor reused: playing a catalog id that is already sounding starts a second concurrent copy on another driver channel.

The record's bookkeeping is one deep and does not follow. It keeps only the newest handle, so `Sfx_Stop` and `Sfx_StopAll` (`004647dc`) — both through `sosDIGIStopSample` — can no longer reach the older copies, and the manager's playing-sample count (`+0x3c`) is bumped once per start against one completion callback per copy. The drift is inert: DBSIM binds `sosDIGISamplesPlaying` and `sosDIGISampleDone` but calls neither, and nothing else reads the count.

Because the settings belong to the record and not to the copy, **placing a new copy retunes the one already sounding**. `Sound_Place` sets volume and pan for the sound it is about to start, and `Sfx_SetVolume` (`00464514`) writes the record and then applies it through `Sos_ApplyVolume` (`004739e0`) to the handle at `+0x24` whenever the record is marked playing — which, until the new start overwrites it, is the previous copy. A near footstep therefore takes on the placement of the distant one that follows it.

[The play-request gate](#the-play-request-gate) is what would have thinned this, and it is dead code in the shipped binary — so nothing does.

### Binding the SOS DLL

`Sos_BindLibrary` (`004957f1`) loads `sos9503.dll` on the NT-family branch of its `GetVersion` test and `sos32s03.dll` otherwise, then walks `SosBindingTable` (`004a6ab4`) calling `GetProcAddress` for each entry. The table is 100 records of `0x24` bytes, terminated by a null destination:

```
+0x00  void**  destination slot   -- one of the pointers at 006cbde4-006cbf70
+0x04  char    exportName[0x20]   -- inline, not a pointer
```

Each slot has a one-line thunk in `00495xxx` that does nothing but call through it, so a thunk's meaning is recovered by reading its slot address out of the disassembly and finding that address in the table. The pointers live in BSS and are written only by this loop, which is why nothing in the disassembly appears to assign them.

`Sfx_Open` (`00463910`) chooses the path from its third argument: 0 = `.hmp` song, 1 = sample, 2 = the streamed type. The caller decides by searching the filename for `.hmp` / `.wav`.

### Memory budget and eviction

`Sfx_Cache` (`00463c48`) is the load/unload call. Loading first stats the file, and if `cached + size > cap` it calls `Sfx_EvictUntilFree` (`0046428c`) before committing. The victim picker (`0046417c`) scores every live voice and takes the **lowest**:

```
score = (resource cached ? 100 : 0)
      + priority
      + (playing            ? 1000  : 0)
      + (playing && looping  ? 10000 : 0)
```

so an idle, uncached, low-priority voice goes first and a looping playing one goes last.

`Sound_Init` (`0046230c`) sets the cap to **2,000,000 bytes**, or **1,000,000** in the low-memory mode (`CockpitArt_LoadOnDemand` — `-l`, or under 12 MB physical).

### Backend volume and panning

`Sos_ApplyVolume` (`004739e0`) converts the voice's 0-100 volume to SOS's range as `volume * masterVolume * 0x7fff / 10000`, duplicated into both 16-bit halves for left and right, and for a MIDI song as `volume * 0x7f / 100`. `masterVolume` (`004a0e48`) is a constant 100 — its setter (`004739a8`) has no callers.

## The sound catalog — `str\SOUNDS.STR`

The game addresses sounds by a small integer, 0-56. The mapping lives in `SOUNDS.STR`, a `.STR` string table (layout in [`str-strings.md`](str-strings.md)) whose single group of 57 entries pairs a filename with a 7-byte attribute blob.

`SoundCatalog_Load` (`00462448`) walks the group into three parallel arrays — names (`004d2b0c`), attribute pointers (`004d2bfc`), voice handles (`004d2cfc`) — and for each entry opens a voice, sets priority 5, applies the attributes, then fixes up defaults.

The code treats the blob as **ten** bytes. The file supplies seven; the last three are runtime scratch that the loader initialises in place.

| Byte | Meaning |
|---|---|
| 0 | loop count — `Sfx_SetLooping`. `0` = loop forever, `1` = once, `n` = n times |
| 1 | volume, 0-100, applied as `Math_Q16Multiply(v, 65000)` |
| 2 | preload — nonzero caches the sample at startup instead of on first play |
| 3 | play requests per play (see below) |
| 4 | rolloff start distance, in units of 1024 world units. `0xff` becomes 5 |
| 5 | cutoff distance, same units. `0xff` becomes 100 |
| 6 | variation count — playing id *i* actually plays `i + rand(count)` when count > 1 |
| 7 | *runtime*: "was playing" flag, for suspend/resume |
| 8 | *runtime*: category volume percentage, initialised to 100 |
| 9 | *runtime*: play requests counted |

Because `.STR` attribute blobs point directly into the loaded file buffer, bytes 7-9 of one entry overlap the next entry's length field and first name byte. That is inert — every pointer is collected before the first write — and the four empty entries the file carries after the last real sound give the last one its slack.

`0xff` in bytes 4 and 5 means "use the default", not "not positional".

### Ids 0-9 are music

`Sound_IsCategoryEnabled` (`00462680`) splits the catalog at 10: ids below 10 answer to the music enable flag (`0049f90c`), ids 10 and up to the effects flag (`0049f910`). Every mute/unmute pair in the module respects the same boundary.

All ten music entries name `battle1.wav`, and **no `battle1.wav` ships in any archive**, so the digital-music path is dead in retail — music is the CD. `Sound_ShiftMusicSet` (`00462fbc`) offsets one character of each of the ten filenames by a delta and re-opens them, which is how a different set would have been selected.

### Sample banks

`Sound_ResolveSamplePath` (`00462238`) prefixes the catalog's filename with `HMI\` normally and `HMX\` in the low-memory mode. `SIMSOUND.VOL` carries both: 43 files under `hmi\` and 42 under `hmx\`, each `hmx\` file roughly half the size of its `hmi\` twin — the same content at half the sample rate.

**`EXPLO5.WAV` exists only in `hmi\`.** Catalog id `0x22` names it, so in low-memory mode that one sound fails to open.

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

`-` is the authored `0xff`, i.e. the 5/100 defaults. The four empty entries have no attribute bytes at all and are never opened.

`0x33` is not the flamer: it is the burning-object loop, started by the first live [`FireEffect`](../simulation/destruction-effects.md#fire) and stopped by the last, and kept positioned on whichever fire is nearest the camera — see [`../simulation/destruction-effects.md`](../simulation/destruction-effects.md#where-the-shared-sound-is-heard) for how that one is picked.

This resolves the sound ids scattered through the other docs: `0x0b` is `laser1.wav`, the beam muzzle sound of [`../simulation/weapon-firing.md`](../simulation/weapon-firing.md); `0x16` the target-lost tone of [`../simulation/missile-lock.md`](../simulation/missile-lock.md); `0x21` the torso servo loop of [`../simulation/torso-aim.md`](../simulation/torso-aim.md); `0x2f` and `0x30` the drop pod's fall and landing in [`../simulation/mission-deployment.md`](../simulation/mission-deployment.md). The `+ 10` seen at every data-driven call site — `record.SoundId + 10` in `PROJ.DAT`, `ROCKETS.DAT`, `EXPLOS.DAT` — is exactly the music/effects split: those tables index the effects half of the catalog from zero.

## Playing a sound

Two entry points, both taking a catalog id.

**`Sound_Play` (`0046272c`)** — non-positional. Applies the variation roll, sets volume to `Q16Multiply(vol, 65000) * byte8 / 100` (or 0 if the category is muted), and plays.

**`Sound_PlayAt` (`004627dc`)** — positional, `(id, worldPoint)`. Applies the variation roll, then `Sound_Place` (`00462898`) computes volume and pan; it plays only if the result is audible.

`Sound_Place` resets the model transform and pushes the world point through the current camera transform, so the listener is the camera. Then, with `d = Math_FastMagnitude3D(view)`:

```
minRange = attr[4] * 1024
maxRange = attr[5] * 1024
if d > maxRange:            volume = 0        -- not played at all
else:
    volume = Q16Multiply(attr[1], 65000)
    if d >= minRange:       volume = (maxRange - d) * volume / maxRange
volume = volume * attr[8] / 100
```

The rolloff divides by `maxRange`, not by `maxRange - minRange`, so a sound at exactly `minRange` is already attenuated rather than at full volume.

Pan comes from the horizontal bearing, `a = Math_Atan2Bam(viewX, viewY)`, as `(-2a) & 0xffff` for `a < 0x8000` and `2a & 0xffff` otherwise — a full sweep of the pan range over half a turn, mirrored front to back.

At 166.667 world units per metre ([`../engine/planning.md`](../engine/planning.md)), a `max` of 40 is about 245 m, and the largest — `herceng1`'s 50 — about 307 m.

### The play-request gate

`Sound_ConsumeRequest` (`004626c4`) exists so that a sound fired by many objects at once does not play once per object. **Nothing in DBSIM calls it**: there is no `CALL` to it anywhere in the code section, and its address is stored nowhere, so it is not reached indirectly either. The authored divisors in attribute byte 3 are therefore inert in the shipped game, and every play goes through:

```
interval = (2 - detailSetting) * attr[3]
if interval == 0:  play
else:              attr[9]++;  play only when attr[9] % interval == 0
```

`attr[9]` wraps at `0x0f`. `detailSetting` (`004d1fc7`) is an options-screen 0-2 value, so the highest setting zeroes the interval and lets everything through, while the lowest doubles the authored divisor.

### Mute, suspend and resume

`Sound_MuteMusic` / `Sound_MuteEffects` (`00462c74` / `00462cd8`) zero the volume of their half of the catalog and clear the enable flag; the unmute pair restores each id's own `Q16Multiply(vol, 65000) * byte8 / 100`. Music mutes by stopping the CD instead when a track is set.

`Sound_SuspendAll` (`00463078`) records which voices are playing into attribute byte 7, saves the CD position, and stops everything. `Sound_ResumeAll` (`00463134`) replays exactly those and resumes the CD from the saved TMSF position.

`Sound_SetCategoryVolume` (`00462f5c`) writes attribute byte 8, the per-sound category scale every volume computation multiplies through.

### The cockpit power-up

`Cockpit_PowerUpSound` (`004328cc`) is what the player hears on taking a machine. Two sounds, and the second is conditional:

```
if (cockpit+0x245 == 0):                 -- once per session
    cockpit+0x241 = Time_GetCoarseTicks()
    Sound_Play(0x13)                     -- start3, the start-up sequence
if (mech+0x1f2 -> +0x50 != 0):
    Sound_Play(0x2d)                     -- herceng1, looping forever
    Sound_SetPitch(0x2d, 42000)          -- 42000/65536, about 0.64
```

The engine hum is not started at its recorded rate: it is dropped to roughly two thirds of it immediately, which is what turns the sample into a hum rather than a whine. It loops for the rest of the mission — attribute byte 0 is 0 — and follows its machine through `Sound_UpdatePosition`.

**The hum belongs to the flyer, not to a HERC.** The gate is type record `+0x50`, which is file offset 78, `InputFlagFlyer`, set on the RAZOR alone (see [`../simulation/mech-locomotion.md`](../simulation/mech-locomotion.md)'s type-record table). A walking HERC powers up with `start3` and nothing else; its running noise is its footsteps.

### Sounds a cockpit control makes

Two toggles play a confirmation directly rather than through any data table:

| Trigger | Sound |
|---|---|
| `Mech_ToggleRadarMode` (`0041b468`) | `0x1a` `gnract` going ACTIVE, `0x1b` `gnrdact` going PASSIVE. Not positional — the cockpit makes it, not the world. |
| Heads-down display transmit (`0044cc40`) | The same pair, reused as its accepted/rejected blip. |
| `Widget_ClickSound` (`00438e2c`) | `0x11` `gm_69`, the console click. |

The mode-change tone is the [R] path only. The scanner screen's PASS/ACTIVE buttons write `mech+0x96` directly and play nothing. The radar toggle also announces the new mode in the computer's voice — see [`cockpit-messages.md`](cockpit-messages.md#posters).

`Widget_ClickSound` is the whole of the click: `push 0x11; call Sound_Play; ret`, and it is the image's only reference to that id. Nothing calls it directly — it sits in **fifteen widget vtables**, `PanelGadget`'s own and the fourteen button classes that inherit it, so a widget clicks because of what kind of widget it is and not because its handler did anything. That is why a button wired to nothing still clicks.

Which kind matters: the slot belongs to `PanelGadget`, the mixin base a cockpit widget carries alongside its button or slider class, and `PanelSliderGadget` overrides it with an empty stub (`00439014`). **Dragging the throttle makes no sound at all**, and neither does an alert panel's slider row. A control that takes neither mixin has no such slot to begin with and is silent for that reason: the click surface over the 3D view, the F7 map's surface, `HDDisplayGadget` and `ScrollTrigger` — see [`cockpit-input.md`](cockpit-input.md#the-second-vtable).

## Speech and the comm portraits

Squadmate and commander speech does not go through the catalog. It has its own five-slot channel pool allocated by `Sound_Init`: five records of `0x42` bytes plus a 5 x 100-byte script buffer.

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

`Voice_Acquire` (`00462a98`) looks the requested `.wav` name up across the five slots; a miss evicts the least recently used one (`00462a2c`), copies the name in, opens an `SFX` voice at **priority `0xff`** so the catalog's priority-5 voices can never evict it, caches it, and loads the matching `.SNC` script. Speech is gated on its own enable flag (`0049f97e`). What the `.SNC` script drives is the comm portrait, not the audio — see [`heads-down-display.md`](heads-down-display.md#snc--portrait-lip-sync-scripts).

### File naming

`CommBox_BeginMessage` (`0044afc8`) builds two names from the speaker's squad slot and the message id:

```
suffix = "_" + 2-digit message id + 3-digit variant     e.g. "_01000"
wav    = "P" + voiceBank + suffix        in simvoice/simvoicf/simvoicg
snc    = "P" + ('A' + slot) + suffix     in snc/
```

`voiceBank` is `(slot >> 2) + 1`, with 3 remapped to 4 — so twelve squad slots share three recorded voices, `P1_`, `P2_`, `P4_`. That is the same 1/2/4 grouping as the channel's own message sets ([`cockpit-messages.md`](cockpit-messages.md#its-message-sets)). `SIMVOICE.VOL` holds 147 `P*_*.WAV` and 66 `CVM_*.WAV`, the cockpit computer's own lines.

The three name templates live together in DATA as literals the loader patches digits into: `BC_00000`, `TMx_0000`, `CVM_0000`.

The archive is chosen by `Voice_ArchiveName` (`0045ef68`), which patches the last character of the literal `simvoice` with the language byte — `SIMVOICE` / `SIMVOICF` / `SIMVOICG`. All three are the same size, carry the same `SIMVOICE` folder label inside, and differ only in their recordings.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| `.SNC` is an audio format | It carries no samples. It is a two-byte-per-event portrait animation script, and the audio beside it is an ordinary RIFF WAV — see [`heads-down-display.md`](heads-down-display.md#snc--portrait-lip-sync-scripts). |
| Attribute byte 0 selects a mixer channel or category | Its three retail values (0, 1, 5) look like a small enum, but it is passed straight to `Sfx_SetLooping` as a repeat count — 0 means forever, which is why the music entries and `herceng1`/`fire1a` carry it. |
| Attribute byte 2 is "looping" | It is the preload flag; `Sfx_Cache` is a load call, not a play call. Looping is byte 0. |
| The `battle1.wav` entries are the real music | The file ships in no archive. The ten slots are a stub; music is Red Book CD audio through MCI. |
| A `.wav` name resolves under one directory | It resolves under `HMI\` or `HMX\` depending on the low-memory flag, and the two banks are not identical — `EXPLO5.WAV` is missing from `HMX\`. |
| `herceng1` is the HERC engine hum | The name says so and the sample is one, but the only thing that starts it gates on type record `+0x50` — `InputFlagFlyer`, the RAZOR. A walking HERC never plays it. |
| One voice per catalog id means one copy of that sound at a time | The voice record is bookkeeping, not a hardware channel. `Sfx_Play` starts a fresh `sosDIGIStartSample` every call without testing the `0x100` playing flag, so the copies overlap — see [A repeated play layers; it does not restart](#a-repeated-play-layers-it-does-not-restart). |

## Engine coverage

`Herculan.Engine.Audio` covers the catalog and the effects path: `SoundCatalog` parses `SOUNDS.STR` with the attribute layout above, `SoundBank` picks the `HMI`/`HMX` folder and decodes the samples out of `SIMSOUND.VOL`, and `SoundDirector` is the `Sound_*` layer — one voice per catalog id, the variation roll, the category split, `Sound_Place`'s rolloff and pan, and suspend/resume. `OpenAlBackend` stands in for HMI SOS; `NullAudioBackend` runs the same rules silently. `GameAudio` is the host-facing bundle and is itself the `ISoundSink` the simulation reaches through `SimWorld.Sounds`, with `PlayTableSound` applying the `+ 10` bias for `PROJ.DAT`, `ROCKETS.DAT` and `EXPLOS.DAT` ids.

The five-slot speech channel is ported too: `SquadVoice` opens the `P*_*.WAV` clips and `ComputerVoice` opens `CVM` clips out of `SIMVOICE.VOL`, both keeping every clip they open rather than running the original's five-slot LRU. Which messages reach either voice, and the `.SNC` portrait that plays alongside a squad line, are [`cockpit-messages.md`](cockpit-messages.md) and [`heads-down-display.md`](heads-down-display.md#snc--portrait-lip-sync-scripts)'s.

Triggers ported so far: the beam report, the two table-driven fire sounds and the impact sound (with the ground hit's suppression), footfalls, the console click, the radar mode tone and its spoken announcement, the lock/acquire/loss tones, the power-up with its announcement and its flyer hum, and the missile-inbound warning.

**Copies overlap, as they do in retail, but the channel ceiling is this engine's own.** `OpenAlBackend` keeps one buffer per sample and claims a source from a pool of `ChannelCount` (64) per play, so an id sounding twice occupies two sources; `SoundDirector` keeps the id's volume, pan and pitch and the newest handle, exactly as the original's voice record does. What is not reproduced is the ceiling: retail's is whatever its SOS driver was initialised with, and the `sosDIGIInitDriver` argument block at `006b5614` is filled field by field with nothing to name the words ([Open](#open)). 64 is chosen against what the game asks for and against OpenAL Soft's own limit of 256 sources. A play that finds every channel busy is dropped, which is how `sosDIGIStartSample` fails too.

`SoundDirector.ConsumeRequest` is a faithful port of `Sound_ConsumeRequest` and, like the original, has no caller. It is kept because the attribute it reads is parsed and documented, not because anything uses it.

**The memory budget is not reproduced.** `SoundBank` decodes every sample the catalog names at startup instead of honouring the preload attribute and caching the rest on demand, so none of [Memory budget and eviction](#memory-budget-and-eviction) exists here — no cap, no refcount, no victim scoring. The whole `hmi` bank is about 1.5 MB of 8-bit PCM against the original's own 2,000,000-byte cap, so there is nothing for the eviction machinery to do; it would only start to matter for a bank the retail game does not ship.

### CD music

`SoundDirector` holds the three globals above the device — `CdTrack`, `CdEnabled`, `SavedMusicPosition` — and every branch that tests them: `MuteMusic`/`UnmuteMusic`, the CD arms of `SuspendAll`/`ResumeAll`, and `ApplyMusicOption`, which is the MUSIC row's own handler. `StartMissionMusic` is the mission arm, with `GameAudio.StartMissionMusic` applying the training gate above it. All of that is retail's. The device underneath, `ICdAudio`, is where the engine diverges.

**The transport is the engine's own.** Rather than asking the drive to play, `StreamedCdAudio` reads the track's audio digitally and plays it through OpenAL on a streamed voice of its own (`IAudioBackend.OpenStream`), outside the effect pool. It loops by wrapping its read from the track's last frame to its first, so the seam is gapless, and it is what makes music loop at all on current Windows ([above](#cd-audio)). Its positions are TMSF words at CD-frame resolution, the same shape as MCI's, so the director's saved position means the same thing under either transport. The PCM comes from an `IMusicSource`, and `CdAudio.Open` takes the first of these that works:

1. **`--music-dir`**: a directory of `Track02.wav` … `Track07.wav` (44.1 kHz 16-bit stereo), through `WaveFileMusicSource`. For a machine with no drive.
2. **The disc**, through `CdRipMusicSource`: `IOCTL_CDROM_READ_TOC`, then `IOCTL_CDROM_RAW_READ` in CD-DA mode against `\\.\F:`, which opens unelevated. The track is read on a worker thread, 26 sectors per call (52 fails with `ERROR_INVALID_PARAMETER`), and never past the track's own end: a read that crosses the lead-out fails whole. Playback starts on the first block, a few tens of milliseconds in, because the read runs at 7.8× realtime from a cold drive and about 20× once it has spun up. A read that fails after retries goes in as silence, sector by sector.
3. **MCI**, through `MciCdAudio`, for a drive that refuses raw reads or a machine with no digital output device.
4. **The rip cache** of the one disc this machine has ripped before, with the disc absent.

Every track `CdRipMusicSource` reads whole and undamaged is written to `%LOCALAPPDATA%\Herculan\cd-audio\<disc id>\TrackNN.wav`, where the id is a hash of the table of contents, and is read from there instead of the disc from then on. A cached read takes about 25 ms.

`MciCdAudio` is the `Music_*` layer command for command, with two divergences:

- **The loop is polled, not notified.** Retail asks for `MCI_NOTIFY` and restarts the track from `sfxWndProc`; that wants a Win32 window procedure, and this engine's window is Silk.NET's. `MciCdAudio.Update` asks the device every 200 ms instead, so the seam can be that much later than retail's.
- **The play head, not the device mode, is what says a track ended.** Given the frozen position [above](#cd-audio), the obvious mode poll never fires; the position is compared against the track's own length, with the mode kept only for a device that genuinely stops.

**A drive can be named** under either transport, through `--cd-drive`. Retail opens MCI's default device and nothing else. Without the switch, `CdRipMusicSource` takes the first CD drive holding audio tracks and `MciCdAudio` opens the device type alone, as retail does. Neither checks which disc it is: any audio CD plays, as it does in retail.

`NullCdAudio` is what a machine with none of the four gets, and everything above the device runs unchanged against it.

The `Sound_SetMusicEnabled(1)` that [overrides the MUSIC preference](#the-mission-session-overrides-the-music-preference) is not reproduced: the engine reads the row, starts the mission's music through it, and leaves it alone.

## Open

- **Unported:** the `.hmp` MIDI path. No `.hmp` ships, so nothing is lost in play.
- **Unported:** `ES.EXE`'s track rotation. The engine plays track 2 for every mission unless `--music` gives it a select value.
- **Unported:** reading `SOUND.CFG`. `HercWorks.Core` has `Data/File/Cfg/SoundCfg.cs`, a key holder with no reader.
- **Open:** which word of the `sosDIGIInitDriver` argument block at `006b5614` is retail's channel count.
- **Open:** whether any `Sfx_Open` caller in DBSIM passes open type 2, the streamed voice behind flag `0x1000`. A text search finds none, which does not settle it.
- **Open:** mid-session audio recovery, an engine need retail never had. If the endpoint drops (unplugged headphones, a changed default device), the engine stays silent for good. OpenAL Soft exposes `ALC_EXT_disconnect`/`ALC_CONNECTED`; detecting it is cheap, but reconnecting means recreating the 64-source pool in `OpenChannels` and re-uploading every buffer `CreateSample` handed out, since sample ids are indices into `_buffers` that `SoundDirector` and `ComputerVoice` both hold. Those ids would need to stay stable across a re-open, or both holders would need re-registering.
