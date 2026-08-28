using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.CareerDataTransformer.</summary>
public class CareerDataTransformer : ByteTransformer<CareerMissions> {
	public override CareerMissions? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);

		var data = new CareerMissions();

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

	public override byte[]? Write(CareerMissions data) {

		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Emit(WriteShortLE((short)data.Sectors!.Values.Count));

		foreach (var sector in data.Sectors.Keys) {
			Emit(WriteShortLE(sector.Id));

			var missions = data.Sectors[sector];

			Emit(WriteShortLE((short)missions.Length));

			for (int i = 0; i < missions.Length; i++) {
				Emit(WriteShortLE((short)missions[i]));
			}
		}

		return outStream.ToArray();
	}
}
