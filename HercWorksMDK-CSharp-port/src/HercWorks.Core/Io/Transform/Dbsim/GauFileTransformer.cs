using System.Drawing;
using HercWorks.Core.Data.File.Gau;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .GAU HUD-layout files (see <see cref="GAUFile"/> for the
/// format writeup). New: the Java source had a data model with a detailed doc-comment byte layout
/// but no transformer at all — this implements and verifies that layout against real retail data
/// for the portion that held up (offset 0-691 plus the confirmed <see cref="HMfdPanel"/> at 952 and
/// <see cref="HThrottle"/> at 1016), and preserves the rest as raw bytes rather than guessing past
/// where real bytes stopped lining up with a confirmed layout.
///
/// Worth noting for future work: the Java `GAUFile.java` doc comment turned out to already name
/// both `HMfdPanel`'s offset (`"952- PANEL\MFD"`) and `HThrottle`'s (`"1016- SLIDER\THROTTLE\"`)
/// exactly — confirmed independently in 2026-08-09 sessions via user screenshot measurements and
/// `.HB0` cockpit-art overlays before this correlation was checked. The same Java comment also
/// names offset 1088 (`"PANEL\NAVBAR"`), 1104 (`"INDICATOR\TORSO_TWIST"`), and 1136 (`"RETICLE"`)
/// as further unverified leads within <see cref="GAUFile.Remainder"/>.
///
/// <see cref="ObjectToBytes"/> round-trips byte-exact against all 9 real herc `.GAU` files despite
/// Remainder being undecoded — decoding isn't required to round-trip it, since it's captured and
/// written back verbatim.
/// </summary>
public class GauFileTransformer : ThreeSpaceByteTransformer {
	private const int WeaponSlotCount = 10;

	/// <summary>
	/// Bytes between the end of ShieldDisplay (offset 692) and the start of MfdPanel's rect (offset
	/// 952) — confirmed all-zero for every real file except a single leftover duplicate byte at
	/// offset 692 itself (mirrors ShieldDisplay's last decoded int, meaning unconfirmed — see
	/// GAUFile's class doc comment), so kept raw rather than hardcoded zero.
	/// </summary>
	private const int RemainderBeforeMfdPanelLength = 260;

	/// <summary>Bytes between the end of Throttle (offset 1064) and the start of TorsoTwist (offset 1104) — NOT fully decoded, see class doc comment.</summary>
	private const int RemainderBeforeTorsoTwistLength = 40;

	/// <summary>Bytes between the end of TorsoTwist (offset 1120) and the start of Reticle (offset 1136) — NOT decoded.</summary>
	private const int RemainderBeforeReticleLength = 16;

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

		gau.ShieldDisplay = ReadShieldDisplay();

		gau.RemainderBeforeMfdPanel = IndexSegment(RemainderBeforeMfdPanelLength);

		gau.MfdPanel = ReadRect(() => new HMfdPanel());

		Skip(48); // 3 more confirmed always-zero "null widget" slots, offset 968/984/1000.

		gau.Throttle = ReadThrottle();

		gau.RemainderBeforeTorsoTwist = IndexSegment(RemainderBeforeTorsoTwistLength);

		gau.TorsoTwist = ReadRect(() => new HTorsoTwist());

		gau.RemainderBeforeReticle = IndexSegment(RemainderBeforeReticleLength);

		gau.Reticle = new HReticle { Origin = new Point(IndexIntLE(), IndexIntLE()) };

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

	/// <summary>
	/// Reads the shield display's 4 slots (Unused, Divider, Bounds, Fill, in that on-disk order) —
	/// see <see cref="HShieldDisplay"/>'s doc comment for what each slot means and why they're kept
	/// as raw ints rather than sorted on read.
	/// </summary>
	private HShieldDisplay ReadShieldDisplay() {
		int[] unused = ReadShieldSlot();
		int[] divider = ReadShieldSlot();
		int[] bounds = ReadShieldSlot();
		int[] fill = ReadShieldSlot();

		var display = new HShieldDisplay {
			Unused = unused,
			DividerRaw = divider,
			BoundsRaw = bounds,
			FillRaw = fill,
		};

		int top = Math.Min(bounds[0], bounds[2]);
		int bottom = Math.Max(bounds[0], bounds[2]);
		display.Origin = new Point(bounds[1], top);
		display.Size = new Size(bounds[3] - bounds[1], bottom - top);

		return display;
	}

	private int[] ReadShieldSlot() => new[] { IndexIntLE(), IndexIntLE(), IndexIntLE(), IndexIntLE() };

	/// <summary>Reads the throttle's track rect (normal X1,Y1,X2,Y2 order) plus 4 detent points.</summary>
	private HThrottle ReadThrottle() {
		var throttle = ReadRect(() => new HThrottle());

		for (int i = 0; i < throttle.DetentPoints.Length; i++) {
			throttle.DetentPoints[i] = new Point(IndexIntLE(), IndexIntLE());
		}

		return throttle;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source is not GAUFile gau) {
			return null;
		}

		using var outStream = new MemoryStream();
		void Write(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		Write(WriteIntLE(gau.HudOrigin.X));
		Write(WriteIntLE(gau.HudOrigin.Y));
		Write(WriteIntLE(gau.HudScreenSize.Width));
		Write(WriteIntLE(gau.HudScreenSize.Height));
		Write(WriteIntLE(gau.WeaponListTotal));

		foreach (var weapon in gau.Weapons ?? Array.Empty<HWeaponPanelItem>()) {
			WriteRect(Write, weapon);
		}

		Write(new byte[288]); // confirmed always-zero padding, offset 180-467.
		Write(new byte[16]); // confirmed always (0,0,0,0) container rect at offset 468.

		WriteRect(Write, gau.ChainButton!);
		WriteRect(Write, gau.LinkButton!);
		WriteRect(Write, gau.AutoTrackButton!);

		Write(new byte[32]); // 2 null widget slots, offset 532/548.

		WriteRect(Write, gau.EnergyMeter!);

		Write(new byte[48]); // 3 null widget slots, offset 580/596/612.

		WriteShieldSlot(Write, gau.ShieldDisplay!.Unused);
		WriteShieldSlot(Write, gau.ShieldDisplay.DividerRaw);
		WriteShieldSlot(Write, gau.ShieldDisplay.BoundsRaw);
		WriteShieldSlot(Write, gau.ShieldDisplay.FillRaw);

		if (gau.RemainderBeforeMfdPanel != null) {
			Write(gau.RemainderBeforeMfdPanel);
		}

		WriteRect(Write, gau.MfdPanel!);

		Write(new byte[48]); // 3 null widget slots, offset 968/984/1000.

		WriteRect(Write, gau.Throttle!);
		foreach (var pt in gau.Throttle!.DetentPoints) {
			Write(WriteIntLE(pt.X));
			Write(WriteIntLE(pt.Y));
		}

		if (gau.RemainderBeforeTorsoTwist != null) {
			Write(gau.RemainderBeforeTorsoTwist);
		}

		WriteRect(Write, gau.TorsoTwist!);

		if (gau.RemainderBeforeReticle != null) {
			Write(gau.RemainderBeforeReticle);
		}

		Write(WriteIntLE(gau.Reticle!.Origin.X));
		Write(WriteIntLE(gau.Reticle.Origin.Y));

		if (gau.Remainder != null) {
			Write(gau.Remainder);
		}

		return outStream.ToArray();
	}

	/// <summary>Writes a widget's Origin/Size back out as a 16-byte (X1,Y1,X2,Y2) rectangle.</summary>
	private void WriteRect(Action<byte[]> write, WidgetBase widget) {
		write(WriteIntLE(widget.Origin.X));
		write(WriteIntLE(widget.Origin.Y));
		write(WriteIntLE(widget.Origin.X + widget.Size.Width));
		write(WriteIntLE(widget.Origin.Y + widget.Size.Height));
	}

	/// <summary>Writes a shield-display slot's 4 raw ints back out verbatim, in on-disk order.</summary>
	private void WriteShieldSlot(Action<byte[]> write, int[] raw) {
		foreach (int v in raw) {
			write(WriteIntLE(v));
		}
	}
}
