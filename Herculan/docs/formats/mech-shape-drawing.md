# How a machine's shape reaches the screen (DBSIM.EXE)

Everything below sits *above* [`dts-node-posing.md`](dts-node-posing.md), which starts at `TSGroup_RenderPolys`. Two of the three mechanisms here rewrite the shape before a poly is ever drawn, so a reader who starts at the poly renderer will conclude the file's geometry is what appears on screen. For a machine it is not.

## The chain

| Address | Role |
| --- | --- |
| `0042841c` | `maybe_Scene_SubmitFrameObjects` — files every object into `ObjList::drawTable` with its terrain cell. No visibility decision is made here |
| `0042883c` | Walks the flat bucket, calling each object's vtable `+0x00` |
| `004174c8` | `Mech_Draw`, the mech's `+0x00`: splices the hardpoints, then draws |
| `004033e4` | `Shape_DrawAtDetailLevel` — picks the LOD root, installs the transform, calls the shape instance's `+0x1c` |
| `00401fe4` | `SimObject_InstallModelTransform` — builds the Q14 euler matrix if dirty, installs it |

## The LOD root is chosen per frame, per object

A machine's `.DTS` roots are **complete alternate models**, not parts of one: `SAMSON.DTS` carries 7, descending 228 → 227 → 226 → 206 → 131 → 41 → 18 polys. `MechType_InitOne` loads them into a detail struct at `typeRec+0xde` — root array at `+0`, thresholds at `+4`, root count at `+0xc` — and `Mech_Constructor` (`00415bb0`) hands that struct to the object through `SimObjectBase_ConstructWithDetailTable` (`004033a4`), which stores it at `obj+0x41`. **Only the machine does.** `SimObjectBase_ConstructAnimated` and `SimObjectBase_ConstructStatic` both null that field, so a flyer, a structure and everything else draw the one root they were built with.

`Shape_DrawAtDetailLevel` then picks one every frame:

```c
radius = shapeInstance->shape->boundingRadius                   // shape+8
size   = (radius << DAT_006c60ac) / max(FastMagnitude3D(viewOffset) - radius, 1)
if (size == 0) size = 1
t      = Q10Multiply(g_ShapeDetailSizeScaleQ10, size)

i = min(rootCount - 1, g_ShapeDetailBias)                            // the detail bias
for (j = 0; i < rootCount - 1 && t < thresholds[j]; j++) i++;   // j starts at 0, not at i
shapeInstance->shape = roots[i];
... render ...
shapeInstance->shape = roots[0];                                // restored after
```

`DAT_006c60ac` is the view's perspective shift, installed per view by `Raster_InstallViewProjection` (`0048c1d8`) — `2^shift` is the focal length in pixels, so **`size` is the shape's projected radius in screen pixels** and the thresholds are a count of pixels. The subtraction measures to the near face of the bounding sphere rather than to its centre. Because the restore puts root 0 back after every draw, the radius read here is always root 0's, whichever root was last drawn.

The loop advances while the projected size is *below* the threshold, so a distant machine walks toward the crude roots and a close one stops at the starting index. That is the opposite sense to `TSDetailPart`'s ascending table — see [`dts-texture-binding.md`](dts-texture-binding.md#tsdetailpart-level-selection-and-structure-detail), which is the same shape of selection one level down, inside a shape rather than over its roots.

**The two indices are not the same index.** The root index starts at the bias and the threshold cursor starts at 0, and both advance together. At bias 0 they coincide and the walk reads as the plain descending-table lookup it looks like; at any other bias the machine starts that many roots down and is still compared against the finest thresholds. A nonzero bias therefore makes root 0 unreachable at any distance.

### Each root numbers its own nodes

**Every root carries its own `ANAnimList`, and a node id means nothing outside the root that declares it.** The pose array, however, is built from root 0 alone — see "The pose array is root 0's" below, which is what makes that a problem rather than a detail.

APOCA's seven roots, as parent>child pairs:

