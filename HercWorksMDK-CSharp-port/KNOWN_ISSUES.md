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

### `ThreeSpaceByteTransformer.SetBytes()` — never reset `Index`, corrupting any transformer instance reused for a second parse (fixed)
**File:** `src/HercWorks.Core/Io/Transform/ThreeSpaceByteTransformer.cs`
**Java source:** `org.hercworks.core.io.transform.ThreeSpaceByteTransformer`

`SetBytes()`/`setBytes()` only assigned the new byte array — it never reset the cursor (`Index`/
`index`) back to 0. A transformer's *first* `BytesToObject()` call on a fresh instance works fine
(the field defaults to 0), but every call after that on the *same instance* starts reading the new
array from wherever the previous parse's cursor stopped, silently misreading the header of an
unrelated file. Some individual transformers (e.g. `WeaponPDGTransformer`, see above) had already
worked around this by manually resetting `index = 0` themselves, which is exactly the kind of
per-call-site inconsistency that signals a base-class gap rather than an intentional design.

Found while building the new DTS 3D model viewer (`HercWorks.UI`), whose `Model3DViewerForm` holds
one long-lived `DTSModelTransformer` field reused across every "Open DTS" click — confirmed with a
throwaway batch probe against all 58 real `.DTS` files in `ES2\VOL\simvol0\dts\`: the first file
parsed correctly (7 meshes, 2064 triangles for `SAMSON.DTS`), and every subsequent file in the same
transformer instance failed, all with the identical garbage byte offset left over from the previous
file's cursor position. Fixed by resetting `Index = 0` inside `SetBytes()` itself, so the reset
can't be forgotten per call site — this benefits every transformer in the codebase that might ever
be reused for more than one parse, not just DTS.

### `HMeter` constructor — origin parameter never used (fixed)
**File:** `src/HercWorks.Core/Data/File/Gau/HMeter.cs`
**Java source:** `org.hercworks.core.data.file.gau.HMeter`

Used to call `setOrigin(getOrigin())` instead of `setOrigin(origin)` — assigning the `Origin`
field to its own current (null/default) value rather than the constructor's `origin` parameter,
making the parameter effectively dead. Fixed to assign `Origin = origin`.

---

## Round-trip bugs (write and read paths disagree with each other)

### `PlayerSaveTransform` — write path completely ignored `PlayerSave.UnlockedHercs` (fixed, now verified)
**File:** `src/HercWorks.Core/Io/Transform/Common/PlayerSaveTransform.cs`
**Java source:** `org.hercworks.core.io.transform.common.PlayerSaveTransform`

On read, the herc-unlock segment is parsed into `save.UnlockedHercs` — a `Dictionary<HercLUT,
short>` keyed by every `HercLUT` up to (not including) `Mongoose` (id 9), with each entry's
actual stored value preserved. The write path used to ignore that dictionary entirely and
instead iterate `HercLUT.Values()`, writing a hardcoded `1` for every herc with `Id <
Achilles.Id` (id 13) — a different range and a fabricated value with no connection to what was
read or edited. Fixed to mirror the read path exactly: same id range (`0` until
`HercLUT.Mongoose.Id`), reading each value from `save.UnlockedHercs` (defaulting to `0` if a key
is somehow missing) instead of hardcoding.

**Update (2026-08-10):** now verified against all 9 real `.sav` files in `ES2\SAV\` (`GAME_0`
through `GAME_6`, `GAME_R`, `GAME_T`) via a throwaway console probe (read → `ObjectToBytes` →
`SequenceEqual` against the original bytes). All 9 round-trip byte-exact, confirming this fix is
correct — see the two related bugs below, found by the same probe, that had to be fixed first
before any file could round-trip at all. With this verified, `CampaignResourcesForm` now exposes
herc-unlock editing.

### `PlayerSaveTransform.ObjectToBytes` — herc bay and weapon-socket writes assumed contiguous dictionary keys (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Common/PlayerSaveTransform.cs`
**Java source:** `org.hercworks.core.io.transform.common.PlayerSaveTransform`

Found while verifying the `UnlockedHercs` fix above against real save files (2/9 crashed with
`KeyNotFoundException`) — two separate instances of the same mistake, both in `WriteHercEntry`'s
call sites:

