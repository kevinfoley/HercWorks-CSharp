using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /DBSIM/fm/RAZOR.DAT and DBSIM/fm/SKIMMER.DAT — flight physics parameters.
/// Ported from org.hercworks.core.data.file.dbsim.FlightModel.
/// </summary>
public class FlightModel : DataFile {
	public short PitchRate { get; set; }
	public short RollRate { get; set; }
	public short RudderForce { get; set; }
	public short PitchForce { get; set; }
	public short RollForce { get; set; }
	public short ThrustFactor { get; set; }
	public short RollMax { get; set; }
	public short Unk22_val16 { get; set; } = 16;
	public short Unk26_5or6 { get; set; }
	public short RollFriction { get; set; }
	public int AltitudeMax { get; set; }
	public int Unk38_val6000 { get; set; } = 6000;
	public int AirSpeedMax { get; set; }
	public int AirSpeedMin { get; set; }
	public int RollAccel { get; set; }
}
