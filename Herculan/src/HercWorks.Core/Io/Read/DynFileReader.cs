using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Read;

/// <summary>Ported from org.hercworks.core.io.read.DynFileReader.</summary>
public static class DynFileReader {
	/// <summary>Loads a DynamixPalette file into memory.</summary>
	public static DynamixPalette? LoadDPL(string filePath) {
		if (!File.Exists(filePath)) {
			throw new IOException("File not found:" + filePath);
		}

		byte[] fileBytes = File.ReadAllBytes(filePath);

		if (fileBytes.Length <= 0) {
			return null;
		}

		var convert = new DynamixPaletteTransformer();
		var newDPal = (DynamixPalette?)convert.BytesToObject(fileBytes);

		if (newDPal != null) {
			newDPal.FileName = DataFile.MakeFileName(filePath);
			newDPal.AssignDir(filePath);
		}

		return newDPal;
	}

	public static DynamixBitmap? ParseBytesToDBM(byte[] data) {
		if (data.Length == 0) {
			// TODO (carried over from Java): log empty file warning.
			return null;
		}

		var convert = new DynamixBitmapTransformer();
		return (DynamixBitmap?)convert.BytesToObject(data);
	}

	public static DynamixBitmapArray? ParseBytesToDBA(byte[] data) {
		if (data.Length == 0) {
			// TODO (carried over from Java): log warning about empty file
			return null;
		}

		var transform = new DynamixBitmapArrayTransformer();
		return (DynamixBitmapArray?)transform.BytesToObject(data);
	}

	public static DynamixBitmap? LoadDBM(string filePath) {
		if (!File.Exists(filePath)) {
			throw new IOException("File not found:" + filePath);
		}

		byte[] fileBytes = File.ReadAllBytes(filePath);

		DynamixBitmap? newDBM = null;
		if (fileBytes.Length > 0) {
			newDBM = ParseBytesToDBM(fileBytes);
			if (newDBM != null) {
				newDBM.FileName = DataFile.MakeFileName(filePath);
				newDBM.AssignDir(filePath);
			}
		}

		return newDBM;
	}

	public static DynamixBitmapArray? LoadDBA(string filePath) {
		if (!File.Exists(filePath)) {
			throw new IOException("File not found:" + filePath);
		}

		// Original tagged this read as byteOrder(LITTLE_ENDIAN), but per the confirmed
		// Bytes-library semantics that tag has no effect on .array() output — the bytes read
		// here are unaffected either way.
		byte[] fileBytes = File.ReadAllBytes(filePath);

		DynamixBitmapArray? newDBA = null;
		if (fileBytes.Length > 0) {
			newDBA = ParseBytesToDBA(fileBytes);
			if (newDBA != null) {
				newDBA.FileName = DataFile.MakeFileName(filePath);
				newDBA.AssignDir(filePath);
			}
		}

		return newDBA;
	}

	public static List<byte> GetDBMUniqueColors(DynamixBitmap dbm) {
		var matches = new List<byte>();

		foreach (var b in dbm.ImageData!) {
			byte off = (byte)(b - 8);
			if (!matches.Contains(off)) {
				matches.Add(b);
			}
		}
		return matches;
	}

	public static List<byte> MatchUniqueColorToPalette(List<byte> colors, DynamixPalette dpl) {
		var matches = new List<byte>();

		foreach (var b in colors) {
			if (dpl.Colors.ContainsKey(b)) {
				matches.Add(b);
			}
		}
		return matches;
	}

	public static string? GetVolDirOfFile(string filePath, string fileName) {
		var tokens = filePath.Split('\\');

		string? dir = null;
		foreach (var t in tokens) {
			if (FileTypeExtensions.FromExtension(t) != null) {
				dir = t;
				break;
			}
		}

		if (dir != null) {
			dir = filePath.Substring(0, filePath.LastIndexOf("\\" + dir, StringComparison.Ordinal)) + "\\";
		}
		return dir;
	}
}
