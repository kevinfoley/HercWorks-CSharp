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
public class HercsStartTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - warn empty array
			return null;
		}
		SetBytes(inputArray);

		var startHercs = new Hercs {
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Gam,
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

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var objectStream = new MemoryStream();

		var hercs = (Hercs)source!;

		void Write(byte[] bytes) => objectStream.Write(bytes, 0, bytes.Length);

		Write(WriteShortLE((short)hercs.Data!.Length));
		for (int i = 0; i < hercs.Data.Length; i++) {
			var entry = hercs.Data[i];

			Write(WriteShortLE(entry.BayId));
			Write(WriteShortLE(entry.Herc!.HercId));
			Write(WriteShortLE(entry.Herc.HealthRatio));
			Write(WriteShortLE(entry.Herc.BuildCompleteLevel));
			Write(WriteShortLE((short)entry.Herc.Hardpoints!.Count));

			for (int h = 0; h < entry.Herc.Hardpoints.Count; h++) {
				var item = entry.Herc.Hardpoints.GetValueOrDefault((short)h);
				Write(WriteShortLE((short)h));
				Write(WriteShortLE(item!.ItemId));
				Write(WriteShortLE(item.HealthPercent));
				Write(WriteShortLE((short)item.MissileType!.Id));
			}
		}

		return objectStream.ToArray();
	}
}
