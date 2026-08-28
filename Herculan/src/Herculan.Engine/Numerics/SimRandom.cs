namespace Herculan.Engine.Numerics;

/// <summary>
/// Port of DBSIM's pseudo-random generator (<c>FUN_00492dd4</c>, state block at <c>0x4d261d</c>) —
/// an additive lagged Fibonacci generator over a 56-entry table of <see cref="short"/>s with two
/// rotating cursors:
/// <code>
///   table[i] += table[j];  result = table[i];  i = (i + 1) % 56;  j = (j + 1) % 56;
/// </code>
/// The original returns the new <c>table[i]</c> in the low 16 bits; every caller found so far masks
/// it down further (<c>&amp; 0xfff</c> for the terrain material roll and the explosion's
/// per-component ~51% roll), so <see cref="NextMasked"/> is the shape simulation code actually uses.
///
/// <para><b>Not yet vanilla — the seed state.</b> The algorithm above is a literal translation, but
/// the 56 initial table values live in DBSIM's data section and have not been extracted, so this
/// starts from a locally-generated table instead. Bit-exact parity with the original would need
/// more than that table anyway: a roll's result depends on how many times the generator was already
/// advanced before that point in the frame, so matching a specific run means matching the whole call
/// history, not just the seed. Nothing this generator currently drives is visible in the first
/// milestone (terrain material bits select detail textures, which aren't rendered yet), but anything
/// built on it later — the explosion damage roll especially — should be treated as
/// statistically-faithful rather than replay-faithful until that gap is closed.</para>
/// </summary>
public sealed class SimRandom {
	/// <summary>Table length, from the original's <c>== '8'</c> (0x38) cursor wrap test.</summary>
	private const int TableLength = 0x38;

	private readonly short[] _table = new short[TableLength];
	private int _cursorI;
	private int _cursorJ;

	/// <summary>
	/// Creates a generator with a locally-seeded table (see the type's "not yet vanilla" note). The
	/// cursor offsets reproduce the original's lag: DBSIM stores them as two independent bytes that
	/// each wrap at 56, and both start from whatever the data section holds.
	/// </summary>
	public SimRandom(int seed) {
		var seeder = new Random(seed);
		for (int i = 0; i < TableLength; i++) {
			_table[i] = (short)seeder.Next(short.MinValue, short.MaxValue + 1);
		}
		_cursorI = 0;
		_cursorJ = TableLength / 2;
	}

	/// <summary>
	/// One step of the generator, returning the low 16 bits the original returns. Signed
	/// <see cref="short"/> arithmetic wraps exactly as the original's 16-bit add does.
	/// </summary>
	public short Next() {
		_table[_cursorI] = (short)(_table[_cursorI] + _table[_cursorJ]);
		short result = _table[_cursorI];

		_cursorI++;
		if (_cursorI == TableLength) {
			_cursorI = 0;
		}

		_cursorJ++;
		if (_cursorJ == TableLength) {
			_cursorJ = 0;
		}

		return result;
	}

	/// <summary>
	/// <c>Next() &amp; mask</c> — the form every located caller uses (always with a power-of-two-minus-one
	/// mask, e.g. <c>0xfff</c> for the terrain material roll and the AoE component roll).
	/// </summary>
	public int NextMasked(int mask) => Next() & mask;

	/// <summary>
	/// <c>FUN_00492e18</c> — a draw in <c>[0, bound)</c>, as <c>(Next() &amp; 0x7fff) % bound</c>. The
	/// mask before the modulo is the original's own: it drops the sign bit rather than taking an
	/// absolute value, so the distribution is the low fifteen bits' and not the full sixteen.
	///
	/// <para>A bound of zero would divide by zero in the original; nothing reaches it there and
	/// nothing does here either, so it is answered with zero rather than guarded upstream.</para>
	/// </summary>
	public int NextBelow(short bound) => bound == 0 ? 0 : (Next() & 0x7fff) % bound;
}
