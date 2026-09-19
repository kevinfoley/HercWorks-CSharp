# Handoff: Indeo Video 3.2 (`IV32`) decoding

Scratchpad for the unfinished half of `HercWorks.Video`. The container, MS-RLE, audio and playback are done and documented in `docs/formats/avi-video.md`. Drain this into a topic doc and delete it once `IV32` decodes.

`IV32` is 80 of the 93 files in `ES2/AVI/`, including `INTR_PT1.AVI` / `INTR_PT2.AVI` and every mission briefing. All are 240x180 or 288x180 at 15 fps, with 8-bit stereo 11025 Hz PCM alongside.

## State

The codebooks are solved, in the tree, and written up in `docs/formats/indeo3.md`:

- `Codecs/Indeo3/Indeo3SeedData.cs` — the seed area, held as a literal, taken from `.data` at `1004d26a` in `IR32_32.DLL`.
- `Codecs/Indeo3/Indeo3Codebooks.cs` — the expansion of that seed into the codebook image the codec builds at init, which is why the objects at `10043e4c` that have a codebook's shape are zero-filled on disk.
- `tests/HercWorks.Video.Tests/Indeo3CodebookTests.cs` — checks the expansion, and checks the seed literal against the retail DLL whenever that file is present.

What remains:

1. **The cell decoder.** There is no `Indeo3Decoder`. Nothing yet reads a frame.
2. **Registry wiring.** `CodecRegistry.Create` still returns null for `IV32`, so `MoviePlayback.Open` refuses these files. The `Indeo3` fourcc constant is already declared there.
3. **Draining this file.** `docs/formats/indeo3.md` owns the codebooks. The header layout and cell structure below stay here until a decoder confirms them against real frames, then move across.

## Frame layout, confirmed against `INTR_PT1.AVI`

Each `00dc` chunk holds one frame: a 16-byte frame header, then a 48-byte bitstream header, then plane data.

Frame header, four little-endian `uint32`: frame number, a field that is zero throughout this corpus, a checksum, and the frame size in bytes counted from the start of the bitstream header. The obvious XOR reading of the checksum does not reproduce it; nothing needs it.

Bitstream header:

| Offset | Size | Field |
|---|---|---|
| 0 | 2 | decoder version; `0x20` for 3.2, and a hard reject otherwise |
| 2 | 2 | frame flags |
| 4 | 4 | data size **in bits** |
| 8 | 1 | `cb_offset`, one of the two codebook selectors |
| 9 | 1 | reserved |
| 10 | 2 | checksum, zero throughout this corpus |
| 12 | 2 | height |
| 14 | 2 | width |
| 16 | 4 | Y plane offset |
| 20 | 4 | V plane offset |
| 24 | 4 | U plane offset |
| 28 | 4 | reserved |
| 32 | 16 | `alt_quant`, paired codebook indices, one pair per byte as two nibbles |

Checks that held on the first frame and are worth asserting in a test:

- Version is `0x20`, and width and height are 288 and 180, matching the `strf` the container already read. A disagreement between the two is a good reason to reject the frame.
- Data size in bits is exactly the frame size in bytes times eight.
- Plane offsets are relative to the **start of the bitstream header**. The U plane's offset is `0x30` — immediately after the 48-byte header — and the file order is U, V, Y, which is not the order the header lists them in.
- `alt_quant` on that frame reads as high nibbles running 0 through 15 and low nibbles cycling 2, 4, 6, 8, 10, 12, 14, 15. Whether that is a default or frame-specific is untested; check a mid-stream frame before assuming.

Planes are Y, V, U. Chroma is quarter resolution, so 288x180 gives a 288x180 luma plane and two 144x90 chroma planes.

## Structure below the plane

From the published format description, not yet verified against this corpus:

- A plane is cut into vertical strips, 160 pixels wide for luma and 40 for chroma, the last narrower.
- A strip is split recursively, each split halving the region one way or the other, until the pieces are small enough to code. Two-bit codes drive the tree: two mean "split", the other two end the recursion as an intra or inter cell, an inter cell being followed by a motion-vector index byte.
- Inside a cell a further pair of codes selects null data (copy or skip) or real quantised data.
- Cells bottom out at 4x4, 4x8 or 8x8 pixels.
- A 4x4 cell is eight dyads, a dyad being one VQ code standing for a delta over two pixels at once. Two dyads can pair into a quad, a delta over four.
- Byte values `0xF8`–`0xFF` are escapes rather than VQ codes: null deltas to the end of a line, to the end of the block, or across the next block, and skip/copy runs of blocks, one of which takes a following count byte.

The per-mode pixel ordering and the exact escape semantics still need confirming against real frames. The requantisation table these deltas are applied through is in `docs/formats/indeo3.md`.

## Guardrails

`HercWorks.Video` compiles without `AllowUnsafeBlocks` and takes no dependencies, and this decoder must not be what changes either. Two `VideoLimits` fields exist for it and are so far unused:

- `MaxCellDepth` bounds the split recursion. The split codes come from the bitstream, so a stream of nothing but splits recurses without limit.
- `MaxChunkBytes` and the frame-size checks bound the plane offsets. All three are attacker-controlled `uint32` values and must be range-checked against the frame before any plane is read, not as they are used.

Motion vectors are likewise stream-supplied and address the previous frame; clamp them to the reference plane rather than trusting the index.

## Validation

The check that matters is the one MS-RLE passed: decode a frame, write it out, and look at it. `MoviePlayback` will step a whole file once the registry is wired, and a wrong decoder fails visibly. Frame 0 is intra, so it needs no motion compensation and is the right first target.
