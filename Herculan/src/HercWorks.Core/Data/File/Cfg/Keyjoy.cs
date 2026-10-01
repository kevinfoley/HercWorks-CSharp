using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// <c>DATA\KEYJOY.CFG</c> — four INI keys under <c>[Keyjoy]</c>, each inverting one control axis when
/// set to <c>Reverse</c>. See docs/formats/joystick-input.md, "data\keyjoy.cfg".
/// </summary>
public class Keyjoy : DataFile {
	public enum KeyJoyLabel {
		Tilt,
		Backturn,
		Missile,
		Rudder
	}

	public Dictionary<KeyJoyLabel, string> Values { get; set; } = new();

	public Keyjoy() : base("KEYJOY.CFG", "DATA/") {
		Values[KeyJoyLabel.Tilt] = "";
		Values[KeyJoyLabel.Backturn] = "";
		Values[KeyJoyLabel.Missile] = "";
		Values[KeyJoyLabel.Rudder] = "";
	}
}
