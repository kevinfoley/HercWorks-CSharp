using HercWorks.Core.Data.File;
using HercWorks.Vol;
using System.Text;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Ported from org.hercworks.core.io.transform.common.BinStringFileTransformer.</summary>
public class BinStringFileTransformer : ByteTransformer<StringBinaryFile> {
	public override StringBinaryFile? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}
		Index = 0;
		SetBytes(inputArray);

		var binFile = new StringBinaryFile();

		// note - the reading here ditches the structure of the file; writing back to the format
		// will do the metadata generation.
		int totalStrings = IndexIntLE();

		Skip(4); // skips total strings size, not needed here.

		int indexStart = Index;
		int stringStart = Index + totalStrings * 2;

		var values = new string[totalStrings];
		for (int i = 0; i < totalStrings; i++) {
			Index = indexStart + i * 2;
			int offset = IndexShortLE();

			if (i < totalStrings - 1) {
				Index = indexStart + (i + 1) * 2;
				int nextIndex = IndexShortLE();

				Index = stringStart + offset;
				values[i] = IndexString(nextIndex - offset).Trim();
			} else {
				Index = stringStart + offset;
				values[i] = IndexString(inputArray.Length - Index).Trim();
			}
		}

		binFile.Values = values;

		return binFile;
	}

	public override byte[]? Write(StringBinaryFile? sbf) {
		if (sbf == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		var index = new short[sbf.Values!.Length];

		int size = 0;
		for (int s = 0; s < sbf.Values.Length; s++) {
			index[s] = (short)size;
			size += sbf.Values[s].Length;
			size += sbf.Values[s].EndsWith(" ") ? 0 : 1; // null terminal byte
		}

		var totalBytes = WriteIntLE(sbf.Values.Length);
		outStream.Write(totalBytes, 0, totalBytes.Length);

		var sizeBytes = WriteIntLE(size);
		outStream.Write(sizeBytes, 0, sizeBytes.Length);

		for (int i = 0; i < sbf.Values.Length; i++) {
			var idxBytes = WriteShortLE(index[i]);
			outStream.Write(idxBytes, 0, idxBytes.Length);
		}

		for (int t = 0; t < sbf.Values.Length; t++) {
			var strBytes = Encoding.ASCII.GetBytes(sbf.Values[t]);
			outStream.Write(strBytes, 0, strBytes.Length);
			outStream.WriteByte(0x00);
		}

		return outStream.ToArray();
	}
}
