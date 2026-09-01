using System.Diagnostics;

namespace HercWorks.Core.Data.File.Dts;

/// <summary>
/// <b>A superseded stand-in, kept because it is still live code.</b> A 13-entry guess table of RGB
/// approximations for a surface's <c>FrontColor</c>, from before the real mechanism was traced;
/// anything it has no entry for clamps to the cyan error colour.
///
/// <para>The real resolution is in docs/formats/dts-texture-binding.md, "Poly types and their colour
/// mechanisms": the value is a palette index (TSSolidPoly) or a shade-ramp number
/// (TSShadedPoly/TSGouraudPoly) resolved through the theater's <c>.RMP</c> and <c>.DPL</c>, which
/// <c>Herculan.Engine</c> implements. <c>HercWorks.UI.DtsGeometryBuilder</c> still colours every
/// surface through this table instead, so the WinForms model viewer's colours are not the game's;
/// do not treat them as evidence.</para>
///
/// Ported from org.hercworks.core.data.file.dts.DefaultShapeColors.
/// </summary>
public sealed class DefaultShapeColors {
	public static readonly DefaultShapeColors Error = new(-1, new[] { 0.0, 2.0, 3.0 });
	public static readonly DefaultShapeColors Black0 = new(0, new[] { 0.0, 0.0, 0.0 });
	public static readonly DefaultShapeColors Brown2 = new(2, new[] { 0.40, 0.23, 0.15 });
	public static readonly DefaultShapeColors Green4 = new(4, new[] { 0.29, 0.28, 0.188 });
	public static readonly DefaultShapeColors Green5 = new(5, new[] { 0.28, 0.31, 0.28 });
	public static readonly DefaultShapeColors Green6 = new(6, new[] { 0.26, 0.32, 0.26 });
	public static readonly DefaultShapeColors Gray8 = new(8, new[] { 0.83, 0.83, 0.83 });
	public static readonly DefaultShapeColors Gray9 = new(9, new[] { 0.9, 0.9, 0.9 });
	public static readonly DefaultShapeColors Unk10 = new(10, new[] { 0.2, 0.5, 2.0 });
	public static readonly DefaultShapeColors Gray12 = new(12, new[] { 0.75, 0.75, 0.75 });
	public static readonly DefaultShapeColors Unk18 = new(18, new[] { 0.5, 0.5, 1.0 });
	public static readonly DefaultShapeColors Unk99 = new(99, new[] { 1.0, 2.0, 0.0 });
	public static readonly DefaultShapeColors Unk103 = new(103, new[] { 0.0, 2.0, 0.0 });
	public static readonly DefaultShapeColors Gray200 = new(200, new[] { 0.14, 0.14, 0.14 });

	private static readonly IReadOnlyList<DefaultShapeColors> All = new[]
	{
		Error, Black0, Brown2, Green4, Green5, Green6, Gray8, Gray9, Unk10, Gray12, Unk18, Unk99,
		Unk103, Gray200
	};

	private readonly short _num;
	private readonly double[] _val;

	private DefaultShapeColors(short num, double[] val) {
		_num = num;
		_val = val;
	}

	public double[] Rgb() => _val;

	public static DefaultShapeColors Color(short num) {
		foreach (var dsc in All) {
			if (dsc._num == num) {
				return dsc;
			}
		}
		Debug.WriteLine("UNKNOWN COLOR: " + num);
		return Error;
	}
}
