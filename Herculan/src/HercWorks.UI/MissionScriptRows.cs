using HercWorks.Core.Data.File.Msn.Script;

namespace HercWorks.UI;

/// <summary>
/// Formats/parses the short arrays that several script.dat records carry as a single editable
/// comma-separated grid cell. Fixed-length arrays (weapon fits, member ref slots, ...) are written
/// back in place and must keep their exact element count — the on-disk record stride depends on it —
/// so a wrong-length edit is rejected rather than silently padded; -1 is the format's own
/// "unused slot" sentinel and is what an empty slot should be set to.
/// </summary>
internal static class ShortCsv {
	public static string Format(short[] values) => string.Join(", ", values);

	public static short[] Parse(string? text) {
		if (string.IsNullOrWhiteSpace(text)) {
			return [];
		}

		return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(part => short.TryParse(part, out short v)
				? v
				: throw new FormatException($"'{part}' is not a valid 16-bit value."))
			.ToArray();
	}

	/// <summary>Parses into an array that must stay exactly <paramref name="target"/>.Length long.</summary>
	public static void ParseInto(string? text, short[] target) {
		var parsed = Parse(text);
		if (parsed.Length != target.Length) {
			throw new FormatException(
				$"This field holds exactly {target.Length} values ({parsed.Length} given). " +
				"Use -1 for unused slots rather than leaving them out.");
		}
		Array.Copy(parsed, target, target.Length);
	}
}

/// <summary>
/// Base for every script.dat grid row: rows wrap a live model record and write edits straight
/// through to it, so there is no separate apply step when saving. Index is the record's position in
/// its own block — the number every other block's refs use to point at it.
/// </summary>
internal abstract class ScriptRow {
	public int Index { get; init; }
}

/// <summary>Block 1 — a world position other blocks reference by index.</summary>
internal sealed class ScriptPointRow : ScriptRow {
	public required ScriptCoordinate Source { get; init; }

	public int X { get => Source.X; set => Source.X = value; }
	public int Y { get => Source.Y; set => Source.Y = value; }
	public int Z { get => Source.Z; set => Source.Z = value; }
}

/// <summary>
/// Block 2 — a heading in degrees. DBSIM multiplies this by 182 into BAM at load; the BAM column
/// shows what it becomes there but is not itself stored.
/// </summary>
internal sealed class ScriptHeadingRow : ScriptRow {
	public required ScriptHeading Source { get; init; }

	public short Degrees { get => Source.Value; set => Source.Value = value; }
	public int Bam => Source.Value * 182;
}

/// <summary>Block 3 — an ordered list of block-1 point refs forming one route.</summary>
internal sealed class ScriptRouteRow : ScriptRow {
	public required ScriptWaypointGroup Source { get; init; }

	public int Count => Source.Waypoints.Length;

	/// <summary>Variable-length by format, so unlike the fixed ref arrays this may be any length.</summary>
	public string Waypoints {
		get => ShortCsv.Format(Source.Waypoints);
		set => Source.Waypoints = ShortCsv.Parse(value);
	}
}

/// <summary>Block 4.</summary>
internal sealed class ScriptLinkRewardRow : ScriptRow {
	public required ScriptLinkOrReward Source { get; init; }

	public short TypeFlag { get => Source.TypeFlag; set => Source.TypeFlag = value; }
	public short RefA { get => Source.RefA; set => Source.RefA = value; }
	public short RefBOrLiteral { get => Source.RefBOrLiteral; set => Source.RefBOrLiteral = value; }
}

/// <summary>
/// Block 5. ArrayA/ArrayB are the record's undecoded interleaved constant span — shown so an edit
/// elsewhere can be sanity-checked against them, but read-only, since nothing is known about what a
/// changed value would mean.
/// </summary>
internal sealed class ScriptActionRow : ScriptRow {
	public required ScriptAction Source { get; init; }

