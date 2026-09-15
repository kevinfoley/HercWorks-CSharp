using System.Globalization;
using System.Text;

using Herculan.Engine.Content;

namespace Herculan.Engine.Input;

/// <summary>
/// Which physical control on a real stick fills each field of a <see cref="JoystickReading"/> —
/// the one layer of joystick configuration the original does not have, and the reason a modern USB
/// device can drive a retail <c>prefs.cfg</c> without changing a byte of it.
///
/// <para><b>This is Herculan's invention, not vanilla behaviour.</b> Retail has no equivalent and no
/// need of one: <c>FUN_00477614</c> reads <c>joyGetPosEx</c>'s X, Y, Z and R in that fixed order,
/// falls back to a second stick's X and Y for the throttle and rudder when <c>JOYCAPS</c> reports no
/// <c>HASZ</c>/<c>HASR</c>, and never asks which physical control any of them is. That works because
/// a 1996 gameport stick had exactly those controls in exactly that order. A modern HOTAS does not:
/// its twist is as likely to be axis 2 as its throttle, it can carry six axes where the original
/// requests four, and it can carry thirty buttons where the original binds eight.</para>
///
/// <para>So the split is: <c>prefs.cfg</c> keeps owning what each control <i>does</i>, byte for byte
/// as retail wrote it, and this owns which piece of hardware each control <i>is</i>. Retail never
/// reads this file and does not know it exists; Herculan writes it beside <c>keyjoy.cfg</c> in the
/// same INI shape, and works from <see cref="ForDevice"/>'s defaults when it is absent.</para>
///
/// <para><b>What still cannot be expressed</b> is anything the twelve bytes have no room for: a
/// fifth axis, a ninth button, a second hat, or a hat diagonal. Those are limits of the retail
/// format, not of this map, and keeping the file compatible means keeping them.</para>
/// </summary>
public sealed class JoystickDeviceMap {
	/// <summary>Where Herculan keeps the file, relative to the game's <c>data</c> folder.</summary>
	public const string FileName = "herculan-joystick.cfg";

	private const string Section = "joystick";

	/// <summary>Means "no physical control fills this field".</summary>
	public const int Unmapped = -1;

	/// <summary>Which enumerated device to read, when more than one is attached.</summary>
	public int DeviceIndex { get; init; }

	/// <summary>The device axis feeding <see cref="JoystickReading.StickX"/>. Retail's X.</summary>
	public int StickXAxis { get; init; } = 0;

	/// <summary>The device axis feeding <see cref="JoystickReading.StickY"/>. Retail's Y.</summary>
	public int StickYAxis { get; init; } = 1;

	/// <summary>
	/// The device axis feeding <see cref="JoystickReading.Throttle"/>. Retail's Z, which on a modern
	/// stick is as often the twist — this is the setting most worth checking against real hardware.
	/// </summary>
	public int ThrottleAxis { get; init; } = 2;

	/// <summary>The device axis feeding <see cref="JoystickReading.Rudder"/>. Retail's R.</summary>
	public int RudderAxis { get; init; } = 3;

	/// <summary>Which hat to read, or <see cref="Unmapped"/> for none. Only one is bindable.</summary>
	public int HatIndex { get; init; } = 0;

	/// <summary>Per-axis sense, in <see cref="StickXAxis"/>..<see cref="RudderAxis"/> order.</summary>
	public bool InvertStickX { get; init; }

	/// <inheritdoc cref="InvertStickX"/>
	public bool InvertStickY { get; init; }

	/// <summary>
	/// The throttle's sense, and the one inversion with a right answer rather than a preference: the
	/// absolute-lever read is <c>|axis − 0x100| × 2</c>, so the machine idles at <c>+0x100</c> and
	/// opens fully at <c>−0x100</c>. Set this the way round that puts the lever's <b>closed</b> stop
	/// at the positive end, or the throttle will run backwards.
	/// </summary>
	public bool InvertThrottle { get; init; }

	/// <inheritdoc cref="InvertStickX"/>
	public bool InvertRudder { get; init; }

	/// <summary>
	/// The centre band, in <see cref="JoystickReading.RawFull"/> units. Defaults to the original's own
	/// <see cref="JoystickReading.Deadzone"/>; a modern stick with a tighter centre can take it down,
	/// and a worn one up. Note it also shapes the response curve, which is scaled to reach full
	/// deflection from wherever the band ends — see <see cref="JoystickReading.ApplyResponse"/>.
	/// </summary>
	public int Deadzone { get; init; } = JoystickReading.Deadzone;

	/// <summary>
	/// Whether the throttle lever is read centre-zero rather than end-to-end — <b>this engine's
	/// invention, and a divergence from retail</b>, which has only the end-to-end mode.
	///
	/// <para>Off, the retail reading: the whole travel runs idle-to-full in one direction and
	/// <c>CHANGE DIRECTION</c> flips which, because a 1996 gameport throttle had no centre detent.
	/// On: the middle of the travel is idle, forward of it is forward and aft of it is reverse, so
	/// one lever reaches both directions. Worth having on a HOTAS whose throttle has a detent, and
	/// unpleasant on one that does not, since idle then sits at no particular place.</para>
	///
	/// <para><see cref="InvertThrottle"/> still applies first and decides which half is forward.
	/// <c>CHANGE DIRECTION</c> keeps working and reverses the whole lever, which is of no use here
	/// but costs nothing to leave alone.</para>
	/// </summary>
	public bool BipolarThrottle { get; init; }

