using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.ArmWeapTransformer.</summary>
public class ArmWeapTransformer : ByteTransformer<ArmWeap> {
	public override ArmWeap? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			return null;
		}

		SetBytes(inputArray);
		short count = IndexShortLE();
		var armWeap = new ArmWeap(count) {
			TotalWeapons = count
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

	public override byte[]? Write(ArmWeap data) {

		using var objectBytes = new MemoryStream();

		void Emit(byte[] bytes) => objectBytes.Write(bytes, 0, bytes.Length);

		Emit(WriteShortLE(data.TotalWeapons));

		for (int i = 0; i < data.TotalWeapons; i++) {
			var icon = data.Entries![i];
			Emit(WriteShortLE(icon.Id));
			Emit(WriteIntLE(icon.OriginX));
			Emit(WriteIntLE(icon.OriginY));
			Emit(WriteShortLE(icon.FrameId));
		}

		Emit(WriteShortLE(data.TotalSecondList));

		for (int i = 0; i < data.TotalSecondList; i++) {
			var icon = data.Secondary![i];
			Emit(WriteShortLE(icon.Id));
			Emit(WriteIntLE(icon.OriginX));
			Emit(WriteIntLE(icon.OriginY));
			Emit(WriteShortLE(icon.FrameId));
		}

		return objectBytes.ToArray();
	}
}