	public short Type { get => Source.Type; set => Source.Type = value; }
	public short Verb { get => Source.Verb; set => Source.Verb = value; }
	public short SecondaryValue { get => Source.SecondaryValue; set => Source.SecondaryValue = value; }
	public short Target { get => Source.Target; set => Source.Target = value; }

	public string RefsRow9 {
		get => ShortCsv.Format(Source.RefsRow9);
		set => ShortCsv.ParseInto(value, Source.RefsRow9);
	}

	public string LutRefs {
		get => ShortCsv.Format(Source.LutRefs);
		set => ShortCsv.ParseInto(value, Source.LutRefs);
	}

	public string ArrayA => ShortCsv.Format(Source.ArrayA);
	public string ArrayB => ShortCsv.Format(Source.ArrayB);
}

/// <summary>Block 6.</summary>
internal sealed class ScriptActionPairRow : ScriptRow {
	public required ScriptActionPair Source { get; init; }

	public short PrimaryActionRef { get => Source.PrimaryActionRef; set => Source.PrimaryActionRef = value; }
	public short TimerValue { get => Source.TimerValue; set => Source.TimerValue = value; }

	public string SequenceRefs {
		get => ShortCsv.Format(Source.SequenceRefs);
		set => ShortCsv.ParseInto(value, Source.SequenceRefs);
	}
}

/// <summary>
/// Block 7 — one mech roster slot. A slot only spawns if some group (block 11) names it, and in
/// every retail file Position/Heading are -1, meaning the mech takes its group's instead.
/// </summary>
internal sealed class ScriptMechRow : ScriptRow {
	public required ScriptSpawnRecordExport Source { get; init; }

	/// <summary>
	/// Index into <c>nam\MECHS.NAM</c>, presented as a name via <see cref="HercTypeOption"/>; the
	/// underlying model field keeps Core's own name.
	/// </summary>
	public short HercType { get => Source.SmallDiscrete; set => Source.SmallDiscrete = value; }
	public short PositionRef { get => Source.PositionRef; set => Source.PositionRef = value; }
	public short HeadingRef { get => Source.HeadingRef; set => Source.HeadingRef = value; }

	public string WeaponRefs {
		get => ShortCsv.Format(Source.WeaponRefs);
		set => ShortCsv.ParseInto(value, Source.WeaponRefs);
	}
}

/// <summary>Block 8 — one flyer roster slot.</summary>
internal sealed class ScriptFlyerRow : ScriptRow {
	public required ScriptEntity102Export Source { get; init; }

	public short FlyerType { get => Source.BinaryField; set => Source.BinaryField = value; }
	public short PositionRef { get => Source.PositionRef; set => Source.PositionRef = value; }
	public short HeadingRef { get => Source.HeadingRef; set => Source.HeadingRef = value; }
}

/// <summary>Block 9 — one base/structure roster slot.</summary>
internal sealed class ScriptBaseRow : ScriptRow {
	public required ScriptMiscEntityExport Source { get; init; }

	public short BaseType { get => Source.TypeLikeScalar; set => Source.TypeLikeScalar = value; }
	public short PositionRef { get => Source.PositionRef; set => Source.PositionRef = value; }
	public short HeadingRef { get => Source.HeadingRef; set => Source.HeadingRef = value; }
}

/// <summary>
/// Block 10 — route links. DBSIM's spawn pass resolves these to give a group with no point of its
/// own a spawn position (the first waypoint of the referenced route).
/// </summary>
internal sealed class ScriptRouteLinkRow : ScriptRow {
	public required ScriptLinkedRef22Export Source { get; init; }

