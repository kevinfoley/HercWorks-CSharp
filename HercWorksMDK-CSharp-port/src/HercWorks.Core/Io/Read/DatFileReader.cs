using HercWorks.Core.Data.File.Dat.Shell;
using HercWorks.Core.Data.Struct;
using HercWorks.Core.Data.Struct.Vshell.Hercs;
using HercWorks.Core.Util;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Read;

/// <summary>
/// Reads various .DAT file types in. .DAT files are wildcards — they could be almost any kind of
/// data, and are dependent on the file name or folder for contextual determination.
/// Ported from org.hercworks.core.io.read.DatFileReader.
/// </summary>
public static class DatFileReader {
	public static InitHerc ParseIniHercDatStats(byte[]? volByte, DataFile file) {
		if (volByte == null || volByte.Length == 0) {
			throw new Exception("ERROR: vol data bytes array was empty.");
		}

		var iniStats = new InitHerc(file.FileName!, file.FilePath!);

		if (file.RawBytes == null || file.RawBytes.Length == 0) {
			throw new Exception($"ERROR: file({file.FileName}) raw bytes was null or empty.");
		}

		iniStats.RawBytes = file.RawBytes;

		byte[] data = iniStats.RawBytes;
		int cursor = 0;

		iniStats.Data = new ShellHercData {
			HercId = EndianOps.ToShort(data, cursor, ByteOrder.LittleEndian)
		};
		cursor += 2;

		iniStats.Data.HealthRatio = EndianOps.ToShort(data, cursor, ByteOrder.LittleEndian);
		cursor += 2;

		iniStats.Data.BuildCompleteLevel = EndianOps.ToShort(data, cursor, ByteOrder.LittleEndian);
		cursor += 2;

		// WARN (carried over from Java): this does not sync up to the herc's total hardpoint
		// count... so we'll have to figure out why.
		short activeHardpoints = EndianOps.ToShort(data, cursor, ByteOrder.LittleEndian);
		cursor += 2;

		// NOTE ON A LIKELY BUG: the original initializes this map but the loop below never
		// actually inserts any of the parsed UiWeaponEntry objects into it — each entry is
		// constructed and then discarded. Ported literally (the map stays empty after parsing),
		// since I can't be sure without real game data whether some other caller populates it.
		iniStats.Data.Hardpoints = new Dictionary<short, UiWeaponEntry>();

		for (int i = cursor; i < iniStats.RawBytes.Length; i += 2) {
			// 'id' is read here but never stored anywhere — matches the original, which reads
			// it into a local variable and never uses it beyond advancing the cursor.
			short id = EndianOps.ToShort(data, i, ByteOrder.LittleEndian);
			var hardpoint = new UiWeaponEntry();
			i += 2;

			hardpoint.ItemId = EndianOps.ToShort(data, i, ByteOrder.LittleEndian);
			i += 2;

			hardpoint.HealthPercent = EndianOps.ToShort(data, i, ByteOrder.LittleEndian);
			i += 2;

			short mslType = EndianOps.ToShort(data, i, ByteOrder.LittleEndian);
			hardpoint.MissileType = MissileType.GetById(mslType);
		}

		return iniStats;
	}

	/// <summary>
	/// NOTE: the "newData" parameter is unused in the original — despite the method name, it
	/// doesn't splice newData in anywhere; it just concatenates the file's existing Header with
	/// its existing RawBytes. Ported literally.
	/// </summary>
	public static VolEntry ReplaceDatBytes(byte[] newData, DataFile targetFile) {
		if (targetFile is not VolEntry entry) {
			throw new Exception($"ERROR: file({targetFile.FileName}) was not a <VolEntry> object.");
		}

		var spliceData = new List<byte>();
		if (targetFile.Header != null) {
			spliceData.AddRange(targetFile.Header);
		}
		if (targetFile.RawBytes != null) {
			spliceData.AddRange(targetFile.RawBytes);
		}

		targetFile.RawBytes = spliceData.ToArray();

		return entry;
	}
}