| Root | Sequences | Keyframes | Node tree |
|---|---|---|---|
| 0, 2 | 8 | 372 | `-1>0 -1>1 1>2 1>3 1>4 2>9 3>10 10>7 9>5 9>6 10>8 4>11` |
| 1, 3 | 1 | 21 | the same, less `-1>0` |
| 4 | 1 | 17 | `-1>1 1>2 1>3 1>4 2>7 3>8 7>5 8>6 4>9` |
| 5 | 1 | 16 | `-1>1 1>2 1>3 1>4 2>7 3>8 7>5 8>6` |
| 6 | 2 | 12 | `-1>1 1>2 1>3 1>4 2>5 3>6` |

The topology is the same machine throughout — pelvis 1, hips 2 and 3, torso mount 4, upper body hanging off 4, knees under the hips — but from root 4 down the numbering is **compacted**: dropping the camera node `0` and the separate lower-leg nodes closes the gaps, so root 0's upper body at node 11 is root 4's at node 9, and root 0's node 9 is a knee. A root that merely *drops* nodes keeps the rest where they were; a root that compacts renumbers them.

Across the 21 machine shapes, the leading four to six roots keep root 0's numbering and the crudest one to three compact it — 99 of 152 roots in total. The split is always a prefix.

### The pose array is root 0's

One per-node world transform array exists per object, and **every root is drawn through it**:

- `ShapeInst_Construct` (`0047872c`), the shape-instance constructor, sizes the dirty-flag, local and world arrays from `shape->animList->nodeCount` at `shapeInst+0xc` and allocates them once. The shape it reads is the one the instance is built with, root 0. Nothing reallocates them.
- `ShapeInst_BuildWorldTransforms` (`00478b58`) has exactly two callers — that constructor, and `ShapeInstance_StepAnimation` (`00478c2c`), which `SimObject_ApplyRootMotion` runs once per sim tick. `Shape_DrawAtDetailLevel` restores root 0 into the instance at the end of every draw, so **both callers run with root 0 installed** and the relation list walked is always root 0's.
- `ShapeInst_BindNodeTransformArray` (`00475fd8`) binds that one array to the render global; its two callers are the generic shape-render entry and exit. Nothing rebinds per root.
- `AnimThread_EvalNodeLocals` (`004799a4`) indexes the locals by the part ids of the sequence the **thread's own** anim list names — `thread+0` — not the drawn root's.

So a root whose numbering is compacted has its geometry composed against whatever joint shares the number in root 0's tree. For APOCA at rest, root 0 places the nodes the crude roots hang their torso on at:

| Root | Torso node | Where root 0 puts it | Correct torso position |
|---|---|---|---|
| 0-3 | 11 | `(0.00, 0.00, 10.14)` m | — |
| 4 | 9 (a knee in root 0) | `(-2.76, -1.68, 4.08)` m | 6 m low, 2.8 m off-axis |
| 5, 6 | 4 (the torso mount) | `(0.00, 0.00, 0.00)` m | on the ground |

