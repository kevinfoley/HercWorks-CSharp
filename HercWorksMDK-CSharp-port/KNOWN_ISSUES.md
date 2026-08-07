# Known issues carried over from the original Java source

This file tracks every place where the original `herc-works-mdk` Java code appears to have a bug
or rough edge. Per the porting approach used throughout, these were **ported literally
(bug-for-bug)** rather than silently fixed, since:
- there's no real ES2 game data available in this environment to verify what "correct" behavior
  should actually be, and
- some other part of the original codebase (or a downstream consumer) might already compensate
  for the quirk, so "fixing" it here could break compatibility with data produced by the
  original tool.

Each entry notes the file, what the issue is, and how confident the assessment is. If you have
real game files to test against, these are the first places to check when something looks wrong.

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

### `FlightModelTransformer` — read and write are not symmetric, and one field is misassigned
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/FlightModelTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.FlightModelTransformer`

Two separate issues in the same class:
1. The read path sets `RollForce` a second time (overwriting its earlier value) in the slot
   where the write path uses `RollFriction` — so `RollFriction` is never actually populated when
   reading a file, and the intended second `RollForce`-adjacent field is lost.
2. Tallying every field by hand, the write path produces **47 bytes** total but the read path
   consumes **54 bytes** — the `Skip()` amounts between fields on read don't match the zero-byte
   padding actually written. Read and write disagree about the file's layout by 7 bytes. This is
   a real inconsistency in the original Java, not something introduced by porting it — there's no
   real `.DAT` flight-model file available here to determine which side (if either) reflects the
   true on-disk format, so both methods are ported exactly as written rather than guessing at a
   fix. This is the one most worth testing first if you have real game files.

### `HercDamageFileTransformer` — fixed loop count ignores the read component total; CritChance scaling mismatch
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/HercDamageFileTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.HercDamageFileTransformer`

Two separate issues:
1. `ComponentData` is allocated with length `totalComponents` (read from the file), but the
   parse loop always runs exactly 29 times regardless of that value. If a real file reports fewer
   than 29 components, this throws an index-out-of-range exception.
2. The write path multiplies each `CritChance` by 100 before writing it out; the read path
   assigns the raw value directly with no corresponding division. Reading a file then writing it
   back out would inflate every `CritChance` value by 100x.

### `DatFileReader.ParseIniHercDatStats()` — hardpoints map never populated
**File:** `src/HercWorks.Core/Io/Read/DatFileReader.cs`
**Java source:** `org.hercworks.core.io.read.DatFileReader`

Initializes `iniStats.Data.Hardpoints` as an empty dictionary, then loops through the file bytes
constructing a `UiWeaponEntry` for each hardpoint — but never inserts any of them into the
dictionary. Every parsed entry is discarded. The dictionary comes back empty after parsing
completes, regardless of how many hardpoints the file actually describes.

This is the one bug in this list most likely to cause visible, immediate problems if exercised
on real data (as opposed to the round-trip/write-path bugs below, which only surface on
write-then-read-back).

