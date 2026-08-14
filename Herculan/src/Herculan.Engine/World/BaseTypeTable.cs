using System.Buffers.Binary;
using Herculan.Engine.Content;

namespace Herculan.Engine.World;

/// <summary>Which model library a base type's shape comes out of.</summary>
public enum BaseShapeSource {
	/// <summary>
	/// <c>dgs\BASES.DGS</c> — the static-structure library, covering 57 of the 65 structure types.
	/// Read by <see cref="HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer"/> — see
	/// docs/formats/dgs-hd0-notes.md for the format.
	/// </summary>
	StaticLibrary,

	/// <summary>
	/// <c>dts\BASES_AN.DTS</c> — the animated-structure library, an ordinary DTS whose eight roots
	/// are eight unrelated objects rather than LODs of one.
	/// </summary>
	AnimatedLibrary
}

/// <summary>
/// One entry of the base-type table: everything the engine needs to draw a placed structure.
/// </summary>
/// <param name="Index">The type index a mission's block-9 record carries.</param>
/// <param name="ShapeIndex">Which shape inside <paramref name="Source"/>'s library this type is.</param>
/// <param name="Source">Which library that is.</param>
/// <param name="TextureBankName">
/// <c>BASETEX</c>, <c>VEHTEX</c> or <c>RAZORTEX</c> — the <c>.DBA</c> the type's polys sample from.
/// </param>
public readonly record struct BaseType(
	int Index, int ShapeIndex, BaseShapeSource Source, string TextureBankName);

/// <summary>
/// <c>dat\BASES.DAT</c> — the game's table of structure types, the thing that turns a mission's
/// block-9 type number into a model and a texture bank. 65 entries in the retail file.
///
/// <para>Read from <c>Bases_LoadTypeTable</c> (<c>0043a2e0</c>, <c>base.cpp</c>'s resource-load
/// sequence), which streams a count and then fixed-shape records with one nested variable-length
/// array each, and from <c>BaseType_LoadShape</c> (<c>00405ebc</c>), which is the whole of the model
/// selection:</para>
/// <code>
/// rec = typeIndex * 0x3c + table
/// if (rec[6] == 0) shape = load("dgs\bases",    rec[2])   // static
/// else             shape = load("dts\bases_an", rec[2])   // animated
/// texture = rec[0x32] == 0 ? basetex : (typeIndex == 0x34 ? razortex : vehtex)
/// </code>
///
/// <para>That lone <c>typeIndex == 0x34</c> case is in the original exactly as written — type 52
/// borrows RAZOR's mech texture bank rather than either structure bank.</para>
///
/// <para>The record shape is confirmed by construction: walking it consumes the retail file's 6,422
/// content bytes exactly, with nothing left over. It also lines up with the model libraries on the
/// other side — exactly eight types select the animated library, and <c>BASES_AN.DTS</c> holds
/// exactly eight roots, numbered 0-7 the way those eight types reference them.</para>
///
/// <para>Only the fields the engine uses are named. The rest of each record (hit points, collision
/// class, the nested sub-record array, the debris table) is skipped rather than modelled, because
/// nothing consumes it yet and guessing at field meanings from position is how this project's
/// earlier <c>BASES.DGS</c> hypothesis went wrong.</para>
/// </summary>
public sealed class BaseTypeTable {
	/// <summary>VOL folder and name of the table.</summary>
	public const string ResourceFolder = "dat";

	/// <summary>The table's resource name.</summary>
	public const string ResourceName = "BASES.DAT";

	/// <summary>The animated library, an ordinary DTS the engine can already build meshes from.</summary>
	public const string AnimatedLibraryName = "BASES_AN.DTS";

	/// <summary>The static library, read via <see cref="HercWorks.Core.Io.Transform.Dbsim.BasesDgsTransformer"/>.</summary>
	public const string StaticLibraryName = "BASES.DGS";

	private readonly BaseType[] _types;

	private BaseTypeTable(BaseType[] types) {
		_types = types;
	}

	/// <summary>How many base types the table declares.</summary>
	public int Count => _types.Length;

	/// <summary>A type by index, or null when a mission names one the table does not have.</summary>
	public BaseType? this[int typeIndex] =>
		typeIndex >= 0 && typeIndex < _types.Length ? _types[typeIndex] : null;

	public static BaseTypeTable Load(GameContent content) {
		byte[] bytes = content.ReadRequired(ResourceFolder, ResourceName);
		int offset = 0;

		short Next() {
			short value = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
			offset += 2;
			return value;
		}

		int count = Next();
		var types = new BaseType[count];

		for (int i = 0; i < count; i++) {
			Next();                          // +0x00 — unread here
			short shapeIndex = Next();       // +0x02 — index into the selected library
			Next();                          // +0x04
			short animated = Next();         // +0x06 — 0 selects the static library
			Next();                          // +0x08
			offset += 6;                     // +0x0a
			Next();                          // +0x10
			short nested = Next();           // +0x12 — count of the variable-length sub-record array
			offset += nested * 30;
			offset += 6;                     // +0x18
			Next();                          // +0x1e
			offset += 4;                     // +0x20
			offset += 2 * 7;                 // +0x24 .. +0x30
			short textureSelector = Next();  // +0x32

			types[i] = new BaseType(
				i,
				shapeIndex,
				animated == 0 ? BaseShapeSource.StaticLibrary : BaseShapeSource.AnimatedLibrary,
				textureSelector == 0 ? "BASETEX" : i == 0x34 ? "RAZORTEX" : "VEHTEX");
		}

		if (offset != bytes.Length) {
			throw new InvalidDataException(
				$"{ResourceFolder}\\{ResourceName}: walked {offset} of {bytes.Length} bytes across " +
				$"{count} records — the record shape does not match this file.");
		}

		return new BaseTypeTable(types);
	}
}
