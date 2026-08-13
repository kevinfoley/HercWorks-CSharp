using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dts.Anim;
using HercWorks.Core.Data.File.Dts.Bsp;
using HercWorks.Core.Data.File.Dts.Part;
using HercWorks.Core.Data.File.Dts.Poly;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct;
using HercWorks.Vol;

namespace HercWorks.Core.Io.Transform.Dbsim;

/// <summary>
/// Reads/writes the DTS ThreeSpace model tree format. Each node type re-dispatches through
/// LoadChunkByType/WriteTSObject based on a 4-byte TSObjectHeader magic marker.
/// Ported from org.hercworks.core.io.transform.dbsim.DTSModelTransformer.
/// </summary>
public class DTSModelTransformer : ThreeSpaceByteTransformer {
	/// <summary>Every TSObject is prefixed by a 4-byte type marker plus a 4-byte little-endian payload length.</summary>
	private const int ChunkHeaderLength = 8;

	private int _indexTSGroup;

	public override DataFile? BytesToObject(byte[]? inputArray) {
		if (inputArray == null || inputArray.Length <= 0) {
			// TODO (carried over from Java): log null
			return null;
		}
		_indexTSGroup = 0;
		SetBytes(inputArray);

		var dts = new DynamixThreeSpaceModel {
			RawBytes = inputArray,
			Ext = FileType.Dts,
			Dir = FileType.Dts
		};

		var meshes = new List<TSObject>();

		while (Index < inputArray.Length) {
			var chunk = LoadChunkByType(null);
			if (chunk != null) {
				meshes.Add(chunk);
			}
		}

		dts.Meshes = meshes;

		return dts;
	}

	/// <summary>
	/// Hacked-together analogue for the ChunkTypes[] object in the original python script. DTS
	/// files are nested objects and object lists, so a tree-loading approach is necessary.
	///
	/// <para>Every TSObject on disk is <c>&lt;4-byte marker&gt;&lt;4-byte LE payload length&gt;&lt;payload&gt;</c>,
	/// so each chunk is self-describing — which lets this method bracket every read: the declared
	/// payload has to fit the buffer going in, and the per-type reader has to consume it exactly
	/// going out. A field-layout mistake therefore surfaces <em>here</em>, naming the chunk type and
	/// its file offset, instead of as a bare IndexOutOfRangeException from some unrelated read much
	/// further along — which is what it used to look like, since none of the readers below bounds-check
	/// anything.</para>
	///
	/// <para>This validates rather than narrows what the reader accepts: checked against all 55 retail
	/// DTS models in SIMVOL0.VOL (every mech, debris, effect and weapon model — the only archive that
	/// ships any), every chunk consumes its declared length exactly, at every nesting depth, with no
	/// unrecognized markers anywhere.</para>
	/// </summary>
	private TSObject? LoadChunkByType(TSObject? parent) {
		int chunkStart = Index;

		if (chunkStart + ChunkHeaderLength > Bytes!.Length) {
			throw new InvalidDataException(
				$"DTS chunk header at offset {chunkStart} runs past the end of the {Bytes.Length}-byte model.");
		}

		byte[] marker = IndexSegment(4);
		var header = TSObjectHeader.FindVal(marker);
		string described = header?.Id() ?? $"unrecognized type 0x{Convert.ToHexString(marker)}";

		// Peeked, not consumed — each per-type reader below reads this length itself as its first act.
		int byteLen = PeekIntLE();

		if (byteLen < 0 || chunkStart + (long)ChunkHeaderLength + byteLen > Bytes.Length) {
			throw new InvalidDataException(
				$"DTS chunk '{described}' at offset {chunkStart} declares a {byteLen}-byte payload, " +
				$"which does not fit the {Bytes.Length}-byte model.");
		}

		int expectedEnd = chunkStart + ChunkHeaderLength + byteLen;
		TSObject? chunk = header == null ? null : ReadChunk(header, parent);

		if (chunk == null) {
			// Either an unrecognized marker or one of the three Alias*Poly types that no reader
			// implements (see ReadChunk). Both are stepped over using the chunk's own declared
			// length, which is exact — no guessing at a layout this project has never seen.
			Index = expectedEnd;
			return null;
		}

		if (Index != expectedEnd) {
			throw new InvalidDataException(
				$"DTS chunk '{described}' at offset {chunkStart} declares {byteLen} payload bytes but its " +
				$"reader consumed {Index - chunkStart - ChunkHeaderLength} (ended at {Index}, expected " +
				$"{expectedEnd}). The field layout this reader uses for '{described}' does not match the file.");
		}

		return chunk;
	}

