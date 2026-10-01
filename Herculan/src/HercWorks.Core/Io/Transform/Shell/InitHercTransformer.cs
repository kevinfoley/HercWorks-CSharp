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
		data.BuildPercent = IndexShortLE();
		data.BuildMissionsLeft = IndexShortLE();

		short hardpointCount = IndexShortLE();
		data.Hardpoints = new Dictionary<short, UiWeaponEntry>();

		for (short h = 0; h < hardpointCount; h += 1) {
			var entry = new UiWeaponEntry();
			short hardpointId = IndexShortLE();
			entry.WeaponId = IndexShortLE();
			entry.Condition = IndexShortLE();
			entry.Guidance = MissileType.GetById(IndexShortLE());
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
		Emit(WriteShortLE(herc.BuildPercent));
		Emit(WriteShortLE(herc.BuildMissionsLeft));
		Emit(WriteShortLE((short)herc.Hardpoints!.Count));

		foreach (var id in herc.Hardpoints.Keys) {
			var entry = herc.Hardpoints[id];
			Emit(WriteShortLE(id));
			Emit(WriteShortLE(entry.WeaponId));
			Emit(WriteShortLE(entry.Condition));
			Emit(WriteShortLE((short)entry.Guidance!.Id));
		}

		return output.ToArray();
	}
}
