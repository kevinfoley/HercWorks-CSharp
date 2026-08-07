namespace HercWorks.Core.Util;

/// <summary>
/// WARN: As far as can be tell, the binaries (VSHELL, DBSIM) must have some kind of logic or
/// lookup table for determining how a DPL palette is bound to any given DBA/DBM. So far there's
/// no observable external data for how the two file types get matched up.
///
/// What follows here is a rough way of making our own bindings with some fuzzy name checking.
/// Ported from org.hercworks.core.util.PaletteBinding.
/// </summary>
public sealed class PaletteBinding {
	private static PaletteBinding? _instance;

	private readonly List<PaletteBindingEntry> _bindings = new();

	private const bool IndexAlpha = true;
	private const bool NoAlphaPixel = false;

	private PaletteBinding() {
		// VSHELL
		_bindings.Add(new PaletteBindingEntry("ALPH.DPL", new List<string> { "ALPH2." }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("ALPHA.DPL", new List<string> { "ALPHA." }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("ARMING.DPL", new List<string>
		{
			"MT_3QTR", "_BOD.", "_INT.", "_OUT.", "_WEP.", "ARM_WEAP.", "ARM_HERC.", "ARM_WPNS.", "RPR_"
		}, IndexAlpha));

		_bindings.Add(new PaletteBindingEntry("BAY.DPL", new List<string>
		{
			"BAY2A_80", "BAY2A_81", "BAY2A_82", "BAY2A_83", "BAY2A_84"
		}, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("BR_ER.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BR_MN.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BR_W1.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BR_W2.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BR_W3.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BR_W4.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BR_W5.DPL", new List<string> { "" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("BRAV.DPL", new List<string> { "BRAV1." }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("BRAVO.DPL", new List<string> { "BRAVO." }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("CAM_ER.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("CAM_MOON.DPL", new List<string> { "" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("DB_ER.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DB_MOON.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DB_W1.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DB_W2.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DB_W3.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DB_W4.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DB_W5.DPL", new List<string> { "" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("DELT.DPL", new List<string> { "DELT1." }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("DELTA.DPL", new List<string> { "DELTA." }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("ESII.DPL", new List<string> { "" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("INTR_PT1.DPL", new List<string> { "" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("LUNA.DPL", new List<string> { "LUNA", "LUNA1" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("MAP.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("MAP_PAL1.DPL", new List<string> { "" }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("MAP_PAL2.DPL", new List<string> { "" }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("OMIC.DPL", new List<string> { "OMIC." }, NoAlphaPixel));
		_bindings.Add(new PaletteBindingEntry("OMICRON.DPL", new List<string> { "OMICRON." }, NoAlphaPixel));

		_bindings.Add(new PaletteBindingEntry("PALETTE.DPL", new List<string> { "MAINPIC1" }, IndexAlpha));

		// SIMVOL
	}

	public PaletteBindingEntry? GetPalette(string fileName) {
		PaletteBindingEntry? found = null;

		foreach (var binding in _bindings) {
			var fragments = binding.Files;
			if (fragments is { Count: > 0 }) {
				foreach (var stub in fragments) {
					if (fileName.Contains(stub, StringComparison.OrdinalIgnoreCase)) {
						found = binding;
						break;
					}
				}
			}
		}

		return found;
	}

	public static PaletteBinding Instance() {
		return _instance ??= new PaletteBinding();
	}
}
