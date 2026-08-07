using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.TrainingHercsTransform.</summary>
public class TrainingHercsTransform : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - warn empty array
			return null;
		}
		SetBytes(inputArray);

		var training = new TrainingHercs {
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Gam,
			Data = new List<ShellHercData>()
		};

		while (Index < Bytes!.Length) {
			var herc = new ShellHercData();
			herc.HercId = IndexShortLE();
			herc.HealthRatio = IndexShortLE();
			herc.BuildCompleteLevel = IndexShortLE();
			herc.Hardpoints = new Dictionary<short, UiWeaponEntry>();
			int activeHardpoints = IndexShortLE();

			for (int h = 0; h < activeHardpoints; h++) {
				var entry = new UiWeaponEntry();
				short id = IndexShortLE();
				entry.ItemId = IndexShortLE();
				entry.HealthPercent = IndexShortLE();
				entry.MissileType = MissileType.GetById(IndexShortLE());
				herc.Hardpoints[id] = entry;
			}
			training.Data.Add(herc);
		}
		return training;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var training = (TrainingHercs)source!;
		using var outStream = new MemoryStream();

		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		foreach (var herc in training.Data!) {
			Write(WriteShortLE(herc.HercId));
			Write(WriteShortLE(herc.HealthRatio));
			Write(WriteShortLE(herc.BuildCompleteLevel));
			Write(WriteShortLE((short)herc.Hardpoints!.Count));

			foreach (var id in herc.Hardpoints.Keys) {
				var entry = herc.Hardpoints[id];
				Write(WriteShortLE(id));
				Write(WriteShortLE(entry.ItemId));
				Write(WriteShortLE(entry.HealthPercent));
				Write(WriteShortLE((short)entry.MissileType!.Id));
			}
		}

		return outStream.ToArray();
	}
}
