# Indeo Video 3.2

Intel's `IV32`, the compression behind 80 of the 93 files in `ES2/AVI/` — both halves of the intro and every mission briefing. See `docs/formats/avi-video.md` for the container they sit in.

This doc holds what is settled. The frame and bitstream header layout, the cell tree below it and the decoder's own progress are still being worked out and live in `docs/engine/handoff-indeo3.md` until they are.

Addresses are virtual addresses in `IR32_32.DLL`, the Intel codec the game's installer drops in `ES2/INDEO/`. Its image base is `10000000`. Nothing in this engine loads that DLL; it is read as evidence only.

## Codebooks

A cell's pixel data is a stream of one-byte VQ codes, and a codebook is what turns one of those bytes into the pixel deltas it stands for. Decode a cell against the wrong codebook and it comes out as noise.

The shipped codec does not carry them. The region from `10043000` runs 16 KB of zeroes, and the eight 512-byte objects at `10043e4c` that have exactly a codebook's shape are inside it. `.bss` is 144 KB with no file backing behind it, which is where the tables are built: codec init expands a much smaller seed into a 96 KB image, and the zero-filled objects are where the results are indexed from.

So the codebooks are derived rather than read. `Codecs/Indeo3/Indeo3Codebooks.cs` is that derivation.

### The seed area

`1004d26a` in `.data`, 5,251 bytes, ending at a zero count byte at `1004e6ec`. `Codecs/Indeo3/Indeo3SeedData.cs` holds it verbatim.

It is a run of variable-length blocks:

```
block := count            ; one byte, UNSIGNED
         pair[count]      ; two signed bytes each
         expand           ; one byte, SIGNED
area  := block* 0x00      ; a zero count byte ends it
```

The count being unsigned is load-bearing. The first block's is `0xC3`; read as -61 the walk desynchronises at the very first block, and — because a desynchronised walk still finds a zero byte eventually — it terminates rather than failing, so the mistake surfaces as a bad picture much later.

Walked correctly the area is exactly 24 blocks. Their counts run 195, 159, 133, 115, 101, 93, 87, 77 and then the same eight again, then 128 and seven 79s; their expansion bytes 7, 9, 10, 11, 12, 12, 12, 13 twice, then -11 and seven -13s. That regularity is itself the check that the walk stayed in step.

A pair's two bytes are the deltas for two adjacent pixels. Read together as one signed 16-bit value, high byte first, they are the unit the expansion arithmetic works in, and folding that value to 16 bits matters: carrying the wider intermediate through the shift in the expansion step below produces a different image that still looks structurally plausible.

### The expansion

Codec init builds a 96 KB image, tiled as two sets of 24 blocks of `0x800` bytes:

| Image offset | Contents |
|---|---|
| `+0x0000` | 24 blocks of paired deltas |
| `+0xc000` | 24 blocks of the same, byte-replicated |

Each `0x800` block is two 256-entry sub-tables, at `+0x000` and `+0x400`, addressed as 32-bit words.

Three passes, in this order, because each overwrites the last:

1. **Prefill.** Every word takes its own byte offset from the image base; word 0 is left alone. These are not codebook entries. They are what an index past the end of a block's real content reads, and reproducing them matters only so a stream that indexes out of its block gets the same garbage the retail codec gets rather than a zero.

2. **Seeding.** Each of the block's `N` pairs becomes entry `k` of both its sub-tables, as the pair's 16-bit value biased by `0x8000` and moved into the top half of the word. The bias is what keeps the two coded pixels independent: it puts the delta add's carry out at bit 31, where it can be tested, instead of letting it borrow into the neighbouring pixel. The byte-replicated set gets the pair's two bytes as `(b, b, a, a)`, accumulated with 32-bit addition rather than assembled by OR — not the same thing, because a negative low byte borrows upward, so `(-2, -2)` gives `0xFDFDFDFE` and not `0xFEFEFEFE`. The second sub-table of that set holds the same word with bit 31 flipped.

3. **Expansion.** A further `d*d` entries, `d` being the magnitude of the block's expansion byte, over ordered pairs `(i, j)` drawn from the first `d` seeds: `(value[i] << 16) + value[j]` as a 32-bit sum. These are the two-pixel combinations the seeds alone do not cover. The sign of the expansion byte decides which index runs outermost, and for a negative byte the replicated set's second sub-table again takes the bit-31 flip.

Entries from `count + d*d` up to 255 keep their prefill.

### Applying a mode byte

The innermost step of cell reconstruction. A row predictor holds four 7-bit pixels packed one per byte, so one 32-bit add against a codebook entry updates all four at once.

The entry is read from the block's `+0x400` sub-table at the mode byte's index. Bit 31 of the sum is the carry the `0x8000` bias arranged for, and it doubles as the signal that the delta did not fit in one byte: when it comes back set, the stream carries a second byte, whose entry's top half is the correction for the low half of the result. A second byte that still leaves bit 31 set is the retail codec's error code 2 — a malformed stream.

### Evidence

Three independent checks, in `tests/HercWorks.Video.Tests/Indeo3CodebookTests.cs`:

- The 5,251 seed bytes compiled into the assembly match `IR32_32.DLL` byte for byte, checked against the retail file whenever it is present in the tree.
- The whole expanded image matches an FNV-1a digest of `60d8ce28421b3ef5`.
- Two entries reproduce values from a real decoded `IV32` luma plane: from the strip boundary predictor `40404040`, mode byte `6c` with its continuation byte gives four pixels of 10, and the byte after it gives four of 8.

The first two only say the derivation is self-consistent with its input. The third is the one that would catch a derivation that is wrong in the same way throughout.

The grammar and the three passes were not read out of the binary here — no disassembler is available in this environment. They come from the OxideAV `oxideav-indeo` project (MIT), which recovered them from this same DLL, and were re-derived independently against our own copy before being ported.

## The requantisation table

`1003d088`, `[8][128]`, initialised in the image and readable straight out. Row *q* is a staircase of step *q*+2: row 0 runs 0, 2, 2, 4, 4, 6, 6, 8, row 1 steps by 3, row 2 by 4. It remaps a pixel before a delta is added so the sum cannot leave the 7-bit range, and the rows top out at 126 and 127 accordingly.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The codebook seed is the interleaved region at `1004eb56`. | The region is real and its head does read as small signed pairs, and it even walks cleanly to a terminator under the seed grammar above — but the blocks it yields have no regularity at all (counts from 1 to 211, expansion bytes including 0 and -103), where the true area's 24 blocks fall into three obvious groups. It is a coincidental parse. The seed area is at `1004d26a`. |
| The codebooks can be lifted from the DLL, given the right offset. | The eight objects at `10043e4c` shaped exactly like eight codebooks of 256 two-byte entries are zero on disk, as is the rest of the 16 KB around them, and so are the 1 KB objects near `1003f04c`. There is nothing to lift; the generator is what had to be recovered. |
| `1004f253` is codebook data. | It is large — 17,953 bytes — but triangular, and nothing in a codebook is. Each of its 16 identical-shaped tables is 1,122 bytes: a leading index *K* counting down from 32 to 0, each followed by 2(32−*K*)+1 entries, with one spare byte at the end of the region. The entries look like indices rather than deltas. `IR32_32.DLL` is an encoder as well as a decoder, and this has an encoder search table's shape, as does the 512-byte table at `1004e954` holding *n*² for *n* = 0…255. |
