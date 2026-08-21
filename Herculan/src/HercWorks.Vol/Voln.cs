namespace HercWorks.Vol;

/// <summary>
/// Library representation of a Dynamix ThreeSpace VOL ('.vol') file.
/// Use <see cref="VolnBuilder"/> to construct one from scratch.
/// Ported from org.hercworks.voln.Voln.
/// </summary>
public class Voln : DataFile {
	public const int FileListHeaderLen = 18;

	// Order-specific data captured during VOL load/parse from a compiled VOL file.
	public string? DestPath { get; set; }
	public ExeUse ExeType { get; set; }
	public bool DbsimFlag { get; set; }
	public bool VshellFlag { get; set; }

	/// <summary>0x05 for 'first loaded', 0x0A 'load second' (e.g. SHELL1.vol, SIMPATCH.vol).</summary>
	public byte VolOrderNum { get; set; }

	public byte DirCount { get; set; }
	public ushort DirSize { get; set; }
	public VolEntry[] FilesSet { get; set; } = Array.Empty<VolEntry>();
	public ushort ListCount { get; set; }
	public int ListSize { get; set; }

	// Expanded to memory for export or editing.
	public Dictionary<byte, VolDir> Folders { get; set; } = new();
	public HashSet<DataFile> LooseFiles { get; set; } = new();

	public Voln() { }

	private Voln(VolnBuilder b) {
		DestPath = b.DestPath;
		RawBytes = b.RawBytes;
		GameDirPath = b.GameDirPath;
		FileName = b.FileName;
		DbsimFlag = b.DbsimFlag;
		VshellFlag = b.VshellFlag;
		DirCount = b.DirCount;
		DirSize = b.DirSize;
	}

	public override string ToString() {
		return $"Vol {FileName} ({FilePath})";
	}

	public class VolnBuilder {
		public string? FileName { get; private set; }
		public string? GameDirPath { get; private set; }
		public string? DestPath { get; private set; }
		public byte[]? RawBytes { get; private set; }
		public bool DbsimFlag { get; private set; }
		public bool VshellFlag { get; private set; }
		public byte DirCount { get; private set; }
		public ushort DirSize { get; private set; }

		public Voln Build() => new(this);

		public VolnBuilder SetFileName(string fileName) { FileName = fileName; return this; }
		public VolnBuilder SetGameDirPath(string gameDirPath) { GameDirPath = gameDirPath; return this; }
		public VolnBuilder SetDestPath(string destPath) { DestPath = destPath; return this; }
		public VolnBuilder SetRawBytes(byte[] rawBytes) { RawBytes = rawBytes; return this; }
		public VolnBuilder SetDbsimFlag(bool dbsimFlag) { DbsimFlag = dbsimFlag; return this; }
		public VolnBuilder SetVshellFlag(bool vshellFlag) { VshellFlag = vshellFlag; return this; }
		public VolnBuilder SetDirCount(byte dirCount) { DirCount = dirCount; return this; }
		public VolnBuilder SetDirSize(ushort dirSize) { DirSize = dirSize; return this; }
	}

	public enum ExeUse {
		Dbsim = 0,
		Vshell = 1
	}

	/// <summary>
	/// Pre-computed header byte sequences.
	/// Credit for the header layout notes: an Earthsiege 2 reverse-engineering
	/// write-up whose original author/site could not be identified.
	///
	/// lang0     56 4F 4C 4E 00 01 00 00 05 03 0F 00  len: D484
	/// shell0    56 4F 4C 4E 00 01 00 00 05 06 1E 00  len: 86B052
	/// shell1    56 4F 4C 4E 00 01 00 00 0A 01 05 00  len: 10EE1
	/// shlsound  56 4F 4C 4E 00 01 00 00 05 01 05 00  len: 206B68
	/// simlang   56 4F 4C 4E 01 00 00 00 05 02 0A 00  len: A1F1
	/// simalert  56 4F 4C 4E 01 00 00 00 05 07 23 00  len: B6B10
	/// simpatch  56 4F 4C 4E 01 00 00 00 0A 29 CB 00  len: 11AF5
	/// simsound  56 4F 4C 4E 01 00 00 00 05 03 0F 00  len: 22F692
	/// simvoicg  56 4F 4C 4E 01 00 00 00 05 01 0A 00  len: 64DCAE
	/// simvoice  56 4F 4C 4E 01 00 00 00 05 01 0A 00  len: 6B7567
	/// simvoicf  56 4F 4C 4E 01 00 00 00 05 01 0A 00  len: 6ECD7F
	/// simvol0   56 4F 4C 4E 01 00 00 00 05 27 C1 00  len: 1D63B29
	/// zones     56 4F 4C 4E 01 01 00 00 05 03 0F 00  len: 208969
	///
	/// The first four bytes ("VOLN" in ASCII) indicate an ES volume file (magic bytes).
	/// The 5th byte is 0x01 if the file is used in the simulator, 0x00 if used in the shell.
	/// The 6th byte is 0x01 if the file is used in the shell, 0x00 if used in the simulator.
	/// Two bytes are ignored and always zero.
	/// The 9th byte is some kind of counter (currently unknown).
	/// The 10th byte is the directory count (how many directories are in the volume).
	/// The 11th and 12th bytes determine how many bytes the directory list has (little-endian).
	/// </summary>
	public static class ByteHeader {
		public static readonly byte[] Voln = { 0x56, 0x4F, 0x4C, 0x4E };
		public static readonly byte[] VolnHeaderLen = { 0x56, 0x4F, 0x4C, 0x4E, 0x00, 0x01, 0x00, 0x00, 0x00 };
	}

	public static List<string> FilesNoHeader() {
		return new List<string> { FileType.Dat.Val(), FileType.Gl.Val(), FileType.Bnd.Val() };
	}
}
