namespace HercWorks.Core.Data.Struct;

/// <summary>
/// A 2D integer point, standing in for System.Drawing.Point so HercWorks.Core has no
/// System.Drawing.Common dependency (which throws PlatformNotSupportedException on non-Windows
/// as of .NET 7+ — see docs/engine/planning.md's "Known technical debt" section). Field names
/// match System.Drawing.Point's for a mechanical migration at call sites.
/// </summary>
public readonly record struct PixelPoint(int X, int Y);
