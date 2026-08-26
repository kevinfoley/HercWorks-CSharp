# `TSBitmapPart` billboards and `TSCellAnimPart` flipbooks (DBSIM.EXE)

Solved 2026-08-26; addresses are DBSIM virtual addresses. Ported in
`Herculan.Engine.Render.{DtsSpriteBuilder, SpriteRenderer}`.

The one `TSObject` render slot that puts pixels on screen with no polygon involved. It is what
`BULLETS.DTS` roots 2 and 3 (the three EMP cannons' rounds) and all twenty `EXPLOS.DTS` roots (every
impact effect, see [`../simulation/impact-effects.md`](../simulation/impact-effects.md)) are made of.

Which `.DBA` a sprite resolves against is the shape instance's own bound bank, the same binding a
mesh uses — see [`dts-texture-binding.md`](dts-texture-binding.md).

## Class identification

Via DBSIM's `g_TSObjectTypeRegistry` (`004a63c8`), not resemblance:

| Tag | Type | Constructor | Vtable | Render (`+0x1c`) |
|---|---|---|---|---|
| `0x00140013` | `TSBitmapPart` | `0048fa68` | `004a62d0` | `004762e8` |
| `0x0014000b` | `TSCellAnimPart` | `0048fee8` | `004a629c` | `004767e4` |

## `TSCellAnimPart_Render` (`004767e4`)

```
child = children[ cellFrames[AnimSequence] % childCount ];
child->vtable[+0x1c]();
```

**One child per frame, not a container.** `cellFrames` is the drawing shape instance's own
per-sequence `ushort` array, published to the global `DAT_006b7bf0` by whatever installs the
instance for drawing. Walking every child the way a `TSPartList` is walked stacks the whole animation
on top of itself.

Children need not be bitmaps: `BULLETS.DTS` root 8 (plasma) is a two-cell animation over real
`TSGroup` geometry.

## `TSBitmapPart_Render` (`004762e8`)

`BmpTag` (`part+0x10`) indexes the bound bank directly. The rest is a screen-space blit of a rotated,
scaled quad, built from four things:

**Scale.** `scale = (radius * 4 << focalShift) / depth`, where `radius` is `TSBasePart.Radius` and
`depth` is the part's centre in view space. Every dimension below is `Q8Multiply`d by it. Since the
projection maps a view-plane length `L` at depth `D` to `(L << focalShift) / D` pixels, the
projection constant cancels: **one bitmap pixel spans `radius / 64` world units**, whatever the field
of view is.

**Rotation.** The model origin and the model point `(0, 0, 0x800)` — a fixed distance up the model's
*own* Z axis — are both transformed and projected, and the quad is blitted rotated by the screen
angle between them. For a shot in flight that axis is the shot's frame, not the world's.

**Vertical squash.** Before projecting, the same axis's length surviving in the view plane is
measured (`Vec2_DistanceBetween` on the view-space x/z pair, range `0..0x800`). The drawn height is

```
height = cols + (rows - cols) * measured / 0x800
```

so it runs from the bitmap's *width* (axis pointing at the viewer) to its *height* (axis across the
view). A round puff therefore reads as a disc from overhead rather than collapsing to nothing. The
drawn width is always `cols`.

**Anchor.** The destination quad's corners are `(0,0), (w,0), (w,h), (0,h)`, each rotated and then
offset by the projected centre displaced by `-(OfsX, OfsY')` — same scale, same rotation — where
`OfsY' = OfsY * height / rows`. So the part's centre lands on bitmap pixel `(OfsX, OfsY')`.
`OfsX` is read signed (`*(char *)(part + 0x12)`), `OfsY` unsigned (`*(byte *)(part + 0x13)`).

In-memory bitmap header: `+4` = rows, `+6` = cols. Confirmed from the blit's own source quad
(`FUN_00488a8c` builds it as `(0,0), (p[1]-1, 0), (p[1]-1, p[0]-1), (0, p[0]-1)`).

Every retail bitmap part carries `Transform == -1` and a centre of the origin, so the node
composition `00476014` performs is the identity throughout retail data.

### The offset is a hotspot

All twenty `EXPLOS.DTS` roots carry an offset near half their frame's size — shape 6 is `(23, 22)`
against a 48x47 frame, shape 9 `(52, 53)` against 112x107 — which is what fixes the mechanism as
"anchor lands on this pixel" rather than "quad starts here".

`BULLETS.DTS` roots 2 and 3 are the exception and are authored oddly: all five parts read `(45, 45)`
against 40x30 frames, so the EMP puff draws up and to the left of the round rather than centred on
it. Retail behaves the same way; the port reproduces it.

## Engine port

`DtsSpriteBuilder` extracts one `SpriteQuad[]` per flipbook frame; `SpriteRenderer` draws them.
Deviations:

- **The quad is built in view space, not screen space.** Its four corners are placed in the plane
  parallel to the image plane at the sprite's depth, from a right/down basis derived from the
  projected model up axis, and handed to the projection alone. Perspective then reproduces the
  `1 / depth` scaling exactly rather than by interpolation. The squash and the anchor are the
  original's formulas verbatim; only the rotation's own perspective skew differs, which the original
  does not model either.
- **Alpha test, not a span skip.** Sprite banks decode palette index 0 to alpha 0
  (`SceneModelLibrary.LoadAtlas`'s `transparentIndex0`) and the fragment shader discards it. Only
  sprite banks are decoded that way; a mesh texture frame has no transparent index.
- **Depth test on, depth write off**, as [`../simulation/beam-visuals.md`](../simulation/beam-visuals.md)
  has it and for the same reason.
- **One draw call per sprite.** A frame holds a handful.
