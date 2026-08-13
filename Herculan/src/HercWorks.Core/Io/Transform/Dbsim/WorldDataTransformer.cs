using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .WLD world/environment files (see <see cref="WorldData"/>
/// for the format writeup). New: no Java equivalent existed — WorldData.java was modeled but never
/// wired to a transformer, and its own TODO ("finish") was never completed.
///
/// Read-only: MidSectionA/MidSectionB are undecoded, so there's no confirmed structure to write
/// back byte-exact.
/// </summary>
public class WorldDataTransformer : ThreeSpaceByteTransformer {
	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);

		var wld = new WorldData {
			RawBytes = inputArray,
			Ext = FileType.Wld,

			Unk0_val2 = IndexShortLE(),
			SkyPaletteId = IndexShortLE(),
			SkyHorizonHeight = IndexShortLE(),
			SkyHorizonStartHeight = IndexShortLE(),
			Unk8_val = IndexShortLE(),
			Unk10_val = IndexShortLE(),
			Unk12_val = IndexShortLE(),
			Spacer14 = IndexShortLE(),
			Unk16_val = IndexShortLE(),
			Unk18_val = IndexShortLE(),
			Unk20_val = IndexShortLE(),
			Unk22_val = IndexShortLE(),
			Unk24_val = IndexShortLE(),
			Unk26_val = IndexShortLE(),
			Unk28_val = IndexShortLE(),
			Spacer30 = IndexShortLE(),
			Unk32_val = IndexIntLE(),
			Unk34_val = IndexIntLE(),
		};

		wld.MidSectionA = IndexSegment(190);
		wld.MidSectionB = IndexSegment(48);

		wld.WorldTypeStr = ReadFixedNullTerminated(8);
		wld.CloudStr = ReadFixedNullTerminated(8);
		wld.ImpactSt = ReadFixedNullTerminated(8);

		wld.TextureBaseName = ReadNullTerminatedToEnd(inputArray);
		wld.TextureExtension = ReadNullTerminatedToEnd(inputArray);

		return wld;
	}

	private string ReadFixedNullTerminated(int fieldLen) {
		string s = IndexString(fieldLen);
		int nul = s.IndexOf('\0');
		return nul >= 0 ? s[..nul] : s;
	}

	/// <summary>Reads one null-terminated string starting at the current position, stopping at the byte array's end if no null byte is found.</summary>
	private string ReadNullTerminatedToEnd(byte[] data) {
		int start = Index;
		int end = start;
		while (end < data.Length && data[end] != 0x00) {
			end++;
		}

		string value = IndexString(end - start);
		if (end < data.Length) {
			Skip(1); // the null terminator itself.
		}
		return value;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		// TODO: not implemented — MidSectionA/MidSectionB are undecoded (see class doc comment), so
		// a byte-exact round-trip isn't currently achievable.
		return null;
	}
}
