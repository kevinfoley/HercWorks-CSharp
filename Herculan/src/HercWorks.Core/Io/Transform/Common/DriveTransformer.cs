using System.Text;
using HercWorks.Core.Data.File.Cfg;

namespace HercWorks.Core.Io.Transform.Common;

/// <summary>Reads and writes <c>data\drive.cfg</c> (<see cref="Drive"/>).</summary>
public class DriveTransformer : ByteTransformer<Drive> {
	public override Drive? Parse(byte[]? bytes) =>
		bytes == null
			? null
			: new Drive {
				Directory = Encoding.Latin1.GetString(bytes)
					.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
					.FirstOrDefault()
			};

	public override byte[]? Write(Drive source) => Encoding.Latin1.GetBytes(source.Directory ?? string.Empty);
}
