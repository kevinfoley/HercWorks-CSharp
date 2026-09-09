# The armory, the repair bay and the salvage economy

What the shell does with the numbers in [`../formats/herc-catalogs.md`](../formats/herc-catalogs.md) and [`../formats/weapons-dat.md`](../formats/weapons-dat.md): buying a chassis, queueing weapons, repairing damage and scrapping what is not worth keeping. **Every address in this doc is in `VSHELL.EXE`**, and the shell's source module is named in the assertion strings for each function cited.

## One currency, two units

There is a single resource — **salvage** — and it lives in one pool at `00482af4`, seeded at career start ([`campaign-loop.md`](campaign-loop.md#starting-a-campaign--fun_0040e2ed)) and spent on everything below.

**The pool is in kilograms and every screen prints tons.** The crew screen divides by 1000 before formatting against `estext.bin` `0x2f` (`Tons`), and the two catalog price fields are stored in tons and multiplied by 1000 when charged:

| Quantity | Stored | Charged / compared |
|---|---|---|
| chassis price, `herc_inf.dat` `+0x08` | tons | `x1000` by `Herc_Order` (`00411019`) |
| weapon price, `weapons.dat` `+0x14` | tons | `x1000` at load by `WeaponsDat_ReadRecord` (`00411d57`) |

So a 107,000 pool is 107 tons, an Outlaw at `+0x08 = 60` costs 60,000, and `WPN_INFO.BIN`'s prose `Salvage Required: 5,000 kg` for the `ATC20` is that weapon's stored 5 read back in the third unit the game uses for the same thing.

The Herc Construction screen is the one place that prints a chassis price without converting, formatting the raw `+0x08` against `TONS` — correct, and the reason the stored figure looks like a display value.

## Buying a chassis — `Herc_Order` (`00411019`)

Purchase gates on three things: the chassis's availability flag in `herc_inf.dat` `+0x0e`, a free hangar slot, and the pool covering the price *after* whatever the weapon queue has already committed (`DAT_00482af4 - Armory_QueuedTotal() <= price` refuses).

`Herc_Order` then builds the record in place — type, capacity from the in-code table, `+0x4a` progress 0, no hardpoints occupied, `+0x78` set to `herc_inf.dat` `+0x0c` — and returns the price to charge. A bought chassis therefore arrives **empty and unbuilt**, and `Herc_BuildTick` (`00411086`) advances it one mission per debrief until `+0x78` reaches zero.

Retail's `gam\hercs.dat` opens a new career with this state already on the books: a Razor at 0% with three missions to run ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamhercsdat--the-starting-hangar)).

## The weapon build queue — `Armory_*` (`armory.cpp`, `00411efd`)

Weapons are not bought off a shelf; they are queued into five build slots and delivered later. The queue is two globals, reset by `Armory_ResetQueue` (`0041213d`) to five free slots and five empty ids:

```
0046f8d4   int16       free slots, 0-5
0046f8d6   5 x int16   one weapon catalog id per slot, 0 = empty
```

The armory screen prints all three views of it: `Armory_QueuedTotal` (`00412586`) sums `weapons.dat` `+0x14` over the occupied slots against `Allocated:` (`estext.bin` `0xd6`), the free count against `Workspace Available:` (`0xd3`), and `5 - free` against `Workspace In Use:` (`0xd4`).

- **Enqueue** — `Armory_Enqueue` (`004125e7`) takes the first free slot from `Armory_FirstFreeSlot` (`004125bb`) and decrements the free count; with no slot free it does nothing.
- **Dequeue** — `Armory_Dequeue` (`0041260d`) clears every slot holding that id and increments the free count per slot cleared.
- **Delivery** — `Armory_DeliverQueue` (`00412428`) allocates a ten-byte weapon unit per queued id, initializes it as `{ id, 100, 100, ammo type }` — ammo type `1` for the missile racks (ids 13–16), `5` for everything else — appends it to that catalog record's owned-unit list via `Armory_AddUnit`, and returns the total price to charge.
- **Trim** — `Armory_TrimQueueToBudget` (`004123ba`) clears queued slots from the front while the pool cannot cover the committed total, refunding workspace as it goes.
- **Auto-fill** — `Armory_AutoFillQueue` (`00412341`) resets the queue and then walks the catalog in rank order through `WeaponsDat_IdAtRank` (`0041230c`), enqueueing every weapon that is unlocked, that the player owns **fewer than two of**, and that the running total still leaves affordable. The "keep two of each" rule is visible in `gam\weapons.dat`'s starting stock, which is two units of sixteen ids.

This queue is [`../formats/save-games.md`](../formats/save-games.md#savgame_sav--block-order)'s block 2, and the `{ index, value }` pairs it writes are these five slots.

## Repairing and scrapping

Both price against the same three-mode view of a machine's 66-byte status block — 13 external component conditions, 9 internal, and one per hardpoint ([`../formats/save-games.md`](../formats/save-games.md#the-66-byte-status-block)) — and against the unit values `damage.dat` expands per chassis ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamdamagedat--the-component-and-weapon-value-table)).

### Repair levels

Two six-entry `int16` tables in the image drive the whole thing:

```
0046fd78   { 100, 89, 79, 59, 29, 0 }   repair target per level
0046fd84   {  90, 80, 60, 30,  1, 0 }   the condition band each level covers
```

`Repair_LevelForCondition` (`0041381c`) returns the first level whose band floor is at or below the condition — so 90 and up is level 0, 80–89 level 1, 60–79 level 2, 30–59 level 3, 1–29 level 4, and 0 level 5.

`Repair_ItemCost` (`0041392a`) prices one component: nothing if it is already at or above the target, otherwise `(target - current) * unitValue / 100`. `Repair_HercCost` (`004139f7`) sums that over the six external groups, the nine internals and every occupied hardpoint, taking each hardpoint's unit value from the per-weapon table at `0048431e`.

`Repair_Auto` (`00411328`) is the `Auto Repair` mode (`estext.bin` `0x42`): start at level 0, step down a level at a time while the cost exceeds the budget, stop once the machine's own average condition already sits at the level under consideration, then apply through `Repair_Apply` (`004113af`) — which writes the chosen target across all three arrays, leaving an empty hardpoint at 100. If no level is affordable nothing is repaired and nothing is charged.

The debrief charges repairs through `FUN_0040e804`, which runs `Repair_Auto` over the player's machine and each on-strength squad member's, deducting each result from the pool.

**A full rebuild costs about 72.5% of the chassis price**, uniformly across the fleet: the fifteen `damage.dat` percentages sum to 950 and the two Q10 scale steps (`950/1024` then `800/1024`) land there for every chassis, since both factors are chassis-independent and the only per-chassis input is the price itself.

### Scrapping

`Herc_ScrapValue` (`00413b50`) values a machine by summing `condition * unitValue / 100` over the six external groups and the nine internals, plus the value of each mount too damaged to return to stock. Condition is the multiplier, so a healthy machine is worth more than a wrecked one — this is a yield, not a repair bill (`estext.bin` `0x43` `Salvage Available:`, `0xcc` `This herc will yield`).

`Herc_StripMounts` (`00411795`) decides what survives: a mount at **80 condition or better goes back into armory stock** through `FUN_00411efd`, and anything below is destroyed. `Herc_ScrapValue`'s mount loop counts exactly the complement — the ones under 80 — so a returned weapon is credited as inventory rather than as salvage.

At debrief `FUN_00410c7c` applies the same judgement to the machine itself: below 30 average condition it is scrapped out of the hangar for its value and the hangar count drops; at 30 or above only the overall-condition slot is reset to 100.

**The under-construction branch of `Herc_ScrapValue` pays nothing or everything.** For a machine whose `+0x4a` is below 100 the value is `((100 - +0x4a) / 100) * price * 1000`, and that integer division yields 0 for every progress figure from 1 to 99 — only an untouched 0% chassis returns anything, and it returns the full price. The intended form is almost certainly `(100 - +0x4a) * price * 1000 / 100`.

The order is confirmed in the instruction stream, not just in the decompiler's parentheses:

```
00413b85  2b c2                 sub   eax, edx                    ; eax = 100 - pct
00413b87  b9 64 00 00 00        mov   ecx, 100
00413b8c  99                    cdq
00413b8d  f7 f9                 idiv  ecx                         ; the divide, on (100 - pct) alone
00413b96  0f bf 8a 5c 3b 48 00  movsx ecx, word [edx+0x00483b5c]  ; price, after it
00413b9d  f7 e9                 imul  ecx
00413b9f  69 f8 e8 03 00 00     imul  edi, eax, 1000
```

Every instruction length chains from the function entry to `00413bac`, which is independently the `jge` target of the `+0x4a` test — so the block is bounded on both ends. The codegen is unoptimized throughout (a real `idiv` by a constant rather than a magic-number reciprocal, and a `push`/`pop` pair to compute one subtraction), which is why no reassociation could have moved the divide.

## What unlocks over the campaign

Two parallel mechanisms, both keyed on the campaign flag array and both consuming the flag as they grant:

| | Weapons | Chassis |
|---|---|---|
| Flag | `weapons.dat` `+0x16` | `herc_inf.dat` `+0x0e` |
| Granter | the mission-load path | `Herc_GrantUnlocks` (`004118c5`) |
| Persisted as | save block 1's leading byte per catalog id | save block 7 |
| Ships locked | — | Raptor II, Ogre, Maverick, Razor |

See [`../formats/weapons-dat.md`](../formats/weapons-dat.md#0x16-is-the-weapon-unlock-flag) and [`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#chassis-unlocks--herc_grantunlocks-004118c5) for each.

A locked weapon's armory panel is drawn disabled and its price withheld; a locked chassis is refused by the construction screen. Neither flag affects a machine already in the hangar.

## What the armory will sell

Twenty-six of the thirty-three catalog ids have an armory panel, and the seven without one cannot be bought at all: `NONE`, the three Bull weapons, and `LAEW`, `MINE` and `MFAC`. The panel list is `gam\arm_weap.dat`'s record list, and the thirty-entry class table at `0046f868` states the Bull exclusion a second way ([`../formats/herc-catalogs.md`](../formats/herc-catalogs.md#gamarm_weapdat)).

`gam\weapons.dat` closes with 39 units of starting stock across eighteen ids — four `ATC50`, three `ECM`, two of each of the other sixteen — every one at condition 100, and every one an id the armory also sells.
