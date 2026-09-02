namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The cockpit message port's box — content offset 1684, the last sixteen bytes of the file, read as
/// an (X1,Y1,X2,Y2) rect by the <c>.GAU</c> loader's caller (<c>FUN_00431bf8</c>) rather than by the
/// loader itself, coordinate-shifted into device pixels and handed to the port's constructor
/// (<c>FUN_004369a4</c>).
///
/// This is the scrolling one-line ticker the cockpit computer writes to. The rect immediately before
/// it, at 1668, is the second port of the same class — the pilot and squad channel at
/// <c>view+0x207</c>, which paints several wrapped lines instead — and is not surfaced here because
/// nothing reads it yet.
///
/// Position varies per herc in the file's own 320-wide space, but only vertically: every one of the
/// nine files is <c>100,y - 220,y+9</c>, a 120x9 box horizontally centred on the 320-wide screen.
/// RAZOR puts it at <c>y = 100</c>, low in that cockpit's differently laid out canopy; APOCA at 43;
/// the other seven at 34.
///
/// Read out of <see cref="GAUFile.Remainder"/> rather than carved out of it, exactly as
/// <see cref="HGunsightArea"/> and <see cref="HHudScanner"/> are: surfaced here and still written back
/// verbatim, so the byte-exact round-trip is untouched.
/// </summary>
public class HMessageTicker : WidgetBase {
}
