using System.Text;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #1 (14 bytes/record) — the mission's conditions. Every other row's condition field names one
/// of these by its GUID at 0x00, and a record whose condition names no surviving row-1 record is
/// dropped at load. The type at 0x04 decides how a row-1 record itself survives — 0 a flag
/// comparison, 1 a random draw, 2 a range over its parent's draw, 3 a variant key's parent — and
/// type 3 is what the other rows' 0x04 variant keys name. The load is
/// <see cref="Io.Transform.Common.MissionGenerator"/>; the rules are
/// docs/formats/msn-mission-file.md#the-conditions--row-1.
/// </summary>
public class UnkHeaderEntry {
	/// <summary>0x00 — the GUID the other rows' condition refs name.</summary>
	public short Ordinal { get; set; }

	/// <summary>0x02 — this record's own condition; for type 2, its parent.</summary>
	public short ConditionInput { get; set; }

	/// <summary>0x04 — 0 a flag comparison, 1 a draw, 2 a range over the parent's draw, 3 a variant key's parent.</summary>
	public short TypeDiscriminator { get; set; }

	/// <summary>0x06 — the flag index (type 0), the draw's bound (type 1), the range's low end (type 2) or the variant key (type 3).</summary>
	public short FlagIndexOrRangeLower { get; set; }

	/// <summary>
	/// 0x08 — for type 0 the 0x119-0x11e operator code, overwritten at load with the result; for type
	/// 1 the draw, written at load; for type 2 the range's high end.
	/// </summary>
	public short OperatorOrRangeUpperOrResult { get; set; }

	/// <summary>0x0A — the comparison operand (type 0), or the bound a variant is drawn below (type 3).</summary>
	public short ComparisonOperand { get; set; }

	/// <summary>0x0C — 0 in every file; the load stores a type-3 record's latest variant draw here.</summary>
	public short AlwaysZero { get; set; }

	public UnkHeaderEntry() { }

	public UnkHeaderEntry(short ordinal, short conditionInput, short typeDiscriminator,
		short flagIndexOrRangeLower, short operatorOrRangeUpperOrResult, short comparisonOperand,
		short alwaysZero) {
		Ordinal = ordinal;
		ConditionInput = conditionInput;
		TypeDiscriminator = typeDiscriminator;
		FlagIndexOrRangeLower = flagIndexOrRangeLower;
		OperatorOrRangeUpperOrResult = operatorOrRangeUpperOrResult;
		ComparisonOperand = comparisonOperand;
		AlwaysZero = alwaysZero;
	}

	public override string ToString() {
		var b = new StringBuilder();
		b.Append('[')
			.Append(Ordinal).Append(", ")
			.Append(ConditionInput).Append(", ")
			.Append(TypeDiscriminator).Append(", ")
			.Append(FlagIndexOrRangeLower).Append(", ")
			.Append(OperatorOrRangeUpperOrResult).Append(", ")
			.Append(ComparisonOperand).Append(", ")
			.Append(AlwaysZero)
			.Append(']');

		return b.ToString();
	}
}
