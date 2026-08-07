using HercWorks.Vol;
using System.Numerics;

namespace HercWorks.Core.Data.File.Msn.Script;

/// <summary>
/// FILE - /DATA/SCript.DAT — somehow a parsed version of the MSN file found in /zone.vol/msn/.
///   0 - UINT16 World Id num, 2 - UINT16 ZonesXXX.dat number, 4-18 - UINT16 unknown values,
///   20 - UINT16 counter, SEQ_0: possible UINT32 coords — player spawn (X/Y/Z), then waypoint
///   coords (X/Y/Z) repeating.
/// Ported from org.hercworks.core.data.file.msn.script.ScriptDat. Apache Commons Math's
/// Vector3D maps to System.Numerics.Vector3.
/// </summary>
public class ScriptDat : DataFile {
	public short WorldId { get; set; }
	public short ZoneId { get; set; }

	public short Unk1 { get; set; }
	public short Unk2 { get; set; }
	public short Unk3 { get; set; }
	public short Unk4 { get; set; }
	public short Unk5 { get; set; }
	public short Unk6 { get; set; }
	public short Unk7 { get; set; }
	public short Unk8 { get; set; }

	/// <summary>With a UINT16 counter.</summary>
	public Vector3[]? EntityOrigins { get; set; }
}
