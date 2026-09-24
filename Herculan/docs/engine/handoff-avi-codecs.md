# Handoff: MS Video 1 and Cinepak

Scratchpad for the two `HercWorks.Video` codecs that are not written. MS-RLE, Indeo 3 and the container are done and documented in `docs/formats/avi-video.md` and `docs/formats/indeo3.md`. Drain this into the format doc and delete it once both decode.

Between them these cover 8 of the 93 files: 6 MS Video 1 and 2 Cinepak.

| File | Codec | Size | Notes |
|---|---|---|---|
| `ALPHA.AVI`, `BRAVO.AVI`, `DELTA.AVI`, `OMICRON.AVI` | MS Video 1 | 292x200 | territory briefings, no audio |
| `LUNA.AVI` | MS Video 1 | 292x200 | no audio |
| `ESTAB2.AVI` | MS Video 1 | 640x480 | 3 frames at 1 fps, no audio |
| `CREDITS.AVI` | Cinepak | 576x360 | 1390 frames, 16-bit mono 22050 Hz |
| `ES2CREDC.AVI` | Cinepak | 288x180 | 1392 frames |

## Container facts already handled

Two traps, both already dealt with in `CodecRegistry`, worth not re-learning:

- The MS Video 1 files name `msvc` as their handler in `strh`, but the `strf` compression fourcc they are actually dispatched on is **`CRAM`**. Matching on `msvc` finds nothing.
- The MS-RLE files likewise name `mrle` in `strh`, but their `strf` compression is the numeric `BI_RLE8` (1), not a fourcc at all.

All six MS Video 1 files are **16 bits per pixel**, so the 8-bit palettised variant of the codec is not needed for this corpus. All are bottom-up.

## MS Video 1: what the bitstream shows

Reverse engineering was attempted from the data alone and did not converge. What was established, from `ALPHA.AVI`:

Packet sizes alternate strictly: 15638, 13634, 15638, 13466, 15470, 13464 … so frames alternate between a more fully coded form and a lighter one.

The decisive observation is that frame 1 and frame 2 code the same opening differently:

- Frame 1 begins `8409`, then continues `3fff 0000 1123 0d00 8000 …`
- Frame 2 begins with **nine** `8000` words, then continues `3fff 0000 1123 0d00 8000 …` — byte-identical from that point on.

So `0x8409` advances the same nine blocks that nine `0x8000` words do. That fixes two things:

- `0x8400 | n` is a skip run of *n* blocks, and the count is the low bits.
- `0x8000` covers **exactly one block and consumes no payload** — it is a one-word code, not a code with colours after it.

Also established: `0x98F0` occurs 720 times in frame 1 and `0xE2C7` 147 times, while `0x18F0` and `0x62C7` both occur as ordinary colour values. Since these pairs differ only in bit 15, bit 15 on a colour word is a flag rather than part of the colour — consistent with it marking the eight-colour block form. RGB555 leaves bit 15 spare, so there is room for exactly that.

Blocks are 4x4 and the picture is 73x50 of them for the 292x200 files, 3650 in total.

## What was ruled out

Each of these was tested by requiring a parse to consume a packet **exactly** and land on **exactly** the block count, across all frames of all six files. None got past about 45 of 193 frames, and the 45 are the short ones where the ambiguous cases never arise:

- Skip range `[0x8000, 0x8400)` or `[0x8000, 0x8800)`, count `code & 0x3FF`, with and without a bias of 1 on the count.
- Skip range `[0x8400, 0x8800)` with `[0x8000, 0x8400)` as a payload-free one-block code.
- Eight-colour marker on the first colour's bit 15, on the second colour's bit 15, on the control word's bit 15, and on `c0 > c1`.
- A solid-colour block form of control word plus one colour, at thresholds `0x8400`, `0x8800`, `0x9000`, `0xA000`.

Two searches were also run and did not settle it:

- A reachability DP allowing every plausible transition freely found valid parses, but 2894 different end block-counts are reachable, so it does not identify a rule.
- A backtracking search constrained so that the transition must be a consistent function of observable bits — control-word range, both colours' bit 15, their ordering — did not terminate within seven minutes on one frame.

The parse reliably runs to block 84 of `ALPHA.AVI` frame 1 before it desynchronises, at byte 1006, on a long run of `98f0` words. Whatever that run is, it is the thing the model gets wrong. A run of identical eight-colour blocks would be pointless for an encoder to emit, so it is more likely a form not yet modelled.

## Recommended way in

Do not resume this from the data. It is a documented format and the reason it stalled is that the reference — the MultimediaWiki page — was unreachable for the whole attempt. Read the format description first, then write the decoder; the observations above are enough to check a reading against real bytes quickly, and the exact-consumption test makes a wrong reading obvious in seconds.

Cinepak was not attempted at all, for the same reason: it is a strip-and-codebook format whose chunk type IDs and codebook loading rules are not derivable from a couple of files in reasonable time, and it is fully documented elsewhere.

## Validation

The check that matters is the one MS-RLE passed: decode a frame, write it out, and look at it. `MoviePlayback` will step a whole file, and a wrong decoder fails visibly rather than subtly. The exact-consumption test is a good first filter but is not sufficient on its own — a rule set can consume a packet exactly and still assign pixels wrongly.
