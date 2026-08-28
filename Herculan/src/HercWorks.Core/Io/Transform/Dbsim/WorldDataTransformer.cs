using System.Text;
using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .WLD world/environment files — see <see cref="WorldData"/>
/// for the format and the RE behind it. New: no Java equivalent existed (WorldData.java was modeled
/// but never wired to a transformer, and its own TODO "finish" was never completed).
///
/// <para>Now writes as well as reads: the earlier version could not, because it kept the middle of
/// the file as two undecoded blocks. Every field is accounted for by the walk, so both directions
/// are byte-exact on all ten retail files.</para>
/// </summary>
public class WorldDataTransformer : ByteTransformer<WorldData> {
	public override WorldData? Parse(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length < WorldData.HeaderShorts * 2) {
			return null;
		}

		SetBytes(inputArray);

		var wld = new WorldData {
			Header = IndexShortLEArray(WorldData.HeaderShorts),
		};

		wld.DistanceBandsA = ReadCountedInts();
		wld.DistanceBandsB = ReadCountedInts();

		wld.RampRows = IndexShortLE();
		wld.RampColumns = IndexShortLE();
		int columns = wld.RampColumns < 0 ? 0 : wld.RampColumns;

		wld.RampTableA = IndexIntLEArray(columns);
		wld.BetweenRampTables = IndexShortLE();
		wld.RampTableB = IndexIntLEArray(columns);
		wld.RampExtraA = IndexSegment(4);
		wld.RampExtraB = IndexSegment(4);

		wld.Trailer0 = IndexShortLE();
		wld.Trailer1 = IndexShortLE();
		wld.Trailer2 = IndexIntLE();
		wld.Trailer3 = IndexIntLE();

		wld.WorldTypeStr = ReadNullTerminated(inputArray);
		wld.CloudStr = ReadNullTerminated(inputArray);
		wld.ImpactStr = ReadNullTerminated(inputArray);
		wld.TextureBaseName = ReadNullTerminated(inputArray);
		wld.TextureExtension = ReadNullTerminated(inputArray);

		return wld;
	}

	public override byte[]? Write(WorldData wld) {
		if (wld == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		void WriteCountedInts(int[] values) {
			Emit(WriteIntLE(values.Length));
			foreach (int value in values) {
				Emit(WriteIntLE(value));
			}
		}

		void WriteString(string? value) {
			Emit(Encoding.ASCII.GetBytes(value ?? string.Empty));
			outStream.WriteByte(0);
		}

		foreach (short value in wld.Header) {
			Emit(WriteShortLE(value));
		}

		WriteCountedInts(wld.DistanceBandsA);
		WriteCountedInts(wld.DistanceBandsB);

		Emit(WriteShortLE(wld.RampRows));
		Emit(WriteShortLE(wld.RampColumns));

		foreach (int value in wld.RampTableA) {
			Emit(WriteIntLE(value));
		}

		Emit(WriteShortLE(wld.BetweenRampTables));

		foreach (int value in wld.RampTableB) {
			Emit(WriteIntLE(value));
		}

		Emit(wld.RampExtraA);
		Emit(wld.RampExtraB);

		Emit(WriteShortLE(wld.Trailer0));
		Emit(WriteShortLE(wld.Trailer1));
		Emit(WriteIntLE(wld.Trailer2));
		Emit(WriteIntLE(wld.Trailer3));

		WriteString(wld.WorldTypeStr);
		WriteString(wld.CloudStr);
		WriteString(wld.ImpactStr);
		WriteString(wld.TextureBaseName);
		WriteString(wld.TextureExtension);

		return outStream.ToArray();
	}

	/// <summary>A count-prefixed int32 array; a count that does not fit reads as empty.</summary>
	private int[] ReadCountedInts() {
		int count = IndexIntLE();
		return count < 0 || Index + (long)count * 4 > Bytes!.Length
			? Array.Empty<int>()
			: IndexIntLEArray(count);
	}

	/// <summary>One null-terminated string, stopping at end of file if the terminator is missing.</summary>
	private string ReadNullTerminated(byte[] data) {
		int start = Index;
		int end = start;
		while (end < data.Length && data[end] != 0x00) {
			end++;
		}

		string value = IndexString(end - start);
		if (end < data.Length) {
			Skip(1);
		}

		return value;
	}
}