1. The herc bay loop wrote `save.HercBay[h]` for `h` in `0..HercBay.Count-1`, assuming bay ids
   are a contiguous `0..Count-1` range. Real saves have sparse bay ids — `GAME_2.SAV` has no bay
   id `2`, `GAME_5.SAV` has no bay id `7` — so this threw on any save where a bay slot was ever
   vacated/skipped. The read path never made this assumption; it reads an explicit `bayId` per
   entry and keys the dictionary by that.
2. The per-herc weapon-socket loop (inside `WriteHercEntry`) had the identical bug one level
   deeper: `herc.Weapons[w]` for `w` in `0..ActiveSockets-1`, assuming socket ids are contiguous
   from 0, when the read path stores each weapon under its own explicit `socketId`.

Both fixed by iterating the dictionary directly (`foreach (var kv in ...)`) and writing each
entry's actual key, instead of reconstructing a key from a loop counter. Verified: all 9 real
`.sav` files (including `GAME_2`/`GAME_5`, the two that previously crashed) now round-trip
byte-exact.

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

### `GauFileTransformer.ObjectToBytes()` — unimplemented, always returned `null` (fixed)
**File:** `src/HercWorks.Core/Io/Transform/Dbsim/GauFileTransformer.cs`
**Java source:** none — no Java equivalent existed; this transformer was written from scratch
against the Java `GAUFile.java` data model's own (previously unimplemented) doc-comment layout.

`BytesToObject` was implemented and verified against real retail `.GAU` data in an earlier
session, but `ObjectToBytes` was a stub that unconditionally returned `null`, on the reasoning
that the file's tail (`GAUFile.Remainder`, offset 628 onward) is undecoded so a round trip
couldn't be achieved. That reasoning didn't hold up: `Remainder` is captured and preserved as raw
bytes on read, so it can simply be written back verbatim — nothing about it needs to be decoded to
round-trip it. Implemented `ObjectToBytes` to reconstruct the confirmed offset 0-627 region
(HUD origin/size, weapon list total, the 10 weapon-slot rects, the confirmed always-zero padding
regions, the Chain/Link/Auto-track buttons, and the Energy meter) and append `Remainder` as-is.
Verified byte-exact (`SequenceEqual`) round trip against all 9 real `(herc).GAU` files in
`ES2\VOL\simvol0\gau\`, including the loose-file VOL-entry prefix wrap via
`VolEntryPrefixCodec`. Registered in `TransformerRegistry` for `FileType.Gau`.

**Follow-up, same day:** decoded 64 more bytes of what was `Remainder` (offset 628-691) as a new
`HShieldDisplay` field, with help from a user working interactively from a real screenshot and
real `.HB0` cockpit-texture renders (see `HShieldDisplay.cs`'s own doc comment for the full method
and slot breakdown). `Remainder` now starts at offset 692 (1008 bytes, down from 1072) instead of
628. Round-trip re-verified byte-exact against all 9 files after the change.

**Second follow-up, same day:** decoded another 48 bytes (offset 1016-1063) as a new `HThrottle`
field, same method (user screenshot measurement matched against real bytes, confirmed by
overlaying the candidate track/points on real `.HB0` cockpit art — see `HThrottle.cs`'s own doc
comment). Unlike the shield display, the throttle's track uses the file's normal
X1,Y1,X2,Y2 rect convention with no field-order surprises. Since this widget sits in the middle of
what was `Remainder` rather than at its start, the undecoded bytes are now split into
`GAUFile.RemainderBeforeThrottle` (offset 692-1015, 324 bytes — a confirmed zero gap plus a
still-undecoded ~64-byte live region) and `GAUFile.Remainder` (offset 1064 onward, 636 bytes).
Round-trip re-verified byte-exact against all 9 files after the change.

**Third follow-up, same day:** decoded the ~64-byte live region flagged above as a new `HMfdPanel`
field (offset 952-967, a single normal X1,Y1,X2,Y2 rect — the Multi-Function Display screen bounding
box), same method again (user screenshot measurement, confirmed by overlaying the candidate rect on
real `.HB0` cockpit art — it lands exactly on the console's central screen bezel). Checking the
original Java `GAUFile.java` doc comment afterward (should have been checked first) found it had
already named this exact offset (`"952- PANEL\MFD"`) and `HThrottle`'s exact offset
(`"1016- SLIDER\THROTTLE\"`) — never implemented or verified, but both offsets held up precisely.
That same Java comment names further unverified leads worth checking before guessing blind:
`"1064- SLIDER\THROTTLE\SLIDE_DIR"` (exactly where `Remainder` now starts), `"1088- PANEL\NAVBAR"`,
`"1104- INDICATOR\TORSO_TWIST"`, `"1136- RETICLE"`. `GAUFile.RemainderBeforeThrottle` is now split
further into `RemainderBeforeMfdPanel` (offset 692-951, 260 bytes — confirmed all-zero except one
still-unexplained leftover byte at offset 692) + `MfdPanel` + a confirmed-zero 48-byte gap (offset
968-1015) + `Throttle` (unchanged). Round-trip re-verified byte-exact against all 9 files; MFD panel
size is a consistent 115x60 across every herc, only position varies.

