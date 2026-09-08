# Simulation object layout

What a DBSIM simulation object *is* in memory: how the classes derive from one another, how long each one is, and where the shared fields sit.

The field-by-field and slot-by-slot inventories are **not here**. They live in two machine-readable files under `tools/ghidra_scripts/`, because they are applied to the Ghidra database rather than read by a human, and because scattering 34 vtable slots and 48 field offsets across ten topic docs is what those files exist to stop:

| File | Owns | Applied by |
|---|---|---|
| `known_vtables.json` | Which vtable slot means what, and which addresses hold a table of that shape | `ES2ApplyVtables.java` |
| `known_structs.json` | Field offset, width and meaning inside an object | `ES2ApplyStructures.java` |
| `known_symbols.json` | Address → function findings | `ES2ApplySymbolNames.java` |

Each file's `_readme` carries its schema and its rules. All three apply scripts are idempotent, and once the `/ES2` types exist in the project any of them can be run alone, in any order. Only a database that has never had them applied cares about the order — a signature naming a struct type cannot resolve until that type exists — so after a fresh import and analysis, run them all in one go:

```bash
sh tools/scripts/ghidra_apply_all.sh
```

and after that run whichever one you need:

```bash
sh tools/gh.sh ES2ApplyStructures "E:\ES2Stuff\tools\ghidra_scripts\known_structs.json"
```

Once a function's parameter is typed with `SimObject *`, the decompiler renders `*(char *)(param_2 + 0x99)` as `param_2->destroyed` and `(**(code **)(*param_1 + 0x48))(...)` as `(*param_1->vtbl->AiEnemySighted)(...)`. That is the whole point of the exercise: an offset recorded once in the JSON reads back named in every function that touches an object.

## Two hierarchies, one root

`SimObjectBase_Constructor` (`00402188`) installs the vtable at `004a0b98` — the same table the projectile base derives from. **The simulation objects and the projectiles share a root class.** That is why `SimObjectVtable`'s first six slots are `ProjectileVtable` unchanged, and why slots `+0x4`, `+0xc` and `+0x10` hold the same three functions in every table in the file.

```
root (004a0b98)                     ── SimObjectBase_Constructor (00402188)
├── projectile base (004987a0)      ── rocket, bullet, beam tracer, one dead class
└── shape layer (004973ac/004973cc) ── FUN_0040332c / FUN_00403368 / FUN_004033a4
    └── SimObject (0049a54c)
        ├── MechObject  (0049a282)  ── Mech_Constructor  (00415bb0)
        ├── FlyerObject (0049a5e0)  ── Flyer_Constructor (004215f4)
        └── StructureVtable (00497940)         ── Base_Construct (00405314)
            ├── StructureRadarVtable    (004979d4)
            ├── StructureType0x22Vtable (00497784)
            └── StructureArmedVtable    (004978ac)
                └── StructureEmplacementVtable (00497818)
```

The structure branch is the odd one: `Base_Construct` switches on the BASES.DAT type index. **Every branch installs `StructureVtable` first and then overwrites it**, which is what establishes the four others as derived from it — and the emplacement branch installs three in a row, so that class is two levels down. All five are the same 34-slot shape.

Only three slots differ across the five, which is the fastest way to see what separates them: the destructor, `ThinkTick` (`+0x18`), and `GetTorsoTwistAngle` (`+0x3c`). **That last one is the armed/unarmed line.** `StructureArmedVtable` and `StructureEmplacementVtable` install `Base_GetTurretAngle` (`00403594`), which returns a real aim angle from `structure+0x20f`; the plain building and the radar mast keep the shared `00411a5c` zero stub. That agrees with `Base_Construct` setting `disarmed` (`+0xa5`) at spawn on exactly the two that keep the stub.

## Sizes come from the pool, not from the highest known offset

None of these classes is allocated with a literal `operator new` size. Each is drawn from a free-list pool: `Pool_Init` (`004719cc`) takes `(pool, count, elementSize)` and allocates `count * (elementSize + 8)`; the allocator `00471a24` pops a node and returns `node + 8`. **The pool's element size is the object's true length.**

