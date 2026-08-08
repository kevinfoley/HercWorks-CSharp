# Known issues carried over from the original Java source

This file tracks every place where the original `herc-works-mdk` Java code appears to have a bug
or rough edge. Entries marked **(fixed)** have been corrected — most of those were verified
against real retail ES2 game files (see the individual entries for which files and how), so they
carry real confidence rather than a guess. A handful of lower-confidence entries remain
intentionally unfixed — either because no real file exists to verify against, because the
"correct" direction genuinely can't be determined from the data alone, or because fixing a shared
method would ripple across multiple call sites that would each need separate re-verification; see
each entry's own reasoning for why it was left alone.

Each entry notes the file, what the issue is/was, and how confident the assessment is.

---

## Java source that wouldn't actually compile as written

### `DynamixBitmapArrayTransformer` / `DynamixBitmapTransformer` — calls to a nonexistent `setHeader`/`setFileSize`
**Files:** `src/HercWorks.Core/Io/Transform/Common/DynamixBitmapArrayTransformer.cs`,
`src/HercWorks.Core/Io/Transform/Common/DynamixBitmapTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.common.DynamixBitmapArrayTransformer`,
`org.hercworks.core.io.transform.common.DynamixBitmapTransformer`

The Java read paths call `dba.setHeader(...)` / `dbm.setHeader(...)` and
`dba.setFileSize(...)` / `dbm.setFileSize(...)`. `setFileSize` doesn't exist anywhere on
`DynamixBitmapArray`, `DynamixBitmap`, or their `DataFile` superclass under that name — but
`DataFile` does declare a plain `header`/`setHeader` **instance** field distinct from the
`public static Bytes header` constant separately declared on `DynamixBitmapArray`/`DynamixBitmap`
themselves (the constant holds the expected magic-byte value; the instance field holds whatever
was actually read). This port's `DataFile.Header` and `DataFile.FileSize` instance properties
cover both by name already — the only thing that didn't survive the translation was keeping the
per-class magic-byte constant as a *separate* identifier from the inherited instance property, so
it was originally named `Header` too, which C# doesn't allow (a static member can't share a name
with an inherited instance member and still be targetable from an object initializer — this
raised `CS1914`, an actual C# compile error, not present in the Java version since Java's
field-hiding rules allowed it). Renamed the per-class constants to `HeaderMagic` to keep both
identifiers distinct; the read/write logic itself is unchanged and still bug-for-bug with the
original (see the `FileSize` byte-order entry below, which still applies).

### `DebrisHercTransformer.ObjectToBytes()` — malformed setter call
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/DebrisHercTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.DebrisHercTransformer`

The write method calls `entry.setSpawnDebrisFlag()` with **no arguments**, in a position
symmetric with getter calls for every other field around it. There's no zero-argument overload of
that setter anywhere sensible in the data model, so this line looks like it wouldn't actually
compile as written — almost certainly a typo for `entry.getSpawnDebrisFlag()`. Unlike everything
else in this document, there's no bug to faithfully preserve here since broken code can't be
"ported literally" — this uses the getter, matching the read path and every other field in the
method.

---

## Confirmed functional bugs (would produce visibly wrong data)

### `FlightModelTransformer` — read/write asymmetry and a field misassignment (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/FlightModelTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.FlightModelTransformer`

