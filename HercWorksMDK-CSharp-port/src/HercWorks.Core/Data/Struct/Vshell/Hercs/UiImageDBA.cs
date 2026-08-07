using HercWorks.Core.Data.File.Dyn;

namespace HercWorks.Core.Data.Struct.Vshell.Hercs;

/// <summary>
/// Data struct observed throughout VSHELL.
/// Ported from org.hercworks.core.data.struct.vshell.hercs.UiImageDBA.
/// </summary>
public class UiImageDBA {
	public DynamixBitmapArray? Dba { get; set; }
	public int OriginX { get; set; }
	public int OriginY { get; set; }
	public short FrameId { get; set; }
	public RFlag? Flags { get; set; }

	public sealed class RFlag {
		public static readonly RFlag Normal = new(0);
		public static readonly RFlag FlipXY = new(1);
		public static readonly RFlag FlipX = new(2);
		public static readonly RFlag FlipY = new(3);

		private static readonly IReadOnlyList<RFlag> All = new[] { Normal, FlipXY, FlipX, FlipY };
		private static readonly Dictionary<short, RFlag> ById = All.ToDictionary(f => f.Val);

		public short Val { get; }

		private RFlag(short flag) {
			Val = flag;
		}

		public static RFlag? Get(short v) => ById.GetValueOrDefault(v);
	}
}
