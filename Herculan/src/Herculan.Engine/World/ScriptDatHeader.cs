using System.Buffers.Binary;

namespace Herculan.Engine.World;

/// <summary>
/// The 20-byte header of <c>data\script.dat</c>, the mission handoff VSHELL writes and DBSIM reads
/// (see docs/formats/script-dat.md for the 13 record blocks that follow it).
///
/// <para>Two of its fields are what a scene needs before anything else, and both are now decoded
/// from <c>DBSim_LoadScriptDat</c> (<c>00424308</c>), which reads the header into one global and then
/// uses it twice: it hands the whole 20 bytes to <c>maybe_World_LoadTheater</c> (which takes the
/// short at 0 and the short at 18 as <c>world&lt;index * 2 + variant&gt;</c>) and passes the short at
/// 2 straight to <c>Terrain_LoadZone</c>.</para>
///
/// <para>Verified against the ten real files in the retail install (<c>ES2\DATA\script.dat</c> plus
/// the <c>ES2\SAV\script*.dat</c> snapshots): every <see cref="ZoneIndex"/> is a zone that actually
/// ships (555, 123, 22, 234, 3333 — all present as <c>dat\zoneNNNN.dat</c>), and every
/// <see cref="TheaterIndex"/> is 0, 1 or 2. This resolves script-dat.md's open question about the
/// header field at offset 2, which that doc guessed might be a mission id or checksum.</para>
/// </summary>
public readonly struct ScriptDatHeader {
	/// <summary>Bytes the header occupies; the original reads exactly this many in one call.</summary>
	public const int Size = 20;

	private ScriptDatHeader(int theaterIndex, int zoneIndex, int theaterVariant) {
		TheaterIndex = theaterIndex;
		ZoneIndex = zoneIndex;
		TheaterVariant = theaterVariant;
	}

	/// <summary>Theater to load, 0-4 — see <see cref="TheaterDescriptor"/>.</summary>
	public int TheaterIndex { get; }

	/// <summary>Which <c>zoneNNNN</c> the mission plays in.</summary>
	public int ZoneIndex { get; }

	/// <summary>
	/// Selects between a theater's two descriptors. Every retail file carries 0, so what the
	/// second variant of each theater is for (weather? time of day?) is not established here.
	/// </summary>
	public int TheaterVariant { get; }

	/// <summary>
	/// Reads the header from the start of a <c>script.dat</c>'s bytes. The remaining fields are left
	/// undecoded rather than exposed as raw numbers — <c>DBSim_LoadScriptDat</c> zeroes the one at
	/// offset 4 before use, and the rest are constant across every retail file.
	/// </summary>
	public static ScriptDatHeader Read(ReadOnlySpan<byte> scriptDat) {
		if (scriptDat.Length < Size) {
			throw new InvalidDataException(
				$"script.dat is {scriptDat.Length} bytes; its header alone is {Size}.");
		}

		return new ScriptDatHeader(
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat),
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[2..]),
			BinaryPrimitives.ReadInt16LittleEndian(scriptDat[18..]));
	}
}
