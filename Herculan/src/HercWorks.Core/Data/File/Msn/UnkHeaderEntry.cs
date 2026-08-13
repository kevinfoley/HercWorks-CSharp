using System.Text;

namespace HercWorks.Core.Data.File.Msn;

/// <summary>
/// Row #1 (14 bytes/record) — the mission's shared campaign flag/condition-trigger store, every
/// other row's condition field ultimately points into this array by index. Unlike every other
/// row in the file, offset 0x00 is never read as a dedup/lookup key by VSHELL's own load loop
/// (row #1 records are never GUID-deduplicated), so it's modeled here as a plain ordinal rather
/// than inheriting the GUID/dedup convention <see cref="MapObject"/> gives every other row.
///
/// Type discriminator (0x04) selects how the record is evaluated: 0 = plain comparison (operator
/// code 0x119-0x11e at 0x08, operand at 0x0A), 2 = range-bucket check ([lower,upper] pair at
/// 0x06/0x08, consistently 49 apart in real data), 1 = a third evaluator function, 3 =
/// condition-only. See docs/formats/msn-mission-file.md, "The condition/trigger system".
/// </summary>
public class UnkHeaderEntry {
	/// <summary>0x00 — authoring-tool bookkeeping index; never consumed by VSHELL's own load loop.</summary>
	public short Ordinal { get; set; }

	/// <summary>0x02 — fed to the type-specific evaluator for every type (0/1/2/3).</summary>
	public short ConditionInput { get; set; }

	/// <summary>0x04 — 0 = comparison, 1 = alternate evaluator, 2 = range-bucket check, 3 = condition-only.</summary>
	public short TypeDiscriminator { get; set; }

	/// <summary>0x06 — flag-index (type 0), range-lower (type 2), or evaluator param (type 1).</summary>
	public short FlagIndexOrRangeLower { get; set; }

	/// <summary>
	/// 0x08 — for type 0, holds the 0x119-0x11e operator code on input, overwritten in place with
	/// the boolean result. For type 2, the range's upper bound (consistently lower+49).
	/// </summary>
	public short OperatorOrRangeUpperOrResult { get; set; }

	/// <summary>0x0A — comparison operand (type 0 only); mostly 0.</summary>
	public short ComparisonOperand { get; set; }

	/// <summary>0x0C — always 0 in all real data; fully dead.</summary>
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
