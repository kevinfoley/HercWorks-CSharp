using Herculan.Engine.Render;
using Silk.NET.OpenGL;

namespace Herculan.Engine.Gl;

/// <summary>
/// An RGBA8 texture on the GPU, uploaded from a CPU-side <see cref="TextureAtlas"/>.
///
/// <para>Sampling is <b>nearest-neighbour with no mipmaps</b>, which is a deliberate fidelity call
/// rather than a shortcut: the original is a 1996 software rasterizer that point-samples its
/// texels, so bilinear filtering would render something visibly softer than the game ever looked.
/// Per docs/engine/planning.md's "vanilla by default" principle, filtering and mipmapping belong in
/// the opt-in enhancement bucket alongside the other precision upgrades, not in the default path.
/// It also means the one-pixel gutter <see cref="TextureAtlas"/> leaves between frames is belt and
/// braces — nearest sampling inside an exact frame rect cannot reach a neighbour regardless.</para>
/// </summary>
public sealed class GpuTexture : IDisposable {
	private readonly GL _gl;

	public GpuTexture(GL gl, TextureAtlas atlas) : this(gl, atlas.Pixels, atlas.Width, atlas.Height) { }

	/// <summary>
	/// Uploads a plain RGBA8 image with no atlas packing — for a single-frame source like
	/// <see cref="Content.CockpitFrame"/>, where packing would be pure overhead (see
	/// <c>Content.CockpitArt</c>'s doc comment).
	/// </summary>
	public GpuTexture(GL gl, ReadOnlySpan<byte> rgbaPixels, int width, int height) {
		_gl = gl;
		Handle = _gl.GenTexture();

		_gl.BindTexture(TextureTarget.Texture2D, Handle);

		unsafe {
			fixed (byte* pixels = rgbaPixels) {
				_gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
					(uint)width, (uint)height, 0,
					PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
			}
		}

		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

		// Clamp rather than repeat: every UV this engine generates is inside a frame's own rect, so a
		// value outside 0..1 means something upstream is wrong, and clamping keeps that as a visible
		// smear at one frame's edge instead of tiling a neighbouring frame across the poly.
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
		_gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

		_gl.BindTexture(TextureTarget.Texture2D, 0);
	}

	/// <summary>The GL texture name, for binding via <see cref="ShaderProgram.SetSamplerTexture"/>.</summary>
	public uint Handle { get; }

	/// <summary>
	/// Replaces the whole image in place, keeping the same texture name and sampler state. For art
	/// the CPU side repaints — the cockpit's shield-meter rings, whose colours are per-frame state
	/// baked into the canopy bitmap (see <c>Content.CockpitArt.UpdateShieldRings</c>).
	/// </summary>
	public void Update(ReadOnlySpan<byte> rgbaPixels, int width, int height) {
		_gl.BindTexture(TextureTarget.Texture2D, Handle);

		unsafe {
			fixed (byte* pixels = rgbaPixels) {
				_gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)width, (uint)height,
					PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
			}
		}

		_gl.BindTexture(TextureTarget.Texture2D, 0);
	}

	public void Dispose() => _gl.DeleteTexture(Handle);
}
