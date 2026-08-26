using System.Numerics;
using HercWorks.Core.Data.File.Dat.Sim;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Io.Transform.Common;
using HercWorks.Core.Io.Transform.Dbsim;

namespace Herculan.Engine.Content;

/// <summary>
/// What a beam looks like: the two resources <c>FUN_0040b6e0</c> — the beam module's own init, which
/// the string <c>BEAM.CPP</c> at <c>00498781</c> names — loads once at startup.
///
/// <list type="bullet">
/// <item><c>dat\BEAM.DAT</c>, a count followed by that many six-byte records
/// (<see cref="BeamData"/>), read straight into the table at <c>DAT_004a9888</c> and indexed by the
/// firing <c>PROJ.DAT</c> record's <b>subtype id</b> — not by weapon id.</item>
/// <item><c>dba\BEAMTEX.DBA</c>, whose frames become the descriptor table at <c>DAT_004a988c</c>
/// (<c>FUN_00469f38</c>, twenty bytes each: a UV rect and a texture handle) that the draw indexes
/// with the record's third field.</item>
/// </list>
///
/// <para><b>Retail ships one frame and every record points at it.</b> <c>BEAMTEX.DBA</c> holds a
/// single 128x25 bitmap whose every row is one constant palette index — 11 at both edges, then the
/// ramp 84..95 in to the middle and back out — so the frame is a pure cross-section profile with
/// nothing varying along the beam's length. In a <c>WORLD&lt;n&gt;.DPL</c> that ramp is the fire
/// ramp, dark orange (184, 92, 20) climbing to near-white (252, 248, 228). That is why this class
/// keeps a profile of <see cref="ProfileTexels"/> RGBA samples rather than a 2D image.</para>
///
/// <para><b>Only the width differs per weapon.</b> The record's first field is the beam's half-width
/// in world units — <c>FUN_0040bc14</c> feeds it to the perspective divide at <c>0048c4c0</c> and
/// floors the result at two pixels. Retail widths run 20 (LAS100) to 120 (BPBW).</para>
///
/// <para><b>The record's colour index is not used by a straight beam</b>, which is worth stating
/// plainly because it looks as though it must be. The draw does publish it to the graphics context's
/// <c>+0x22c</c> colour pair as <c>{0, index}</c>, but the poly it then submits goes through
/// <c>FUN_00468310</c> with mode <c>0</c>, and mode 0's span routine (<c>FUN_0046ab10</c>) is a plain
/// affine texture copy: it fetches <c>atlasPage[v][u]</c> and stores that byte to the framebuffer
/// with no shade level, no colour lookup and no read of the context pair at all. The shade level the
/// context pair would feed is mode <b>1</b>'s (<c>FUN_0046ac48</c>), which nothing here selects. So
/// every retail beam draws the same orange-to-white ribbon and is told apart only by how wide it is.
/// The indices are still parsed and exposed on <see cref="BeamData.Entry.ColorId"/> because they are
/// what the file holds — PBW/BPBW 10, PBW2 1, ELF 104, ELF2 99, the LAS family 88 — and because the
/// undecoded ELF/ELF2 branch does extra rasterizer setup that may well consume them.</para>
///
/// <para><b>There is no alpha.</b> <c>Bullet_FireBurst</c>'s draw passes <c>FUN_00468310</c>'s last
/// parameter as 0, which selects the span routine's opaque half; the non-zero form is a colour-key
/// skip of palette index 0, not blending, and the profile contains no index 0 anyway. A beam is an
/// opaque ribbon over whatever it crosses.</para>
/// </summary>
public sealed class BeamAppearance {
	/// <summary>The <c>dat</c> resource <c>FUN_0040b6e0</c> opens by the literal name <c>beam</c>.</summary>
	public const string TableResource = "BEAM.DAT";

	/// <summary>The <c>dba</c> resource it loads next, by the literal name <c>beamtex</c>.</summary>
	public const string TextureResource = "BEAMTEX.DBA";

	private readonly BeamData _table;
	private readonly byte[][] _profiles;

	private BeamAppearance(BeamData table, byte[][] profiles, int profileTexels) {
		_table = table;
		_profiles = profiles;
		ProfileTexels = profileTexels;
	}

	/// <summary>How many <c>BEAM.DAT</c> records were read.</summary>
	public int Count => _table.Data?.Length ?? 0;

	/// <summary>How many samples wide <see cref="Profile"/> is — the source frame's row count.</summary>
	public int ProfileTexels { get; }