| Class | Length | Pool created at | Pool global |
|---|---|---|---|
| Mech | `0x36a` (874) | `00425185`, in `DBSim_LoadScriptDat` | `004a9bfe` |
| Flyer | `0x291` (657) | `00425220`, same function | `004a9e3d` |
| Structure | `0x26d` (621) | `00405e1e`, in `FUN_00405df4` | `004a9624` |

The pool globals are zero in the image and filled in at load, so the size is not visible at the allocation site — `MOV EAX,[0x004a9bfe]` there loads the *pool pointer*. Follow the write to the global to find the size.

**A flyer is not a shortened mech.** Two pools, two lengths, two constructors, and the flyer starts its own fields at `+0x1fa` where the mech starts at `+0x1f2`. They are siblings.

## Where the base ends — `0x1f2`

`SimObject` is never allocated on its own, so it has no pool of its own to read a size from. Its extent is bounded above by where the derived classes start writing fields nothing else has:

- `Mech_Constructor` stores the HERCS.DAT record at `+0x1f2` and allocates the component-damage header at `+0x206`.
- `Base_Construct` stores the BASES.DAT record at `+0x1f2`, the alive-flag array at `+0x201` and the state array at `+0x205`.
- `Flyer_Constructor` stores its type record at `+0x1fa` and its damage header at `+0x200`.

Below that line all three constructors write an *identical* block — `+0x1a8 = 0xffff`, `+0x98 = 1`, `+0xa7 = 1`, `ObjectList_Add`, then `+0x1b6`, `+0x1b2`, `+0x1be`, `+0x1bc`, `+0x1ba` — which is what identifies the layout as shared rather than three classes coincidentally agreeing. `SimObject` is therefore `0x1f2` bytes.

## The object's frame is a transform, and its position is that transform's translation

`obj+0x12` is a complete 32-byte transform record of the kind `Transform_Concat` (`0047f914`) composes: nine Q14 `int16` matrix entries, a rank byte at `+0x12`, and an `int32` translation at `+0x14`/`+0x18`/`+0x1c`. Laid at `obj+0x12`, that translation falls at `obj+0x26`/`+0x2a`/`+0x2e` — **which is the object's world position**. The two are the same storage, not a copy: `SimObject_InstallModelTransform` (`00401fe4`) builds the matrix half from the euler angles at `obj+0x0c` whenever the dirty flag at `obj+0x32` is clear, and every caller that wants a position passes `obj+0x26` as a `Vec3i`.

## The two per-object tables are 112 rows each

`obj+0xc2` is the contact table and `obj+0x132` the line-of-sight cache, both flat byte arrays indexed by the *other* object's `listIndex` (`obj+0x4b`, assigned by `ObjectList_Add`). Neither has a length written down anywhere; both are `0x70` = 112 bytes, from the gap between them, corroborated by the identical `0x70` gap from `+0x132` to the next member at `+0x1a2`. **112 is therefore the simulation's object cap**, and it is why the engine's `SimObject.EnsureTableSize` is a deliberate divergence rather than a port.

## The "out of the fight" triple — `+0x99`, `+0xa4`, `+0xa5`

Read together by `Group_IsWipedOut` (`00412be4`) and `Ai_IsTargetable` (`00411e80`), and separately by everything else. They are **three different conditions, not three damage latches**, and each has its own writers:

| Byte | Condition | Written by |
|---|---|---|
| `+0x99` | **Destroyed** | The three classes' damage-write paths, and nothing else: `Mech_ComponentDamageWrite` (`00417de4`) when a core component reaches full damage, `Flyer_ComponentDamageWrite`, `Base_ApplyDamage`. `Base_Construct` also sets it for a structure spawned already destroyed |
| `+0xa4` | **Immobilised** — cannot move under its own power | `Mech_ComponentDamageWrite` when half or more legs reach full damage; `Razor_MovementTick` (`004198f4`) when the airframe loses its nose or belly |
| `+0xa5` | **Disarmed** — has no working weapon | `Ai_ChooseWeapon` (`0041f358`) the first time it walks a machine's whole mount list and finds nothing; `Base_Construct` at spawn, for a structure type that has no weapons |