	/// <summary>
	/// Whether a hat diagonal resolves to its two components. <b>Off by default, which is retail:</b>
	/// <c>FUN_00477614</c> tests <c>dwPOV</c> against the four cardinals exactly and reports nothing
	/// for anything between them, so a diagonal does nothing at all in the original.
	/// </summary>
	public bool HatDiagonals { get; init; }

	/// <summary>
	/// Which device button drives each of the eight the format binds, logical BUTTON 1 first.
	/// <see cref="Unmapped"/> leaves a row dead, which is also what a device with too few buttons
	/// produces.
	/// </summary>
	public IReadOnlyList<int> Buttons { get; init; } = new[] { 0, 1, 2, 3, 4, 5, 6, 7 };

	/// <summary>
	/// A map for a device reporting these counts: retail's own X/Y/Z/R order, the first hat, and the
	/// first eight buttons, with anything the device does not have left unmapped. This is what runs
	/// when no file has been written, and it is right for a plain stick.
	/// </summary>
	public static JoystickDeviceMap ForDevice(int axisCount, int buttonCount, int hatCount,
			int deviceIndex = 0) {
		int Axis(int index) => index < axisCount ? index : Unmapped;

		var buttons = new int[JoystickCapabilities.MaxButtons];
		for (int i = 0; i < buttons.Length; i++) {
			buttons[i] = i < buttonCount ? i : Unmapped;
		}

		return new JoystickDeviceMap {
			DeviceIndex = deviceIndex,
			StickXAxis = Axis(0),
			StickYAxis = Axis(1),
			ThrottleAxis = Axis(2),
			RudderAxis = Axis(3),
			HatIndex = hatCount > 0 ? 0 : Unmapped,
			Buttons = buttons,
		};
	}

	/// <summary>
	/// What <c>Input_QueryCapabilities</c> would report for a device read through this map. The
	/// CONTROLS panel greys its rows against it, so an unmapped throttle greys the THROTTLE row
	/// exactly as a stick without one does in retail.
	/// </summary>
	public JoystickCapabilities Capabilities() => new(
		Present: true,
		ButtonCount: Buttons.Count(button => button != Unmapped),
		HasThrottle: ThrottleAxis != Unmapped,
		HasRudder: RudderAxis != Unmapped,
		HasHat: HatIndex != Unmapped);

	/// <summary>
	/// Builds one frame's <see cref="JoystickReading"/> from a device's raw state.
	/// </summary>
	/// <param name="axis">
	/// One device axis as a signed fraction of full travel, <c>-1</c> to <c>+1</c>, or 0 for an index
	/// the device does not have. Retail's own normalisation is
	/// <c>(raw &lt;&lt; 8) / 0xffff - 0x80</c> over a 16-bit reading, which is the same mapping in
	/// integers.
	/// </param>
	/// <param name="button">Whether a device button is held.</param>
	/// <param name="hat">The hat's direction, already resolved to the four cardinals or a diagonal.</param>
	public JoystickReading Read(Func<int, float> axis, Func<int, bool> button, JoystickHat hat) {
		int ReadAxis(int index, bool invert) {
			if (index == Unmapped) {
				return 0;
			}

			int raw = (int)MathF.Round(Math.Clamp(axis(index), -1f, 1f) * JoystickReading.RawFull);
			return JoystickReading.ApplyResponse(invert ? -raw : raw, Deadzone);
		}

		byte buttons = 0;
		for (int i = 0; i < Buttons.Count && i < JoystickCapabilities.MaxButtons; i++) {
			if (Buttons[i] != Unmapped && button(Buttons[i])) {
				buttons |= (byte)(1 << i);
			}
		}

		return new JoystickReading(
			ReadAxis(StickXAxis, InvertStickX),
			ReadAxis(StickYAxis, InvertStickY),
			ReadAxis(ThrottleAxis, InvertThrottle),
			ReadAxis(RudderAxis, InvertRudder),
			HatIndex == Unmapped ? JoystickHat.None : Resolve(hat),
			buttons);
	}

	/// <summary>
	/// Drops a diagonal unless <see cref="HatDiagonals"/> says otherwise, which is what the original
	/// does to anything that is not one of the four cardinal <c>dwPOV</c> values.
	/// </summary>
	private JoystickHat Resolve(JoystickHat hat) {
		if (HatDiagonals || hat == JoystickHat.None) {
			return hat;
		}

		return hat is JoystickHat.North or JoystickHat.South or JoystickHat.West or JoystickHat.East
			? hat
			: JoystickHat.None;
	}

