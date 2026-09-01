using System.Numerics;
using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dbsim;
using HercWorks.Core.Data.File.Dgs;
using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Core.Io.Transform.Dbsim;
using Herculan.Engine.Content;
using Herculan.Engine.Gl;
using Herculan.Engine.Render;
using Herculan.Engine.Sim;
using Herculan.Engine.Sim.Anim;
using Herculan.Engine.World;

namespace Herculan.Engine.Scene;

/// <summary>
/// One drawable model, built once and shared by every object of its type. A mission that fields
/// three ACHILLES and five of one structure holds two entries here, not eight.
/// </summary>
/// <param name="Key">Stable identity, e.g. <c>dts\ACHILLES.DTS#0</c>.</param>
/// <param name="Mesh">Triangles then outline edges in model space, ready to upload — see
/// <see cref="MeshBuild"/>.</param>
/// <param name="TriangleVertexCount">Where <paramref name="Mesh"/>'s outline range starts.</param>
/// <param name="Atlas">
/// The model's packed texture bank, or null when no bank could be resolved — in which case the
/// mesh's UVs mean nothing and it must be drawn untextured.
/// </param>
/// <param name="RadiusWorldUnits">Coarse collision radius derived from the model's own bounds.</param>
/// <param name="HeightWorldUnits">Height of the model's bounding box, in world units.</param>
/// <param name="Segments">
/// The same geometry split by the node that places each part, for an object whose shape animates —
/// see <see cref="DtsMeshBuilder.BuildSegments"/>. A caller draws either <paramref name="Mesh"/> or
/// these, never both: they are the same triangles twice.
/// </param>
/// <param name="Sprites">
/// The shape's billboards, a frame at a time — see <see cref="DtsSpriteBuilder.Build"/>. Empty for
/// ordinary geometry; for a shape that is <i>only</i> billboards (every EMP round, every impact
/// effect) this is the whole model and <paramref name="Mesh"/> is empty instead. A caller draws both
/// where both exist: they are different parts of one shape, not two versions of it.
/// </param>
public sealed record SceneModel(
	string Key, MeshVertex[] Mesh, int TriangleVertexCount, TextureAtlas? Atlas,
	int RadiusWorldUnits, int HeightWorldUnits, MeshSegment[] Segments, SpriteQuad[][] Sprites);

/// <summary>
/// Loads and caches the models a mission needs, keyed so identical unit types share one mesh and one
/// atlas.
///
/// <para>Three resolution paths, one per roster, each following the original's own selection rule:</para>
/// <list type="bullet">
/// <item><b>Mechs</b> — <c>dts\&lt;name&gt;.DTS</c> root 0, textured by the bank
/// <c>HercSimDat.ModelSkinId</c> selects (see docs/formats/dts-texture-binding.md).</item>
/// <item><b>Flyers</b> — <c>dts\&lt;name&gt;.DTS</c> root 0, untextured: which bank DBSIM binds for a
/// flyer has not been traced, and drawing them flat-shaded is honest about that where picking
/// <c>VEHICLES.DBA</c> because the name looks right would not be.</item>
/// <item><b>Structures</b> — either a root of <c>dts\BASES_AN.DTS</c> (the 8 animated types) or a
/// record of <c>dgs\BASES.DGS</c> (the other 57, static types), textured by the bank
/// <c>dat\BASES.DAT</c> names — see <see cref="BasesDgsTransformer"/> for how the latter resolves
/// to the same drawable shape as the former.</item>
/// </list>
///
/// <para>Every path is best-effort: a missing file or an unresolvable bank yields null or an
/// untextured model rather than throwing, because one unknown unit type is not a reason to refuse to
/// show a mission.</para>
/// </summary>
public sealed class SceneModelLibrary {
	private readonly GameContent _content;
	private readonly DynamixPalette? _palette;
	private readonly SurfaceShading? _shading;

