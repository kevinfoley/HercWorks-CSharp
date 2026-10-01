namespace HercWorks.Core.Data.Struct;

/// <summary>
/// A 2D integer size, standing in for System.Drawing.Size — see <see cref="PixelPoint"/>'s doc
/// comment for why. Field names match System.Drawing.Size's.
/// </summary>
public readonly record struct PixelSize(int Width, int Height);
