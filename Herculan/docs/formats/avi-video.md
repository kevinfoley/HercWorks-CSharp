# AVI cutscenes

The game's full-motion video sits loose in `ES2/AVI/` as 93 `.AVI` files — mission briefings, the two-part intro, the territory maps on the campaign screen, the ending and the credits. They are ordinary Microsoft RIFF AVI containers holding one video stream and, usually, one uncompressed PCM audio stream.

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

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| Dispatch the decoder on the `strh` handler fourcc | The handler and the format compression disagree. The six Microsoft Video 1 files name `msvc` as their handler but carry `CRAM` in `strf`, and the four MS-RLE files name `mrle` but carry the numeric `BI_RLE8`. Dispatching on the handler finds none of the ten; the compression in `strf` is what names the codec. |
| A positive `biHeight` means top-down rows | It means bottom-up, which is the DIB convention and what every file in this corpus uses. A negative height is the top-down case. Reading the sign the other way renders every frame vertically mirrored — recognisable, so it survives a careless glance. |

## Open

- **Unported:** the `CRAM` (Microsoft Video 1) and `cvid` (Cinepak) decoders, 8 files. See `docs/engine/handoff-avi-codecs.md`.
- **Unported:** the `IV41` (Indeo Video 4.1) decoder, 1 file.