	public short SmallInt1 { get => Source.SmallInt1; set => Source.SmallInt1 = value; }
	public short SmallInt2 { get => Source.SmallInt2; set => Source.SmallInt2 = value; }
	public short PointRef { get => Source.RefRow6; set => Source.RefRow6 = value; }
	public short RouteRef { get => Source.RefRow8; set => Source.RefRow8 = value; }
	public short DiscriminatorType { get => Source.DiscriminatorType; set => Source.DiscriminatorType = value; }
	public short DiscriminatedRef { get => Source.DiscriminatedRef; set => Source.DiscriminatedRef = value; }
	public short ActionRef { get => Source.RefRow10; set => Source.RefRow10 = value; }
}

/// <summary>
/// Block 11 — a group: the record that actually decides what exists and where. Roster picks which
/// roster block MemberRefs indexes (0 mechs / 1 flyers / 2 bases), and every record past record 0
/// activates the slots it names. Record 0 is the player squad's placeholder — it activates nothing
/// and DBSIM fills its members from data\player.mec — so it is shown but its member list is
/// meaningless.
/// </summary>
internal sealed class ScriptGroupRow : ScriptRow {
	public required ScriptEntity164Export Source { get; init; }

	public bool IsPlayerSquad => Index == 0;

	public short BinaryFlag { get => Source.BinaryFlag; set => Source.BinaryFlag = value; }
	public short Roster { get => Source.Discriminator; set => Source.Discriminator = value; }
	public short Formation { get => Source.SmallDiscrete; set => Source.SmallDiscrete = value; }
	public short PointRef { get => Source.RefRow6; set => Source.RefRow6 = value; }
	public short HeadingRef { get => Source.RefRow7; set => Source.RefRow7 = value; }
	public short RouteRef { get => Source.RefRow8; set => Source.RefRow8 = value; }
	public short TriStateFlag { get => Source.TriStateFlag; set => Source.TriStateFlag = value; }
	public short ActionRef { get => Source.RefRow10; set => Source.RefRow10 = value; }

	/// <summary>
	/// The 20 member slots, indexing whichever roster <see cref="Roster"/> names. Slot position
	/// matters beyond membership: it is also the formation slot, so slot 0 always stands exactly on
	/// the group's point and reordering members moves them.
	/// </summary>
	public string MemberRefs {
		get => ShortCsv.Format(Source.DiscriminatedRefs);
		set => ShortCsv.ParseInto(value, Source.DiscriminatedRefs);
	}

	public string RouteLinkRefs {
		get => ShortCsv.Format(Source.Row15Refs);
		set => ShortCsv.ParseInto(value, Source.Row15Refs);
	}
}

/// <summary>Block 12 — read and discarded by DBSIM; kept editable for round-trip completeness.</summary>
internal sealed class ScriptEntityLinkRow : ScriptRow {
	public required ScriptLinkedRef58Export Source { get; init; }

	public short Unk02 { get => Source.Unk02; set => Source.Unk02 = value; }
	public short Unk04 { get => Source.Unk04; set => Source.Unk04 = value; }
	public short Discriminator { get => Source.Discriminator; set => Source.Discriminator = value; }
	public short DiscriminatedRef { get => Source.DiscriminatedRef; set => Source.DiscriminatedRef = value; }
	public short PointRef { get => Source.RefRow6; set => Source.RefRow6 = value; }
	public short RouteRef { get => Source.RefRow8; set => Source.RefRow8 = value; }
	public short LutRef { get => Source.LutRef; set => Source.LutRef = value; }

	public string PairRefs {
		get => ShortCsv.Format(Source.PairRefs);
		set => ShortCsv.ParseInto(value, Source.PairRefs);
	}

	public string PairTags {
		get => ShortCsv.Format(Source.PairTags);
		set => ShortCsv.ParseInto(value, Source.PairTags);
	}
}

/// <summary>
/// Block 13 — one entry of the mission's herc/weapon unlock package. This block is a plain
/// count-prefixed list with nothing referencing it, so rows here can be added and removed freely;
/// the whole array is rebuilt from the grid on save.
/// </summary>
internal sealed class ScriptUnlockRow {
	public short Value { get; set; }
}
