namespace Herculan.Engine.Content;

/// <summary>
/// One <c>str\*.STR</c> resource: DBSIM's localised text, as a list of string groups.
///
/// <para>The simulator keeps every piece of UI text out of its code. Layout, the attribute bytes and
/// the <c>STRINGS0.STR</c> group index are in docs/formats/str-strings.md; this class parses that
/// layout and hands groups out by index.</para>
///
/// <para>The one fact the API rests on: <c>SimStrings_LoadAll</c> (<c>00437598</c>) consumes the
/// groups strictly in file order, so a group's index here is its position in that registration
/// sequence, and <see cref="Group"/> is how a caller reaches the one it wants. The 9-byte VOL entry
/// prefix the layout is written against has already been stripped by
/// <see cref="GameContent.Read"/>.</para>
/// </summary>
public sealed class SimStringTable {
	/// <summary>The resource folder <c>.STR</c> files live in.</summary>
	public const string ResourceFolder = "str";

	/// <summary>The simulator's general UI text, and the file the MFD's own captions come from.</summary>
	public const string SimulatorStrings = "STRINGS0.STR";

	private readonly List<Entry[]> _groups;

	private SimStringTable(List<Entry[]> groups) {
		_groups = groups;
	}

	/// <summary>One string and its attribute bytes, if it has any.</summary>
	public readonly record struct Entry(string Text, byte[] Attributes);

	/// <summary>How many groups the file held.</summary>
	public int GroupCount => _groups.Count;

	/// <summary>
	/// Group <paramref name="index"/>'s strings, or an empty list when the file had no such group —
	/// a caller drawing text is better off drawing none than an invented caption.
	/// </summary>
	public IReadOnlyList<Entry> Group(int index) =>
		index >= 0 && index < _groups.Count ? _groups[index] : Array.Empty<Entry>();

	/// <summary>
	/// String <paramref name="index"/> of <paramref name="group"/>, or null when either index is out
	/// of range.
	/// </summary>
	public string? Text(int group, int index) {
		var entries = Group(group);
		return index >= 0 && index < entries.Count ? entries[index].Text : null;
	}

	/// <summary>
	/// Reads one <c>.STR</c> out of the mounted archives, or null when it is absent or does not parse
	/// as the layout above. A partially-read file is discarded rather than returned: group indices are
	/// positional, so a truncated walk would silently shift every later group.
	/// </summary>
	public static SimStringTable? Load(GameContent content, string name = SimulatorStrings) =>
		content.Read(ResourceFolder, name) is { } bytes ? Parse(bytes) : null;

	/// <summary>
	/// Walks the layout above. Returns null on any inconsistency — a length running past the declared
	/// content, or a group that does not complete — for the reason in <see cref="Load"/>.
	/// </summary>
	public static SimStringTable? Parse(byte[] bytes) {
		if (bytes.Length < 4) {
			return null;
		}

		int end = 4 + BitConverter.ToInt32(bytes, 0);
		if (end > bytes.Length) {
			return null;
		}

		var groups = new List<Entry[]>();
		int at = 4;
		while (at + 2 <= end) {
			int count = BitConverter.ToInt16(bytes, at);
			at += 2;
			if (count < 0) {
				return null;
			}

			var entries = new Entry[count];
			for (int i = 0; i < count; i++) {
				if (at + 2 > end) {
					return null;
				}

				int length = BitConverter.ToInt16(bytes, at);
				at += 2;
				if (length < 0 || at + length >= end) {
					return null;
				}

				// The stored length counts the NUL terminator; the text is everything before it.
				entries[i] = new Entry(
					System.Text.Encoding.ASCII.GetString(bytes, at, Math.Max(length - 1, 0)),
					Array.Empty<byte>());
				at += length;

				int attributes = bytes[at++];
				if (at + attributes > end) {
					return null;
				}

				if (attributes > 0) {
					entries[i] = entries[i] with { Attributes = bytes[at..(at + attributes)] };
					at += attributes;
				}
			}

			groups.Add(entries);
		}

		return at == end ? new SimStringTable(groups) : null;
	}
}
