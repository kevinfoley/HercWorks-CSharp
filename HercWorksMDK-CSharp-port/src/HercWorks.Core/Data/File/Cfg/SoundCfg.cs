using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// FILE - [ROOT]/DATA/SOUND.CFG. Ported from org.hercworks.core.data.file.cfg.SoundCfg.
/// </summary>
public class SoundCfg : DataFile {
	public enum SoundCfgLabel {
		Driver,
		Buffers,
		Rate,
		Width
	}

	public Dictionary<SoundCfgLabel, string> Values { get; set; } = new();

	public SoundCfg() : base("SOUND.CFG", "DATA/") {
		Values[SoundCfgLabel.Driver] = "";
		Values[SoundCfgLabel.Buffers] = "";
		Values[SoundCfgLabel.Rate] = "";
		Values[SoundCfgLabel.Width] = "";
	}
}
