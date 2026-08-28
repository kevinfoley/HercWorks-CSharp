using HercWorks.Core.Data.Struct;

namespace HercWorks.Core.Data.File.Dat.Shell;

/// <summary>
/// FILE - /SHELL/GAM/CAREER.DAT
///   0 - UINT16 - related to MSN_GEN.CPP, always 6; 0/1 cause issues.
///   SEQ_0: 0_0 UINT16 sector ID, 0_2 UINT16 total missions in sector, SEQ_1: 1_0 UINT16 mission id.
/// Ported from org.hercworks.core.data.file.dat.shell.CareerMissions.
/// </summary>
public class CareerMissions {
	public Dictionary<MissionSector, int[]>? Sectors { get; set; }
}