	private readonly Dictionary<string, SceneModel?> _models = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, TextureAtlas?> _atlases = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, DynamixThreeSpaceModel?> _files = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, BaseShapeLibrary?> _shapeLibraries = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<int, ShapeVolume?> _volumes = new();
	private readonly Dictionary<string, HercSimDat?> _mechData = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, FlyerSimData?> _flyerData = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, ShapeAnimation?> _animations = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, GunLayout?> _hardpoints = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, ColliderNode[]> _collision = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, HercSimDamage?> _damageData = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The folder hardpoint lists live in — <c>ResourcePath_BuildFolderName(name, "gl")</c> at the
	/// head of the mech-type loader (<c>FUN_00420298</c>).
	/// </summary>
	private const string GunLayoutFolder = "gl";

	/// <summary>
	/// <paramref name="theater"/> supplies the palette every bank in this mission decodes against —
	/// the WORLD&lt;n&gt; palettes are per-theater, loaded once by <c>maybe_World_LoadTheater</c> and
	/// active for everything the theater draws, so a mech's colours depend on where it is standing.
	/// </summary>
	public SceneModelLibrary(GameContent content, TheaterDescriptor theater) {
		_content = content;

		byte[]? paletteBytes = content.Read("dpl", theater.PaletteName + ".DPL");
		_palette = paletteBytes != null
			? new DynamixPaletteTransformer().Parse(paletteBytes) as DynamixPalette
			: null;

		// The theater ships its colour ramp beside its palette, under the same base name, and a flat
		// solid face is nothing without it — see DtsMeshBuilder's ResolveSolidColors.
		_shading = ShadeRamp.Load(content, theater.PaletteName) is { } ramp
			? new SurfaceShading(ramp, _palette)
			: null;

		// Every lit flat surface — which is nearly every surface of a HERC or a building — takes its
		// colour from the palette's shade-ramp table, so without one they all come out
		// DtsMeshBuilder.FallbackColor grey. Every retail theater palette carries the table, so this
		// means the palette failed to load or is not one of the game's.
		if (_shading is not { HasShadeRamps: true }) {
			Console.Error.WriteLine(
				$"WARNING: theater palette {theater.PaletteName}.DPL has no shade-ramp table; " +
				"lit flat surfaces will draw untextured and unlit.");
		}
	}

	/// <summary>Every model built so far, in first-requested order.</summary>
	public IEnumerable<SceneModel> Models => _models.Values.Where(m => m != null)!;

	/// <summary>
	/// The theater's ramp and palette, or null when the ramp did not load. Exposed because the same
	/// pair that colours a flat solid face also carries the zone's fog colour — see
	/// <see cref="Atmosphere"/>.
	/// </summary>
	public SurfaceShading? Shading => _shading;

