# AVI cutscenes

The game's full-motion video sits loose in `ES2/AVI/` as 93 `.AVI` files — mission briefings, the two-part intro, the territory maps on the campaign screen, the ending and the credits. They are ordinary Microsoft RIFF AVI containers holding one video stream and, usually, one uncompressed PCM audio stream.

Playback is `HercWorks.Video`, a standalone assembly with no dependencies, and `Herculan.Engine.Video.MoviePlayer`, the seam that puts its frames on a GL texture and its sound through the engine's audio backend. Nothing calls into Video for Windows or any system codec; the decoding is this project's own.

## What the corpus uses

| Compression | Files | Geometry | Decoder |
|---|---|---|---|
| `IV32` — Indeo Video 3.2 | 80 | 240x180, 288x180 | yes, see `docs/formats/indeo3.md` |
| `CRAM` — Microsoft Video 1 | 6 | 292x200, 640x480 | [Open](#open) |
| `BI_RLE8` — Microsoft RLE | 4 | 295x226 | yes |
| `cvid` — Cinepak | 2 | 576x360, 288x180 | [Open](#open) |
| `IV41` — Indeo Video 4.1 | 1 | 288x180 | [Open](#open) |

Video runs at 15 fps except the Microsoft Video 1 files, which are 10 fps, and `ESTAB2.AVI`, which is three frames at 1 fps. Audio, where present, is format tag 1 — uncompressed PCM — as either 8-bit stereo at 11025 Hz or 16-bit mono at 22050 Hz. The four MS-RLE thumbnails and all six Microsoft Video 1 files have no audio stream at all.

Every one of the 93 files parses as a container. Only the codecs differ.

## Container

Standard RIFF: a chain of `[fourcc][int32 length][body]` records with bodies padded to an even length, where `RIFF` and `LIST` bodies begin with a further four-character code and hold chunks of their own. `AviFile` reads the `hdrl` list for stream headers and the `movi` list for packets.

Streams are numbered by position — the n-th `strl` list is stream n — and that number is what the two ASCII digits leading each `movi` chunk id refer to. The suffix says what the payload is: `dc` or `db` for video, `wb` for audio. A packet is only accepted when the suffix and the stream header agree about the kind, so a mislabelled chunk cannot land in the wrong queue.

Packets are grouped into `rec ` lists in some files and loose in others, so the walk descends through lists rather than assuming either layout.

### The index is not followed

`idx1`, where present, is a table of offsets and lengths supplied by the file. Following it means dereferencing attacker-chosen pointers. `AviFile` instead walks the `movi` list, so every packet's bounds were established by the walk itself.

This costs nothing: the index is not needed to enumerate frames, and because every codec in this corpus is interframe, seeking to an arbitrary frame would require decoding from the previous key frame anyway.

## Microsoft RLE

The four `*_TH.AVI` territory thumbnails. Eight bits per pixel over a palette carried in the stream format header as BGRX quads, rows bottom-up.

The payload is a stream of two-byte opcodes. A non-zero first byte is a run of that many pixels in the colour named by the second. A zero first byte makes the second an escape:

| Escape | Meaning |
|---|---|
| 0 | end of row — drop a line, return to column zero |
| 1 | end of frame |
| 2 | delta — two bytes follow giving a (right, up) skip |
| 3 and above | that many literal palette indices follow, padded to an even length |

Skipped pixels keep what the previous frame left, which is why the codec is interframe and why the thumbnails work: each is a still map with one territory blinking, so every frame after the first is deltas and end-of-row escapes over an unchanged background.

The 4bpp variant, which packs two indices per byte, appears nowhere in the corpus and is rejected rather than guessed at.

## Audio

`AviAudioTrack` concatenates a stream's packets and widens them to signed 16-bit. 8-bit WAVE data is unsigned with `0x80` as silence, so it is widened about that midpoint rather than shifted.

Tracks are decoded whole rather than streamed. The longest in the corpus is under a megabyte once widened, so streaming would be machinery with nothing to show for it.

A stereo track is folded to mono before it reaches the engine, because `Herculan.Engine.Audio.WaveSample` is mono and the backend pans at play time — it has nowhere to put a second channel. This is a loss against retail, which played the intro in stereo; the fix belongs in `IAudioBackend`, not here ([Open](#open)).

## Playback

`MoviePlayback` owns the clock. The caller advances it by a delta and it decodes however many frames that crossed, returning whether the frame buffer changed so a host can skip re-uploading a texture on the many ticks that fall inside one frame's interval.

Frames are decoded in sequence and never skipped, even when several fall due at once. Each frame is the previous one plus a delta, so skipping corrupts everything after it — a host that falls behind drops presentation, not decoding.

`MoviePlayer` adds the engine side: a `GpuTexture` updated in place when the frame's revision changes, and the soundtrack started once through `IAudioBackend` as a single sample. Video is the clock and audio free-runs; nothing re-syncs them mid-playback, which is also what the original did.

## Looking at one

`--movie` plays a single cutscene instead of running a mission or the front end:

```
dotnet run --project Herculan/src/Herculan.Engine.Host -- --movie ALPH_TH.AVI
```

It takes a path, or a bare name — with or without the extension — to look up in the install's `AVI` folder. `--screenshot <file>` captures a frame and exits, `--silent` skips audio. The frame is letterboxed at its own aspect ratio and sampled nearest-neighbour.

A file whose codec has no decoder reports what the container says and what the compression is, and exits without opening a window, because that is the answer to "why will this not play".

This exists because a video decoder cannot be validated by a unit test alone. A codec that is subtly wrong — rows inverted, a block quadrant transposed, a colour channel swapped — still returns frames and still passes any test that only checks it did not throw. The failure is visual, so the check has to be.

## Security posture

These files are the one class of game asset a user might obtain from somewhere other than the user's own install, so `HercWorks.Video` is built to parse hostile input:

- It builds without `AllowUnsafeBlocks` and takes no `PackageReference` and no `ProjectReference`. There is no transitive code to audit, and the CLR's bounds checks are not given up for speed.
- Nothing in it opens a file, resolves a path, starts a process, or reflects. It is handed bytes and returns pixels.
- Every declared length is treated as a claim to verify. A length that is negative, that overflows when added to the cursor, or that runs past the enclosing chunk ends the walk rather than being followed.
- `VideoLimits` bounds frame dimensions, pixel count per frame, file size, chunk size, frame count, RIFF nesting depth and, for Indeo 3, cell recursion depth. Frame area is computed as `long` so two dimensions that each pass the per-axis cap cannot wrap when multiplied.
- Pixel writes clip in one place, `VideoFrame.SetPixel`, so a malformed run cannot reach another row or past the buffer. Run and skip counts come straight out of the bitstream and are never trusted as bounds.
- Malformed input returns null or false. Nothing throws on bad data, so a damaged cutscene cannot take down the host.

The suite covers these directly: every prefix of a valid file is parsed to prove truncation is safe at any length, a chunk is rewritten to claim `0x7FFFFFFF` bytes, and every prefix of a valid opcode stream is decoded.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Dispatch the decoder on the `strh` handler fourcc | The handler and the format compression disagree. The six Microsoft Video 1 files name `msvc` as their handler but carry `CRAM` in `strf`, and the four MS-RLE files name `mrle` but carry the numeric `BI_RLE8`. Dispatching on the handler finds none of the ten. `CodecRegistry` matches on `AviVideoFormat.Compression`, which comes from `strf`. |
| A positive `biHeight` means top-down rows | It means bottom-up, which is the DIB convention and what every file in this corpus uses. A negative height is the top-down case. Reading the sign the other way renders every frame vertically mirrored — recognisable, so it survives a careless glance. |

## Open

- **Unported:** the `CRAM` (Microsoft Video 1) and `cvid` (Cinepak) decoders, 8 files. See `docs/engine/handoff-avi-codecs.md`.
- **Unported:** the `IV41` (Indeo Video 4.1) decoder, 1 file.
- **Unported:** stereo cutscene audio. It needs a stereo path through `IAudioBackend`.
