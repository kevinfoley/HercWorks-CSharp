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
/// One destructible part of a structure — the 30-byte sub-record a type's nested array holds, of
/// which three fields are read by the damage path (<c>Base_ApplyDamage</c>, <c>00404d70</c>) and the
/// rest are not touched by anything traced.
///
/// <para>Every structure has at least one of these, so "the building's hit points" is really "its
/// components' hit points": a structure falls when <i>all</i> of them are destroyed, which is what
/// <see cref="Herculan.Engine.Sim.BaseObject.DamageFraction"/> measures.</para>
/// </summary>
/// <param name="MaxDamage">
/// <c>+0</c> — how much damage this part absorbs before it is destroyed. Retail values are 1000 to
/// 30000, and a component sitting at exactly this figure is dead.
/// </param>
/// <param name="DestroyedSubShape">
/// <c>+2</c> — which of the shape's sub-parts stops being drawn once this component is destroyed,
/// or <c>-1</c> for a component with no separate geometry (every single-component type). Nothing
/// draws it yet; it is carried because it is the only field that ties a component to the model.
/// </param>
/// <param name="DestroyedEffect">
/// <c>+4</c> — which entry of DBSIM's small fixed effect table (<c>0049741c</c>) plays when this
/// component is destroyed, or <c>-1</c> for none. The effect itself is not ported.
/// </param>
public readonly record struct BaseComponentType(
	short MaxDamage, short DestroyedSubShape, short DestroyedEffect);

/// <summary>
/// One entry of the base-type table: everything the engine needs to draw a placed structure and to
/// shoot at it.
/// </summary>
/// <param name="Index">The type index a mission's block-9 record carries.</param>
/// <param name="ShapeIndex">Which shape inside <paramref name="Source"/>'s library this type is.</param>
/// <param name="Source">Which library that is.</param>
/// <param name="TextureBankName">
/// <c>BASETEX</c>, <c>VEHTEX</c> or <c>RAZORTEX</c> — the <c>.DBA</c> the type's polys sample from.
/// </param>
/// <param name="HulkTypeIndex">
/// <c>+0x04</c> — the wreck this type leaves behind, or <c>-1</c> for one that leaves none. The hit
/// test reads it too: a <i>destroyed</i> structure that has a hulk switches over to the coarse
/// volume test regardless of what it used while it was standing.
/// </param>
/// <param name="HitRadius">
/// <c>+0x2a</c> — the coarse collision radius, in world units, that the type's vtable <c>+0x5c</c>
/// hands out (<c>FUN_004035a4</c>). <b>This is the type's own stated figure, not a measurement of
/// the model</b>, which is what the engine substituted before; retail values run 1000 to 9600, and
/// four types state zero.
/// </param>
/// <param name="Invulnerable">
/// <c>+0x1e != 0</c> — the type takes no damage at all. Both damage entry points
/// (<c>Base_ApplyDamage</c> and the blast sweep <c>FUN_00404f20</c>) open by testing it and
/// returning. Three retail types set it (21, 22 and 23), which share a shape family and are the
/// tallest things in the table.
/// </param>
/// <param name="HasCollisionModel">
/// <c>+0x30 != 0</c> — whether this type's <c>dat\BASECOL.DAT</c> sphere model is installed. It is
/// the whole of the hit test's branch: a type with one is tested sphere by sphere and can name the
/// component struck, a type without falls back to the shape's own collision volume and can only ever
/// report component 0. 25 of the 65 retail types set it, and one type (3) carries a full
/// <c>BASECOL.DAT</c> model that the flag leaves switched off — the data is there and unused.
/// </param>
/// <param name="Components">
/// <c>+0x14</c> — the type's destructible parts, in the order the file states them, which is the
/// order both the health array and <c>BASECOL.DAT</c>'s component indices address them in.
/// </param>
public readonly record struct BaseType(
	int Index, int ShapeIndex, BaseShapeSource Source, string TextureBankName,
	short HulkTypeIndex, int HitRadius, bool Invulnerable, bool HasCollisionModel,
	BaseComponentType[] Components);

/// <summary>
/// <c>dat\BASES.DAT</c> — the game's table of structure types, the thing that turns a mission's
/// block-9 type number into a model, a texture bank and a set of destructible parts. 65 entries in
/// the retail file.
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
/// <para><b>The file's field order is the runtime record's field order.</b>
/// <c>Bases_LoadTypeTable</c> reads straight into a 60-byte struct offset by offset, and the only
/// place the two diverge is the nested component array, inline on disk and a pointer at
/// <c>+0x14</c> in memory. So every offset named in this file's doc comments is both.</para>
///
/// <para>The record shape is confirmed by construction: walking it consumes the retail file's 6,422
/// content bytes exactly, with nothing left over. It also lines up with the model libraries on the
/// other side — exactly eight types select the animated library, and <c>BASES_AN.DTS</c> holds
/// exactly eight roots, numbered 0-7 the way those eight types reference them.</para>
///
/// <para>Fields still unread are left as skips rather than guessed at: <c>+0x00</c>, <c>+0x08</c>,
/// <c>+0x0a</c> (6 bytes), <c>+0x10</c>, <c>+0x18</c> (6 bytes), <c>+0x20</c> (4 bytes),
/// <c>+0x24</c>-<c>+0x28</c>, <c>+0x2c</c> and <c>+0x2e</c>.</para>
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

	/// <summary>Bytes per entry of a type's nested component array.</summary>
	private const int ComponentRecordLength = 30;

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
			short hulkTypeIndex = Next();    // +0x04
			short animated = Next();         // +0x06 — 0 selects the static library
			Next();                          // +0x08
			offset += 6;                     // +0x0a
			Next();                          // +0x10
			short componentCount = Next();   // +0x12 — count of the nested component array

			var components = new BaseComponentType[componentCount < 0 ? 0 : componentCount];
			for (int c = 0; c < components.Length; c++) {
				var record = bytes.AsSpan(offset, ComponentRecordLength);
				components[c] = new BaseComponentType(
					BinaryPrimitives.ReadInt16LittleEndian(record),
					BinaryPrimitives.ReadInt16LittleEndian(record[2..]),
					BinaryPrimitives.ReadInt16LittleEndian(record[4..]));
				offset += ComponentRecordLength;
			}

			offset += 6;                     // +0x18
			short invulnerable = Next();     // +0x1e
			offset += 4;                     // +0x20
			Next();                          // +0x24
			Next();                          // +0x26
			Next();                          // +0x28
			short hitRadius = Next();        // +0x2a
			Next();                          // +0x2c
			Next();                          // +0x2e
			short collisionModel = Next();   // +0x30
			short textureSelector = Next();  // +0x32

			types[i] = new BaseType(
				i,
				shapeIndex,
				animated == 0 ? BaseShapeSource.StaticLibrary : BaseShapeSource.AnimatedLibrary,
				textureSelector == 0 ? "BASETEX" : i == 0x34 ? "RAZORTEX" : "VEHTEX",
				hulkTypeIndex,
				hitRadius,
				invulnerable != 0,
				collisionModel != 0,
				components);
		}

		if (offset != bytes.Length) {
			throw new InvalidDataException(
				$"{ResourceFolder}\\{ResourceName}: walked {offset} of {bytes.Length} bytes across " +
				$"{count} records — the record shape does not match this file.");
		}

		return new BaseTypeTable(types);
	}
}