**Observed retail behaviour does not show a displaced upper body at the lowest HERC DETAIL setting.** Everything above is read from the binary and the shipped shapes ([Open](#open)). Ruled out so far: the shape loader truncating the root list (`Shape_LoadAllRoots` (`00474bcc`) loads all of them, into a 100-slot buffer); the bias coming from anywhere but the HERC DETAIL byte (`0045fbaf` and `00461dc9` both push `DAT_004d1fc5` straight into `ShapeDetail_ApplyHercDetailSetting`); and `Mech_Draw` bypassing the selection (`004174c8` calls `Shape_DrawAtDetailLevel` after its splice loop).

### The three tunables

| Global | Source | Value |
|---|---|---|
| `thresholds` | `g_ShapeDetailThresholds`, which `MechType_InitOne` writes into **every** type record's `+0xe2` | `{75, 60, 45, 25, 18, 12, 6}` — one shared table, not per-chassis data |
| `g_ShapeDetailSizeScaleQ10` | `ShapeDetail_ApplyHercDetailSetting` from the HERC DETAIL setting, through `g_HercDetailScaleValues` | `2000` for all five settings. The image's own initialiser leaves it at Q10 one (1024), so the runtime value is the load-bearing one |
| `g_ShapeDetailBias` | the same function, through `g_HercDetailBiasValues` | `{4, 3, 2, 1, 0}` by setting — setting 4 is the finest |

`ShapeDetail_ApplyHercDetailSetting` is called from `maybe_Sim_RenderFrame` (`0045fb9c`) and from `00461ddc` with the HERC DETAIL option ([`../simulation/preferences.md`](../simulation/preferences.md)) as its argument, so the setting is re-read as the player steps the row.

## Hardpoint attachment slots are overwritten every frame

**A machine's own `.DTS` carries one placeholder part per visible hardpoint, and DBSIM never draws it.** `Mech_Draw` replaces each one before rendering, with the fitted weapon's shape or with a blank record, so the shipped geometry appears nowhere in the original.

Built once, per LOD root, by `MechType_InitOne`:

- `GunLayout_CollectHardpointBones` (`0040fc50`) walks the `.GL` and emits each record's `BoneId`, or `-1` when its mounting code (`.GL +6`) is 4, the invisible mounting.
- `MechType_BindHardpointSlots` (`0040304c`) resolves each id through the shape's vtable `+0x24` to the **address of the part slot** holding the part whose `TSBasePart.IdNumber` is that id, and stores null for `-1`. The result is a `{void **slots; short count}` pair per root, at `typeRec+0xe6`.

Applied every frame by `Mech_SpliceHardpointShapes` (`004030d0`), for each root:

```c
replacement = mount->shape ? mount->shape : blankRecord;   // blanks from typeRec+0xec, 0x18 apart
replacement[+4] = (*slot)[+4];                             // inherit TSBasePart.Transform
replacement[+6] = (*slot)[+6];                             // inherit TSBasePart.IdNumber
*slot = replacement;
```

**The match is on `IdNumber`, not on the transform node.** Verified against every retail chassis, where the ids are exactly the visible hardpoints' bones:

| Chassis | Visible hardpoints | Placeholder `IdNumber`s |
|---|---|---|
| SAMSON | 7 | 8, 9, 10, 11, 18, 66, 77 |
| APOCA | 4 | 10, 11, 66, 77 |
| RAZOR | 4 | 10, 11, 66, 77 |
| OUTLAW | 3 | 8, 10, 11 |
| PITBULL | 1 | 5 |

The invisible mounting has to be excluded on its own merits, not merely for tidiness: SAMSON's bone 5 carries a real torso part, and splicing it would delete the machine's middle.

On most chassis the placeholders are recognisable in isolation — flat, two-sided, untextured, every slot of their surface record `0/1024` — but **the PITBULL's is an ordinary-looking `TSGroup`**, so that signature is a description of the usual case and not the rule.

## A destroyed component hides its own geometry

Every body part of a machine is a `TSCellAnimPart` of **three cells**: intact, damage-shaded (the same geometry, every poly moved to one dark ramp), and a bare `TSPoly` that draws nothing. `Component_DestroyAndCascade` (`0040d434`) ends by advancing that component's flipbook to the last cell:

```c
if (damageRecord[+3] >= 0)                                  // signed byte: the sequence this
    shapeInstance[+8][damageRecord[+3]] = 2;                // component drives, -1 for none
```

`shapeInstance+8` is the per-sequence cell-frame array `TSCellAnimPart_Render` indexes by `AnimSequence` — see [`dts-billboards.md`](dts-billboards.md). So losing a component is drawn by stepping its parts to their blank cell, and the `.DMG` record's `+3` byte is the component-to-sequence map. The same byte gates the fire that component lights — see [`../simulation/destruction-effects.md`](../simulation/destruction-effects.md#who-catches-fire) — and the damage arithmetic behind it is in [`../simulation/component-damage.md`](../simulation/component-damage.md).

**The blank cell is blank because a `TSPoly` has no colour.** The surface index (`ColorIndexId`) lives on `TSSolidPoly`, and all three flat renderers the engine ships — `TSSolidPoly_Render` (`00474db4`), `TSShadedPoly_Render` (`0047542c`) and `TSTexture4Poly_Render` (`00474e9c`) — resolve their fill through it. A plain `TSPoly` carries no such field on disk, so there is nothing for a renderer to fill it with, and stepping to the third cell is what removes the part. This is read off the chunk layout and the set of renderers that exist, not off a disassembled `TSPoly` vtable slot.

The same reasoning covers 14 plain `TSPoly`s reachable at cell 0 across every drawn root of the mech, flyer and structure libraries; the engine emits geometry for none of them.

**No shape nests one `TSCellAnimPart` inside another** — across every `.DTS` a mission loads and both `.DGS` libraries, the deepest nesting is one. A piece of geometry therefore stands on at most one cell of one sequence, and whether it is drawn is a single test rather than a chain of them.

## Rejected readings

| Reading | Why it is wrong |
|---|---|
| The roots share one node space, because each crude root's transform ids are a **subset** of root 0's | A subset of ids is not the same joints. The ids are drawn from one range because the numbering is compacted, not because a node kept its number: APOCA's upper body is node 11 on root 0 and node 9 on root 4, where node 9 is a knee. The relation list is the only thing that says what a node is, and each root declares its own |
| A mech `.DTS` carries one `ANAnimList`, on its root shape | One **per root**, and they differ in every dimension — APOCA's root 0 declares 8 sequences over 372 keyframes and 12 nodes, its root 4 declares 1 over 17 and 9 |
| A nonzero detail bias is harmless because the walk can still reach root 0 | The walk only ever advances, and it starts at the bias. `g_ShapeDetailBias` is the floor on how fine a machine is ever drawn, which is what makes the lowest HERC DETAIL setting a visible change at point-blank range and not only at distance |

## HERCULAN Engine

| Mechanism | Status |
|---|---|
| Hardpoint attachment slots | **Skipped**, not spliced — `DtsMeshBuilder.AttachmentPartIds` derives the id set from the `.GL` and `SceneModelLibrary.Mech` leaves those parts out of the mesh. The fitted case is drawn separately from `MECHWPNS.DTS` (`SceneModelLibrary.MechWeapon`), which is the same picture by a different route |
| LOD root selection | **Ported, over a shortened chain.** `Render.ShapeDetail` is the rule and the three tables; `SceneModelLibrary.MechDetailRoots` builds the roots and the host selects one per machine per frame (`SelectDetailRoots`). The focal length is the window's rather than retail's fixed 512, so the thresholds stay a count of pixels on the screen being drawn. HERC DETAIL supplies the bias |
| Compacted roots | **Not drawn.** The chain stops at the last root that keeps root 0's numbering (`ShapeAnimation.SharesNodeNumbering`), so the crudest one to three roots of each chassis are never selected. Drawing them reproduced the displacement in the table above — APOCA's upper body at a knee. Whether this truncation is a divergence to lift or a retail behaviour to match ([Open](#open)) |
| Component sub-shape cells | **Drawn.** `DtsMeshBuilder.BuildSegments` builds every cell of every sequence into its own segment under a `CellGate`, and the renderer draws the one `Sim.ComponentDamage.CellFrames` names — the same array, per object, that `shapeInstance+8` is |

## Open

- **Open:** what reconciles the compacted-root pose displacement (see [The pose array is root 0's](#the-pose-array-is-root-0s)) with observed retail behaviour, which shows no displaced upper body at the lowest HERC DETAIL setting. Deciding it settles whether the engine's truncation at the last root sharing root 0's numbering is a divergence to lift or a retail behaviour to match.