Used to have two issues: (1) the read path set `RollForce` a second time instead of populating
`RollFriction`, and (2) the write path produced 47 bytes total against the read path's 54,
disagreeing about the file's zero-padding layout by 7 bytes. Fixed and verified against real
`RAZOR.FM`/`SKIMMER.FM` from a retail install (`ES2\VOL\simvol0\fm\`): their own declared
content-size field reads 54 bytes, and decoding the read path's field layout against the real
bytes confirms every `Skip()` region is genuine zero-padding, and that the "RollFriction" slot
holds a distinct value from `RollForce` in both files. Read now assigns into `RollFriction`; write
now pads with the same byte counts the read path skips (6/2/2/2/2 instead of 3/1/1/1/1), making
both paths symmetric at 54 bytes.

### `HercDamageFileTransformer` — fixed loop count, missing skimmer-shaped padding case, and CritChance scaling (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/HercDamageFileTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.HercDamageFileTransformer`

Three issues, all confirmed and fixed against real `.DMG` files from a retail install
(`ES2\VOL\simvol0\dmg\{SKIMMER,SPIDER,OUTLAW}.DMG`):
1. The component parse loop always ran exactly 29 times regardless of the actual
   `totalComponents` read. SPIDER.DMG/OUTLAW.DMG genuinely have 29 (so this happened to work for
   them), but SKIMMER.DMG has only 1 — hand-decoding confirmed the old fixed loop would write
   past the end of a 1-element array, an immediate crash on real data. Now loops
   `totalComponents` times.
2. A second, previously undocumented bug found while verifying #1: the internals-padding skip
   (`22 - internals.Length` shorts) is only correct for hercs storing all 22 internals slots
   (skip amount is 0 there, so the padding path was never actually exercised before). For
   SKIMMER.DMG's 1-internal record, unconditionally skipping `(22-1)*2 = 42` bytes overruns its
   18-byte content. Decoding confirmed the fix mirrors what the write path already does for a
   skimmer-shaped record (skip 0 padding) — now only applies the 22-slot padding skip when there
   are more than 1 internals.
3. The write path multiplied `CritChance` by 100 before writing; read assigned the raw value
   directly. Real files settle this: `CritChance` reads as exactly `20` for the large majority of
   components across all three files (matching this class's own "0x14 in every known example"
   doc comment on `HercSimDamage.InternalsTarget`), not `2000` — the write path's `* 100` was the
   actual bug. Now writes the raw value.

### `DatFileReader.ParseIniHercDatStats()` — hardpoints map never populated (fixed)
**File:** `src/HercWorks.Core/Io/Read/DatFileReader.cs`
**Java source:** `org.hercworks.core.io.read.DatFileReader`

Used to initialize `iniStats.Data.Hardpoints` as an empty dictionary, then loop through the file
bytes constructing a `UiWeaponEntry` for each hardpoint without ever inserting any of them into
the dictionary — every parsed entry was discarded, so the dictionary came back empty regardless
of how many hardpoints the file actually described. Fixed by adding
`iniStats.Data.Hardpoints[id] = hardpoint;` at the end of the loop body.

### `WeaponPDGTransformer.BytesToObject()` — never called `SetBytes`, so `Bytes` was left null/stale (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/WeaponPDGTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.WeaponPDGTransformer`

Every other transformer's read path calls `setBytes(inputArray)` right after the null/empty
check, which populates the `bytes` field the rest of the indexing methods read from. This one
reset `index = 0` but never called `setBytes(...)` at all — on a fresh transformer instance,
`Bytes` was `null` and the first `IndexIntLE()` call threw a `NullReferenceException`. Fixed by
adding the missing `SetBytes(inputArray)` call.

### `HMeter` constructor — origin parameter never used (fixed)
**File:** `src/HercWorks.Core/Data/File/Gau/HMeter.cs`
**Java source:** `org.hercworks.core.data.file.gau.HMeter`

Used to call `setOrigin(getOrigin())` instead of `setOrigin(origin)` — assigning the `Origin`
field to its own current (null/default) value rather than the constructor's `origin` parameter,
making the parameter effectively dead. Fixed to assign `Origin = origin`.

---

## Round-trip bugs (write and read paths disagree with each other)

### `PlayerSaveTransform` — write path completely ignored `PlayerSave.UnlockedHercs` (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Common/PlayerSaveTransform.cs`
**Java source:** `org.hercworks.core.io.transform.common.PlayerSaveTransform`

On read, the herc-unlock segment is parsed into `save.UnlockedHercs` — a `Dictionary<HercLUT,
short>` keyed by every `HercLUT` up to (not including) `Mongoose` (id 9), with each entry's
actual stored value preserved. The write path used to ignore that dictionary entirely and
instead iterate `HercLUT.Values()`, writing a hardcoded `1` for every herc with `Id <
Achilles.Id` (id 13) — a different range and a fabricated value with no connection to what was
read or edited. Fixed to mirror the read path exactly: same id range (`0` until
`HercLUT.Mongoose.Id`), reading each value from `save.UnlockedHercs` (defaulting to `0` if a key
is somehow missing) instead of hardcoding. Not verified against a real `.sav` file's exact unlock
values (several real `.sav` files exist under `ES2\SAV\`, but hand-decoding the full variable-length
`PlayerSave` layout just to reach this one segment wasn't done here) — but mirroring the read
path exactly is the only defensible fix regardless, since the old write path didn't even attempt
to reflect the in-memory object. With this fixed, `CampaignResourcesForm` could reasonably expose
herc-unlock editing now — it was deliberately left out only because of this bug.

### `DynamixPaletteTransformer` — green/blue channels swapped on write (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Common/DynamixPaletteTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.common.DynamixPaletteTransformer`

The read path (`ToColorBytes`) interprets a color's 4 bytes as R, G, B, A (byte0=R, byte1=G,
byte2=B). The write path (`ToDynamixColor`) used to output them as R, B, G (byte0=R, byte1=B,
byte2=G), so reading a palette, writing it back out, and reading it again would silently swap the
green and blue channels. Fixed write to match read's channel order.

### `DynamixBitmapArrayTransformer` — FileSize byte order inconsistent between read and write (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Common/DynamixBitmapArrayTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.common.DynamixBitmapArrayTransformer`

On read, `FileSize` is stored via `IndexSegmentLE` — which (see below) is actually a no-op alias
of `IndexSegment`, so the stored bytes are in raw on-disk order. On write, those same stored
bytes used to be explicitly reversed before being written out, flipping the byte order on a round
trip. Fixed write to write `FileSize` as stored (no reversal), matching read.

### `ArmHercTransformer.WriteUiImage()` — outline coordinates lost, origin written twice instead (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Shell/ArmHercTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.shell.ArmHercTransformer`

The top/bottom herc images (`HercTopImg`/`HercBotImg`) are actually `UiHardpointGraphic`
instances with real `OutlineX`/`OutlineY` data populated on read. But the private helper that
serializes them (`WriteUiImage`) took a `UiImageDBA`-typed parameter — the *base* class, which
only exposes `OriginX`/`OriginY` — so it wrote `OriginX, OriginY, OriginX, OriginY`, silently
discarding the outline values and duplicating the origin instead. The Java original had the same
restriction via its own static typing. Fixed in C# via a runtime type check
(`img is UiHardpointGraphic`) inside `WriteUiImage`, since the field's declared type
(`ArmHerc.HercTopImg`/`HercBotImg`, both `UiImageDBA?`) wasn't changed — this class always
constructs `UiHardpointGraphic` instances for these fields in practice, so the check reliably
finds the real outline data and writes it.

### `HercInfoTransformer` — `TotalHercs` and `HercId` read little-endian, written big-endian (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Shell/HercInfoTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.shell.HercInfoTransformer`

Every field in this transformer except two read with `IndexShortLE`/wrote with `WriteShortLE`
consistently. The header `TotalHercs` count and each entry's `HercId` were the exceptions: both
were read with `IndexShortLE` (little-endian) but written back out with `WriteShort`
(big-endian). Fixed write to use `WriteShortLE` for both, matching read — the read side is
already confirmed correct in practice (the WinForms Herc Stats editor uses it successfully
against real retail `HERC_INF.DAT` data).

### `TSShape.JsonString()` — printed the same list twice (fixed)
**File:** `src/HercWorks.Core/Data/File/Dts/TSShape.cs`
**Java source:** `org.hercworks.core.data.file.dts.TSShape`

The debug/JSON-string output used to print `SequenceList` under both the `"sequences"` and
`"transforms"` keys — a copy/paste error where the second one should have been `TransformList`.
Only affected the human-readable `ToString()` output, not the underlying data or any read/write
logic. Fixed to print `TransformList` under `"transforms"`.

---

## Methods that don't do what their name claims (byte-order / `Bytes`-library confusion)

These all trace back to the same root cause: the original Java code used the
`at.favre.lib.bytes` library, and in several places the author called `.byteOrder(...)` expecting
it to physically reorder bytes — but per the library's actual (verified-from-source) behavior,
`.byteOrder(...)` only changes how later `.toInt()/.toShort()/.toChar()` calls **interpret**
already-stored bytes; it does **not** touch the physical array. Only `.reverse()` (or
constructing a `Bytes` from a primitive fresh) actually reorders bytes. Where the original called
`.byteOrder(...)` and then `.array()` (instead of `.reverse()`), the byte-order tag silently had
no effect — despite the method name.

### `ByteOps.Bytes2LEToInt()` (fixed)
**File:** `src/HercWorks.Core/Util/ByteOps.cs`

Used to read the first two bytes of the input as big-endian (`(b[0] << 8) | b[1]`) despite the
name. Confirmed unused anywhere in this codebase before fixing, so changed to a genuine
little-endian read with no risk to existing behavior.

### `ByteOps.ShortLEToByteArr()` (fixed)
**File:** `src/HercWorks.Core/Util/ByteOps.cs`

Used to write the given `short` in big-endian order into the destination array despite the name.
Its only caller (`UiWeaponEntry.ToByte()`) has no callers of its own anywhere in this codebase, so
confirmed safe to fix to a genuine little-endian write.

### `ThreeSpaceByteTransformer.IndexSegmentLE()` — left as-is (see reasoning)
**File:** `src/HercWorks.Core/Io/Transform/ThreeSpaceByteTransformer.cs`

Byte-identical to `IndexSegment()` — the "LE" in the name has no effect. Any transformer that
calls this method for a multi-byte field is reading raw on-disk byte order, not
little-endian-corrected order. Unlike the two `ByteOps` methods above, this one has multiple real
call sites that already depend on its current (no-op) behavior — `DynamixBitmapArrayTransformer.FileSize`
and `DynamixPaletteTransformer`'s per-color reads both got their *write* sides fixed to match this
method's existing read behavior (see those entries above) rather than the other way around, to
avoid a change here rippling across every caller at once. Left unchanged; fixing it for real would
require re-auditing every call site's paired write logic together, not in isolation.

### `ThreeSpaceByteTransformer.PeekAt()` — left as-is (see reasoning)
**File:** `src/HercWorks.Core/Io/Transform/ThreeSpaceByteTransformer.cs`

Doesn't read or dereference anything — just returns `index + at` as a plain integer offset.
Looks unused or unfinished in the original; no callers were found anywhere in the ported code, and
with zero callers there's no way to infer what its intended correct behavior should have been.
Left unchanged rather than guessing at a "fix" for genuinely unfinished code.

### `DTSBoneFlags` — flag value never actually assigned (fixed)
**File:** `src/HercWorks.Core/Data/Struct/Herc/DTSBoneFlags.cs`
**Java source:** `org.hercworks.core.data.struct.herc.DTSBoneFlags`

The constructor took a `flagNum` parameter but never assigned it to the instance's `flag` field,
so `Flag()` returned `0` for every enum value regardless of which constant you accessed. Confirmed
unused anywhere else in this codebase before fixing, so changed the constructor to assign
`_flag = flagNum` with no risk to existing behavior.

---

## Suspicious-looking but probably intentional (or unverified) — lower confidence

### `HercsStartTransformer` — write path assumes hardpoint IDs are contiguous and zero-based
**File:** `src/HercWorks.Core/Io/Transform/Shell/HercsStartTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.shell.HercsStartTransformer`

On read, each herc's hardpoints are stored in a map keyed by the actual hardpoint ID read from
the file (`hardpointId`), which is not assumed to be sequential. On write, the loop instead does
`hardpoints.get(h)` for `h` from `0` to `count-1` — i.e. it looks up by loop index, not by any
ID actually present in the map. If a real file's hardpoint IDs are ever non-contiguous or don't
start at 0, this silently fetches the wrong entry (or a missing one, which is null in Java and
would NPE on the next line; the C# port uses `GetValueOrDefault` to reproduce that same
null-then-crash behavior rather than the different `KeyNotFoundException` a plain indexer would
throw). Not confirmed against real game data — hardpoint IDs may genuinely always be 0-based and
contiguous, in which case this is harmless — but it's a real assumption baked into the write path
that the read path doesn't share.

### `DynamixPalette.Header` — "hex" string that wasn't actually hex (fixed, verified against real data)
**File:** `src/HercWorks.Core/Data/File/Dyn/DynamixPalette.cs`

Used to build this header constant from `Encoding.UTF8.GetBytes("0F002800")` — the literal
8-byte ASCII encoding of that string, not a hex-decoded 4-byte value, despite looking like hex.
Checked against a real `.DPL` file (`ES2\VOL\SHELL0\DPL\ALPHA.DPL`): its actual first 4 content
bytes are `0F 00 28 00` — the genuine hex-decoded value. Fixed `Header` to the real 4-byte value;
this is also consistent with `DynamixPaletteTransformer.BytesToObject`'s read path only ever
having skipped 4 bytes for this header, not the 8 the old (wrong) ASCII encoding would need.

### `InitHerc.Header` — same "hex string" pattern, checked but left as-is (genuinely unused)
**File:** `src/HercWorks.Core/Data/File/Dat/Shell/InitHerc.cs`

Same `Encoding.UTF8.GetBytes("661FAF55")`-style construction as `DynamixPalette.Header` above,
but checked against a real `INI_[herc].DAT` file (`ES2\VOL\SHELL0\GAM\INI_OUTL.DAT`) and found
this constant has **no corresponding bytes in the real file at all** — `InitHercTransformer`
never reads, skips, or writes a header; `HercId` is the very first field at content offset 0 in
the real file (confirmed: reads as `0`, matching Outlaw's id). So `InitHerc.Header` is genuinely
dead/unused, with no real on-disk bytes to verify a "correct" value against — left unchanged
rather than guessing.

### `HercDataRef` — `.toInt()` called on a 2-byte value
**File:** `src/HercWorks.Core/Data/Ref/Constants/HercDataRef.cs`
**Java source:** `org.hercworks.core.data.ref.constants.HercDataRef`

The original calls `.toInt()` on a 2-byte (not 4-byte) `Bytes` value — that method's documented
behavior is for 4-byte values. This port uses a big-endian read of the 2 bytes, consistent with
the library's confirmed default behavior elsewhere, but this specific case (`.toInt()` on a
short array) wasn't verified against the library source the way the others in this list were.
Treat as an educated guess, not a confirmed reading.

---

## C#-port-only defects (no Java equivalent — introduced during earlier porting work)

These aren't carried over from Java; they're mistakes introduced while porting to C# that
happened to not be caught until the `io/transform/dbsim` and `io/transform/shell` packages were
finished and the solution was built end-to-end for the first time. Fixed as straightforward
compile/logic corrections rather than preserved.

### `Voln.ExeUse` property collided with the nested `Voln.ExeUse` enum type
**File:** `src/HercWorks.Vol/Voln.cs`

Java's `Voln` has an instance field `exeUse` (lowercase) of type `ExeUse` (the enum) — legal in
Java's case-sensitive naming. The port capitalized both to PascalCase, producing
`public ExeUse ExeUse { get; set; }` in the same class as `public enum ExeUse`, which C#
disallows (a member and a nested type can't share an identifier) — `CS0102`. This was a hard
compile error blocking the entire solution from building, not something that could be "ported
literally." Renamed the property to `ExeType` (matching one of the two redundant Java
accessor-name pairs, `getExeType`/`setExeType`, that both wrapped the same underlying field).

### `DebrisHercTransformer` referenced a misspelled property name
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/DebrisHercTransformer.cs`

Referenced `entry.Unk1_val` on both the read and write paths, but the data model
(`DebrisHerc.Entry`) declares the property as `Unk1Val` (no underscore) — a naming mismatch
between the transformer and its data class, not present in the Java source (Java's `unk1Val`
field and `getUnk1Val()`/`setUnk1Val()` accessors are internally consistent). Fixed by correcting
both call sites to `Unk1Val`.

### `UnkEntity164Bytes.ToString()` — ambiguous `string.Join` overload on a reference-type array
**File:** `src/HercWorks.Core/Data/File/Msn/UnkEntity164Bytes.cs`

`string.Join(", ", MapEntities)` (where `MapEntities` is `MapObject[]`) is ambiguous between
`string.Join(string?, object?[])` and `string.Join<T>(string?, IEnumerable<T>)` — both apply
equally well to a reference-type array, which the C# compiler can't resolve without help
(`CS0121`). The other two `string.Join` calls in the same method don't hit this, since their
arrays (`short[]`) are value-type arrays where only the generic overload applies. Fixed by
explicitly casting to `(object[])MapEntities` at the call site; output is unchanged.

## Not bugs, but rough edges worth knowing about

### `VolFileCompiler.Compile()` — hardcoded developer path
**File:** `src/HercWorks.Core/Io/Write/VolFileCompiler.cs`

Writes its output to `E:\ES2_OS\dev\earthsiege2\VOL` — the original author's own machine path,
carried over directly from the Java source. This isn't a logic bug, just a leftover that won't
work as-is on anyone else's machine. Whatever calls `Compile()` from the eventual UI should pass
a real destination instead.

### Duplicate reader/writer classes
**Files:** `src/HercWorks.Core/Io/Read/VolFileReader.cs`, `src/HercWorks.Core/Io/Write/VolFileWriter.cs`

The Java project had two separate implementations of VOL reading and writing —
`org.hercworks.voln.io.*` (now `HercWorks.Vol.Io.*`) and `org.hercworks.core.io.*` — with
near-identical logic. Looks like a leftover from before ES2Vol was split into its own module.
These two C# classes are thin wrappers that delegate to the `HercWorks.Vol` versions rather than
duplicating the same logic twice.

### `DatFileReader.ReplaceDatBytes()` — unused parameter
**File:** `src/HercWorks.Core/Io/Read/DatFileReader.cs`

Despite its name and the `newData` parameter, this method never actually uses `newData` — it just
concatenates the target file's existing `Header` and `RawBytes` unchanged.

### `VolFileWriter.PackKnownVolFiles()` — trailing marker byte previously written as a hardcoded zero (fixed)
**File:** `src/HercWorks.Vol/Io/VolFileWriter.cs`

Used to write a literal `0x00` for each entry's trailing marker byte (sized off
`entry.UnknownEoFByte.Length`) rather than the actual byte value `VolFileReader` captured into
`UnknownEoFByte` when the VOL was first read — inconsistent with `WriteVolAssetFile()`'s
single-file unpack path, which already wrote the real captured value. Fixed to write
`entry.UnknownEoFByte` directly, same as `WriteVolAssetFile()`.