None of the three means "removed from the simulation". Which subset a test reads is the behaviour: the detection sweep, the player's target selection and `Group_ConditionTier` read `+0x99` and `+0xa4` only; the AI's own tests add `+0xa5`, which is why a disarmed machine flees and is abandoned as a target while remaining a legal player target.

**An ordinary building is born disarmed.** `Base_Construct` sets `+0xa5` for the plain-structure vtable (`00497940`, BASES.DAT types 0–4, 7, 9, `0x0c`–`0x1c`, `0x1f`, `0x21`, `0x24`–`0x2c`) and for the radar-mast vtable (`004979d4`, types 5, 6, `0x1d`, `0x1e`) — and for neither of the armed families (`004978ac`, `00497784`, `00497818`). That the structure branch and the mech branch arrive at the same meaning from opposite directions is what settles the reading.

**`Group_IsWipedOut` (`00412be4`) is the trap.** Its name says destroyed; what it tests is all three bytes, so a group whose members are alive but disarmed answers yes — and because an unarmed building is born disarmed, a group of ordinary structures answers yes from the moment it is built. For its one caller, `Group_NoRivalOrderOnSubject` (`00412e74`), that is the *right* answer: the question a guard order asks is whether anything can still contest the post, and an unarmed building cannot. Read the function as "is this group out of the fight" and it is correct; read it as "is this group destroyed" and it looks broken. Anything that reuses it for a destroy-the-group objective would be reading it the second way. See [`ai-goals.md`](ai-goals.md).

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| A field the scalar search cannot find is unused | Every reader of the flag bytes from `obj+0x92` up materialises that address first (`LEA ECX,[EBX + 0x92]`) and then uses a small displacement off it — `destroyed` is read as `[EDX + 0x7]`, `+0xa7` written as `[EAX + 0x15]`. `ES2FindFieldRefs` on `0x95`, `0x99`, `0xa1`, `0xa5` or `0xa7` returns **zero sites in the whole binary**, and all five are live fields. |
| The allocation site's argument is the object's size | It is the pool pointer. See "Sizes" above. |
| A mech's length can be inferred from the highest documented offset | The highest offset anyone has written down is a lower bound that moves every time someone reads another function. `0x36a` is a fact about the binary. |
| `+0xa4` is "removed" and `+0xa5` is "destroyed" | `+0xa4` is written where a machine loses its legs and a RAZOR loses its fuselage, and the flyer's position integration refuses to run while it is set — it is *immobilised*. `+0xa5` is written by the weapon chooser and by `Base_Construct` for unarmed structure types — it is *disarmed*. `+0x99` is the one the damage paths write. |

## Open questions

- **The word at `+0x88`, just past the last slot.** Every one of these tables sits on a uniform `0x94` stride — 34 slots, then one code address, then eight zero bytes — and the code address is in the same thunk block as the class's destructor (`00427xxx` for the object classes, `00406xxx` for the structures). That is suggestive, but across the 116 simulation functions that take an object, the highest slot anything calls through is `+0x7c`. Nothing calls `+0x88`, so it stays outside the definition.

- **What `obj+0x92` is in the source.** Whether it is a sub-object the compiler is addressing or just a base register it chose is not settled, so `known_structs.json` places those bytes at their absolute offsets rather than inside an invented struct.
- **What class `004a0b98` is.** The projectile base derives from it and `SimObjectBase_Constructor` installs it, but the `0046bxxx` block its slots point into is shared engine code and none of it is named.
- **The table at `004a0bb8`.** Next after the root in memory, with `FireEffect_Dtor` in its destructor slot but data at `+0x14` — possibly a five-slot sibling of `ProjectileVtable` rather than a sixth instance of it. Left out of `known_vtables.json` deliberately.
- **The eight unnamed `SimObjectVtable` slots** (`+0x04`, `+0x0c`, `+0x28`, `+0x30`, `+0x60`, `+0x68`, `+0x80`, `+0x84`). `+0x4` and `+0xc` hold the same function in every table in the file, base and projectile alike, which makes them the likeliest to be worth a name.
