# DTS node posing — how a shape's geometry follows its skeleton (DBSIM.EXE)

For a machine, two mechanisms rewrite the shape before any of this runs — the hardpoint splice and the per-frame LOD root pick. See [`mech-shape-drawing.md`](mech-shape-drawing.md).

Every geometry group in a DTS shape is drawn through the transform of the node it names, taken from the shape instance's per-node world array. That array is what the animation pipeline in [`mech-locomotion.md`](../simulation/mech-locomotion.md#keyframe-interpolation) writes, so posing geometry is the same mechanism as posing the cockpit eye — just applied to every node instead of one.

## The draw path

| Address | Role |
| --- | --- |
| `004758c8` | `TSGroup_RenderPolys` — DBSIM's counterpart to the VSHELL symbol of that name |
| `00476014` | Reads the group's own transform id and forwards it |
| `00476030` | Composes the node's world transform with the object-to-view one, and installs it |

`TSGroup_RenderPolys` binds the group's index/point/surface arrays to the poly-render globals, then walks `group+0x1c` calling each poly's vtable slot `+0x1c`. Before any of that it calls `00476014`, which is a one-line forwarder:

```c
FUN_00476030(group, out, (int)*(short *)(group + 4));   // group+4 == TSBasePart.Transform
```

`00476030` then, for a non-negative transform id:

```c
FUN_0047f914((short *)(id * 0x20 + _DAT_006b7bec), DAT_006b7c14, out);  // Concat(nodeWorld[id], objectToView)
FUN_0048c338((undefined2 *)out);                                        // install as current transform
```

`_DAT_006b7bec` is the shape instance's `+0x16` per-node world array (stride `0x20`, indexed by transform id); `DAT_006b7c14` is the current object-to-view transform. `&DAT_006bb335` is a per-node "already composed this frame" flag array, so a shape with several groups on one node composes once.

A negative transform id skips the composition and leaves the object-to-view transform standing.

`TSBasePart.Transform` at offset `+4` is the same field `Cockpit_TargetAnglesFromCameraBone` (`0041ef14`) and the cockpit eye resolve `CameraBoneId` through — one field, one meaning, geometry and named nodes alike.

## Fleet shape

All 18 retail HERCs: geometry occupies **11 groups**, on transform ids **1-11**, out of 12 nodes (13 for MONGOOSE and HEADHUNT). Transform 0 carries sequence root motion and never places geometry.

**No node in any retail HERC has a rotation in its rest pose.** Every entry `ANAnimList.DefaultTransforms` points at has all three euler shorts zero, fleet-wide. Rotation is something an *animated* node acquires; a rest pose is pure translation.

## Cyclic and one-shot sequences

An animation list holds two kinds of sequence, and the difference is the **chunk's class**, not a flag: `ANCyclicSequence` loops, plain `ANSequence` plays once. DBSIM reaches the frame step through the sequence object's own vtable, so one list mixes both freely.

| Slot | Cyclic | One-shot |
|---|---|---|
| `+0x20` next frame | `004786d8` — wraps to 0 past the last frame | `00478654` — **clamps**, returning the last frame forever |
| `+0x24` previous frame | `004786f8` — wraps to the last frame | `00478670` — holds at 0 |

The clamp is what makes a one-shot observable: `frame == nextFrame` is true only on a played-out non-cyclic sequence, and that equality is the sole end-of-sequence test in the simulation. See [`mech-locomotion.md`](../simulation/mech-locomotion.md#going-down).

**Every retail chassis carries exactly one one-shot**, and its own `AnimId_Death` names it: index 7 on the 18 bipeds, 2 on the PITBULL, 1 on the SPIDER. The RAZOR's list holds a single sequence and its `AnimId_Death` of 7 is out of range, which never bites because a flyer has no locomotion thread.
