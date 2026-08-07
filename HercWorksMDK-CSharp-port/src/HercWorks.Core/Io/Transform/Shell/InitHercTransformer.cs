using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.InitHercTransformer.</summary>
public class InitHercTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		var initHerc = new InitHerc {
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Gam
		};

		var data = new ShellHercData();

		data.HercId = IndexShortLE();
		data.HealthRatio = IndexShortLE();
		data.BuildCompleteLevel = IndexShortLE();

		short hardpointCount = IndexShortLE();
		data.Hardpoints = new Dictionary<short, UiWeaponEntry>();

		for (short h = 0; h < hardpointCount; h += 1) {
			var entry = new UiWeaponEntry();
			short hardpointId = IndexShortLE();
			entry.ItemId = IndexShortLE();
			entry.HealthPercent = IndexShortLE();
			entry.MissileType = MissileType.GetById(IndexShortLE());
			data.Hardpoints[hardpointId] = entry;
		}

		initHerc.Data = data;
		return initHerc;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (InitHerc)source!;
		var herc = data.Data!;
		using var output = new MemoryStream();

		void Write(byte[] bytes) => output.Write(bytes, 0, bytes.Length);

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

		return output.ToArray();
	}
}
