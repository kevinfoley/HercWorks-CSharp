namespace Herculan.Engine.Content;

/// <summary>
/// <c>data\prefs.cfg</c>, the simulator's option array — what the [F12] preferences panel shows and
/// edits.
///
/// <para><b>The file is the array.</b> <c>Prefs_LoadOptions</c> (<c>00459754</c>) memsets
/// <c>DAT_004d1fbc</c> to zero for <c>0x36</c> bytes and then reads the file straight over it, with
/// no parse at all, so an option's index is its byte offset and a retail <c>prefs.cfg</c> is 54
/// bytes. <c>Prefs_SetOption</c> (<c>0045993c</c>) writes one byte by that index and calls the
/// option's own handler from the parallel table at <c>DAT_004d2060</c>.</para>
///
/// <para>Only the options the preferences panel puts on screen are named here; the remaining bytes
/// are read and carried, not interpreted. The panel's own reader is
/// <c>FUN_004571f4</c>, which maps a byte to one of the <c>PRF_ALRT.STR</c> captions —
/// see <see cref="PreferencesPanel"/>.</para>
/// </summary>
public sealed class SimulatorPreferences {
	/// <summary>Where the simulator keeps the file, relative to the game's <c>data</c> folder.</summary>
	public const string FileName = "prefs.cfg";

	/// <summary>How many bytes the simulator reads, and so how long a usable file is.</summary>
	public const int Length = 0x36;

	private readonly byte[] _options;

	private SimulatorPreferences(byte[] options, string? sourceDirectory = null) {
		_options = options;
		SourceDirectory = sourceDirectory;
	}

	/// <summary>
	/// The <c>data</c> folder this was read from, or null when it was not read from one. A
	/// <see cref="Save"/> goes back where it came from, as the original's does — it keeps the one path
	/// buffer at <c>DAT_0049e844</c> that <c>Prefs_LoadOptions</c> reads through.
	/// </summary>
	public string? SourceDirectory { get; }

	/// <summary>
	/// Whether <see cref="Save"/> may touch the file. On by default; the host clears it for
	/// <c>--no-write-prefs</c>. Nothing in the original corresponds — retail has no reason to doubt
	/// that it owns its own install.
	/// </summary>
	public bool SaveEnabled { get; set; } = true;

	/// <summary>MUSIC — CD audio on or off.</summary>
	public const int MusicOption = 0;

	/// <summary>SOUNDS — the effect mixer on or off.</summary>
	public const int SoundsOption = 1;

	/// <summary>PILOT MESSAGE, <c>DAT_004d1fbe</c> — the pilot and squad channel's two halves.</summary>
	public const int PilotMessageOption = 2;

	/// <summary>COMPUTER MESSAGE, <c>DAT_004d1fbf</c> — the computer ticker's two halves.</summary>
	public const int ComputerMessageOption = 3;

	/// <summary>TERRAIN DISTANCE, <c>DAT_004d1fc3</c> — the draw radius, see <see cref="Terrain.TerrainDetail"/>.</summary>
	public const int TerrainDistanceOption = 7;

	/// <summary>TERRAIN TEXTURE, <c>DAT_004d1fc4</c>.</summary>
	public const int TerrainTextureOption = 8;

	/// <summary>HERC DETAIL, <c>DAT_004d1fc5</c>.</summary>
	public const int HercDetailOption = 9;

	/// <summary>STRUCTURE DETAIL, <c>DAT_004d1fc6</c>.</summary>
	public const int StructureDetailOption = 10;

	/// <summary>EFFECTS DETAIL, <c>Sound_DetailSetting</c> (<c>004d1fc7</c>).</summary>
	public const int EffectsDetailOption = 11;

	/// <summary>
	/// Where the [F12] → CONTROLS panel's twelve options start for a walking HERC — <c>DAT_004d25fb</c>
	/// as <c>Sim_InitMissionSession</c> (<c>004614fc</c>) sets it, and the value
	/// <c>PreferencesManager_Reset</c> (<c>0045cad8</c>) starts it on.
	/// </summary>
	public const int HercControlsBase = 0x0d;