	/// <summary>
	/// Loads both resources. Returns null when either is missing or unreadable, in which case beams
	/// simply are not drawn — nothing else in the simulation depends on this.
	/// </summary>
	/// <param name="content">Mounted archives.</param>
	/// <param name="paletteName">
	/// The theater's palette, <c>dpl\WORLD&lt;n&gt;.DPL</c> — the live display palette, and the one
	/// both the profile's indices and the records' colour indices are in. The cockpit scheme
	/// <see cref="CockpitPalette"/> installs over it touches slots 42-65 only, and no beam colour
	/// lands in that window, so reading the theater palette directly is the same answer.
	/// </param>
	public static BeamAppearance? Load(GameContent content, string? paletteName) {
		byte[]? tableBytes = content.Read("dat", TableResource);
		if (tableBytes == null
			|| new BeamDatFileTransformer().BytesToObject(tableBytes) is not BeamData table
			|| table.Data is not { Length: > 0 }) {
			return null;
		}

		byte[]? textureBytes = content.Read("dba", TextureResource);
		if (textureBytes == null
			|| new DynamixBitmapArrayTransformer().BytesToObject(textureBytes) is not DynamixBitmapArray bank
			|| bank.Images is not { Length: > 0 } frames) {
			return null;
		}

		DynamixPalette? palette = null;
		if (!string.IsNullOrWhiteSpace(paletteName)
			&& content.Read("dpl", paletteName + ".DPL") is { } paletteBytes) {
			palette = new DynamixPaletteTransformer().BytesToObject(paletteBytes) as DynamixPalette;
		}

		var profiles = new byte[frames.Length][];
		int texels = 0;
		for (int i = 0; i < frames.Length; i++) {
			profiles[i] = BuildProfile(frames[i], palette);
			texels = Math.Max(texels, profiles[i].Length / 4);
		}

		return texels == 0 ? null : new BeamAppearance(table, profiles, texels);
	}

	/// <summary>
	/// The record for <paramref name="missileId"/>, or null when the id is outside the table — which
	/// no retail beam is, but a hand-edited <c>PROJ.DAT</c> could be.
	/// </summary>
	public BeamData.Entry? Record(int missileId) =>
		_table.Data is { } data && missileId >= 0 && missileId < data.Length ? data[missileId] : null;

	/// <summary>
	/// Half the beam's width, in world units — the record's first field, which the original uses as
	/// the perpendicular offset applied to both sides of the centre line. Zero when the id is unknown.
	/// </summary>
	public int HalfWidth(int missileId) => Record(missileId)?.Width ?? 0;

	/// <summary>
	/// The cross-section for <paramref name="missileId"/> as RGBA texels, one per source row, running
	/// from one edge of the beam to the other. Falls back to frame 0 when the record names a frame the
	/// bank does not have.
	/// </summary>
	public ReadOnlySpan<byte> Profile(int missileId) {
		int frame = Record(missileId)?.DBAFrameNum ?? 0;
		if (frame < 0 || frame >= _profiles.Length) {
			frame = 0;
		}

		return _profiles[frame];
	}

	/// <summary>
	/// One row of the frame, expanded through the palette. Every retail row is a single repeated
	/// index, so the first column is the whole row; the general case takes it anyway rather than
	/// assuming a constant row.
	/// </summary>
	private static byte[] BuildProfile(DynamixBitmap frame, DynamixPalette? palette) {
		byte[] indices = frame.ImageData ?? Array.Empty<byte>();
		int rows = frame.Rows;
		int cols = frame.Cols;
		if (rows <= 0 || cols <= 0 || indices.Length < rows * cols) {
			return Array.Empty<byte>();
		}

		var texels = new byte[rows * 4];
		for (int row = 0; row < rows; row++) {
			var color = Lookup(palette, indices[row * cols]);
			texels[row * 4] = (byte)(color.X * 255f);
			texels[row * 4 + 1] = (byte)(color.Y * 255f);
			texels[row * 4 + 2] = (byte)(color.Z * 255f);

			// Opaque, because the span routine that draws a beam stores every texel it fetches. The
			// edges are a hard band of dark orange against the sky in the original too.
			texels[row * 4 + 3] = 255;
		}

		return texels;
	}

	private static Vector3 Lookup(DynamixPalette? palette, int index) {
		if (palette == null || !palette.Colors.TryGetValue(index, out var entry)) {
			return new Vector3(index / 255f);
		}

		var color = entry.GetColor();
		return new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
	}

}
