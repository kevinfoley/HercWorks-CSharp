using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Io.Transform.Dbsim;

namespace Herculan.Engine.Content;

/// <summary>
/// One herc's <c>vue\&lt;HERC&gt;.VUE</c> — where each cockpit view's window sits inside the cockpit
/// canvas, in device pixels. See docs/formats/cockpit-hud.md.
///
/// <para><b>The canvas is the mechanism behind the heads-down pan.</b> DBSIM's cockpit lives in a
/// virtual space taller than the screen — 320x480 in the low-res mode, 640x960 in the two 640x480
/// modes — and each view is a screen-sized window into it at that view's own origin.
/// <c>Sim_InitMissionSession</c> (<c>004614fc</c>) blits view 1's canopy art and then view 0's during
/// mission bring-up, so both are resident in the canvas at once; switching between them is a pure
/// scroll of the display window, never a redraw. That is why the transition can be animated at all,
/// and it is why this engine draws the forward and heads-down art as two quads at fixed canvas
/// offsets and moves the window rather than cross-fading two full-screen images.</para>
///
/// <para>Coordinates in the file are authored in the 320-wide space and shifted left by
/// <c>VideoMode_X/YCoordShift</c> at load — 0 in mode 0, 1 in the 640x480 modes. Herculan renders the
/// <c>hb&lt;n&gt;</c>/<c>hba\</c> hi-res assets throughout (see <see cref="CockpitArt"/>), so this
/// class applies shift <see cref="CoordShift"/> = 1 and reports device pixels: the heads-down view's
/// authored origin of (0,237) becomes (0,474).</para>
/// </summary>
public sealed class CockpitViewGeometry {
	/// <summary><c>VideoMode_YCoordShift</c>/<c>XCoordShift</c> for the 640x480 video modes.</summary>
	public const int CoordShift = 1;

	/// <summary>DBSIM's own view numbering: the forward view, canvas origin (0,0).</summary>
	public const int ForwardViewIndex = 0;

	/// <summary>DBSIM's own view numbering: the heads-down display, canvas origin (0,237) authored.</summary>
	public const int HeadsDownViewIndex = 1;

	/// <summary>
	/// The canvas origin every retail <c>.VUE</c> gives the heads-down view, in device pixels — used
	/// when the herc's own file is missing so a pan still runs the right distance. All nine player
	/// hercs author (0,237); none of them differs.
	/// </summary>
	public const int DefaultHeadsDownOriginY = 237 << CoordShift;

	/// <summary>Height in device pixels of the window each view shows into the canvas — the canopy art's own height.</summary>
	public const int ViewHeight = 480;

	/// <summary>Width in device pixels of that window, and of every <c>.HB&lt;n&gt;</c> frame.</summary>
	public const int ViewWidth = 640;

	private readonly Vue _vue;

	private CockpitViewGeometry(Vue vue) => _vue = vue;

	/// <summary>How many views the file declares (4 in every retail file).</summary>
	public int ViewCount => _vue.Entries?.Length ?? 0;

	/// <summary>
	/// The vertical distance the display window travels between the forward view and the heads-down
	/// view, in device pixels — the whole extent of the pan animation.
	/// </summary>
	public int HeadsDownTravelY =>
		CanvasOriginY(HeadsDownViewIndex) - CanvasOriginY(ForwardViewIndex);

	/// <summary>
	/// Loads a herc's view geometry, or null when <c>vue\&lt;HERC&gt;.VUE</c> is missing or does not
	/// parse — in which case the caller should fall back to
	/// <see cref="DefaultHeadsDownOriginY"/> rather than skipping the pan.
	/// </summary>
	public static CockpitViewGeometry? Load(GameContent content, string hercName) =>
		content.Read("vue", hercName + ".VUE") is { } bytes
			&& new VueTransformer().BytesToObject(bytes) is Vue vue
			&& vue.Entries is { Length: > 0 }
				? new CockpitViewGeometry(vue)
				: null;

	/// <summary>
	/// The projection centre every retail <c>.VUE</c> gives in x: the middle of the 320-wide view,
	/// used when the herc's file is missing. Device pixels, from the view window's left edge.
	/// </summary>
	public const int DefaultProjectionCenterX = 160 << CoordShift;

	/// <summary>
	/// The projection centre in y to fall back on, device pixels from the view window's top. 95 is
	/// APOCA's; the retail spread is 95 (APOCA, RAPTOR2) to 146 (RAZOR), so this is a middling guess
	/// and not a value any herc is guaranteed to want.
	/// </summary>
	public const int DefaultProjectionCenterY = 95 << CoordShift;

	/// <summary>
	/// Where this view's perspective is centred — <b>not</b> the middle of its viewport rect, which is
	/// the whole point of the field existing. In device pixels from the view window's top-left.
	///
	/// <para><c>FUN_0048c5c4</c> is the projection's last step: <c>screenX = x + centreX</c>,
	/// <c>screenY = centreY - y</c>. So this point is where the view axis lands — the vanishing point
	/// of anything running straight away from the eye, and the point the gunsight reticle is drawn
	/// over. For APOCA that is (160, 95) authored, 95 rows down a 240-row view rather than the 93 its
	/// 186-row 3D rect would put at its own middle, and 45 rows above where the middle of the full
	/// 240-row window would be.</para>
	///
	/// <para><b>The negation is the original's.</b> The file stores (-160, -95), and
	/// <c>CockpitView_ApplyViewState</c> (<c>00429e60</c>) installs the pair at the render context's
	/// <c>+0x220</c> with the view's canvas origin added, after which <c>FUN_0048c1d8</c>
	/// (<c>0048c1d8</c>) computes the centre as <c>viewportTopLeft - that</c>. With every retail
	/// viewport rect starting at (0,0), the whole chain collapses to negating the stored pair — and it
	/// collapses the same way for the two side glances, whose canvas origins of ±320 cancel against
	/// their own window origins. Each view's centre is therefore the same point in its own window, and
	/// every retail file gives all four views the same pair anyway.</para>
	/// </summary>
	public (int X, int Y) ProjectionCenter(int viewIndex) =>
		Entry(viewIndex) is { } e
			? (-e.CenterX << CoordShift, -e.CenterY << CoordShift)
			: (DefaultProjectionCenterX, DefaultProjectionCenterY);

	/// <summary>This view's canvas origin x in device pixels, or 0 when the view is not declared.</summary>
	public int CanvasOriginX(int viewIndex) => Entry(viewIndex) is { } e ? e.CanvasOriginX << CoordShift : 0;

	/// <summary>This view's canvas origin y in device pixels, or 0 when the view is not declared.</summary>
	public int CanvasOriginY(int viewIndex) => Entry(viewIndex) is { } e ? e.CanvasOriginY << CoordShift : 0;

	/// <summary>
	/// True when this view declares a non-empty 3D viewport rect. Every retail herc but RAZOR gives
	/// the heads-down view a zero-size rect, which is exactly why the heads-down display shows no live
	/// world behind its panels.
	/// </summary>
	public bool HasWorldViewport(int viewIndex) =>
		Entry(viewIndex) is { } e && e.ViewportX1 > e.ViewportX0 && e.ViewportY1 > e.ViewportY0;

	private Vue.Entry? Entry(int viewIndex) =>
		_vue.Entries is { } entries && viewIndex >= 0 && viewIndex < entries.Length ? entries[viewIndex] : null;
}
