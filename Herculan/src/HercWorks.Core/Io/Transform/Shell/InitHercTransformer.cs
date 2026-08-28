using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.InitHercTransformer.</summary>
public class InitHercTransformer : ByteTransformer<InitHerc> {
	public override InitHerc? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}
		SetBytes(inputArray);

		var initHerc = new InitHerc {
			RawBytes = inputArray,
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

	public override byte[]? Write(InitHerc data) {
		var herc = data.Data!;
		using var output = new MemoryStream();

		void Emit(byte[] bytes) => output.Write(bytes, 0, bytes.Length);

		Emit(WriteShortLE(herc.HercId));
		Emit(WriteShortLE(herc.HealthRatio));
		Emit(WriteShortLE(herc.BuildCompleteLevel));
		Emit(WriteShortLE((short)herc.Hardpoints!.Count));

		foreach (var id in herc.Hardpoints.Keys) {
			var entry = herc.Hardpoints[id];
			Emit(WriteShortLE(id));
			Emit(WriteShortLE(entry.ItemId));
			Emit(WriteShortLE(entry.HealthPercent));
			Emit(WriteShortLE((short)entry.MissileType!.Id));
		}

		return output.ToArray();
	}
}
