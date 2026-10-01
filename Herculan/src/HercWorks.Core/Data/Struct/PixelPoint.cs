namespace HercWorks.Core.Data.Struct;

/// <summary>
/// A 2D integer point, standing in for System.Drawing.Point so HercWorks.Core has no
/// System.Drawing.Common dependency (which throws PlatformNotSupportedException on non-Windows
/// as of .NET 7+). Field names match System.Drawing.Point's.
/// </summary>
public readonly record struct PixelPoint(int X, int Y);
