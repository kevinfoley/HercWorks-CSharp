using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Shell;

/// <summary>
/// Reads and writes <c>gam\arm_hots.dat</c> and <c>gam\rpr_hots.dat</c>, which share one format —
/// see <see cref="HardpointOverlayConfig"/> for it and for the evidence. Mirrors
/// <c>Squad_BuildScreen</c>'s own reader (<c>0043c1a0</c>): a count, then that many groups of a
/// chassis id, an area count and that many four-int32 rects.
///
/// <para>Ported from <c>org.hercworks.core.io.transform.shell.HardpointOverlayTransformer</c>.</para>
/// </summary>
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
				seg.X0 = IndexIntLE();
				seg.Y0 = IndexIntLE();
				seg.X1 = IndexIntLE();
				seg.Y1 = IndexIntLE();
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

				Emit(WriteIntLE(seg.X0));
				Emit(WriteIntLE(seg.Y0));
				Emit(WriteIntLE(seg.X1));
				Emit(WriteIntLE(seg.Y1));
			}
		}

		return outStream.ToArray();
	}
}
