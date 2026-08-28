namespace HercWorks.Core.Data.File.Dat.Sim;

/// <summary>
/// FILE - /DBSIM/DAT/BEAM.DAT
///   0 - UINT16 - total beam count
///   SEQ0: width, color index id (only active on ELF weapons for some reason), BEAMTEX.DBA frame
///   number.
/// Stock beam order maps to ProjectileData "missile_id" when "type" == "BEAM":
///   0 PBW I, 1 ELF I, 2 ?, 3 LAS100, 4 LAS200/LAS400, 5 LAS300/LAS500, 6 PBW II, 7 ELF II,
///   8 ???, 9 ???
/// Ported from org.hercworks.core.data.file.dat.sim.BeamData.
/// </summary>
public class BeamData {
	public short Total { get; set; }
	public Entry[]? Data { get; set; }

	public BeamData() { }

	public BeamData(short total) {
		Total = total;
		Data = new Entry[total];
	}

	public Entry NewEntry(short width, short colorId, short dbaFrameNum) => new(width, colorId, dbaFrameNum);

	public class Entry {
		public short Width { get; set; }
		public short ColorId { get; set; }
		public short DBAFrameNum { get; set; }

		public Entry(short width, short colorId, short dbaFrameNum) {
			Width = width;
			ColorId = colorId;
			DBAFrameNum = dbaFrameNum;
		}
	}
}