**Fourth follow-up, same day:** decoded offset 1136 as a new `HReticle` field (a single (X,Y) point,
not a rect — the only widget in this file stored that way). Same method: user description
("horizontally centered, a bit above vertical center") matched real bytes (X constant 160 = exact
screen horizontal center across all 9 files; Y 95-115 for 8 of 9, RAZOR the usual exception at 146),
confirmed decisively by rendering the point over real `APOCA.HB0` — lands exactly centered in the
transparent viewport gap between the cockpit struts. Matches the Java doc's exact named offset
(`"1136- RETICLE"`) again. `GAUFile.Remainder` is now split further into `RemainderBeforeReticle`
(offset 1064-1135, 72 bytes) + `Reticle` + `Remainder` (offset 1144 onward, 556 bytes). Round-trip
re-verified byte-exact against all 9 files.

Also investigated the Java doc's `"1104- INDICATOR\TORSO_TWIST"` per a user description (~92x6,
horizontally centered near the top of the screen) — **not found**. An exhaustive search (every
4-int-window starting position across the *entire* remaining undecoded region, every one of the 24
possible field orderings per window, allowing one herc to fail) for a rect matching that shape
found zero matches. Left undecoded; noted in `GAUFile.cs`'s doc comment as a confirmed negative
result rather than an unexplored gap, so a future session doesn't repeat the same search.