	/// <summary>The mech type's stats, or null when the install has no <c>dat\&lt;name&gt;.DAT</c>.</summary>
	public HercSimDat? MechData(string mechName) {
		if (_mechData.TryGetValue(mechName, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read("dat", mechName + ".DAT");
		var data = bytes != null
			? new HercSimDataTransformer().Parse(bytes) as HercSimDat
			: null;

		_mechData[mechName] = data;
		return data;
	}

	/// <summary>
	/// The mech type's hardpoint list, <c>gl\&lt;NAME&gt;.GL</c> — where its weapons sit, which
	/// cockpit row each owns and which slot of a fit each draws from. Null when the install has none,
	/// in which case the machine is fitted with nothing at all: the fit is addressed through this
	/// list, so without it there is nothing to address.
	/// </summary>
	public GunLayout? MechHardpoints(string mechName) {
		if (_hardpoints.TryGetValue(mechName, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read(GunLayoutFolder, mechName + ".GL");
		var data = bytes != null
			? new GunLayoutTransformer().Parse(bytes) as GunLayout
			: null;

		_hardpoints[mechName] = data;
		return data;
	}

	/// <summary>
	/// A mech or flyer type's hit-sphere model, <c>col\&lt;NAME&gt;.COL</c> — empty when the install
	/// ships none, which on retail data is true of <c>HOVTANK</c> and <c>DROPSHIP</c> and of nothing
	/// else. Shared per type: the model is read in the type's own space and posed per object.
	/// </summary>
	public ColliderNode[] Collision(string typeName) {
		if (_collision.TryGetValue(typeName, out var cached)) {
			return cached;
		}

		var model = CollisionModelReader.Load(_content, typeName);
		_collision[typeName] = model;
		return model;
	}

	/// <summary>
	/// A mech or flyer type's component health record, <c>dmg\&lt;NAME&gt;.DMG</c>. Shared per type
	/// because it is the <i>maxima</i>, not the damage: the damage is per object, in
	/// <see cref="ComponentDamage"/>.
	/// </summary>
	public HercSimDamage? DamageData(string typeName) {
		if (_damageData.TryGetValue(typeName, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read(DamageFolder, typeName + ".DMG");
		var data = bytes != null
			? new HercDamageFileTransformer().Parse(bytes) as HercSimDamage
			: null;

		_damageData[typeName] = data;
		return data;
	}

	/// <summary>
	/// The folder component health records live in — <c>ResourcePath_BuildFolderName(name, "dmg")</c>
	/// in both the mech constructor and the flyer type loader.
	/// </summary>
	private const string DamageFolder = "dmg";

	/// <summary>The flyer type's stats, or null — only <c>SKIMMER</c> ships one.</summary>
	public FlyerSimData? FlyerData(string flyerName) {
		if (_flyerData.TryGetValue(flyerName, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read("dat", flyerName + ".DAT");
		var data = bytes != null
			? new FlyerSimDataTransformer().Parse(bytes) as FlyerSimData
			: null;

		_flyerData[flyerName] = data;
		return data;
	}

	/// <summary>
	/// A mech type's animation data, or null when its <c>.DTS</c> is missing or carries no
	/// <c>ANAnimList</c>. Shared per type, as the model is: an animation thread holds only a cursor
	/// into it, so several machines of one type play the same data independently.
	/// </summary>
	public ShapeAnimation? MechAnimation(string mechName) {
		if (_animations.TryGetValue(mechName, out var cached)) {
			return cached;
		}

		var animation = ShapeAnimation.FromModel(LoadDts(mechName + ".DTS"));
		_animations[mechName] = animation;
		return animation;
	}

	/// <summary>The model for a mech type, or null when its <c>.DTS</c> is missing or empty.</summary>
	public SceneModel? Mech(string mechName) {
		string? bankName = MechData(mechName) is { } data
			? HercSimDat.TextureGroupDbaBaseName(data.ModelSkinId)
			: null;

		return Build(mechName + ".DTS", 0, bankName, segmented: true);
	}

	/// <summary>The model for a flyer type, or null when the install has no <c>.DTS</c> for it.</summary>
	public SceneModel? Flyer(string flyerName) => Build(flyerName + ".DTS", 0, bankName: null);

	/// <summary>
	/// The shape a travelling shot is drawn as — a root of <c>dts\BULLETS.DTS</c>, textured from
	/// <c>dba\BULLETS.DBA</c>. Both come from the bullet module's own init (<c>FUN_0040ade0</c>),
	/// which loads the shape file into <c>DAT_004a9784</c> and binds that one bank to every shape in
	/// it; <paramref name="modelId"/> is the <c>BULLETS.DAT</c> record's first field, which
	/// <c>Bullet_Construct</c> uses as the index into that array.
	///
	/// <para>Retail ships nine roots and the twelve records between them name all nine, but two of
	/// them — roots 2 and 3, which are the <b>three EMP cannons' rounds</b> — are not geometry at
	/// all: they are a <c>TSCellAnimPart</c> of five <c>TSBitmapPart</c>s, a flipbook of billboard
	/// sprites out of the same <c>.DBA</c>. <see cref="DtsMeshBuilder"/> builds triangles and those
	/// roots have none, so their mesh is empty and their <see cref="SceneModel.Sprites"/> is
	/// everything — see <see cref="DtsSpriteBuilder"/>. The plasma round (root 8) is a two-frame cell
	/// animation over real geometry and draws its first frame; every autocannon round is plain static
	/// geometry.</para>
	///
	/// <para>The bank is decoded with index 0 transparent, which only the sprite roots read: the
	/// other seven are <c>TSSolidPoly</c> geometry coloured through the theater ramp and never sample
	/// it at all.</para>
	/// </summary>
	public SceneModel? Bullet(int modelId) =>
		Build(BulletLibraryName, modelId, BulletBankName, transparentBank: true);

	/// <summary>The shape file <c>FUN_0040ade0</c> opens, by the literal name <c>bullets</c>.</summary>
	public const string BulletLibraryName = "BULLETS.DTS";

	/// <summary>And the bank it binds to every shape in it, opened by the same literal.</summary>
	public const string BulletBankName = "BULLETS";

	/// <summary>
	/// The shapes a launcher's round is drawn as — a root of <c>dts\ROCKETS.DTS</c>, one entry per
	/// cell of its flipbook, and <b>untextured</b>. <c>Rocket_LoadTypeTable_Unguided</c>
	/// (<c>0040a818</c>) loads the table and the shape file and stops: unlike the bullet module's init
	/// it opens no <c>.DBA</c> and binds no bank to any of the shapes it just read, and there is no
	/// <c>ROCKETS.DBA</c> in the install to open. So a rocket is <c>TSSolidPoly</c> geometry coloured
	/// through the theater's own ramp, the same way a flyer with no bank is.
	///
	/// <para><b>Why a list.</b> A rocket's <see cref="TSCellAnimPart"/>s hold <i>geometry</i>, not
	/// billboards — the flipbook is the exhaust flame, two alternate cones of flat polys at the tail
	/// in the palette's red/orange/white range (indices 109, 94/93, 87/88, 86), sitting beside the
	/// static grey body (index 200). Both retail roots carry one sequence of two cells, and both
	/// cell-anim parts in each read sequence 0 — the same sequence every <c>ROCKETS.DAT</c> record
	/// names. So the record's frame interval really does drive them, and the shape has to be built
	/// once per cell for the flame to move. See <see cref="DtsMeshBuilder.CellFrameCount"/>.</para>
	///
	/// <para><paramref name="modelId"/> is the <c>ROCKETS.DAT</c> record's first field. Retail ships
	/// two roots and the five records name both: root 0 for the four ordinary missiles, root 1 for
	/// <c>BMSL</c>.</para>
	/// </summary>
	/// <returns>The cells in order, or empty when the shape file or the index is missing.</returns>
	public IReadOnlyList<SceneModel> Rocket(int modelId) {
		if (Root(RocketLibraryName, modelId) is not { } root) {
			return Array.Empty<SceneModel>();
		}

		var cells = new List<SceneModel>();
		for (int cell = 0; cell < DtsMeshBuilder.CellFrameCount(root); cell++) {
			if (Build(RocketLibraryName, modelId, bankName: null, cellFrame: cell) is { } model) {
				cells.Add(model);
			}
		}

		return cells;
	}

	/// <summary>The shape file <c>Rocket_LoadTypeTable_Unguided</c> opens, by the literal name <c>rockets</c>.</summary>
	public const string RocketLibraryName = "ROCKETS.DTS";

	/// <summary>
	/// The model for a structure type. <see cref="BaseShapeSource.AnimatedLibrary"/> types are a
	/// root of <c>dts\BASES_AN.DTS</c>; <see cref="BaseShapeSource.StaticLibrary"/> types are a
	/// record of <c>dgs\BASES.DGS</c> — see <see cref="BasesDgsTransformer"/> for how that record's
	/// embedded DTS subtree resolves to the same <see cref="TSObject"/> shape either path builds
	/// from. Null only when the install is missing the relevant file or the index is out of range.
	/// </summary>
	public SceneModel? Base(BaseType type) =>
		type.Source == BaseShapeSource.AnimatedLibrary
			? Build(BaseTypeTable.AnimatedLibraryName, type.ShapeIndex, type.TextureBankName,
				transparentBank: true)
			: BuildFromShapeLibrary(BaseTypeTable.StaticLibraryName, type.ShapeIndex, type.TextureBankName,
				transparentBank: true);

	/// <summary>
	/// The hit geometry a structure type's shape carries, as distinct from the geometry it is drawn
	/// from: the coarse collision volume in its <c>.DGS</c> record and that record's own stated
	/// bounding radius. Both are null/zero for an <see cref="BaseShapeSource.AnimatedLibrary"/> type,
	/// whose shape is an ordinary DTS and has neither — see <see cref="Sim.BaseObject"/> for why
	/// that costs nothing on retail data.
	/// </summary>
	public (int BoundingRadius, ShapeVolume? Volume) BaseShapeCollision(BaseType type) {
		if (type.Source == BaseShapeSource.AnimatedLibrary) {
			return (0, null);
		}

		if (LoadShapeLibrary(BaseTypeTable.StaticLibraryName)?.Shapes is not { Length: > 0 } shapes
				|| type.ShapeIndex < 0 || type.ShapeIndex >= shapes.Length) {
			return (0, null);
		}

		var shape = shapes[type.ShapeIndex];
		if (!_volumes.TryGetValue(type.ShapeIndex, out var volume)) {
			volume = shape.Collision.IsSolid ? new ShapeVolume(shape.Collision) : null;
			_volumes[type.ShapeIndex] = volume;
		}

		return (shape.BoundingRadius, volume);
	}

	/// <summary>
	/// One root of <c>dts\EXPLOS.DTS</c> — an impact effect's shape, a flipbook of billboards out of
	/// whichever <c>dba\EXPLO&lt;n&gt;.DBA</c> its <c>EXPLOS.DAT</c> row names. Both the shape file
	/// and the fifteen banks come from the effect subsystem's own init (<c>FUN_00407b54</c>), which
	/// writes each bank straight into its shape's bound-bank pointer; this is that binding, expressed
	/// as which atlas the model carries.
	/// </summary>
	public SceneModel? Explosion(int shapeIndex, int textureBankIndex) =>
		Build(ExplosionCatalog.ShapeLibraryName, shapeIndex,
			ExplosionCatalog.TextureBankPrefix + textureBankIndex, transparentBank: true);

	private SceneModel? Build(string dtsName, int rootIndex, string? bankName,
			bool segmented = false, bool transparentBank = false, int cellFrame = 0) {
		string key = cellFrame == 0 ? $"dts\\{dtsName}#{rootIndex}" : $"dts\\{dtsName}#{rootIndex}@{cellFrame}";
		if (_models.TryGetValue(key, out var cached)) {
			return cached;
		}

		var model = BuildFromRoot(key, Root(dtsName, rootIndex), bankName, segmented, transparentBank, cellFrame);
		_models[key] = model;
		return model;
	}

	/// <summary>One root of a shape file, or null when the file or the index is missing.</summary>
	private TSObject? Root(string dtsName, int rootIndex) =>
		LoadDts(dtsName)?.Meshes is { Count: > 0 } roots && rootIndex >= 0 && rootIndex < roots.Count
			? roots[rootIndex]
			: null;

	private SceneModel? BuildFromShapeLibrary(string libraryName, int shapeIndex, string? bankName,
			bool transparentBank = false) {
		string key = $"dgs\\{libraryName}#{shapeIndex}";
		if (_models.TryGetValue(key, out var cached)) {
			return cached;
		}

		TSObject? root = null;
		if (LoadShapeLibrary(libraryName)?.Shapes is { Length: > 0 } shapes
				&& shapeIndex >= 0 && shapeIndex < shapes.Length) {
			root = shapes[shapeIndex].Geometry;
		}

		var model = BuildFromRoot(key, root, bankName, transparentBank: transparentBank);
		_models[key] = model;
		return model;
	}

	private SceneModel? BuildFromRoot(string key, TSObject? root, string? bankName,
			bool segmented = false, bool transparentBank = false, int cellFrame = 0) {
		if (root == null) {
			return null;
		}

		var atlas = bankName != null ? LoadAtlas(bankName, transparentBank) : null;
		var build = DtsMeshBuilder.BuildRoot(root, atlas, _shading, cellFrame);
		var (min, max) = DtsMeshBuilder.Bounds(build.Vertices);

		Vector3 extent = max - min;
		float radiusInRenderUnits = MathF.Max(extent.X, extent.Z) * 0.5f;

		// Bounds and radius both come off the flat mesh whichever way the model ends up being drawn:
		// they describe the machine at rest, and a walk cycle should not change how wide it is for
		// collision purposes. Segments cost a second pass over the shape, so only the rosters that
		// animate ask for them.
		return new SceneModel(key, build.Vertices, build.TriangleVertexCount, atlas,
			(int)(radiusInRenderUnits * WorldScale.WorldUnitsPerMeter),
			(int)(extent.Y * WorldScale.WorldUnitsPerMeter),
			segmented ? DtsMeshBuilder.BuildSegments(root, atlas, _shading) : Array.Empty<MeshSegment>(),
			DtsSpriteBuilder.Build(root));
	}

	/// <summary>
	/// A mech file's roots are LOD variants of the same machine, so root 0 is taken as the primary;
	/// a library file's roots (<c>BASES_AN.DTS</c>) are unrelated objects and the caller picks. The
	/// files carry no flag distinguishing the two cases — that knowledge lives here, as it does in
	/// the original.
	/// </summary>
	private DynamixThreeSpaceModel? LoadDts(string dtsName) {
		if (_files.TryGetValue(dtsName, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read("dts", dtsName);
		DynamixThreeSpaceModel? model = null;

		if (bytes != null) {
			model = new DTSModelTransformer().Parse(bytes) as DynamixThreeSpaceModel;
		}

		_files[dtsName] = model;
		return model;
	}

	/// <summary>
	/// A shape library's records are unrelated structures, one shape each (see
	/// <see cref="BasesDgsTransformer"/>) — the caller picks by index, same as
	/// <c>BASES_AN.DTS</c>'s roots.
	/// </summary>
	private BaseShapeLibrary? LoadShapeLibrary(string libraryName) {
		if (_shapeLibraries.TryGetValue(libraryName, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read("dgs", libraryName);
		var library = bytes != null
			? new BasesDgsTransformer().Parse(bytes) as BaseShapeLibrary
			: null;

		_shapeLibraries[libraryName] = library;
		return library;
	}

	/// <summary>
	/// <paramref name="transparentIndex0"/> decodes palette index 0 to alpha 0 rather than to an
	/// opaque colour. An explosion frame is a round puff on a field of index 0 and the original's
	/// blit skips that index rather than writing it, so every sprite bank asks for it.
	///
	/// <para><b>CORRECTS</b> the claim that the frames a mesh samples have no transparent index at
	/// all. The <b>structure</b> banks are cutouts too: <c>BASETEX</c> frames 11, 36, 38, 39, 52, 53,
	/// 60, 61, 63, 64 and 65 are 20-73% index 0 each, and they are the lattice girders on a
	/// structure's support towers — drawn opaque they come out as black panels where the original
	/// shows sky through the frame. The original's own switch is per frame rather than per bank
	/// (<c>TSTexture4Poly_Render</c> passes a flag from the runtime frame descriptor's <c>+0x12</c>
	/// down to <c>Raster_DrawPolygon</c>, which selects the span routine's transparent half), and
	/// where that flag is authored has not been traced — but a frame with no index 0 in it draws
	/// identically either way, so decoding the whole bank transparent reproduces the original on this
	/// data.</para>
	///
	/// <para>It is still not done for <i>every</i> mesh bank: the mech skins carry a handful of stray
	/// index-0 texels each (9 of 44376 in <c>LIGHT</c>, 7 of 68464 in <c>MEDIUM</c>) that are plainly
	/// paint rather than cutouts, and punching single-pixel holes in a HERC to generalise a rule this
	/// session did not fully trace would be a worse trade than leaving them opaque.</para>
	/// </summary>
	private TextureAtlas? LoadAtlas(string bankName, bool transparentIndex0 = false) {
		// The two decodings of one bank are different images, so they cache apart. No retail bank is
		// asked for both ways; the key keeps that from being an assumption.
		string key = transparentIndex0 ? bankName + "#alpha" : bankName;
		if (_atlases.TryGetValue(key, out var cached)) {
			return cached;
		}

		byte[]? bytes = _content.Read("dba", bankName + ".DBA");
		TextureAtlas? atlas = null;

		if (bytes != null
			&& new DynamixBitmapArrayTransformer().Parse(bytes) is DynamixBitmapArray bank) {
			atlas = TextureAtlas.Build(bank, _palette, transparentIndex0);
		}

		_atlases[key] = atlas;
		return atlas;
	}
}
