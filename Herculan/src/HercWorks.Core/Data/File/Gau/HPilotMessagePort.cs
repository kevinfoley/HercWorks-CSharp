namespace HercWorks.Core.Data.File.Gau;

/// <summary>
/// The pilot and squad channel's message box — content offset 1668, the sixteen bytes immediately
/// before <see cref="HMessageTicker"/>, read as an (X1,Y1,X2,Y2) rect by the <c>.GAU</c> loader's
/// caller (<c>Gau_BuildCockpitWidgets</c>, <c>00431bf8</c>) rather than by the loader itself, coordinate-shifted into device
/// pixels and handed to the port's constructor (<c>MessagePort_Ctor</c>, <c>004369a4</c>).
///
/// Same class as the ticker, second instance, at <c>view+0x207</c>. Only the rect's <b>vertical</b>
/// half survives into the drawn box: both of that port's paints recompute the left and right edges
/// every frame, centring a box of the measured text's own width on the screen, so the authored x
/// pair is overwritten before anything is drawn with it.
///
/// Read out of <see cref="GAUFile.Remainder"/> rather than carved out of it, exactly as
/// <see cref="HGunsightArea"/>, <see cref="HHudScanner"/> and <see cref="HMessageTicker"/> are:
/// surfaced here and still written back verbatim, so the byte-exact round-trip is untouched.
/// </summary>
public class HPilotMessagePort : WidgetBase {
	/// <summary>
	/// Content offset 1664, the <c>int32</c> immediately before the rect: how far the box is raised,
	/// both edges, in a training mission. <c>Gau_BuildCockpitWidgets</c> (<c>00431bf8</c>) subtracts it (coordinate-shifted) only
	/// on the arm that builds the training port, which grows downward by eleven lines of text and so
	/// needs the room. 34-85 across the retail files, 0 for RAZOR.
	/// </summary>
	public int TrainingLift { get; set; }
}
