namespace Herculan.Engine.Content;

/// <summary>
/// Everything the front-window HUD's target indicator needs about the current selection, resolved
/// once a frame from the camera and the machine. See <see cref="TargetBox"/> for what is drawn from
/// it and where each number comes from in the original.
///
/// <para>The projection is done outside the renderer for the same reason DBSIM does it in the
/// widget's own update rather than its paint: it depends on the view, and the view is the host's.
/// It is the original's own projection, not the GL one — <c>screen = centre ± (v * focal / depth)</c>
/// against the herc's <c>.VUE</c> projection centre — which lands in the same place because the
/// camera's field of view is derived from that same focal length.</para>
/// </summary>
/// <param name="ScreenX">
/// Where the target's aim point projects, in cockpit device pixels from the canopy art's top-left —
/// the space <see cref="CockpitArt.GauToPixelScale"/> maps <c>.GAU</c> coordinates into. Meaningless
/// when <paramref name="InFront"/> is false.
/// </param>
/// <param name="ScreenY"><inheritdoc cref="ScreenX" path="/summary"/></param>
/// <param name="InFront">
/// Whether the aim point is in front of the view's near plane. The paint's own <c>bVar3</c>: false
/// suppresses the box entirely and sends the arrow to <see cref="TargetBox.BehindOffsetX"/>.
/// </param>
/// <param name="BehindToLeft">
/// Which way the arrow points for a target that is behind — the sign of the aim point's view-space
/// x, which is all the original keeps of a point it could not project.
/// </param>
/// <param name="ShapeRadius">The target's drawn radius in world units, which sizes the box.</param>
/// <param name="Distance">
/// Eye to aim point in world units, by the simulation's own approximate magnitude. The other half of
/// the box's size, and what the MFD prints after <c>DIST:</c>.
/// </param>
/// <param name="Locked">
/// <c>mech+0x9b</c> — whether the armed missile mount has lock. It picks the indicator's second set
/// of frames and the arrow's second colour, and nothing else.
/// </param>
public readonly record struct TargetIndicator(
	float ScreenX,
	float ScreenY,
	bool InFront,
	bool BehindToLeft,
	int ShapeRadius,
	int Distance,
	bool Locked);
