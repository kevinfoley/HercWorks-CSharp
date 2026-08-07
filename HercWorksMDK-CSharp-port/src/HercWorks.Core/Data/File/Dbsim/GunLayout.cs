using HercWorks.Vol;

namespace HercWorks.Core.Data.File.Dbsim;

/// <summary>
/// FILE - /SIMVOL0/GL/{herc}.GL — 'Gun Layout'. Initially mapped by: crow!
///   0 - UINT16 - total weapon count
///   Weapon entries (26 bytes each): BoneID, two unknowns (0xFF), weapon orientation (top/under/
///   left/right/invisible), firing-chain position, several unknown Int16s, mount point offset
///   X/Y/Z, unknown Int8, weapon ID, unknown Int16 (usually divisible by 1000).
/// Ported from org.hercworks.core.data.file.dbsim.GunLayout.
/// </summary>
public class GunLayout : DataFile {
	public short TotalGuns { get; set; }
	public HardpointEntry[]? Hardpoints { get; set; }

	public GunLayout() { }

	public GunLayout(short total) {
		TotalGuns = total;
		Hardpoints = new HardpointEntry[total];
	}

	public HardpointEntry NewEntry() => new();

	public class HardpointEntry {
		public short BoneId { get; set; }
		public short Unk1_val { get; set; }
		public short Unk2_val { get; set; }

		/// <summary>0 = on top, 1 = underneath, 2 = left side, 3 = right side, 4 = invisible.</summary>
		public byte AngleDirOption { get; set; }

		/// <summary>0 is shown as Weapon 1 in HUD.</summary>
		public byte FireChainNumber { get; set; }

		public short Unk3_0or_Neg5000 { get; set; }
		public short Unk4_0or_5000 { get; set; }
		public short Unk5_Neg8000 { get; set; }
		public short Unk6_16000 { get; set; }
		public short[] Offset { get; set; } = new short[3];
		public byte Unk7_val { get; set; }

		/// <summary>Same order as in VSHELL, but starting at 0, not 2.</summary>
		public byte HardpointId { get; set; }

		public short Unk8_val { get; set; }
	}
}
