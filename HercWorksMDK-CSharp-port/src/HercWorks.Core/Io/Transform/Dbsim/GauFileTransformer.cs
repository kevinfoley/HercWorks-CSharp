using System.Drawing;
using HercWorks.Core.Data.File.Gau;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .GAU HUD-layout files (see <see cref="GAUFile"/> for the
/// format writeup). New: the Java source had a data model with a detailed doc-comment byte layout
/// but no transformer at all — this implements and verifies that layout against real retail data
/// for the portion that held up (offset 0-627), and preserves the rest as a raw remainder rather
/// than guessing past where the original doc's offsets stopped matching real bytes.
///
/// Read-only past the confirmed region: Remainder is undecoded, so there's no confirmed structure
/// to write back for it.
/// </summary>
public class GauFileTransformer : ThreeSpaceByteTransformer {
	private const int WeaponSlotCount = 10;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var gau = new GAUFile {
			RawBytes = inputArray,
			Ext = FileType.Gau,

			HudOrigin = new Point(IndexIntLE(), IndexIntLE()),
			HudScreenSize = new Size(IndexIntLE(), IndexIntLE()),
			WeaponListTotal = IndexIntLE(),
		};

		var weapons = new HWeaponPanelItem[WeaponSlotCount];
		for (int i = 0; i < WeaponSlotCount; i++) {
			weapons[i] = ReadRect(() => new HWeaponPanelItem());
		}
		gau.Weapons = weapons;

		Skip(288); // confirmed always-zero padding, offset 180-467 — see class doc comment.

		Skip(16); // confirmed always (0,0,0,0) container rect at offset 468 — see class doc comment.

		gau.ChainButton = ReadRect(() => new HButtonBasic());
		gau.LinkButton = ReadRect(() => new HButtonBasic());
		gau.AutoTrackButton = ReadRect(() => new HButtonBasic());

		Skip(32); // 2 more confirmed always-zero "null widget" slots, offset 532/548.

		gau.EnergyMeter = ReadRect(() => new HMeter());

		Skip(48); // 3 more confirmed always-zero "null widget" slots, offset 580/596/612.

		gau.Remainder = IndexSegment(inputArray.Length - Index);

		return gau;
	}

	/// <summary>Reads one 16-byte (X1,Y1,X2,Y2) rectangle and converts it to the widget's inherited Origin/Size.</summary>
	private T ReadRect<T>(Func<T> factory) where T : WidgetBase {
		int x1 = IndexIntLE();
		int y1 = IndexIntLE();
		int x2 = IndexIntLE();
		int y2 = IndexIntLE();

		var widget = factory();
		widget.Origin = new Point(x1, y1);
		widget.Size = new Size(x2 - x1, y2 - y1);
		return widget;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		// TODO: not implemented — Remainder is undecoded (see class doc comment), so a byte-exact
		// round-trip isn't currently achievable.
		return null;
	}
}
