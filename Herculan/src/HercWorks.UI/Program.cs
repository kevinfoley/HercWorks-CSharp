namespace HercWorks.UI;

internal static class Program {
	[STAThread]
	private static void Main() {
		ApplicationConfiguration.Initialize();

		// The "locate your install" prompt runs from MainForm.OnShown rather than here: an ownerless
		// dialog shown before any window exists leaves the app with nothing to hand the foreground
		// back to when it closes, so MainForm came up behind whatever launched it.
		Application.Run(new MainForm());
	}
}
