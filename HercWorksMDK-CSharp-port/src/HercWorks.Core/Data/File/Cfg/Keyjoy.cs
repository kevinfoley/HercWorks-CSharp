using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// FILE - [ROOT]/DATA/KEYJOY.CFG. Ported from org.hercworks.core.data.file.cfg.Keyjoy.
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
