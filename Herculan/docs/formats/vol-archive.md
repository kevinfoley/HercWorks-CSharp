# .VOL — the archive container and its per-entry prefix

Every one of the game's data files ships inside a `.VOL`. Eleven archives, 3,004 entries total:
`SIMVOL0`, `SIMPATCH`, `ZONES`, `SHELL0`, `LANG0`, `SIMALERT`, `SIMSOUND`, `SIMVOICE`, `SIMVOICF`,
`SIMVOICG`, `SHLSOUND`.

Parsed by `HercWorks.Vol.Io.VolFileReader`, which hands callers the entry's **content** —
`VolEntry.RawBytes` — with the per-entry prefix already split off into its own fields. Every format
doc in this folder describes offsets from the start of that content. A format whose own file is
unpacked with the prefix still attached — as [bnd-notes.md](bnd-notes.md) covers for `.BND` — is
easy to misread as owning these nine bytes.

## File layout

```
0x00   4       "VOLN"
0x04   byte    read by DBSIM
0x05   byte    read by VSHELL
0x06   2       0x0000
0x08   byte    load precedence: 0x05 base, 0x0A second (SIMPATCH, SHELL1)
0x09   byte    directory count
0x0a   uint16  directory-list byte size
0x0c   ...     directory list: name + '\' + 0x00, repeated
       uint16  entry count
       int32   entry-list byte size
       ...     entry list, 18 bytes each:
                 13  name, NUL-padded (a few entries carry junk after the NUL)
                 1   directory index
                 4   uint32 offset of this entry's prefix
       ...     entry data, at the offsets above
```

The first entry's data begins at the byte immediately after the entry list, with no gap
(verified in all eleven archives).

## The per-entry prefix — fixed 9 bytes

```
+0   byte    storage flag           0x02 in all 3,004 entries
+1   int32   content size, LE       the content alone
+5   uint16  MS-DOS packed date     source file's timestamp
+7   uint16  MS-DOS packed time
+9   size    content
+9+size  byte  trailer, repeats content's last byte
```

Entry stride is therefore `size + 10`, and the last entry's trailer is the archive's last byte.

**`+1` is the content length, not the entry length.** Independently confirmed against the 729 RIFF
WAVs in the sound and voice archives, whose own `RIFF` chunk header states their length: for 723 of
them `riffSize + 8` equals the field at `+1` exactly. The other six (`CVM_0028.WAV` and
`CVM_0035.WAV`, in each of the three voice archives) carry 470 bytes of data past the end of their
RIFF chunk — a property of the source files, identical across all three languages, not of the
container.

**`+5` is an MS-DOS timestamp, not a magic number.** Read as `[date:uint16][time:uint16]`, all
3,004 values decode to a valid calendar date and clock time, clustering in 1994 (1,145), 1995
(1,143) and 1996 (715). Read the other way round — time first — 2,515 of 2,578 are invalid, dating
files to 2041 and later. Files built in the same batch share near-identical stamps: `ROCKET.BND`,
`PSTATUS.BND`, `APPINPUT.BND` and `PMISSILE.BND` are all stamped 1996-01-27 15:23:2x.

**The trailer repeats the last content byte** — in all 2,578 entries checked, with no exception,
including the 1,617 whose last byte is nonzero. It sits outside the declared size, so nothing reads
it; the natural reading is an off-by-one in the retail packer's copy loop. `VolFileWriter`,
`VolFileCompiler` and `VolEntryPrefixCodec.Wrap` all reproduce it rather than inventing a value.

**Nothing here is a compression header.** The storage flag is 0x02 everywhere, and the RIFF check
above proves the content is stored verbatim.

## Loose files on disk carry no prefix

The retail install's own override tree is content-only. `DATA\MAT0.DAT` (244 bytes) and
`DATA\MFORMS.DAT` (142 bytes) are byte-identical to the content of the `dat\MAT0.DAT` and
`dat\MFORMS.DAT` entries in SIMVOL0.VOL, whose size fields read 244 and 142 — no prefix, no
trailer. So does `DATA\script.dat`, which `MissionLoader` reads straight off disk at offset 0.

What does carry a prefix is anything unpacked by a tool that copies the archive bytes wholesale.
`ES2/VOL/extractVol.py` slices each entry from its offset to the next entry's offset, so every file
under `ES2/VOL/simvol0/`, `ES2/VOL/ZONES/` and `ES2/VOL/SHELL0/` is `prefix + content + trailer`,
ten bytes longer than the file the game reads.

That extraction is uniform. Comparing all 1,672 entries of those three archives against their
extracted counterparts, byte for byte, against what `VolFileReader` returns: 1,672 are
`prefix + content + trailer`, none are content-only, none differ otherwise, none are missing. A
`ES2/VOL/<name>/` file is always ten bytes longer than its content, never sometimes.

`HercWorks.Vol.VolEntryPrefixCodec` is the one place that tells the two shapes apart, for editors
that open a path the user picked. It detects by the self-declared size field, the same signal the
reader uses.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The 4 bytes at `+5` are an opaque magic number | They are a packed MS-DOS date and time. `TransformerRegistry` matches Herc sim data on the literal value, which works only for unmodified retail archives. |
| The trailer is padding or alignment | The gap is exactly one byte for every entry regardless of size, and its value is the content's last byte, not zero. |
| The storage flag means "compressed" | Content is stored verbatim — 723 WAVs match their own RIFF length. |
