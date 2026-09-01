using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>
/// Ported from org.hercworks.core.io.transform.shell.ArmHercTransformer.
/// <c>WriteUiImage</c> pattern-matches on the actual runtime type rather than taking a
/// <c>UiImageDBA</c>: the top/bottom herc images are <c>UiHardpointGraphic</c> instances carrying
/// real OutlineX/OutlineY, and a <c>UiImageDBA</c>-typed parameter can only see the base members,
/// which would put OriginX/OriginY in the slot where OutlineX/OutlineY belongs. (Always
/// UiHardpointGraphic in practice, since that's the only type this class ever constructs for
/// these fields) to write the real outline values, without changing ArmHerc.HercTopImg/HercBotImg's
/// declared UiImageDBA? type.
/// </summary>
public class ArmHercTransformer : ByteTransformer<ArmHerc> {
	public override ArmHerc? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length == 0) {
			// TODO - error for empty byte array
			return null;
		}
		SetBytes(inputArray);

		var armData = new ArmHerc();

		var topHercImg = new UiHardpointGraphic();
		armData.TopImgArrId = IndexShortLE();
		topHercImg.OriginX = IndexIntLE();
		topHercImg.OriginY = IndexIntLE();
		topHercImg.OutlineX = IndexIntLE(); // note: for some reason these are probably because all UIharpdoint images use the same struct.
		topHercImg.OutlineY = IndexIntLE(); // note: for some reason these are probably because all UIharpdoint images use the same struct.
		topHercImg.FrameId = IndexShortLE();
		topHercImg.Flags = UiImageDBA.RFlag.Get(IndexShortLE());
		armData.HercTopImg = topHercImg;

		var bottomHercImg = new UiHardpointGraphic();
		armData.BottomImgArrId = IndexShortLE();
		bottomHercImg.OriginX = IndexIntLE();
		bottomHercImg.OriginY = IndexIntLE();
		bottomHercImg.OutlineX = IndexIntLE(); // note: for some reason these are probably because all UIharpdoint images use the same struct.
		bottomHercImg.OutlineY = IndexIntLE(); // note: for some reason these are probably because all UIharpdoint images use the same struct.
		bottomHercImg.FrameId = IndexShortLE();
		bottomHercImg.Flags = UiImageDBA.RFlag.Get(IndexShortLE());
		armData.HercBotImg = bottomHercImg;

		armData.TotalWeapons = IndexShortLE();

		// Begun Weapon-Id-Hardpoint map
		var weaponHardpoints = new Dictionary<short, UiHardpointGraphic[]>();
		for (int i = 0; i < armData.TotalWeapons; i++) {
			short weaponId = IndexShortLE();
			short pointTotal = IndexShortLE();

			var graphics = new UiHardpointGraphic[pointTotal];

			for (int h = 0; h < pointTotal; h++) {
				var hardpoint = new UiHardpointGraphic();
				hardpoint.Id = IndexShortLE();
				hardpoint.OriginX = IndexIntLE();
				hardpoint.OriginY = IndexIntLE();
				hardpoint.OutlineX = IndexIntLE();
				hardpoint.OutlineY = IndexIntLE();
				hardpoint.FrameId = IndexShortLE();
				hardpoint.Flags = UiImageDBA.RFlag.Get(IndexShortLE());
				graphics[h] = hardpoint;
			}
			weaponHardpoints[weaponId] = graphics;
		}
		armData.WeaponHardpoints = weaponHardpoints;

		return armData;
	}

	public override byte[]? Write(ArmHerc data) {

		using var objectBytes = new MemoryStream();

		WriteToStream(objectBytes, WriteShortLE(data.TopImgArrId));
		WriteUiImage(data.HercTopImg!, objectBytes);

		WriteToStream(objectBytes, WriteShortLE(data.BottomImgArrId));
		WriteUiImage(data.HercBotImg!, objectBytes);

		WriteToStream(objectBytes, WriteShortLE(data.TotalWeapons));

		// Begun Weapon-Id-Hardpoint map
		foreach (var id in data.WeaponHardpoints!.Keys) {
			var items = data.WeaponHardpoints[id];

			WriteToStream(objectBytes, WriteShortLE(id));
			WriteToStream(objectBytes, WriteShortLE((short)items.Length));

			foreach (var graphic in items) {
				WriteToStream(objectBytes, WriteShortLE(graphic.Id));
				WriteUiHardpoint(graphic, objectBytes);
			}
		}

		return objectBytes.ToArray();
	}

	private void WriteUiImage(UiImageDBA img, MemoryStream targ) {
		WriteToStream(targ, WriteIntLE(img.OriginX));
		WriteToStream(targ, WriteIntLE(img.OriginY));

		if (img is UiHardpointGraphic hardpointImg) {
			WriteToStream(targ, WriteIntLE(hardpointImg.OutlineX));
			WriteToStream(targ, WriteIntLE(hardpointImg.OutlineY));
		} else {
			WriteToStream(targ, WriteIntLE(img.OriginX));
			WriteToStream(targ, WriteIntLE(img.OriginY));
		}

		WriteToStream(targ, WriteShortLE(img.FrameId));
		WriteToStream(targ, WriteShortLE(img.Flags!.Val));
	}

	private void WriteUiHardpoint(UiHardpointGraphic img, MemoryStream targ) {
		WriteToStream(targ, WriteIntLE(img.OriginX));
		WriteToStream(targ, WriteIntLE(img.OriginY));
		WriteToStream(targ, WriteIntLE(img.OutlineX));
		WriteToStream(targ, WriteIntLE(img.OutlineY));
		WriteToStream(targ, WriteShortLE(img.FrameId));
		WriteToStream(targ, WriteShortLE(img.Flags!.Val));
	}

	private static void WriteToStream(MemoryStream targ, byte[] bytes) {
		targ.Write(bytes, 0, bytes.Length);
	}
}
