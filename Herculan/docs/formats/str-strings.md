# `.STR` — simulator string tables

`simvol0\str\*.STR`. DBSIM keeps its UI text out of its code; every caption, readout label and alert
line comes from one of these.

Engine implementation: `Herculan.Engine.Content.SimStringTable`.

## Load

`SimStrings_LoadAll` (`00437598`) opens one file and issues a run of registration calls, each naming
a destination pointer array in `.bss` and a count. Groups are consumed in strict file order, so a
group's index is its position in that sequence. `STRINGS0.STR` registers 41 groups, the sixth
(13 entries, `DAT_004d13e0`) being the MFD's captions.

## Layout

After the 9-byte VOL entry prefix; all integers little-endian:

```
int32 contentLength                  -- bytes that follow
repeat until contentLength consumed:
    int16 count                      -- strings in this group
    count x {
        int16 length                 -- includes the NUL terminator
        byte[length] text            -- NUL-terminated ASCII
        uint8 attributeCount         -- 0, 1, 7 or 8 in retail data
        byte[attributeCount] attributes
    }
```

`attributeCount` is self-describing, which makes the file walkable without the group table. It
matches `SimStrings_LoadAll` passing a second destination array only for groups whose entries carry
attributes and NULL for the rest.

**Verified byte-exact**: this shape consumes `STRINGS0.STR`, `SYSTEM.STR`, `COMMAND1.STR`,
`PILOTS.STR` and `SOUNDS.STR` to their declared content lengths with zero slack, and each file's
group counts reproduce the registration sequence in order.

Attribute use: `STRINGS0.STR` group 0 carries one byte per order; `SYSTEM.STR`'s alert lines carry
eight; `SOUNDS.STR` carries seven (volume, loop, priority, range fields).

## `STRINGS0.STR` groups

Groups referenced by decoded code:

| Group | Count | Contents |
|---|---|---|
| 0 | 18 | Squadmate orders. First six are the FLASH COMM page: `ATTACK MY TARGET`, `IGNORE MY TARGET`, `HELP ME OUT!`, `JOIN ON ME`, `SCAN FOR HOSTILES`, `FIRE AT WILL`. |
| 1 | 3 | `RED1`-`RED3` |
| 4 | 4 | Console button captions: `I`, `LINK`, `TRACK`, `` (entry 0 unused — the chain button's numerals come from a separate `.rdata` table, `ChainCountCaptions` at `0049c71c`) |
| 5 | 13 | MFD captions: `STATUS`, `FLASH COMM`, `NAV MAP`, `SCANNER`, `TARGET`, `MISSILE CAM`, `MODE`, `SELECT`, `RANGE`, `TARGET`, `XMIT`, `PASS`, `ACTIVE`. Entries 0-5 are the screen titles, 6-12 the aux button captions. |
| 10 | 5 | `OK`, `INT DMG`, `SHLD DWN`, `CRITICAL`, `WASTED` — **dead data**: `SimStrings_LoadAll` is the only reference to `DAT_004d1440` in the image. Group 28 is the live condition table. |
| 17 | 1 | `YOU` |
| 20 | 3 | `ID:`, `TARGET:`, `DIST:  ` |
| 21 | 1 | `STATUS:` |
| 22 | 16 | Herc type names |
| 23 | 31 | Structure type names |
| 28 | 5 | Condition, the MFD status screen's fourth label: `OK`, `SHIELDS DN`, `INT DAMAGE`, `CRITICAL`, `DESTROYED` |
| 29-31 | 2,1,1 | Scanner readouts: `ACT`/`PASS`, `TRG:`, `RNG:` |
| 38, 39 | 1,1 | `TIME:`, `SPEED:` — the gunsight readouts |

Other files: `SYSTEM.STR` alert lines, `COMMAND*.STR` mission briefing and tutorial dialogue,
`PILOTS.STR` 36 pilot surnames, `SOUNDS.STR` a 57-entry sample catalog.
