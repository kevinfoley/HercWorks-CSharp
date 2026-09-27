# Video playback

How HERCULAN decodes and plays the game's AVI cutscenes. The files themselves are [`formats/avi-video.md`](../formats/avi-video.md), and the Indeo 3 codec is [`formats/indeo3.md`](../formats/indeo3.md).

Decoding is `HercWorks.Video`, a standalone assembly with no dependencies, and `Herculan.Engine.Video.MoviePlayer`, the seam that puts its frames on a GL texture and its sound through the engine's audio backend. Nothing calls into Video for Windows or any system codec; the decoding is HERCULAN's own.

## Audio

Tracks are decoded whole rather than streamed. The longest in the corpus is under a megabyte once widened, so streaming would be machinery with nothing to show for it.

A stereo track is folded to mono before it reaches the engine, because `Herculan.Engine.Audio.WaveSample` is mono and the backend pans at play time — it has nowhere to put a second channel. This is a loss against retail, which played the intro in stereo; the fix belongs in `IAudioBackend`, not here.

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

## Indeo 3 output

The decoded buffer is converted to RGBA with chroma sampled nearest-neighbour, using ITU-R BT.601 at studio range. The matrix is this engine's choice, not one read from `IR32_32.DLL`.

## Open

- **Open:** a stereo path through `IAudioBackend`, so a stereo cutscene track plays in stereo as it does in retail.