	/// <summary>
	/// Reads the file, or returns null when it is absent — the caller then falls back to
	/// <see cref="ForDevice"/>, which is what an install that has never been configured wants.
	/// A setting the file omits keeps its default.
	/// </summary>
	public static JoystickDeviceMap? Load(string path) {
		Dictionary<string, string> values;
		try {
			values = KeyjoyConfig.ReadSection(File.ReadAllLines(path), Section);
		} catch (IOException) {
			return null;
		} catch (UnauthorizedAccessException) {
			return null;
		}

		if (values.Count == 0) {
			return null;
		}

		var defaults = new JoystickDeviceMap();
		int Int(string key, int fallback) => KeyjoyConfig.IntValue(values, key, fallback);
		bool Bool(string key, bool fallback) => KeyjoyConfig.BoolValue(values, key, fallback);

		var buttons = new int[JoystickCapabilities.MaxButtons];
		for (int i = 0; i < buttons.Length; i++) {
			buttons[i] = Int($"button{i + 1}", defaults.Buttons[i]);
		}

		return new JoystickDeviceMap {
			DeviceIndex = Int("device", defaults.DeviceIndex),
			StickXAxis = Int("stickx", defaults.StickXAxis),
			StickYAxis = Int("sticky", defaults.StickYAxis),
			ThrottleAxis = Int("throttle", defaults.ThrottleAxis),
			RudderAxis = Int("rudder", defaults.RudderAxis),
			HatIndex = Int("hat", defaults.HatIndex),
			InvertStickX = Bool("invertstickx", defaults.InvertStickX),
			InvertStickY = Bool("invertsticky", defaults.InvertStickY),
			InvertThrottle = Bool("invertthrottle", defaults.InvertThrottle),
			InvertRudder = Bool("invertrudder", defaults.InvertRudder),
			Deadzone = Math.Clamp(Int("deadzone", defaults.Deadzone), 0, JoystickReading.RawFull - 1),
			HatDiagonals = Bool("hatdiagonals", defaults.HatDiagonals),
			BipolarThrottle = Bool("bipolarthrottle", defaults.BipolarThrottle),
			Buttons = buttons,
		};
	}

	/// <summary>
	/// Writes the file, comments and all, in the shape <c>keyjoy.cfg</c> uses — the point being that a
	/// player can read it and edit it by hand, which is how retail expects its own side-car to be
	/// maintained.
	/// </summary>
	public void Save(string path) {
		var text = new StringBuilder();
		text.AppendLine("[Joystick]");
		text.AppendLine(";; Herculan's own file. The simulator's own prefs.cfg still says what each");
		text.AppendLine(";; control DOES; this says which piece of hardware each control IS, which is");
		text.AppendLine(";; the one thing a 1996 gameport stick did not need to be told.");
		text.AppendLine(";;");
		text.AppendLine(";; Axes and buttons are zero-based indices into what the device reports.");
		text.AppendLine(";; -1 leaves a control unmapped, which greys its row on the CONTROLS panel.");
		text.AppendLine();
		text.AppendLine(Line("Device", DeviceIndex, "which stick, when more than one is attached"));
		text.AppendLine();
		text.AppendLine(Line("StickX", StickXAxis, "left / right"));
		text.AppendLine(Line("StickY", StickYAxis, "fore / aft"));
		text.AppendLine(Line("Throttle", ThrottleAxis, "the lever -- often NOT axis 2 on a modern stick"));
		text.AppendLine(Line("Rudder", RudderAxis, "pedals, or the stick's twist"));
		text.AppendLine(Line("Hat", HatIndex, "the POV hat; only one is bindable"));
		text.AppendLine();
		text.AppendLine(Line("InvertStickX", InvertStickX));
		text.AppendLine(Line("InvertStickY", InvertStickY));
		text.AppendLine(Line("InvertThrottle", InvertThrottle,
			"the lever's CLOSED stop must read positive"));
		text.AppendLine(Line("InvertRudder", InvertRudder));
		text.AppendLine();
		text.AppendLine(Line("Deadzone", Deadzone,
			$"of {JoystickReading.RawFull}; the original uses {JoystickReading.Deadzone}"));
		text.AppendLine(Line("HatDiagonals", HatDiagonals,
			"the original drops them; on resolves one into its two cardinals"));
		text.AppendLine(Line("BipolarThrottle", BipolarThrottle,
			"on: centre of travel is idle, aft of it is reverse"));
		text.AppendLine();
		text.AppendLine(";; BUTTON 1-8 as the CONTROLS panel numbers them. The format binds eight and");
		text.AppendLine(";; no more, so a stick with more has to leave the rest out.");

		for (int i = 0; i < Buttons.Count && i < JoystickCapabilities.MaxButtons; i++) {
			text.AppendLine(Line($"Button{i + 1}", Buttons[i]));
		}

		File.WriteAllText(path, text.ToString());

		static string Line(string key, object value, string? comment = null) {
			string setting = value switch {
				bool flag => flag ? "on" : "off",
				_ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
			};

			string line = $"{key} = {setting}";
			return comment is null ? line : $"{line,-28} ;; {comment}";
		}
	}
}
