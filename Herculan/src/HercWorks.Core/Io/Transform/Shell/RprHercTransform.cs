using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.RprHercTransform.</summary>
public class RprHercTransform : ByteTransformer<RprHerc> {
	public override RprHerc? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length == 0) {
			// TODO - error for empty byte array
			return null;
		}
		SetBytes(inputArray);
		var repairHerc = new RprHerc();

		repairHerc.BodyImgTotal = IndexShortLE();

		var bodFrames = new Dictionary<short, UiImageDBA>();
		for (int i = 0; i < repairHerc.BodyImgTotal; i++) {
			var frame = new UiImageDBA();
			short id = IndexShortLE();
			frame.OriginX = IndexIntLE();
			frame.OriginY = IndexIntLE();
			frame.FrameId = IndexShortLE();
			frame.Flags = UiImageDBA.RFlag.Get(IndexShortLE());
			bodFrames[id] = frame;
		}
		repairHerc.BodyImages = bodFrames;

		var internals = new UiHardpointGraphic();
		internals.Id = IndexShortLE();
		internals.OriginX = IndexIntLE();
		internals.OriginY = IndexIntLE();
		internals.FrameId = IndexShortLE();
		internals.Flags = UiImageDBA.RFlag.Get(IndexShortLE());
		repairHerc.InternalImage = internals;

		short totalWeapons = IndexShortLE();
		repairHerc.TotalHardpoints = totalWeapons;
		var weapons = new Dictionary<short, UiHardpointGraphic[]>();
		for (int w = 0; w < totalWeapons; w++) {
			short itemId = IndexShortLE();
			short size = IndexShortLE();
			var points = new UiHardpointGraphic[size];
			for (int h = 0; h < size; h++) {
				var socket = new UiHardpointGraphic();
				socket.Id = IndexShortLE();
				socket.OriginX = IndexIntLE();
				socket.OriginY = IndexIntLE();
				socket.FrameId = IndexShortLE();
				socket.Flags = UiImageDBA.RFlag.Get(IndexShortLE());
				points[h] = socket;
			}
			weapons[itemId] = points;
		}
		repairHerc.WeaponHardpoints = weapons;

		return repairHerc;
	}

	public override byte[]? Write(RprHerc data) {
		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Emit(WriteShortLE(data.BodyImgTotal));
		foreach (var id in data.BodyImages!.Keys) {
			var frame = data.BodyImages[id];
			Emit(WriteShortLE(id));
			Emit(WriteUiImage(frame));
		}

		Emit(WriteShortLE(data.InternalImage!.Id));
		Emit(WriteIntLE(data.InternalImage.OriginX));
		Emit(WriteIntLE(data.InternalImage.OriginY));
		Emit(WriteShortLE(data.InternalImage.FrameId));
		Emit(WriteShortLE(data.InternalImage.Flags!.Val));

		Emit(WriteShortLE(data.TotalHardpoints));
		foreach (var id in data.WeaponHardpoints!.Keys) {
			var sockets = data.WeaponHardpoints[id];
			Emit(WriteShortLE(id));
			Emit(WriteShortLE((short)sockets.Length));
			for (int h = 0; h < sockets.Length; h++) {
				var img = sockets[h];
				Emit(WriteShortLE(img.Id));
				Emit(WriteIntLE(img.OriginX));
				Emit(WriteIntLE(img.OriginY));
				Emit(WriteShortLE(img.FrameId));
				Emit(WriteShortLE(img.Flags!.Val));
			}
		}
		return outStream.ToArray();
	}

	// Somehow there's a struct difference between these images
	// and the ones in the ARM_[herc].DAT files
	private byte[] WriteUiImage(UiImageDBA img) {
		using var bass = new MemoryStream();
		void Emit(byte[] bytes) => bass.Write(bytes, 0, bytes.Length);

		Emit(WriteIntLE(img.OriginX));
		Emit(WriteIntLE(img.OriginY));
		Emit(WriteShortLE(img.FrameId));
		Emit(WriteShortLE(img.Flags!.Val));

		return bass.ToArray();
	}
}
