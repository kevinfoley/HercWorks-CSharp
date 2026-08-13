using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/{herc}_DEB.DAT
///   0 - UINT16 - (unidentified)
/// Ported from org.hercworks.core.data.file.dat.sim.DebrisHerc.
/// </summary>
public class DebrisHerc : DataFile {
	public Entry[]? Data { get; set; }

	public Entry NewEntry() => new();

	public class Entry {
		public short Unk1Val { get; set; } // possible spacer
		public short SpawnDebrisFlag { get; set; }
		public short MeshGroupId { get; set; }
		public short Unk4_0A { get; set; }
		public short Unk5_03 { get; set; }
		public short[] ThrowDir { get; set; } = new short[3];
		public short Mass { get; set; }
	}
}
