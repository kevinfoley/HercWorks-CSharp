using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>Ported from org.hercworks.core.io.transform.shell.HardpointOverlayTransformer.</summary>
public class HardpointOverlayTransformer : ByteTransformer<HardpointOverlayConfig> {
	public override HardpointOverlayConfig? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO - error for empty byte array
			return null;
		}
		SetBytes(inputArray);

		var rprHercOverlay = new HardpointOverlayConfig();

		var entries = new HardpointOverlayConfig.Herc[IndexShortLE()];

		for (int i = 0; i < entries.Length; i++) {
			var entry = rprHercOverlay.NewEntry();

			entry.HercId = IndexShortLE();
			var coords = new HardpointOverlayConfig.Herc.OverlayArea[IndexShortLE()];

			for (int c = 0; c < coords.Length; c++) {
				var seg = entry.NewSegment();
				seg.Id = c;
				seg.X = IndexIntLE();
				seg.Y = IndexIntLE();
				seg.Width = IndexIntLE();
				seg.Height = IndexIntLE();
				coords[c] = seg;
			}
			entry.Areas = coords;
			entries[i] = entry;
		}
		rprHercOverlay.Entries = entries;

		return rprHercOverlay;
	}

	public override byte[]? Write(HardpointOverlayConfig data) {
		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Emit(WriteShortLE((short)data.Entries!.Length));

		for (int i = 0; i < data.Entries.Length; i++) {
			var entry = data.Entries[i];

			Emit(WriteShortLE(entry.HercId));
			Emit(WriteShortLE((short)entry.Areas!.Length));
			for (int c = 0; c < entry.Areas.Length; c++) {
				var seg = entry.Areas[c];

				Emit(WriteIntLE(seg.X));
				Emit(WriteIntLE(seg.Y));
				Emit(WriteIntLE(seg.Width));
				Emit(WriteIntLE(seg.Height));
			}
		}

		return outStream.ToArray();
	}
}
