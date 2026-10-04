using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Cfg;

/// <summary>
/// <c>DATA\SOUND.CFG</c> — the sound driver settings, an INI. See docs/retail/formats/audio.md,
/// "DATA\SOUND.CFG".
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
