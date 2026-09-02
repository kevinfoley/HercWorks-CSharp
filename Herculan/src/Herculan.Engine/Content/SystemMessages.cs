namespace Herculan.Engine.Content;

/// <summary>
/// <c>str\SYSTEM.STR</c> — the cockpit computer's own message set: every line it says, and for each
/// one the recorded clip that says it. See docs/formats/audio.md, "The computer's messages".
///
/// <para>The file is an ordinary <c>.STR</c> string table (see docs/formats/str-strings.md) of two
/// groups, 40 entries then 23, each carrying eight attribute bytes. <b>The groups are not a
/// classification</b> — nothing in the simulator addresses a message by group. Call sites pass one
/// flat number, and that number is the entry's position counted straight through both groups, which
/// is also what attribute byte 0 holds. So this type flattens on load and indexes by that
/// number.</para>
///
/// <para>Reached from the simulation through <see cref="Audio.ComputerVoice"/>, which is what turns
/// a posted message into the sound of one.</para>
/// </summary>
public sealed class SystemMessages {
	/// <summary>The resource this is read from.</summary>
	public const string ResourceName = "SYSTEM.STR";

	/// <summary>How many attribute bytes an entry carries.</summary>
	public const int AttributeCount = 8;

	private readonly Entry[] _entries;

	private SystemMessages(Entry[] entries) {
		_entries = entries;
	}

	/// <summary>One message: the text, and the voice clip that reads it aloud.</summary>
	public readonly record struct Entry(int Id, string Text, int VoiceClip, byte[] Attributes);

	/// <summary>Every message, indexed by the flat id the call sites use.</summary>
	public IReadOnlyList<Entry> Entries => _entries;

	/// <summary>How many messages the file held.</summary>
	public int Count => _entries.Length;

	/// <summary>Message <paramref name="id"/>, or null when the id is outside the table.</summary>
	public Entry? this[int id] => id >= 0 && id < _entries.Length ? _entries[id] : null;

	/// <summary>
	/// Reads the message set out of the mounted archives, or null when <c>SYSTEM.STR</c> is absent or
	/// does not parse.
	/// </summary>
	public static SystemMessages? Load(GameContent content) =>
		SimStringTable.Load(content, ResourceName) is { } table ? FromTable(table) : null;

	/// <summary>
	/// Flattens an already-parsed <c>.STR</c>. An entry whose attributes are short is kept with no
	/// voice clip rather than dropped, so that ids past it keep their positions.
	/// </summary>
	public static SystemMessages FromTable(SimStringTable table) {
		var entries = new List<Entry>();

		for (int group = 0; group < table.GroupCount; group++) {
			foreach (var entry in table.Group(group)) {
				var attributes = entry.Attributes;
				int clip = attributes.Length >= AttributeCount ? attributes[VoiceClipAttribute] : 0;
				entries.Add(new Entry(entries.Count, entry.Text, clip, attributes));
			}
		}

		return new SystemMessages(entries.ToArray());
	}

	/// <summary>
	/// Attribute byte 7 — which <c>CVM_nnnn.WAV</c> in the voice archive reads this line, one-based.
	///
	/// <para>It is a field and not an offset from the id: the numbering runs 1 to 66 across the 63
	/// messages with three values skipped, and the archive holds exactly 66 clips. Those three are
	/// recorded lines no message claims.</para>
	/// </summary>
	public const int VoiceClipAttribute = 7;

	/// <summary>
	/// Attribute byte 0 — the flat message id, which is also the entry's own position. Carried
	/// because the file carries it; <see cref="Entry.Id"/> is the position and is what to index on.
	///
	/// <para>It is the id, not the position, that the original keys on:
	/// <c>SystemMessages_Index</c> (<c>00435970</c>) scatters the strings into a 63-slot table at
	/// <c>base + attr[0] * 9</c> and counts how many landed in each slot, so two entries sharing an id
	/// would become variants of one message and the post would roll between them. The retail file
	/// gives all 63 distinct ids, so every count is one and no variant exists.</para>
	/// </summary>
	public const int IdAttribute = 0;

	/// <summary>
	/// Attribute byte 2 — queue priority. <c>MessagePort.Post</c> inserts before the first queued
	/// message of strictly higher value, so low sorts first. Zero on every entry in the retail file,
	/// which makes the queue plain arrival order.
	/// </summary>
	public const int PriorityAttribute = 2;

	/// <summary>
	/// Attribute byte 3 — the shortest time the line stays on screen once it is up, after which it
	/// will step aside for a message waiting behind it. In units of
	/// <see cref="MessagePort.TicksPerTimingUnit"/>, so effectively seconds.
	/// </summary>
	public const int MinDisplayAttribute = 3;

	/// <summary>Attribute byte 4 — the longest it stays up, after which it comes down regardless.</summary>
	public const int MaxDisplayAttribute = 4;

	/// <summary>Attribute byte 5 — how long after posting before it may be shown. Zero throughout.</summary>
	public const int MinDelayAttribute = 5;

	/// <summary>Attribute byte 6 — how long after posting it is dropped unshown. 20 throughout.</summary>
	public const int MaxDelayAttribute = 6;

	/// <summary>
	/// <c>POWERUP INITIATED. ALL SYSTEMS NOMINAL.</c> — posted by the cockpit's power-up sequence
	/// (<c>FUN_00432924</c>) once the start-up run finishes with no damaged component found.
	/// </summary>
	public const int PowerUpNominal = 0x21;

	/// <summary>
	/// <c>POWERUP INITIATED. INTERNAL DAMAGE DETECTED.</c> — the same post when the sequence's walk
	/// over the ten gauges finds one under its threshold.
	/// </summary>
	public const int PowerUpDamaged = 0x22;

	/// <summary><c>ACTIVE RADAR MODE</c> — posted by <c>Mech_ToggleRadarMode</c> (<c>0041b468</c>).</summary>
	public const int ActiveRadarMode = 0x2c;

	/// <summary><c>PASSIVE RADAR MODE</c> — the other arm of the same post.</summary>
	public const int PassiveRadarMode = 0x2d;

	/// <summary>
	/// <c>TRANSFERRING DATA</c> — the one message the port treats specially. It is the only entry
	/// whose timings differ from the rest of the file (10 s and 20 s against 3 s and 6 s), and
	/// <c>FUN_00436abc</c> switches on its id to make it blink; <c>FUN_00436cec</c> then centres it in
	/// the box instead of scrolling it. See <see cref="MessagePort"/>.
	/// </summary>
	public const int TransferringData = 0x36;
}
