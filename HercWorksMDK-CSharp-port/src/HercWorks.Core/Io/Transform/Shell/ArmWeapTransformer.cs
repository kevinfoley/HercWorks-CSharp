using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.ArmWeapTransformer.</summary>
public class ArmWeapTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);
		short count = IndexShortLE();
		var armWeap = new ArmWeap(count) {
			TotalWeapons = count,
			RawBytes = inputArray,
			Ext = FileType.Dat,
			Dir = FileType.Gam,
			FileName = "ARM_WEAP"
		};

		for (int i = 0; i < armWeap.TotalWeapons; i++) {
			var icon = new UiHardpointGraphic();
			icon.Id = IndexShortLE();
			icon.OriginX = IndexIntLE();
			icon.OriginY = IndexIntLE();
			icon.FrameId = IndexShortLE();
			icon.Flags = UiImageDBA.RFlag.Normal;
			armWeap.Entries![i] = icon;
		}

		armWeap.TotalSecondList = IndexShortLE();
		var secondList = new UiHardpointGraphic[armWeap.TotalSecondList];

		for (int i = 0; i < armWeap.TotalSecondList; i++) {
			var icon = new UiHardpointGraphic();
			icon.Id = IndexShortLE();
			icon.OriginX = IndexIntLE();
			icon.OriginY = IndexIntLE();
			icon.FrameId = IndexShortLE();
			icon.Flags = UiImageDBA.RFlag.Normal;
			secondList[i] = icon;
		}
		armWeap.Secondary = secondList;

		return armWeap;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		var data = (ArmWeap)source!;

		using var objectBytes = new MemoryStream();

		void Write(byte[] bytes) => objectBytes.Write(bytes, 0, bytes.Length);

		Write(WriteShortLE(data.TotalWeapons));

		for (int i = 0; i < data.TotalWeapons; i++) {
			var icon = data.Entries![i];
			Write(WriteShortLE(icon.Id));
			Write(WriteIntLE(icon.OriginX));
			Write(WriteIntLE(icon.OriginY));
			Write(WriteShortLE(icon.FrameId));
		}

		Write(WriteShortLE(data.TotalSecondList));

		for (int i = 0; i < data.TotalSecondList; i++) {
			var icon = data.Secondary![i];
			Write(WriteShortLE(icon.Id));
			Write(WriteIntLE(icon.OriginX));
			Write(WriteIntLE(icon.OriginY));
			Write(WriteShortLE(icon.FrameId));
		}

		return objectBytes.ToArray();
	}
}
