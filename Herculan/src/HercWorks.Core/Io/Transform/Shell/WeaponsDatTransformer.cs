using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.WeaponsDatTransformer.</summary>
public class WeaponsDatTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - empty array warning
			return null;
		}

		SetBytes(inputArray);

		short totalWeapons = IndexShortLE();

		var data = new WeaponsDat(totalWeapons) {
			FileName = "WEAPONS",
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Gam
		};

		for (int i = 0; i < totalWeapons; i++) {
			var entry = data.AddEntry(i);
			entry.Id = IndexShortLE();
			entry.NameLen = IndexShortLE();
			entry.Name = IndexSegment(entry.NameLen);
			entry.SalvageCost = IndexShortLE();
			entry.StartUnlock = IndexByte();
			entry.AutobuildPriority = IndexShortLE();
		}

		data.StartWeaponTotal = IndexShortLE();
		data.StartingWeapons = new UiWeaponEntry[data.StartWeaponTotal];

		for (int i = 0; i < data.StartWeaponTotal; i++) {
			var item = new UiWeaponEntry();
			item.ItemId = IndexShortLE();
			item.HealthPercent = IndexShortLE();
			item.MissileType = MissileType.GetById(IndexShortLE());
			data.StartingWeapons[i] = item;
		}

		return data;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		using var objectBytes = new MemoryStream();

		var data = (WeaponsDat)source!;

		void Write(byte[] bytes) => objectBytes.Write(bytes, 0, bytes.Length);

		Write(WriteShortLE(data.TotalCount));
		for (int i = 0; i < data.TotalCount; i++) {
			var entry = data.Data[i];

			Write(WriteShortLE(entry.Id));
			Write(WriteShortLE(entry.NameLen));
			Write(entry.Name!);
			Write(WriteShortLE(entry.SalvageCost));
			objectBytes.WriteByte(entry.StartUnlock);
			Write(WriteShortLE(entry.AutobuildPriority));
		}

		Write(WriteShortLE(data.StartWeaponTotal));
		for (int i = 0; i < data.StartWeaponTotal; i++) {
			var item = data.StartingWeapons![i];
			Write(WriteShortLE(item.ItemId));
			Write(WriteShortLE(item.HealthPercent));
			Write(WriteShortLE((short)item.MissileType!.Id));
		}

		return objectBytes.ToArray();
	}
}
