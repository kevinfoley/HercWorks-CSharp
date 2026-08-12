namespace HercWorks.Core.Data.Struct;

/// <summary>
/// A 2D integer size, standing in for System.Drawing.Size — see <see cref="PixelPoint"/>'s doc
/// comment for why. Field names match System.Drawing.Size's for a mechanical migration at call
/// sites.
/// </summary>
public readonly record struct PixelSize(int Width, int Height);
