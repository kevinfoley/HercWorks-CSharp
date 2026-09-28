# Handoff: Cinepak

Scratchpad for the one `HercWorks.Video` codec left that the shell plays. MS-RLE, Microsoft Video 1, Indeo 3 and the container are done and documented in `docs/formats/avi-video.md` and `docs/formats/indeo3.md`. Drain this into the format doc and delete it once Cinepak decodes.

| File | Size | Notes |
|---|---|---|
| `CREDITS.AVI` | 576x360 | 1390 frames, 16-bit mono 22050 Hz |
| `ES2CREDC.AVI` | 288x180 | 1392 frames |

## Recommended way in

Cinepak was not attempted. It is a strip-and-codebook format whose chunk type IDs and codebook loading rules are not derivable from a couple of files in reasonable time, and it is fully documented elsewhere: read the format description first, then write the decoder.

## Validation

The check that matters is the one MS-RLE and Microsoft Video 1 passed: decode a frame, write it out, and look at it. `MoviePlayback` will step a whole file, and a wrong decoder fails visibly rather than subtly. Exact consumption — every strip's chunks summing to the packet length — is a good first filter but is not sufficient on its own: a rule set can consume a packet exactly and still assign pixels wrongly.
