using HercWorks.Vol.Io;
using HercWorks.Vol.Util;
using System.Text;
using Xunit;

namespace HercWorks.Vol.Tests;

/// <summary>
/// The upstream repo ships no sample .vol files or unit tests, so this hand-builds a
/// minimal but structurally valid synthetic VOL (one directory, one file) to verify the
/// C# reader and 'strict' writer are faithful to the original Java format handling —
/// including a full byte-for-byte round trip.
/// </summary>
public class VolRoundTripTests {
	[Fact]
	public void ParsesAndReWritesAMinimalVolByteForByte() {
		byte[] original = BuildSyntheticVol();

		string tempDir = Directory.CreateTempSubdirectory("hercworks-test-").FullName;
		string volPath = Path.Combine(tempDir, "TEST.VOL");
		File.WriteAllBytes(volPath, original);

		try {
			Voln vol = VolFileReader.ParseVolFile(volPath);

			Assert.Equal((byte)1, vol.DirCount);
			Assert.Equal((ushort)5, vol.DirSize);
			Assert.Equal((ushort)1, vol.ListCount);
			Assert.Equal(18, vol.ListSize);
			Assert.Single(vol.Folders);
			Assert.Equal("DAT", vol.Folders[0].Label);

			VolEntry entry = Assert.Single(vol.FilesSet);
			Assert.Equal("TEST.DAT", entry.FileName);
			Assert.Equal(FileType.Dat, entry.Ext);
			Assert.Equal(41, entry.VolOffsetValue);
			Assert.Equal(new byte[] { 0x44, 0x41, 0x54, 0x41 }, entry.RawBytes); // "DATA"
			Assert.Empty(entry.Header!);
			Assert.Null(entry.UnknownEoFByte);

			string outDir = Path.Combine(tempDir, "out");
			VolFileWriter.PackVolToFileStrict(vol, outDir);

			byte[] roundTripped = File.ReadAllBytes(Path.Combine(outDir, vol.FileName!));
			Assert.Equal(original, roundTripped);
		} finally {
			Directory.Delete(tempDir, recursive: true);
		}
	}

	/// <summary>
	/// Builds the synthetic VOL described above. Offsets are computed rather than
	/// hardcoded so the layout stays correct if any section above it changes.
	/// </summary>
	private static byte[] BuildSyntheticVol() {
		var bytes = new List<byte>();

		// Magic
		bytes.AddRange(new byte[] { 0x56, 0x4F, 0x4C, 0x4E }); // "VOLN"

		// dbsim=false, vshell=true — a shell VOL, so the writer's "uppercase directory
		// name" rule applies, matching this test's uppercase "DAT" directory below.
		bytes.Add(0x00);
		bytes.Add(0x01);

		// Unknown, always zero
		bytes.Add(0x00);
		bytes.Add(0x00);

		// Load precedence
		bytes.Add(0x05);

		byte[] dirListBytes = Encoding.ASCII.GetBytes("DAT\\").Concat(new byte[] { 0x00 }).ToArray();

		// Directory count
		bytes.Add(0x01);

		// Directory list byte size (LE)
		bytes.AddRange(ByteOps.GetUInt16LEBytes(dirListBytes.Length));

		// Directory list
		bytes.AddRange(dirListBytes);

		const string fileName = "TEST.DAT";
		byte[] fileNameBytes = Encoding.ASCII.GetBytes(fileName).Concat(new byte[13 - fileName.Length]).ToArray();

		byte[] payload = Encoding.ASCII.GetBytes("DATA");
		const byte compressionType = 0x00;
		byte[] magic = { 0xDE, 0xAD, 0xBE, 0xEF };

		// The file's on-disk offset points past the file-list header (2+4 bytes) and this
		// one 18-byte entry, so compute it before appending those sections.
		int fileOffset = bytes.Count + 2 + 4 + 18;

		// File list header: total files + byte size
		bytes.AddRange(ByteOps.GetUInt16LEBytes(1));   // 1 file
		bytes.AddRange(ByteOps.GetInt32LEBytes(18));   // one 18-byte entry

		// File list entry: 13-byte name + 1-byte dir index + 4-byte offset (LE)
		bytes.AddRange(fileNameBytes);
		bytes.Add(0x00); // dirIdx 0 ("DAT")
		bytes.AddRange(ByteOps.GetInt32LEBytes(fileOffset));

		// File data: compression type + size (LE) + magic + payload
		bytes.Add(compressionType);
		bytes.AddRange(ByteOps.GetInt32LEBytes(payload.Length));
		bytes.AddRange(magic);
		bytes.AddRange(payload);

		return bytes.ToArray();
	}
}