### `WeaponPDGTransformer.BytesToObject()` — never calls `SetBytes`, so `Bytes` is left null/stale
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/WeaponPDGTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.dbsim.WeaponPDGTransformer`

Every other transformer's read path calls `setBytes(inputArray)` right after the null/empty check,
which populates the `bytes` field the rest of the indexing methods read from. This one resets
`index = 0` but never calls `setBytes(...)` at all. On a fresh transformer instance this means
`Bytes` is `null` and the first `IndexIntLE()` call throws a `NullReferenceException`
(`NullPointerException` in the original Java). If the same transformer instance were ever reused
across multiple files, it would silently read from whatever the *previous* call's byte array was
instead of the new `inputArray`. Ported literally (bug-for-bug) — the fix would be adding a
`SetBytes(inputArray)` call where `Index = 0` currently stands alone.

### `HMeter` constructor — origin parameter never used
**File:** `src/HercWorks.Core/Data/File/Gau/HMeter.cs`
**Java source:** `org.hercworks.core.data.file.gau.HMeter`

The constructor calls `setOrigin(getOrigin())` instead of `setOrigin(origin)` — it assigns the
`Origin` field to its own current (null/default) value rather than the constructor's `origin`
parameter. The parameter is effectively dead; `Origin` is never actually set by this constructor.

---

## Round-trip bugs (write and read paths disagree with each other)

### `DynamixPaletteTransformer` — green/blue channels swapped on write
**File:** `src/HercWorks.Core/Io/Transform/Common/DynamixPaletteTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.common.DynamixPaletteTransformer`

The read path (`ToColorBytes`) interprets a color's 4 bytes as R, G, B, A (byte0=R, byte1=G,
byte2=B). The write path (`ToDynamixColor`) outputs them as R, B, G (byte0=R, byte1=B, byte2=G).
Reading a palette, writing it back out, and reading it again would silently swap the green and
blue channels.

### `DynamixBitmapArrayTransformer` — FileSize byte order inconsistent between read and write
**File:** `src/HercWorks.Core/Io/Transform/Common/DynamixBitmapArrayTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.common.DynamixBitmapArrayTransformer`

On read, `FileSize` is stored via `IndexSegmentLE` — which (see below) is actually a no-op alias
of `IndexSegment`, so the stored bytes are in raw on-disk order. On write, those same stored
bytes are explicitly reversed before being written out. A round trip would flip the byte order of
this field.

### `ArmHercTransformer.WriteUiImage()` — outline coordinates lost, origin written twice instead
**File:** `src/HercWorks.Core/Io/Transform/Shell/ArmHercTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.shell.ArmHercTransformer`

The top/bottom herc images (`HercTopImg`/`HercBotImg`) are actually `UiHardpointGraphic`
instances with real `OutlineX`/`OutlineY` data populated on read. But the private helper that
serializes them (`uiImageToByte` in Java, `WriteUiImage` here) takes a `UiImageDBA`-typed
parameter — the *base* class, which only exposes `OriginX`/`OriginY`. Since the read path
consumes 4 coordinate values per image (origin X/Y, then outline X/Y) but the write path can only
see origin, it writes `OriginX, OriginY, OriginX, OriginY` — silently discarding the outline
values and duplicating the origin instead. A round trip loses the outline coordinates for the
top/bottom herc panel images (the per-weapon hardpoint graphics later in the same file are
unaffected — those go through a separate helper, `uiHardpointToBytes`/`WriteUiHardpoint`, that is
correctly typed as `UiHardpointGraphic` and writes the real outline values). Ported literally
(bug-for-bug); C#'s static typing reproduces the same restriction the original Java had.

### `HercInfoTransformer` — `TotalHercs` and `HercId` read little-endian, written big-endian
**File:** `src/HercWorks.Core/Io/Transform/Shell/HercInfoTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.shell.HercInfoTransformer`

Every field in this transformer except two reads with `IndexShortLE`/writes with `WriteShortLE`
consistently. The header `TotalHercs` count and each entry's `HercId` are the exceptions: both are
read with `IndexShortLE` (little-endian) but written back out with `WriteShort` (big-endian). A
round trip would byte-swap these two fields while leaving the rest of the file's shorts
untouched. Ported literally (bug-for-bug).

### `TSShape.JsonString()` — prints the same list twice
**File:** `src/HercWorks.Core/Data/File/Dts/TSShape.cs`
**Java source:** `org.hercworks.core.data.file.dts.TSShape`

The debug/JSON-string output prints `SequenceList` under both the `"sequences"` and
`"transforms"` keys — looks like a copy/paste error where the second one should have been
`TransformList`. This only affects the human-readable `ToString()` output, not the underlying
data or any read/write logic, so it's cosmetic rather than a data-integrity issue.

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

### `ByteOps.Bytes2LEToInt()`
**File:** `src/HercWorks.Core/Util/ByteOps.cs`

Despite the name, reads the first two bytes of the input as big-endian (`(b[0] << 8) | b[1]`).
If fed genuine little-endian on-disk bytes, this produces a byte-swapped value.

### `ByteOps.ShortLEToByteArr()`
**File:** `src/HercWorks.Core/Util/ByteOps.cs`

Despite the name, writes the given `short` in big-endian order into the destination array.

### `ThreeSpaceByteTransformer.IndexSegmentLE()`
**File:** `src/HercWorks.Core/Io/Transform/ThreeSpaceByteTransformer.cs`

Byte-identical to `IndexSegment()` — the "LE" in the name has no effect. Any transformer that
calls this method for a multi-byte field is reading raw on-disk byte order, not
little-endian-corrected order. (Several transformers do call this — e.g. `DynamixBitmapArrayTransformer.FileSize`, `DynamixPaletteTransformer`'s per-color 4-byte reads. Those individual
call sites are working as originally written; this entry just documents *why* the method itself
doesn't do what its name says.)

### `ThreeSpaceByteTransformer.PeekAt()`
**File:** `src/HercWorks.Core/Io/Transform/ThreeSpaceByteTransformer.cs`

Doesn't read or dereference anything — just returns `index + at` as a plain integer offset.
Looks unused or unfinished in the original; no callers were found elsewhere in the ported code.

### `DTSBoneFlags` — flag value never actually assigned
**File:** `src/HercWorks.Core/Data/Struct/Herc/DTSBoneFlags.cs`
**Java source:** `org.hercworks.core.data.struct.herc.DTSBoneFlags`

The constructor takes a `flagNum` parameter but never assigns it to the instance's `flag` field.
`Flag()` returns `0` for every enum value, regardless of which constant you access.

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

### `InitHerc.Header` / `DynamixPalette.Header` — "hex" strings that aren't actually hex
**Files:** `src/HercWorks.Core/Data/File/Dat/Shell/InitHerc.cs`,
`src/HercWorks.Core/Data/File/Dyn/DynamixPalette.cs`

Both build a header byte constant from a string that reads like hex (e.g. `"661FAF55"`,
`"0F002800"`) via `Encoding.UTF8.GetBytes(...)` — i.e. the literal ASCII bytes of those 8
characters, not a hex-decoded 4-byte value. This might be intentional (a literal magic-byte
signature that happens to look hex-like), or might be a case where the author meant to hex-decode
and didn't. Ported literally either way since both are plausible and there's no way to tell
without a real file exhibiting the actual on-disk header bytes to compare against.

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
