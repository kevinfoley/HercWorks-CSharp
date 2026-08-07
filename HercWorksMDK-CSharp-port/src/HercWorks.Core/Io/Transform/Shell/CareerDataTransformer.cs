using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.CareerDataTransformer.</summary>
public class CareerDataTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new CareerMissions {
			FileName = "CAREER",
			Ext = FileType.Dat,
			Dir = FileType.Gam
		};

		int totalSectors = IndexShortLE();

		var sectors = new Dictionary<MissionSector, int[]>();

		for (int s = 0; s < totalSectors; s++) {
			var sec = MissionSector.GetById(IndexShortLE());
			var missions = new int[IndexShortLE()];

			for (int i = 0; i < missions.Length; i++) {
				missions[i] = IndexShortLE();
			}

			sectors[sec!] = missions;
		}
		data.Sectors = sectors;

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (CareerMissions)source!;

		using var outStream = new MemoryStream();

		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Write(WriteShortLE((short)data.Sectors!.Values.Count));

		foreach (var sector in data.Sectors.Keys) {
			Write(WriteShortLE(sector.Id));

			var missions = data.Sectors[sector];

			Write(WriteShortLE((short)missions.Length));

			for (int i = 0; i < missions.Length; i++) {
				Write(WriteShortLE((short)missions[i]));
			}
		}

		return outStream.ToArray();
	}
}
