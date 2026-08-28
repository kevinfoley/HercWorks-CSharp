using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>
/// Ported from org.hercworks.core.io.transform.shell.HercsStartTransformer.
/// See KNOWN_ISSUES.md — the write path looks hardpoints up by the loop index (0..count-1)
/// rather than by the actual hardpoint ID stored as each dictionary key on read, which assumes
/// hardpoint IDs are always contiguous and zero-based.
/// </summary>
public class HercsStartTransformer : ByteTransformer<Hercs> {
	public override Hercs? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - warn empty array
			return null;
		}
		SetBytes(inputArray);

		var startHercs = new Hercs {
			Data = new Hercs.Entry[IndexShortLE()]
		};

		for (int i = 0; i < startHercs.Data.Length; i++) {
			var entry = startHercs.AddEntry();
			entry.Herc = new ShellHercData();

			entry.BayId = IndexShortLE();
			entry.Herc.HercId = IndexShortLE();
			entry.Herc.HealthRatio = IndexShortLE();
			entry.Herc.BuildCompleteLevel = IndexShortLE();

			short hardpointCount = IndexShortLE();
			entry.Herc.Hardpoints = new Dictionary<short, UiWeaponEntry>();

			for (int h = 0; h < hardpointCount; h++) {
				var item = new UiWeaponEntry();
				short hardpointId = IndexShortLE();
				item.ItemId = IndexShortLE();
				item.HealthPercent = IndexShortLE();
				item.MissileType = MissileType.GetById(IndexShortLE());
				entry.Herc.Hardpoints[hardpointId] = item;
			}
			startHercs.Data[i] = entry;
		}

		return startHercs;
	}

	public override byte[]? Write(Hercs hercs) {
		using var objectStream = new MemoryStream();

		void Emit(byte[] bytes) => objectStream.Write(bytes, 0, bytes.Length);

		Emit(WriteShortLE((short)hercs.Data!.Length));
		for (int i = 0; i < hercs.Data.Length; i++) {
			var entry = hercs.Data[i];

			Emit(WriteShortLE(entry.BayId));
			Emit(WriteShortLE(entry.Herc!.HercId));
			Emit(WriteShortLE(entry.Herc.HealthRatio));
			Emit(WriteShortLE(entry.Herc.BuildCompleteLevel));
			Emit(WriteShortLE((short)entry.Herc.Hardpoints!.Count));

			for (int h = 0; h < entry.Herc.Hardpoints.Count; h++) {
				var item = entry.Herc.Hardpoints.GetValueOrDefault((short)h);
				Emit(WriteShortLE((short)h));
				Emit(WriteShortLE(item!.ItemId));
				Emit(WriteShortLE(item.HealthPercent));
				Emit(WriteShortLE((short)item.MissileType!.Id));
			}
		}

		return objectStream.ToArray();
	}
}