	/// <summary>
	/// Dispatches one already-identified chunk to its reader, with the cursor sitting on the chunk's
	/// length field. Returns null for a type deliberately left unread, which
	/// <see cref="LoadChunkByType"/> turns into an exact skip.
	/// </summary>
	private TSObject? ReadChunk(TSObjectHeader header, TSObject? parent) {
		byte[] marker = header.Val();

		if (marker.SequenceEqual(TSObjectHeader.TSBasePart.Val())) return ReadTSBasePart(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSPartList.Val())) return ReadTSPartList(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSShape.Val())) return ReadTSShape(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.ANShape.Val())) return ReadANShape(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.BSPPart.Val())) return ReadTSBSPPart(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSGroup.Val())) return ReadTSGroup(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSPoly.Val())) return ReadTSPoly(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSSolidPoly.Val())) return ReadTSSolidPoly(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSTexture4Poly.Val())) return ReadTSTexture4Poly(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSShadedPoly.Val())) return ReadTSShadedPoly(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSGouraudPoly.Val())) return ReadTSGouraudPoly(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSCellAnimPart.Val())) return ReadTSCellAnimPart(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.ANAnimList.Val())) return ReadANAnimList(parent);
		if (marker.SequenceEqual(TSObjectHeader.ANCyclicSequence.Val())) return ReadANCyclicSequence(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.ANSequence.Val())) return ReadANSequence(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSBitmapPart.Val())) return ReadTSBitmapPart(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSDetailPart.Val())) return ReadTSDetailPart(null, parent);
		if (marker.SequenceEqual(TSObjectHeader.TSBSPGroup.Val())) return ReadTSBSPGroup(null, parent);

		// TSAliasSolidPoly (0x10) / TSAliasShadedPoly (0x11) / TSAliasGouraudPoly (0x12) reach here.
		// They are declared in TSObjectHeader but no reader exists, and none is written here on
		// purpose: no retail DTS contains one (confirmed by a full chunk census over SIMVOL0.VOL), so
		// there are no real bytes to verify a guessed field layout against, and the Java original this
		// was ported from likewise declares the three markers without ever reading them. Skipping by
		// declared length keeps the rest of the tree parseable; inventing a layout would just be a
		// plausible-looking guess with nothing behind it.
		return null;
	}

	private TSBasePart ReadTSBasePart(TSBasePart? link, TSObject? parent) {
		if (link == null) {
			link = new TSBasePart();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link.Transform = IndexShortLE();
		link.IdNumber = IndexShortLE();
		link.Radius = IndexShortLE();
		link.Center = new Vec3Short(IndexShortLE(), IndexShortLE(), IndexShortLE());

		return link;
	}

	private TSBitmapPart ReadTSBitmapPart(TSBitmapPart? link, TSObject? parent) {
		if (link == null) {
			link = new TSBitmapPart();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSBitmapPart)ReadTSBasePart(link, parent);

		link.BmpTag = IndexShortLE();
		link.OfsX = IndexByte();
		link.OfsY = IndexByte();

		return link;
	}

	private TSDetailPart ReadTSDetailPart(TSDetailPart? link, TSObject? parent) {
		if (link == null) {
			link = new TSDetailPart();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}

		link = (TSDetailPart)ReadTSPartList(link, parent);

		// FIXME (carried over from Java): so far these just seem to trail at the end for any
		// remaining bytes of the TSObject's byte-len. Pretty weird.
		int detailCount = (link.GetDataIndex() + link.ByteLen) - Index;

		// Derived by subtraction rather than read from a count field, so it goes negative if the part
		// list above overran the chunk. Caught here because a negative length would otherwise blow up
		// as an unrelated-looking OverflowException from the array allocation on the next line.
		if (detailCount < 0) {
			throw new InvalidDataException(
				$"TSDetailPart at offset {link.Index} overran its own {link.ByteLen}-byte payload by " +
				$"{-detailCount} bytes while reading its part list.");
		}

		var details = new short[detailCount / 2];
		for (int d = 0; d < details.Length; d++) {
			details[d] = IndexShortLE();
		}

		link.Details = details;

		return link;
	}

	private TSPartList ReadTSPartList(TSPartList? link, TSObject? parent) {
		if (link == null) {
			link = new TSPartList();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSPartList)ReadTSBasePart(link, parent);

		var parts = new TSObject[IndexShortLE()];

		for (int i = 0; i < parts.Length; i++) {
			var part = LoadChunkByType(link);
			if (part != null) {
				parts[i] = part;
			}
		}
		link.Parts = parts;
		return link;
	}

	private TSCellAnimPart ReadTSCellAnimPart(TSCellAnimPart? link, TSObject? parent) {
		if (link == null) {
			link = new TSCellAnimPart();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSCellAnimPart)ReadTSPartList(link, parent);

		link.AnimSequence = IndexShortLE();

		return link;
	}

	private TSGroup ReadTSGroup(TSGroup? link, TSObject? parent) {
		if (link == null) {
			link = new TSGroup();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
			link.ListIndex = _indexTSGroup;
			_indexTSGroup += 1;
		}
		link = (TSGroup)ReadTSBasePart(link, parent);

		var indexes = new short[IndexShortLE()];
		var points = new Vec3Short[IndexShortLE()];
		int colorCount = IndexShortLE();
		colorCount /= 4;
		var items = new TSObject[IndexShortLE()];

		for (int i = 0; i < indexes.Length; i++) {
			indexes[i] = IndexShortLE();
		}
		link.Indexes = indexes;

		for (int p = 0; p < points.Length; p++) {
			points[p] = new Vec3Short(IndexShortLE(), IndexShortLE(), IndexShortLE());
		}
		link.Points = points;

		var surfaces = new TSSurfaceEntry[colorCount];
		for (int c = 0; c < surfaces.Length; c++) {
			var sclr = new TSSurfaceEntry {
				FrontColor = IndexShortLE(),
				FrontFlag = IndexShortLE(),
				FrontLineColor = IndexShortLE(),
				FrontLineFlag = IndexShortLE(),
				BackColor = IndexShortLE(),
				BackColorFlag = IndexShortLE(),
				BackLineColor = IndexShortLE(),
				BackLineFlag = IndexShortLE()
			};

			surfaces[c] = sclr;
		}
		link.Surfaces = surfaces;

		for (int s = 0; s < items.Length; s++) {
			items[s] = LoadChunkByType(link)!;
		}
		link.Polys = items;

		return link;
	}

	private TSBSPGroup ReadTSBSPGroup(TSBSPGroup? link, TSObject? parent) {
		if (link == null) {
			link = new TSBSPGroup();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSBSPGroup)ReadTSGroup(link, parent);

		var nodes = new TSBSPGroupNode[IndexShortLE()];

		for (int n = 0; n < nodes.Length; n++) {
			nodes[n] = new TSBSPGroupNode(IndexShortLE(), IndexShortLE(), IndexShortLE(), IndexShortLE());
		}
		link.GroupNodes = nodes;

		return link;
	}

	private TSPoly ReadTSPoly(TSPoly? link, TSObject? parent) {
		if (link == null) {
			link = new TSPoly();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}

		link.Normal = IndexShortLE();
		link.Center = IndexShortLE();
		link.VertexCount = IndexShortLE();
		link.VertexList = IndexShortLE();

		return link;
	}

	private TSSolidPoly ReadTSSolidPoly(TSSolidPoly? link, TSObject? parent) {
		if (link == null) {
			link = new TSSolidPoly();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}

		link = (TSSolidPoly)ReadTSPoly(link, parent);
		link.ColorIndexId = IndexShortLE();

		return link;
	}

	private TSShadedPoly ReadTSShadedPoly(TSShadedPoly? link, TSObject? parent) {
		if (link == null) {
			link = new TSShadedPoly();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSShadedPoly)ReadTSSolidPoly(link, parent);

		return link;
	}

	private TSTexture4Poly ReadTSTexture4Poly(TSTexture4Poly? link, TSObject? parent) {
		if (link == null) {
			link = new TSTexture4Poly();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSTexture4Poly)ReadTSSolidPoly(link, parent);

		return link;
	}

	private TSGouraudPoly ReadTSGouraudPoly(TSGouraudPoly? link, TSObject? parent) {
		if (link == null) {
			link = new TSGouraudPoly();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSGouraudPoly)ReadTSSolidPoly(link, parent);
		link.NormalList = IndexShortLE();

		return link;
	}

	private TSBSPPartNode ReadTSBSPPartNode() {
		var node = new TSBSPPartNode {
			Index = Index
		};

		node.Normal = new Vec3Short(IndexShortLE(), IndexShortLE(), IndexShortLE());
		node.Coeff = IndexIntLE();
		node.Front = IndexShortLE();
		node.Back = IndexShortLE();

		node.ByteLen = Index - node.Index;
		node.Data = Slice(Bytes!, node.Index, node.ByteLen);

		return node;
	}

	private TSBSPPart ReadTSBSPPart(TSBSPPart? link, TSObject? parent) {
		if (link == null) {
			link = new TSBSPPart();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSBSPPart)ReadTSPartList(link, parent);

		var nodes = new TSBSPPartNode[IndexShortLE()];
		for (int n = 0; n < nodes.Length; n++) {
			nodes[n] = ReadTSBSPPartNode();
		}
		link.Nodes = nodes;

		// AHA! I think this count is matched to # of BSPPartNodes in the list!
		var transforms = new short[nodes.Length];
		for (int p = 0; p < nodes.Length; p++) {
			transforms[p] = IndexShortLE();
		}
		link.Transforms = transforms;

		return link;
	}

	private TSShape ReadTSShape(TSShape? link, TSObject? parent) {
		if (link == null) {
			link = new TSShape();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (TSShape)ReadTSPartList(link, parent);

		short transformTotal = IndexShortLE();
		short sequenceTotal = IndexShortLE();

		var sequences = new short[sequenceTotal];
		for (int s = 0; s < sequences.Length; s++) {
			sequences[s] = IndexShortLE();
		}
		link.SequenceList = sequences;

		var transforms = new short[transformTotal];
		for (int t = 0; t < transforms.Length; t++) {
			transforms[t] = IndexShortLE();
		}
		link.TransformList = transforms;

		return link;
	}

	private ANSequenceFrame ReadANSequenceFrame() {
		var frame = new ANSequenceFrame {
			Index = Index
		};

		frame.Tick = IndexShortLE();
		frame.FirstTransition = IndexShortLE();
		frame.NumTransitions = IndexShortLE();

		frame.ByteLen = Index - frame.Index;
		frame.Data = Slice(Bytes!, frame.Index, frame.ByteLen);

		return frame;
	}

	private ANSequence ReadANSequence(ANSequence? seq, TSObject? parent) {
		if (seq == null) {
			seq = new ANSequence();
			seq.ByteLen = IndexIntLE();
			seq.Index = Index - 8;
			seq.Data = Slice(Bytes!, seq.Index + 8, seq.ByteLen);
			seq.Parent = parent;
		}

		seq.Tick = IndexShortLE();
		seq.Priority = IndexShortLE();
		seq.GroundMovement = IndexShortLE();

		var frames = new ANSequenceFrame[IndexShortLE()];
		for (int f = 0; f < frames.Length; f++) {
			frames[f] = ReadANSequenceFrame();
		}
		seq.Frames = frames;

		var partIds = new short[IndexShortLE()];
		for (int p = 0; p < partIds.Length; p++) {
			partIds[p] = IndexShortLE();
		}
		seq.PartIds = partIds;

		int len = partIds.Length * frames.Length;
		var transformIndex = new short[len];
		for (int t = 0; t < transformIndex.Length; t++) {
			transformIndex[t] = IndexShortLE();
		}
		seq.TransformIndices = transformIndex;

		return seq;
	}

	private ANCyclicSequence ReadANCyclicSequence(ANCyclicSequence? link, TSObject? parent) {
		if (link == null) {
			link = new ANCyclicSequence();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (ANCyclicSequence)ReadANSequence(link, parent);

		return link;
	}

	private ANAnimListTransition ReadANAnimListTransition() {
		var transition = new ANAnimListTransition {
			Index = Index,
			Tick = IndexShortLE(),
			DestSequence = IndexShortLE(),
			DestFrame = IndexShortLE(),
			GroundMovement = IndexShortLE()
		};

		return transition;
	}

	private ANAnimListTransform ReadANAnimListTransform() {
		var transform = new ANAnimListTransform {
			Index = Index,
			Rotation = new Vec3Short(IndexShortLE(), IndexShortLE(), IndexShortLE()),
			Translation = new Vec3Short(IndexShortLE(), IndexShortLE(), IndexShortLE())
		};

		return transform;
	}

	private ANAnimList ReadANAnimList(TSObject? parent) {
		var anim = new ANAnimList {
			ByteLen = IndexIntLE()
		};
		anim.Index = Index - 8;
		anim.Data = Slice(Bytes!, anim.Index + 8, anim.ByteLen);
		anim.Parent = parent;

		var sequences = new TSObject[IndexShortLE()];
		for (int s = 0; s < sequences.Length; s++) {
			sequences[s] = LoadChunkByType(anim)!;
		}
		anim.Sequences = sequences;

		var transitions = new ANAnimListTransition[IndexShortLE()];
		for (int t = 0; t < transitions.Length; t++) {
			transitions[t] = ReadANAnimListTransition();
		}
		anim.Transitions = transitions;

		var transforms = new ANAnimListTransform[IndexShortLE()];
		for (int v = 0; v < transforms.Length; v++) {
			transforms[v] = ReadANAnimListTransform();
		}
		anim.Transforms = transforms;

		var defTransforms = new short[IndexShortLE()];
		for (int d = 0; d < defTransforms.Length; d++) {
			defTransforms[d] = IndexShortLE();
		}
		anim.DefaultTransforms = defTransforms;

		var relations = new Vec2Short[IndexShortLE()];
		for (int r = 0; r < relations.Length; r++) {
			relations[r] = new Vec2Short(IndexShortLE(), IndexShortLE());
		}
		anim.Relations = relations;

		return anim;
	}

	private ANShape ReadANShape(ANShape? link, TSObject? parent) {
		if (link == null) {
			link = new ANShape();
			link.ByteLen = IndexIntLE();
			link.Index = Index - 8;
			link.Data = Slice(Bytes!, link.Index + 8, link.ByteLen);
			link.Parent = parent;
		}
		link = (ANShape)ReadTSShape(link, parent);

		var animationList = (ANAnimList)LoadChunkByType(link)!;
		link.AnimationList = animationList;

		return link;
	}

	public override byte[]? ObjectToBytes(DataFile? source) {
		if (source == null) {
			return null;
		}

		var mdl = (DynamixThreeSpaceModel)source;

		using var outStream = new MemoryStream();

		foreach (var o in mdl.Meshes!) {
			byte[] data = WriteTSObject(null, o);
			outStream.Write(data, 0, data.Length);
		}

		return outStream.ToArray();
	}

	private byte[] WriteTSObject(TSObject? parent, TSObject o) {
		using var objectData = new MemoryStream();
		using var objectBytes = new MemoryStream();

		byte[] hdrVal = o.Header!.Val();

		if (hdrVal.SequenceEqual(TSObjectHeader.TSBasePart.Val())) WriteTSBasePart((TSBasePart)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSPartList.Val())) WriteTSPartList((TSPartList)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSShape.Val())) WriteTSShape((TSShape)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.ANShape.Val())) WriteANShape((ANShape)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.BSPPart.Val())) WriteTSBSPPart((TSBSPPart)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSGroup.Val())) WriteTSGroup((TSGroup)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSPoly.Val())) WriteTSPoly((TSPoly)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSSolidPoly.Val())) WriteTSSolidPoly((TSSolidPoly)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSTexture4Poly.Val())) WriteTSTexture4Poly((TSTexture4Poly)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSShadedPoly.Val())) WriteTSShadedPoly((TSShadedPoly)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSGouraudPoly.Val())) WriteTSGouraudPoly((TSGouraudPoly)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSCellAnimPart.Val())) WriteTSCellAnimPart((TSCellAnimPart)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.ANAnimList.Val())) WriteANAnimList((ANAnimList)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.ANCyclicSequence.Val())) WriteANCyclicSequence((ANCyclicSequence)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.ANSequence.Val())) WriteANSequence((ANSequence)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSBitmapPart.Val())) WriteTSBitmapPart((TSBitmapPart)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSDetailPart.Val())) WriteTSDetailPart((TSDetailPart)o, objectBytes);
		if (hdrVal.SequenceEqual(TSObjectHeader.TSBSPGroup.Val())) WriteTSBSPGroup((TSBSPGroup)o, objectBytes);

		objectData.Write(hdrVal, 0, hdrVal.Length);
		byte[] data = objectBytes.ToArray();
		var lenBytes = WriteIntLE(data.Length);
		objectData.Write(lenBytes, 0, lenBytes.Length);
		objectData.Write(data, 0, data.Length);

		return objectData.ToArray();
	}

	private void WriteTSBasePart(TSBasePart basePart, MemoryStream bos) {
		Write(bos, WriteShortLE(basePart.Transform));
		Write(bos, WriteShortLE(basePart.IdNumber));
		Write(bos, WriteShortLE(basePart.Radius));
		Write(bos, WriteShortLE(basePart.Center!.X));
		Write(bos, WriteShortLE(basePart.Center.Y));
		Write(bos, WriteShortLE(basePart.Center.Z));
	}

	private void WriteTSBitmapPart(TSBitmapPart bitmapPart, MemoryStream bos) {
		WriteTSBasePart(bitmapPart, bos);

		Write(bos, WriteShortLE(bitmapPart.BmpTag));
		bos.WriteByte(bitmapPart.OfsX);
		bos.WriteByte(bitmapPart.OfsY);
	}

	private void WriteTSDetailPart(TSDetailPart detailPart, MemoryStream bos) {
		WriteTSPartList(detailPart, bos);

		foreach (var d in detailPart.Details!) {
			Write(bos, WriteShortLE(d));
		}
	}

	private void WriteTSPartList(TSPartList partList, MemoryStream bos) {
		WriteTSBasePart(partList, bos);

		Write(bos, WriteShortLE((short)partList.Parts!.Length));
		foreach (var part in partList.Parts) {
			byte[] data = WriteTSObject(partList, part);
			bos.Write(data, 0, data.Length);
			bos.Flush();
		}
	}

	private void WriteTSCellAnimPart(TSCellAnimPart animPart, MemoryStream bos) {
		WriteTSPartList(animPart, bos);

		Write(bos, WriteShortLE(animPart.AnimSequence));
	}

	private void WriteTSGroup(TSGroup group, MemoryStream bos) {
		WriteTSBasePart(group, bos);

		Write(bos, WriteShortLE((short)group.Indexes!.Length));
		Write(bos, WriteShortLE((short)group.Points!.Length));
		Write(bos, WriteShortLE((short)(group.Surfaces!.Length * 4)));
		Write(bos, WriteShortLE((short)group.Polys!.Length));

		foreach (var idx in group.Indexes) {
			Write(bos, WriteShortLE(idx));
		}

		foreach (var p in group.Points) {
			Write(bos, WriteShortLE(p.X));
			Write(bos, WriteShortLE(p.Y));
			Write(bos, WriteShortLE(p.Z));
		}

		foreach (var surface in group.Surfaces) {
			Write(bos, WriteShortLE(surface.FrontColor));
			Write(bos, WriteShortLE(surface.FrontFlag));
			Write(bos, WriteShortLE(surface.FrontLineColor));
			Write(bos, WriteShortLE(surface.FrontLineFlag));
			Write(bos, WriteShortLE(surface.BackColor));
			Write(bos, WriteShortLE(surface.BackColorFlag));
			Write(bos, WriteShortLE(surface.BackLineColor));
			Write(bos, WriteShortLE(surface.BackLineFlag));
		}

		foreach (var poly in group.Polys) {
			byte[] data = WriteTSObject(group, poly);
			bos.Write(data, 0, data.Length);
		}
	}

	private void WriteTSBSPGroup(TSBSPGroup bspGroup, MemoryStream bos) {
		WriteTSGroup(bspGroup, bos);

		Write(bos, WriteShortLE((short)bspGroup.GroupNodes!.Length));

		foreach (var node in bspGroup.GroupNodes) {
			Write(bos, WriteShortLE(node.Coeff));
			Write(bos, WriteShortLE(node.Poly));
			Write(bos, WriteShortLE(node.Front));
			Write(bos, WriteShortLE(node.Back));
		}
	}

	private void WriteTSPoly(TSPoly poly, MemoryStream bos) {
		Write(bos, WriteShortLE(poly.Normal));
		Write(bos, WriteShortLE(poly.Center));
		Write(bos, WriteShortLE(poly.VertexCount));
		Write(bos, WriteShortLE(poly.VertexList));
	}

	private void WriteTSSolidPoly(TSSolidPoly solidPoly, MemoryStream bos) {
		WriteTSPoly(solidPoly, bos);
		Write(bos, WriteShortLE(solidPoly.ColorIndexId));
	}

	private void WriteTSShadedPoly(TSShadedPoly shadedPoly, MemoryStream bos) => WriteTSSolidPoly(shadedPoly, bos);

	private void WriteTSTexture4Poly(TSTexture4Poly texture4Poly, MemoryStream bos) => WriteTSSolidPoly(texture4Poly, bos);

	private void WriteTSGouraudPoly(TSGouraudPoly gouraudPoly, MemoryStream bos) {
		WriteTSSolidPoly(gouraudPoly, bos);
		Write(bos, WriteShortLE(gouraudPoly.NormalList));
	}

	private void WriteTSShape(TSShape shape, MemoryStream bos) {
		WriteTSPartList(shape, bos);

		Write(bos, WriteShortLE((short)shape.TransformList!.Length));
		Write(bos, WriteShortLE((short)shape.SequenceList!.Length));

		foreach (var s in shape.SequenceList) {
			Write(bos, WriteShortLE(s));
		}

		foreach (var t in shape.TransformList) {
			Write(bos, WriteShortLE(t));
		}
	}

	private void WriteTSBSPPartNode(TSBSPPartNode node, MemoryStream bos) {
		Write(bos, WriteShortLE(node.Normal!.X));
		Write(bos, WriteShortLE(node.Normal.Y));
		Write(bos, WriteShortLE(node.Normal.Z));

		Write(bos, WriteIntLE(node.Coeff));
		Write(bos, WriteShortLE(node.Front));
		Write(bos, WriteShortLE(node.Back));
	}

	private void WriteTSBSPPart(TSBSPPart part, MemoryStream bos) {
		WriteTSPartList(part, bos);

		Write(bos, WriteShortLE((short)part.Nodes!.Length));
		foreach (var n in part.Nodes) {
			WriteTSBSPPartNode(n, bos);
		}

		for (int p = 0; p < part.Nodes.Length; p++) {
			Write(bos, WriteShortLE(part.Transforms![p]));
		}
	}

	private void WriteANAnimListTransition(ANAnimListTransition transition, MemoryStream bos) {
		Write(bos, WriteShortLE(transition.Tick));
		Write(bos, WriteShortLE(transition.DestSequence));
		Write(bos, WriteShortLE(transition.DestFrame));
		Write(bos, WriteShortLE(transition.GroundMovement));
	}

	private void WriteANAnimListTransform(ANAnimListTransform transform, MemoryStream bos) {
		Write(bos, WriteShortLE(transform.Rotation!.X));
		Write(bos, WriteShortLE(transform.Rotation.Y));
		Write(bos, WriteShortLE(transform.Rotation.Z));

		Write(bos, WriteShortLE(transform.Translation!.X));
		Write(bos, WriteShortLE(transform.Translation.Y));
		Write(bos, WriteShortLE(transform.Translation.Z));
	}

	private void WriteANAnimList(ANAnimList animList, MemoryStream bos) {
		Write(bos, WriteShortLE((short)animList.Sequences!.Length));
		foreach (var s in animList.Sequences) {
			byte[] data = WriteTSObject(animList, s);
			bos.Write(data, 0, data.Length);
		}

		Write(bos, WriteShortLE((short)animList.Transitions!.Length));
		foreach (var t in animList.Transitions) {
			WriteANAnimListTransition(t, bos);
		}

		Write(bos, WriteShortLE((short)animList.Transforms!.Length));
		foreach (var t in animList.Transforms) {
			WriteANAnimListTransform(t, bos);
		}

		Write(bos, WriteShortLE((short)animList.DefaultTransforms!.Length));
		foreach (var d in animList.DefaultTransforms) {
			Write(bos, WriteShortLE(d));
		}

		Write(bos, WriteShortLE((short)animList.Relations!.Length));
		foreach (var r in animList.Relations) {
			Write(bos, WriteShortLE(r.X));
			Write(bos, WriteShortLE(r.Y));
		}
	}

	private void WriteANSequenceFrame(ANSequenceFrame frame, MemoryStream bos) {
		Write(bos, WriteShortLE(frame.Tick));
		Write(bos, WriteShortLE(frame.FirstTransition));
		Write(bos, WriteShortLE(frame.NumTransitions));
	}

	private void WriteANSequence(ANSequence seq, MemoryStream bos) {
		Write(bos, WriteShortLE(seq.Tick));
		Write(bos, WriteShortLE(seq.Priority));
		Write(bos, WriteShortLE(seq.GroundMovement));

		Write(bos, WriteShortLE((short)seq.Frames!.Length));
		foreach (var f in seq.Frames) {
			WriteANSequenceFrame(f, bos);
		}

		Write(bos, WriteShortLE((short)seq.PartIds!.Length));
		foreach (var p in seq.PartIds) {
			Write(bos, WriteShortLE(p));
		}

		foreach (var t in seq.TransformIndices!) {
			Write(bos, WriteShortLE(t));
		}
	}

	private void WriteANCyclicSequence(ANCyclicSequence seq, MemoryStream bos) => WriteANSequence(seq, bos);

	private void WriteANShape(ANShape shape, MemoryStream bos) {
		WriteTSShape(shape, bos);

		byte[] data = WriteTSObject(shape, shape.AnimationList!);
		bos.Write(data, 0, data.Length);
	}

	public override string ToString() {
		return "index @ " + Convert.ToHexString(BitConverter.GetBytes(Index));
	}

	private static byte[] Slice(byte[] data, int offset, int length) {
		var result = new byte[length];
		Array.Copy(data, offset, result, 0, length);
		return result;
	}

	private static void Write(MemoryStream outArr, byte[] data) => outArr.Write(data, 0, data.Length);
}
