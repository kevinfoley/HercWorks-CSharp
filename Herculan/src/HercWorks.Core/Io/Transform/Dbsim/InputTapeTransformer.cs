using HercWorks.Core.Data.File.Dbsim;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Transforms byte[] data to and from .TAP input tapes (see <see cref="InputTape"/> for the model and
/// docs/formats/tap-input-tape.md for the format). New: no Java equivalent, not a ported format — read
/// from DBSIM's own writer.
///
/// <para>The frame stream has no count and no terminator: DBSIM reads until a header read comes up
/// short, so a trailing partial header is dropped here the same way.</para>
/// </summary>
public class InputTapeTransformer : ByteTransformer<InputTape> {
	/// <summary>The fixed part of a frame record.</summary>
	public const int FrameHeaderSize = 0x18;

	/// <summary>One cockpit mouse record.</summary>
	public const int MouseEventSize = 14;

	public override InputTape? Parse(byte[]? inputArray) {
		if (inputArray == null) {
			return null;
		}

		SetBytes(inputArray);
		var tape = new InputTape();

		for (int i = 0; i < InputTape.BundleFileCount; i++) {
			if (Index + 4 > inputArray.Length) {
				return null;
			}

			int size = IndexIntLE();
			if (size < 0 || Index + size > inputArray.Length) {
				return null;
			}

			tape.Bundle[i] = IndexSegment(size);
		}

		if (Index + InputTape.CapabilityBlockSize > inputArray.Length) {
			return tape;
		}

		tape.Capabilities = IndexSegment(InputTape.CapabilityBlockSize);

		while (Index + FrameHeaderSize <= inputArray.Length) {
			var frame = new InputTape.Frame {
				CommandWord = IndexShortLE(),
				Axes = IndexShortLEArray(4),
				TickDelta = IndexShortLE(),
				ButtonBank = IndexByte(),
				RawButtonBank = IndexByte(),
				TriggerBank = IndexByte(),
				Spare = IndexByte(),
			};

			int mouseCount = IndexIntLE();
			int commandCount = IndexIntLE();
			if (mouseCount < 0 || commandCount < 0
				|| Index + (long)mouseCount * MouseEventSize + (long)commandCount * 2 > inputArray.Length) {
				break;
			}

			for (int m = 0; m < mouseCount; m++) {
				frame.MouseEvents.Add(new InputTape.MouseEvent {
					X = IndexIntLE(),
					Y = IndexIntLE(),
					Buttons = (ushort)IndexShortLE(),
					Time = IndexIntLE(),
				});
			}

			frame.Commands = IndexShortLEArray(commandCount);
			tape.Frames.Add(frame);
		}

		return tape;
	}

	public override byte[]? Write(InputTape tape) {
		if (tape == null) {
			return null;
		}

		using var outStream = new MemoryStream();

		void Emit(byte[] bytes) => outStream.Write(bytes, 0, bytes.Length);

		for (int i = 0; i < InputTape.BundleFileCount; i++) {
			byte[] file = tape.Bundle[i] ?? Array.Empty<byte>();
			Emit(WriteIntLE(file.Length));
			Emit(file);
		}

		Emit(tape.Capabilities);

		foreach (var frame in tape.Frames) {
			Emit(WriteShortLE(frame.CommandWord));
			Emit(WriteShortLESegment(frame.Axes));
			Emit(WriteShortLE(frame.TickDelta));
			outStream.WriteByte(frame.ButtonBank);
			outStream.WriteByte(frame.RawButtonBank);
			outStream.WriteByte(frame.TriggerBank);
			outStream.WriteByte(frame.Spare);
			Emit(WriteIntLE(frame.MouseEvents.Count));
			Emit(WriteIntLE(frame.Commands.Length));

			foreach (var mouse in frame.MouseEvents) {
				Emit(WriteIntLE(mouse.X));
				Emit(WriteIntLE(mouse.Y));
				Emit(WriteShortLE((short)mouse.Buttons));
				Emit(WriteIntLE(mouse.Time));
			}

			Emit(WriteShortLESegment(frame.Commands));
		}

		return outStream.ToArray();
	}
}
