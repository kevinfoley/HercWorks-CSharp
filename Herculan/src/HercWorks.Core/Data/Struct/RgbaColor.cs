namespace HercWorks.Core.Data.Struct;

/// <summary>
/// A 32-bit RGBA color, standing in for System.Drawing.Color so HercWorks.Core has no
/// System.Drawing.Common dependency (which throws PlatformNotSupportedException on non-Windows
/// as of .NET 7+ — see docs/engine/planning.md's "Known technical debt" section). Matches
/// System.Drawing.Color's ARGB byte order and <see cref="ToArgb"/> bit layout exactly, so
/// consumers that convert back to System.Drawing.Color (e.g. HercWorks.UI, which is
/// Windows-only and free to keep using GDI+) get byte-identical results.
/// </summary>
public readonly record struct RgbaColor(byte A, byte R, byte G, byte B) {
	public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;

	public static RgbaColor FromArgb(int alpha, int red, int green, int blue) =>
		new((byte)alpha, (byte)red, (byte)green, (byte)blue);

	/// <summary>Same as System.Drawing.Color.FromArgb(r,g,b) — alpha defaults to fully opaque (255).</summary>
	public static RgbaColor FromArgb(int red, int green, int blue) =>
		new(255, (byte)red, (byte)green, (byte)blue);
}
