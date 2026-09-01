# DTS node posing — how a shape's geometry follows its skeleton (DBSIM.EXE)

Every geometry group in a DTS shape is drawn through the transform of the node it names, taken from
the shape instance's per-node world array. That array is what the animation pipeline in
[`mech-locomotion.md`](../simulation/mech-locomotion.md#keyframe-interpolation) writes, so posing
geometry is the same mechanism as posing the cockpit eye — just applied to every node instead of one.

## The draw path

| Address | Role |
| --- | --- |
| `004758c8` | `TSGroup_RenderPolys` — DBSIM's counterpart to the VSHELL symbol of that name |
| `00476014` | Reads the group's own transform id and forwards it |
| `00476030` | Composes the node's world transform with the object-to-view one, and installs it |

`TSGroup_RenderPolys` binds the group's index/point/surface arrays to the poly-render globals, then
walks `group+0x1c` calling each poly's vtable slot `+0x1c`. Before any of that it calls `00476014`,
which is a one-line forwarder:

```c
FUN_00476030(group, out, (int)*(short *)(group + 4));   // group+4 == TSBasePart.Transform
```

`00476030` then, for a non-negative transform id:

```c
FUN_0047f914((short *)(id * 0x20 + _DAT_006b7bec), DAT_006b7c14, out);  // Concat(nodeWorld[id], objectToView)
FUN_0048c338((undefined2 *)out);                                        // install as current transform
```

`_DAT_006b7bec` is the shape instance's `+0x16` per-node world array (stride `0x20`, indexed by
transform id); `DAT_006b7c14` is the current object-to-view transform. `&DAT_006bb335` is a per-node
"already composed this frame" flag array, so a shape with several groups on one node composes once.

A negative transform id skips the composition and leaves the object-to-view transform standing.

`TSBasePart.Transform` at offset `+4` is the same field `Mech_TargetRelativeToPilot` (`0041ef14`) and
the cockpit eye resolve `CameraBoneId` through — one field, one meaning, geometry and named nodes
alike.

## Fleet shape

All 18 retail HERCs: geometry occupies **11 groups**, on transform ids **1-11**, out of 12 nodes
(13 for MONGOOSE and HEADHUNT). Transform 0 carries sequence root motion and never places geometry.

**No node in any retail HERC has a rotation in its rest pose.** Every entry
`ANAnimList.DefaultTransforms` points at has all three euler shorts zero, fleet-wide. Rotation is
something an *animated* node acquires; a rest pose is pure translation.

## HERCULAN Engine implementation

`DtsMeshBuilder.BuildSegments` produces one `MeshSegment` per transform id — the same triangles
`BuildRoot` produces, minus the baked rest-pose offset, so each segment's vertices are in its node's
own space. Coincident-twin removal runs shape-wide first, in the shared rest-pose space; a textured
poly and its flat-shaded twin always share a group, so the survivor is the same either way.

`WorldScale.ToRenderMatrix` turns a `Transform3` into the equivalent render matrix. The rotation is
**conjugated** through the axis map (`RenderToSim * M * SimToRender`), not copied: `ToRender` is a
change of basis, so a rotation about the simulation's Z is a rotation about render Y. The metre scale
cancels out of the conjugation and only reaches the translation.

`MissionScene.PosedTransformOf(mech, transformId)` is the port of `00476030`'s composition —
`ToRenderMatrix(thread.NodeTransform(id)) * ToRenderMatrix(mech.WorldTransform)` — and the host draws
one `SceneItem` per segment with it.

One deliberate deviation from the rigid path (`MissionScene.TransformOf`), and it is the simulation
being let through rather than approximated: the machine's own transform is
`MechObject.WorldTransform`, so lean over sloping ground applies. The rigid path has only a heading
rotation.

Neither path lifts a shape by its bounding box. A shape's origin is already its ground contact
point — see [`dgs-hd0-notes.md`](dgs-hd0-notes.md), "Shape origin".

A machine whose shape carries no `ANAnimList` has no thread to pose with and keeps the flat mesh.

### Verification

- `ToRender(t.TransformPoint(p))` vs. `Transform(ToRender(p), ToRenderMatrix(t))` over 500 random
  transforms and points: worst disagreement 4.8 mm, from Q14 quantisation.
- All 18 HERCs, segments posed at the thread's opening state vs. the flat mesh: every posed vertex
  lands on a flat vertex within 1 mm, zero mismatches, equal vertex counts. The segmented and baked
  paths are the same model at rest.
- OUTLAW at steady full throttle: leg nodes 5-10 swing metres per stride, torso node 11 swings
  0.30 m vertically — consistent with the 0.24-0.42 m eye rise measured independently.