**Fifth follow-up (2026-08-10) — `"1104- INDICATOR\TORSO_TWIST"` resolved, this time via Ghidra
disassembly of DBSIM.EXE instead of black-box byte search.** The earlier byte-search dead end
turned out to be a tolerance problem, not a "doesn't exist" problem: the real widget is a 120x17
rect, well outside the ~92x6 shape the exhaustive search was tuned around. Found by decompiling
DBSIM.EXE's `.GAU` loader (`FUN_00431778` in the project's Ghidra database — allocates exactly
0x6a4=1700 bytes, matching the file's own confirmed content size, and its first placement-new call
matches the already-confirmed 10-slot weapon list at offset 20 exactly, confirming it's the right
function) and its caller (`FUN_00431bf8`, which constructs 7 named panel-level widgets from fixed
struct offsets: 468, 548, 616, 728, 1000, 1088, 1212). The offset-1088 widget's own constructor
(`FUN_0043c7d8`) turned out to be a large composite "roving gunsight" HUD-overlay container — not
navbar as the Java doc guessed for that offset — whose first child rect (read at offset 1104) is
passed to a sub-gadget constructor that loads a bitmap resource literally named `"hudhtick"` ("HUD
H[orizontal]-tick"), i.e. a tick-mark graphic consistent with a torso-twist deviation gauge.
Verified against all 9 real files: X1=100/X2=220 (width 120) constant in every file, centering the
widget exactly on the HUD's horizontal center (160); height (Y2-Y1) is exactly 17px in all 9 files
including RAZOR, even though RAZOR's Y-position is a clear outlier (matching its already-documented
divergent HUD layout elsewhere in this file). Implemented as new `HTorsoTwist` (a plain
X1,Y1,X2,Y2 rect, same pattern as `HMfdPanel`) — see its own doc comment for the full method.
`GAUFile.RemainderBeforeReticle` (1064-1135, 72 bytes) is now split into `RemainderBeforeTorsoTwist`
(1064-1103, 40 bytes — partially understood: offset 1064 is confirmed as `HThrottle`'s own
"slide direction" mode flag, matching the Java doc's `"1064- SLIDER\THROTTLE\SLIDE_DIR"` guess, and
1088-1103 is the gunsight-complex's own bounding rect, mostly constant across hercs) + `TorsoTwist`
+ a shrunk `RemainderBeforeReticle` (1120-1135, 16 bytes — still undecoded, likely another gunsight
sub-gadget per the HUDLockingGunsight/HUDPipper/HUDCrosshairGunsight class names found in DBSIM's
strings). Round-trip re-verified byte-exact against all 9 files.

NAVBAR (Java doc's `"1088- PANEL\NAVBAR"`) remains unresolved — that offset turned out to be the
gunsight complex's container rect, not a navbar/compass widget, so the Java doc's label for that
specific offset was wrong even though its offset-guessing accuracy held up everywhere else in this
file. The `HudScreenSize` (320,400) vs. user-reported real coordinate space (320,240) question also
remains unresolved this session — no new evidence either way turned up in the disassembly.

**Sixth follow-up, same day — finished mapping `.GAU`'s structure; NAVBAR confirmed as a genuine
dead end, not just unverified.** Continued the same Ghidra-disassembly approach to close out the
remaining undecoded regions:

- The 16-byte gap at offset 1120-1135 (left over after `TorsoTwist` was carved out of the old
  `RemainderBeforeReticle`) turned out to be two (X,Y) anchor points for a target-speed text readout
  (a literal `"000 K/H"` format string sits next to the code that positions it) — part of the same
  gunsight complex as `TorsoTwist`, not a mystery gunsight sub-gadget as guessed in the prior
  follow-up.
- Traced the file-data footprint of the remaining two of `.GAU`'s 7 top-level widgets that hadn't
  been examined yet: offset 1000's constructor is `HThrottle` itself (already known); offset 1212's
  constructor (tied to `"hddclip"`/`"pilots"`/`"static"` string resources) is a pilot-roster/
  crew-status HDD readout whose file-data footprint runs from 1212 to roughly 1588 — accounting for
  most of what was `GAUFile.Remainder`.
- **NAVBAR searched for exhaustively and not found anywhere in this file.** Two separate DBSIM
  string-table keyword sweeps (9 keywords: torso/twist/navbar/compass/reticle/hud/panel/gadget/
  indicator; then 12 more: heading/bearing/degree/altimeter/mach and variants) found zero direct
  hits for anything nav/compass/heading-related, and all 7 of `.GAU`'s top-level widget-offset
  constructors were traced to their real purpose (weapon-control-button panel, energy-meter
  container, shield front/rear value labels, MFD radar/mode-switching logic, throttle, the gunsight
  complex, and the pilots/HDD readout) — none is navbar-shaped. This is now a confirmed negative
  result, not an unexplored gap.
- None of the newly-mapped regions (the speed-readout anchor, the pilots/HDD widget's several
  sub-rects, or a further MFD-radar-submode object found near offset 1668 read by the loader's
  *caller* rather than the loader itself) were implemented as typed C# fields, unlike every earlier
  widget in this file. The reason is consistent across all of them: none showed the "constant except
  position" signal that made earlier widgets trustworthy, and — more fundamentally — this app has no
  HUD text/graphics renderer to visually confirm a guess against the way `.HB0` cockpit-art overlays
  confirmed the shield display, throttle, MFD panel, reticle, and torso-twist. Documented as raw
  preserved bytes with a structural map in `GAUFile.cs`'s doc comment instead of force-fitting an
  unverified model.

`.GAU` is now considered functionally complete for this project's purposes: every widget that can be
verified (visually, or via an unambiguous constant-shape signal) is decoded; what's left is real
in-file structure that's understood at the byte-offset level but not worth modeling without a way to
confirm exact field semantics.

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
