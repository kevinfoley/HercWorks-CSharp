using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>
/// Ported from org.hercworks.core.io.transform.shell.HercInfoTransformer.
/// FIXED — see KNOWN_ISSUES.md history: TotalHercs and each entry's HercId used to be read
/// little-endian (IndexShortLE) but written big-endian (WriteShort). Fixed write to use
/// WriteShortLE for both, matching read — confirmed correct in practice: the WinForms Herc Stats
/// editor already uses this read path successfully against real retail HERC_INF.DAT data.
/// </summary>
public class HercInfoTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - null array error
			return null;
		}

		SetBytes(inputArray);
		short totalHercs = IndexShortLE();

		var hercInfo = new HercInf(totalHercs) {
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Gam
		};

		for (short i = 0; i < totalHercs; i += 1) {
			var item = new HercInfEntry();
			item.HercId = IndexShortLE();
			item.Weight = IndexShortLE();
			item.Speed = IndexShortLE();
			item.HardpointTotal = IndexShortLE();
			item.SalvageReq = IndexShortLE();
			item.UnknownFlag = IndexShortLE();
			item.BuildMissionCount = IndexShortLE();
			item.FlagCampaignStart = IndexShortLE();
			hercInfo.Data[i] = item;
		}

		return hercInfo;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (HercInf)source!;

		using var objectBytes = new MemoryStream();

		void Write(byte[] bytes) => objectBytes.Write(bytes, 0, bytes.Length);

		Write(WriteShortLE(data.TotalHercs));

		foreach (var entry in data.Data) {
			Write(WriteShortLE(entry.HercId));
			Write(WriteShortLE(entry.Weight));
			Write(WriteShortLE(entry.Speed));
			Write(WriteShortLE(entry.HardpointTotal));
			Write(WriteShortLE(entry.SalvageReq));
			Write(WriteShortLE(entry.UnknownFlag));
			Write(WriteShortLE(entry.BuildMissionCount));
			Write(WriteShortLE(entry.FlagCampaignStart));
		}
		return objectBytes.ToArray();
	}
}