	/// <summary>
	/// And where they start for the RAZOR, which has a second twelve-byte block of its own. The two
	/// sets are independent: rebinding in one machine does not disturb the other's.
	/// </summary>
	public const int RazorControlsBase = 0x19;

	/// <summary>
	/// How many options a controls block holds: the four axis assignments, then one action code per
	/// joystick button.
	/// </summary>
	public const int ControlsOptionCount = 12;

	/// <summary>How many of those are the axis rows, which come first.</summary>
	public const int ControlsAxisCount = 4;

	/// <summary>Which block the machine being flown reads — the whole of what <c>DAT_004d25f5</c> selects.</summary>
	public static int ControlsBase(bool razor) => razor ? RazorControlsBase : HercControlsBase;

	/// <summary>
	/// The nine options the preferences panel saves — <c>DAT_0049e304</c>, the list
	/// <c>PreferencesPanel_Save</c> (<c>004574cc</c>) hands to
	/// <c>Prefs_SaveSelectedOptions</c>. Its own rows, and nothing else.
	/// </summary>
	public static readonly int[] PreferencesPanelOptions =
		{ 0, 1, 2, 3, 7, 8, 9, 10, 11 };

	/// <summary>
	/// The thirteen options the controls panel saves — <c>ControlsPanel_Save</c> (<c>00459140</c>),
	/// which builds the list at run time as <c>ControlsOptionBase - 1</c> through <c>+11</c>.
	///
	/// <para>That leading entry is option 12, the joystick-configured flag, only for a walker. Flying
	/// the RAZOR the base is 0x19, so it is option 24 — the walker block's last button binding, which
	/// is rewritten with its own unchanged value. Reproduced rather than corrected: it writes what it
	/// read, so it changes nothing, and the arithmetic is the original's.</para>
	/// </summary>
	public static int[] ControlsPanelOptions(bool razor) {
		int start = ControlsBase(razor) - 1;
		var options = new int[ControlsOptionCount + 1];
		for (int i = 0; i < options.Length; i++) {
			options[i] = start + i;
		}

		return options;
	}

	/// <summary>
	/// The options as the simulator would hold them: all zero where no file was read, since that is
	/// what the memset leaves behind.
	/// </summary>
	public static SimulatorPreferences Defaults() => new(new byte[Length]);

	/// <summary>Option <paramref name="index"/>'s byte, or 0 when it is outside the array.</summary>
	public byte this[int index] => index >= 0 && index < _options.Length ? _options[index] : (byte)0;

	/// <summary>
	/// Whether any option has been written since the file was read. The original tracks the same thing
	/// per option rather than for the array as a whole, marking each changed row so its DONE handler
	/// knows what to act on.
	/// </summary>
	public bool Changed { get; private set; }

	/// <summary>
	/// Writes one option — <c>Prefs_SetOption</c> (<c>0045993c</c>) without its third argument's half.
	///
	/// <para>That function does three things: saves the outgoing byte to the shadow array at
	/// <c>DAT_004d2028</c>, stores the new one, and — when told to apply — calls the option's handler
	/// from the parallel table at <c>DAT_004d2060</c>, which is what makes a setting take effect while
	/// the panel is still up. Only the store is modelled here. The shadow is a revert path and the
	/// handler table is the apply path, and neither is implemented; changing a setting moves the
	/// number the panel shows and nothing else yet.</para>
	/// </summary>
	public void Set(int index, byte value) {
		if (index < 0 || index >= _options.Length || _options[index] == value) {
			return;
		}

		_options[index] = value;
		Changed = true;
	}

	/// <summary>
	/// Steps one option to its next value and returns it, wrapping at <paramref name="modulus"/> —
	/// <c>Prefs_StepOption</c> (<c>004599b8</c>) forward and <c>004599f4</c> back. The two are the
	/// whole of how a panel row cycles, and the caller supplies the modulus, which is why the same
	/// pair drives a three-value row and a five-value one.
	/// </summary>
	public byte Step(int index, int modulus, bool forward = true) {
		if (modulus <= 0) {
			return this[index];
		}

		int value = forward
			? this[index] + 1 >= modulus ? 0 : this[index] + 1
			: this[index] - 1 < 0 ? modulus - 1 : this[index] - 1;

		Set(index, (byte)value);
		return (byte)value;
	}

