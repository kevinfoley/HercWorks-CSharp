using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

/// <summary>
/// The effect light manager, <c>DAT_004a968c</c> — twenty slots an impact effect can claim a
/// dynamic light in, and the constants that decide how far one reaches.
///
/// <para>This is the simulation half only: it holds where the lights are and how bright, and the
/// renderer reads <see cref="Slots"/> to decide what each drawn object is lit by. The per-object
/// directional/point selection (<c>FUN_00407098</c>) belongs to the renderer, not here, because it
/// depends on the object being drawn rather than on the light.</para>
///
/// <para>The whole derivation — slot layout, the constants, the selection test and the shade terms —
/// is docs/formats/effect-lights.md.</para>
/// </summary>
public sealed class EffectLightField {
	/// <summary>
	/// Slots the manager has, the <c>Cpp_VectorNew</c> count at <c>mgr+0x6c</c>. A claim past the
	/// last one fails here; the original overruns instead, which is a retail bug and not reproduced.
	/// See KNOWN_ISSUES.md.
	/// </summary>
	public const int SlotCount = 20;

	/// <summary>
	/// <c>A</c>, the denominator offset of both falloffs — <c>mgr+0x10</c>.
	///
	/// <para><b>Zero, not ten.</b> <c>FUN_00406ee4</c> stores its argument shifted right by 5, and
	/// the call that survives into every frame is <c>FUN_004076e4</c>'s <c>(10, 2000)</c>, not the
	/// constructor's <c>(2000, 3000)</c>. <c>10 &gt;&gt; 5</c> is 0.</para>
	/// </summary>
	public const int FalloffOffset = 10 >> 5;

	/// <summary>
	/// <c>B</c>, the numerator of both falloffs — <c>mgr+0x14</c>, <c>2000 &gt;&gt; 5</c>. See
	/// <see cref="FalloffOffset"/> for why it is this pair of literals and not the constructor's.
	/// </summary>
	public const int FalloffRange = 2000 >> 5;

	/// <summary>
	/// The <c>0x20</c> both constants are multiplied back up by wherever they are used — the shift
	/// the setter applied, undone at the point of use rather than at the point of storage.
	/// </summary>
	public const int FalloffScale = 0x20;

	/// <summary>The largest intensity a slot carries, the ramp byte's own ceiling.</summary>
	public const int MaxIntensity = 255;

	private readonly EffectLight[] _slots = new EffectLight[SlotCount];

	/// <summary>
	/// Every slot, live and free alike — index is the handle <see cref="Claim"/> returns. Read
	/// <see cref="EffectLight.IsLive"/> before using one.
	/// </summary>
	public IReadOnlyList<EffectLight> Slots => _slots;

	/// <summary>
	/// <c>FUN_00406f38</c> — claims the first free slot for a light at <paramref name="position"/>
	/// with <paramref name="intensity"/>, returning its handle, or -1 when all twenty are busy.
	///
	/// <para>The original seeds the intensity to 255 and lets <c>FUN_00407048</c> overwrite it a
	/// call later; the two are folded together here because no caller can observe the gap.</para>
	/// </summary>
	public int Claim(Vec3i position, int intensity) {
		for (int i = 0; i < _slots.Length; i++) {
			if (_slots[i].IsLive) {
				continue;
			}

			_slots[i] = new EffectLight(position, ClampIntensity(intensity));
			return i;
		}

		return -1;
	}

	/// <summary>
	/// <c>FUN_00407048</c> — the intensity setter, which is also what recomputes the cull radius.
	/// A handle of -1 does nothing, so a caller that failed to claim needs no branch of its own.
	/// </summary>
	public void SetIntensity(int handle, int intensity) {
		if (handle < 0 || handle >= _slots.Length || !_slots[handle].IsLive) {
			return;
		}

		_slots[handle] = new EffectLight(_slots[handle].Position, ClampIntensity(intensity));
	}

	/// <summary><c>FUN_00406fbc</c> — frees a slot. A handle of -1 does nothing.</summary>
	public void Release(int handle) {
		if (handle >= 0 && handle < _slots.Length) {
			_slots[handle] = default;
		}
	}

	/// <summary>Frees every slot, for a mission teardown.</summary>
	public void Clear() => Array.Clear(_slots);

	private static int ClampIntensity(int intensity) => Math.Clamp(intensity, 0, MaxIntensity);
}

/// <summary>
/// One slot of <see cref="EffectLightField"/> — a dynamic light's position, brightness and the
/// range past which an object stops being lit by it.
/// </summary>
/// <param name="Position">Where it sits, in world units. Nothing moves an effect light.</param>
/// <param name="Intensity">
/// Brightness, 0-255 — <c>slot+0x1b</c>, the field every consumer reads. An
/// <see cref="ImpactEffect"/> drives it from its type row's per-frame ramp.
/// </param>
public readonly record struct EffectLight(Vec3i Position, int Intensity) {
	/// <summary>
	/// Whether the slot holds a light at all. An intensity of zero reads as free, which is also
	/// what the original's own consumers do — <c>FUN_00407098</c> skips a slot whose
	/// <c>+0x1b</c> is 0 before it looks at anything else, so a ramp entry of 0 puts the light out
	/// for that frame.
	/// </summary>
	public bool IsLive => Intensity > 0;

	/// <summary>
	/// <c>FUN_0040735c</c> — how far this light reaches, past which
	/// <c>FUN_00407098</c> detaches it from an object rather than lighting with it:
	/// <code>
	/// cullRadius = (intensity * B * 0x20) / 10 + A * 0x20
	/// </code>
	/// which with the live constants is <c>intensity * 198.4</c>, about 300 m at full brightness.
	/// </summary>
	public int CullRadius =>
		Intensity * EffectLightField.FalloffRange * EffectLightField.FalloffScale / 10
			+ EffectLightField.FalloffOffset * EffectLightField.FalloffScale;
}
