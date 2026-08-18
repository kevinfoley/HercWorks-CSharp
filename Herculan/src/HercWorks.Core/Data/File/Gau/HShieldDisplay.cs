using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The HUD shield-status display — the <c>ShieldsGauge</c> widget block at content offset 616.
///
/// <para>Layout confirmed from DBSIM's own constructor, <c>ShieldsGauge_Ctor</c> (<c>004434fc</c>),
/// which <c>Gau_ShieldDisplayWidget</c> (<c>00432454</c>) calls with this offset. The block is a
/// 16-byte header whose first two ints are an origin offset added to every rect below (all-zero in
/// every retail file), followed by four ordinary <c>x0,y0,x1,y1</c> rects at 632, 648, 664 and 680.
/// The constructor loops twice over shield facings, taking the box rect from <c>+16</c>/<c>+32</c>
/// and the numeric-readout rect from <c>+48</c>/<c>+64</c>:</para>
///
/// <list type="bullet">
/// <item><see cref="FrontBox"/> (632) and <see cref="RearBox"/> (648) — the two facings' meter
/// bodies, stacked. The nested concentric rings inside them are painted into the herc's canopy art
/// in palette indices 66-71; the widget lights them by rewriting those palette slots, so it draws no
/// geometry of its own (see <c>Herculan.Engine.Content.CockpitPalette.InstallShieldRamp</c>).</item>
/// <item><see cref="FrontLabel"/> (664) and <see cref="RearLabel"/> (680) — the rects the two
/// numeric readouts centre in. Retail shows "100" and "100": <c>FUN_00444a68</c> renders the
/// front/rear balance as <c>value * 200 &gt;&gt; 10</c> and its complement, so an even split reads
/// 100/100 out of a 200-point pool.</item>
/// </list>
///
/// <para>RAZOR is a confirmed exception: the manual (line 400) gives it a unique altimeter here
/// instead of a shield display, so its bytes still parse through this struct but do not mean the
/// same thing.</para>
///
/// <para>Rects are kept as raw int arrays so <see cref="Io.Transform.Dbsim.GauFileTransformer.ObjectToBytes"/>
/// round-trips byte-exact, with derived origin/size accessors for consumers that want a rectangle.
/// The retail files author every one of these with x0/y0 as the top-left, but the accessors still
/// normalise with min/max rather than assume it.</para>
/// </summary>
public class HShieldDisplay : WidgetBase {
	/// <summary>The block header at 616 — first two ints are an origin offset added to every rect. All-zero in retail data.</summary>
	public int[] HeaderRaw { get; set; } = new int[4];

	/// <summary>Front facing's meter body, content offset 632.</summary>
	public int[] FrontBoxRaw { get; set; } = new int[4];

	/// <summary>Rear facing's meter body, content offset 648.</summary>
	public int[] RearBoxRaw { get; set; } = new int[4];

	/// <summary>Front facing's numeric readout rect, content offset 664.</summary>
	public int[] FrontLabelRaw { get; set; } = new int[4];

	/// <summary>Rear facing's numeric readout rect, content offset 680.</summary>
	public int[] RearLabelRaw { get; set; } = new int[4];

	public PixelPoint FrontBox => RectOrigin(FrontBoxRaw);
	public PixelSize FrontBoxSize => RectSize(FrontBoxRaw);
	public PixelPoint RearBox => RectOrigin(RearBoxRaw);
	public PixelSize RearBoxSize => RectSize(RearBoxRaw);
	public PixelPoint FrontLabel => RectOrigin(FrontLabelRaw);
	public PixelSize FrontLabelSize => RectSize(FrontLabelRaw);
	public PixelPoint RearLabel => RectOrigin(RearLabelRaw);
	public PixelSize RearLabelSize => RectSize(RearLabelRaw);

	private static PixelPoint RectOrigin(int[] raw) => new(Math.Min(raw[0], raw[2]), Math.Min(raw[1], raw[3]));

	private static PixelSize RectSize(int[] raw) =>
		new(Math.Abs(raw[2] - raw[0]), Math.Abs(raw[3] - raw[1]));

	public override string ToString() {
		string name = HWidgetId != null ? HWidgetId.Name : GetType().Name;
		return $"{name} [front=({FrontBox.X},{FrontBox.Y}) {FrontBoxSize.Width}x{FrontBoxSize.Height}, " +
			$"rear=({RearBox.X},{RearBox.Y}) {RearBoxSize.Width}x{RearBoxSize.Height}, " +
			$"frontLabel=({FrontLabel.X},{FrontLabel.Y}), rearLabel=({RearLabel.X},{RearLabel.Y})]";
	}
}