	/// <summary>
	/// Flips one option between 0 and 1 — <c>Prefs_ToggleOption</c> (<c>00459980</c>), which is
	/// literally <c>value ^ 1</c>. The three on/off rows of the preferences panel take this instead of
	/// <see cref="Step"/>, so a row whose file byte is something other than 0 or 1 lands on 0 or 1 the
	/// first time it is clicked rather than cycling from where it was.
	/// </summary>
	public byte Toggle(int index) {
		byte value = (byte)(this[index] ^ 1);
		Set(index, value);
		return value;
	}

	/// <summary>
	/// Reads the file beside a mission's <c>script.dat</c>, or null when there is none there to read.
	///
	/// <para>A short file is rejected rather than taken as a partial one. The simulator would treat
	/// every byte it could not fill as option 0; here that would silently put every setting on its
	/// lowest value, which a caller with a sounder default of its own should be told about rather
	/// than handed.</para>
	/// </summary>
	/// <param name="dataDirectory">The game's <c>data</c> folder — where its <c>script.dat</c> is.</param>
	public static SimulatorPreferences? Load(string? dataDirectory) {
		if (string.IsNullOrEmpty(dataDirectory)) {
			return null;
		}

		try {
			string path = Path.Combine(dataDirectory, FileName);
			if (!File.Exists(path)) {
				return null;
			}

			byte[] bytes = File.ReadAllBytes(path);
			return bytes.Length < Length ? null : new SimulatorPreferences(bytes, dataDirectory);
		} catch (IOException) {
			return null;
		} catch (UnauthorizedAccessException) {
			return null;
		}
	}

	/// <summary>
	/// Writes the named options back into the file — <c>Prefs_SaveSelectedOptions</c>
	/// (<c>00459b78</c>), and the only shape of save the original performs.
	///
	/// <para><b>It is a read-modify-write, not a dump.</b> The file on disk is re-read, only the
	/// options <paramref name="indices"/> names are copied out of this array over it, and the merged
	/// bytes go back. So an option this call does not name keeps whatever is on disk rather than
	/// whatever is in memory, which is what lets each panel own its own rows: the CONTROLS panel
	/// cannot push out a sound setting, and neither can push out the nineteen options nothing here
	/// interprets. <c>Prefs_SaveAllOptions</c> (<c>0045981c</c>) is the dump, and nothing in the
	/// original calls it.</para>
	///
	/// <para>The file's own length is kept: a longer-than-54-byte file keeps its tail, and a file
	/// that has gone missing or short since it was read is not written at all rather than being
	/// replaced with a fresh 54 bytes.</para>
	///
	/// <para>Clears <see cref="Changed"/> only when every option is back in step with the file,
	/// which after a partial save it need not be.</para>
	/// </summary>
	/// <param name="indices">
	/// Which options to write — <see cref="PreferencesPanelOptions"/> or
	/// <see cref="ControlsPanelOptions"/>. An index outside the array is skipped, as the original's
	/// own byte-indexed copy would leave it.
	/// </param>
	/// <returns>Whether the file was written. False also when it did not need to be.</returns>
	public bool Save(IReadOnlyList<int> indices) {
		ArgumentNullException.ThrowIfNull(indices);

		if (!SaveEnabled || SourceDirectory is not { Length: > 0 } directory) {
			return false;
		}

		try {
			string path = Path.Combine(directory, FileName);
			if (!File.Exists(path)) {
				return false;
			}

			byte[] bytes = File.ReadAllBytes(path);
			if (bytes.Length < Length) {
				return false;
			}

			bool dirty = false;
			foreach (int index in indices) {
				if (index < 0 || index >= Length || bytes[index] == _options[index]) {
					continue;
				}

				bytes[index] = _options[index];
				dirty = true;
			}

			if (dirty) {
				File.WriteAllBytes(path, bytes);
			}

			// What is left over: the options this save did not name and that still differ from the
			// file. After the second panel has closed there are none, and this lands back on false.
			Changed = !bytes.AsSpan(0, Length).SequenceEqual(_options.AsSpan(0, Length));
			return dirty;
		} catch (IOException) {
			return false;
		} catch (UnauthorizedAccessException) {
			return false;
		}
	}
}
