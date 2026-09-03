# How a machine's shape reaches the screen (DBSIM.EXE)

Everything below sits *above* [`dts-node-posing.md`](dts-node-posing.md), which starts at
`TSGroup_RenderPolys`. Two of the three mechanisms here rewrite the shape before a poly is ever
drawn, so a reader who starts at the poly renderer will conclude the file's geometry is what appears
on screen. For a machine it is not.

## The chain

| Address | Role |
| --- | --- |
| `0042841c` | `maybe_Scene_SubmitFrameObjects` — files every object into `ObjList::drawTable` with its terrain cell. No visibility decision is made here |
| `0042883c` | Walks the flat bucket, calling each object's vtable `+0x00` |
| `004174c8` | `Mech_Draw`, the mech's `+0x00`: splices the hardpoints, then draws |
| `004033e4` | `Shape_DrawAtDetailLevel` — picks the LOD root, installs the transform, calls the shape instance's `+0x1c` |
| `00401fe4` | `SimObject_InstallModelTransform` — builds the Q14 euler matrix if dirty, installs it |

## The LOD root is chosen per frame, per object

A machine's `.DTS` roots are **complete alternate models**, not parts of one: `SAMSON.DTS` carries 7,
descending 228 → 227 → 226 → 206 → 131 → 41 → 18 polys. `MechType_InitOne` reads the root count into
`typeRec+0xea` and `Shape_DrawAtDetailLevel` picks one every frame:

```c
radius = shapeInstance->shape->boundingRadius
t      = (radius << DAT_006c60ac) / max(viewDistance - radius, 1)     // projected size
i      = min(rootCount - 1, DAT_0049736c)                             // the detail bias
while (i < rootCount - 1 && Q10Multiply(DAT_00497368, t) < thresholds[i]) i++;
shapeInstance->shape = roots[i];
... render ...
shapeInstance->shape = roots[0];                                      // restored after
```

The loop advances while the projected size is *below* the threshold, so a distant machine walks
toward the crude roots and a close one stops at the starting index. **That starting index is the
bias**, so a nonzero `DAT_0049736c` makes root 0 unreachable at any distance. This is the same shape
of selection as `TSDetailPart`'s, on a different table — see
[`dts-texture-binding.md`](dts-texture-binding.md#tsdetailpart-level-selection-and-structure-detail).

## Hardpoint attachment slots are overwritten every frame

**A machine's own `.DTS` carries one placeholder part per visible hardpoint, and DBSIM never draws
it.** `Mech_Draw` replaces each one before rendering, with the fitted weapon's shape or with a blank
record, so the shipped geometry appears nowhere in the original.

Built once, per LOD root, by `MechType_InitOne`:

- `GunLayout_CollectHardpointBones` (`0040fc50`) walks the `.GL` and emits each record's `BoneId`,
  or `-1` when its mounting code (`.GL +6`) is 4, the invisible mounting.
- `MechType_BindHardpointSlots` (`0040304c`) resolves each id through the shape's vtable `+0x24` to
  the **address of the part slot** holding the part whose `TSBasePart.IdNumber` is that id, and
  stores null for `-1`. The result is a `{void **slots; short count}` pair per root, at
  `typeRec+0xe6`.

Applied every frame by `Mech_SpliceHardpointShapes` (`004030d0`), for each root:

```c
replacement = mount->shape ? mount->shape : blankRecord;   // blanks from typeRec+0xec, 0x18 apart
replacement[+4] = (*slot)[+4];                             // inherit TSBasePart.Transform
replacement[+6] = (*slot)[+6];                             // inherit TSBasePart.IdNumber
*slot = replacement;
```

**The match is on `IdNumber`, not on the transform node.** Verified against every retail chassis,
where the ids are exactly the visible hardpoints' bones:

| Chassis | Visible hardpoints | Placeholder `IdNumber`s |
|---|---|---|
| SAMSON | 7 | 8, 9, 10, 11, 18, 66, 77 |
| APOCA | 4 | 10, 11, 66, 77 |
| RAZOR | 4 | 10, 11, 66, 77 |
| OUTLAW | 3 | 8, 10, 11 |
| PITBULL | 1 | 5 |

The invisible mounting has to be excluded on its own merits, not merely for tidiness: SAMSON's bone
5 carries a real torso part, and splicing it would delete the machine's middle.

On most chassis the placeholders are recognisable in isolation — flat, two-sided, untextured, every
slot of their surface record `0/1024` — but **the PITBULL's is an ordinary-looking `TSGroup`**, so
that signature is a description of the usual case and not the rule.

## A destroyed component hides its own geometry

Every body part of a machine is a `TSCellAnimPart` of **three cells**: intact, damage-shaded (the
same geometry, every poly moved to one dark ramp), and a bare `TSPoly` that draws nothing.
`Component_DestroyAndCascade` (`0040d434`) ends by advancing that component's flipbook to the last
cell:

```c
if (damageRecord[+3] >= 0)                                  // signed byte: the sequence this
    shapeInstance[+8][damageRecord[+3]] = 2;                // component drives, -1 for none
```

`shapeInstance+8` is the per-sequence cell-frame array `TSCellAnimPart_Render` indexes by
`AnimSequence` — see [`dts-billboards.md`](dts-billboards.md). So losing a component is drawn by
stepping its parts to their blank cell, and the `.DMG` record's `+3` byte is the component-to-sequence
map. See [`../simulation/damage-system.md`](../simulation/damage-system.md).

## HERCULAN Engine

| Mechanism | Status |
|---|---|
| Hardpoint attachment slots | **Skipped**, not spliced — `DtsMeshBuilder.AttachmentPartIds` derives the id set from the `.GL` and `SceneModelLibrary.Mech` leaves those parts out of the mesh. The fitted case is drawn separately from `MECHWPNS.DTS` (`SceneModelLibrary.MechWeapon`), which is the same picture by a different route |
| LOD root selection | Not implemented; root 0 is hard-coded. Correct only while `DAT_0049736c` is zero, which is unverified |
| Component sub-shape cells | Not implemented; every part is built at cell 0, so a destroyed component keeps its intact geometry |
