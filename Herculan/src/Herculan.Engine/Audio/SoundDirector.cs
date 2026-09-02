using Herculan.Engine.Numerics;

namespace Herculan.Engine.Audio;

/// <summary>
/// DBSIM's own <c>Sound_*</c> layer: the rules that sit between a catalog id and the mixer —
/// variation rolls, the category split, the throttle, the distance cutoff and the stereo pan.
/// See docs/formats/audio.md for where each of them comes from.
///
/// <para><b>One voice per catalog id.</b> The original allocates exactly one <c>SFX</c> voice per
/// row of <c>SOUNDS.STR</c> and keeps it for the mission, so playing a sound that is already
/// sounding restarts it rather than layering a second copy. That is not an implementation detail to
/// improve on — it is why the throttle and variation attributes exist, and why two machines firing
/// the same weapon in the same tick produce one report rather than two.</para>
/// </summary>
public sealed class SoundDirector : ISoundSink, IDisposable {
	/// <summary>The pan value that is dead centre — HMI SOS's own, and the voice default.</summary>
	public const int PanCentre = 0x8000;

	/// <summary>A pitch of 1.0, as the 16.16 ratio the original stores.</summary>
	public const int PitchOne = 0x10000;

	private readonly SoundBank _bank;
	private readonly IAudioBackend _backend;
	private readonly SimRandom _random;
	private readonly int[] _voices;
	private readonly int[] _repeatsLeft;
	private bool _disposed;

	/// <summary>
	/// Wires a bank to a backend and creates the per-id voices.
	/// </summary>
	/// <param name="bank">The catalog and its samples.</param>
	/// <param name="backend">Where sound goes. Pass a <see cref="NullAudioBackend"/> to run silent.</param>
	/// <param name="random">
	/// The generator the variation roll draws on. The original uses the simulation's single global
	/// state block for this, the same one weapon scatter rolls against, so pass
	/// <see cref="Sim.SimWorld.Random"/> rather than a private generator when there is a world.
	/// </param>
	public SoundDirector(SoundBank bank, IAudioBackend backend, SimRandom? random = null) {
		_bank = bank;
		_backend = backend;
		_random = random ?? new SimRandom(0);

		int count = bank.Catalog.Count;
		_voices = new int[count];
		_repeatsLeft = new int[count];

		for (int id = 0; id < count; id++) {
			_voices[id] = bank.Sample(id) is { } sample ? backend.CreateVoice(sample) : -1;

			if (_voices[id] < 0) {
				continue;
			}

			// Attribute byte 0 is a repeat count, so only 0 — play forever — is the backend's own
			// looping flag. A finite count is re-triggered by Update.
			var entry = bank.Catalog.Entries[id];
			backend.SetLooping(_voices[id], entry.LoopCount == 0);
			backend.SetGain(_voices[id], 0f);
			backend.SetPan(_voices[id], 0f);
			backend.SetPitch(_voices[id], 1f);
		}
	}

	/// <summary>The catalog behind this director.</summary>
	public SoundCatalog Catalog => _bank.Catalog;

	/// <summary>Whether a device actually opened.</summary>
	public bool IsAvailable => _backend.IsAvailable;

	/// <summary>
	/// <c>Sound_MusicEnabled</c> (<c>0049f90c</c>) — the enable flag for catalog ids below
	/// <see cref="SoundId.FirstEffect"/>.
	/// </summary>
	public bool MusicEnabled { get; set; } = true;

	/// <summary>
	/// <c>Sound_EffectsEnabled</c> (<c>0049f910</c>) — the enable flag for ids from
	/// <see cref="SoundId.FirstEffect"/> up.
	/// </summary>
	public bool EffectsEnabled { get; set; } = true;

	/// <summary>
	/// The options-screen 0-2 detail value (<c>004d1fc7</c>) the throttle divisor scales against.
	/// 2 lets every call through; 0 halves the rate the divisor already sets.
	/// </summary>
	public int DetailSetting { get; set; } = 2;

	/// <summary>Where the listener is — the camera, as it is in the original.</summary>
	public Vec3i ListenerPosition { get; set; }

	/// <summary>Which way the listener faces, as a binary angle, 0 being <c>+Y</c>.</summary>
	public int ListenerHeading { get; set; }

	/// <summary>Overall output gain, 0 to 1. Not the original's; a host-level volume control.</summary>
	public float MasterVolume {
		get => _masterVolume;
		set {
			_masterVolume = Math.Clamp(value, 0f, 1f);
			_backend.SetMasterGain(_masterVolume);
		}
	}

	private float _masterVolume = 1f;

	/// <summary>
	/// <c>Sound_Play</c> (<c>0046272c</c>) — the non-positional play. Rolls the variation count,
	/// sets the volume the row's own attributes give it, and starts the voice.
	/// </summary>
	public void Play(int id) {
		id = RollVariation(id);
		if (Entry(id) is not { } entry) {
			return;
		}

		SetVolume(id, CategoryEnabled(id) ? AttributeVolume(entry) : 0);
		Start(id, entry);
	}

	/// <summary>
	/// <c>Sound_PlayAt</c> (<c>004627dc</c>) — the positional play. Rolls the variation count, places
	/// the sound, and starts it only if <see cref="Place"/> found it audible.
	/// </summary>
	/// <returns>Whether the sound was close enough to be played at all.</returns>
	public bool PlayAt(int id, Vec3i position) {
		id = RollVariation(id);
		if (Entry(id) is not { } entry) {
			return false;
		}

		if (!Place(id, position)) {
			return false;
		}

		Start(id, entry);
		return true;
	}

	/// <inheritdoc />
	void ISoundSink.PlayAt(int id, Vec3i position) => PlayAt(id, position);

	/// <inheritdoc />
	void ISoundSink.MoveTo(int id, Vec3i position) => UpdatePosition(id, position);

	/// <summary>
	/// <c>Sound_Place</c> (<c>00462898</c>) — works out one sound's volume and pan from where it is
	/// relative to the listener, and reports whether it is audible at all.
	///
	/// <para>The rolloff is the original's, including its oddity: the falloff divides by the cutoff
	/// distance rather than by the width of the band between the two ranges, so a source sitting
	/// exactly at <see cref="SoundCatalog.Entry.MinRange"/> is already attenuated rather than at full
	/// volume.</para>
	/// </summary>
	/// <returns>False when the source is past the row's cutoff, in which case nothing is played.</returns>
	public bool Place(int id, Vec3i position) {
		if (Entry(id) is not { } entry) {
			return false;
		}

		var offset = position - ListenerPosition;
		int distance = SimMath.FastMagnitude3D(offset.X, offset.Y, offset.Z);

		int minRange = entry.MinRange * SoundCatalog.RangeUnit;
		int maxRange = entry.MaxRange * SoundCatalog.RangeUnit;

		int volume;
		if (!CategoryEnabled(id)) {
			volume = 0;
		} else if (distance > maxRange) {
			volume = 0;
		} else {
			volume = SimMath.Q16Multiply(entry.Volume, SoundCatalog.VolumeTrim);
			if (distance >= minRange && maxRange > 0) {
				volume = (maxRange - distance) * volume / maxRange;
			}
		}

		SetVolume(id, volume * entry.CategoryVolume / 100);

		if (volume == 0) {
			return false;
		}

		// The listener's own frame: +Y is where it faces, +X is to its right. The original takes the
		// camera-transformed point's first two components and hands them to Math_Atan2Bam, which
		// takes its arguments as (x, y) — so those components are x = right, y = forward, the
		// ordinary view-space assignment. SimTrig.Atan2 is the same function with the conventional
		// atan2(y, x) parameter order, hence the swap here.
		int forward = SimMath.Q14Multiply(offset.X, BinaryAngle.Sin(-ListenerHeading))
			+ SimMath.Q14Multiply(offset.Y, BinaryAngle.Cos(-ListenerHeading));
		int right = SimMath.Q14Multiply(offset.X, BinaryAngle.Cos(-ListenerHeading))
			- SimMath.Q14Multiply(offset.Y, BinaryAngle.Sin(-ListenerHeading));

		int bearing = SimTrig.Atan2(forward, right);

		// Doubling the bearing sweeps the whole pan range over half a turn, which is what makes the
		// image mirror front to back — a stereo field cannot tell the two apart anyway.
		//
		// One deliberate deviation, at exactly one input. The original computes the front half as
		// `(ushort)(bearing * -2)`, which for a bearing of zero — a source precisely abeam — is zero,
		// the hard-left end, while every neighbouring bearing on both sides lands at the hard-right
		// end. The continuous value there is 0x10000, and it is only the truncation to sixteen bits
		// that turns it into its opposite. Reproducing that would put an audible snap to the far
		// channel on any sound passing dead abeam, and this engine's placement reaches the exact zero
		// far more often than the original's does: DBSIM's forward component comes out of a full
		// camera matrix carrying pitch and roll, where an exact zero is a coincidence, and this one
		// comes out of a plain horizontal rotation, where it is simply what abeam means.
		int pan = bearing < 0x8000
			? Math.Min(0x10000 - 2 * bearing, 0xffff)
			: (2 * bearing) & 0xffff;

		SetPan(id, pan);
		return true;
	}

	/// <summary>
	/// <c>Sound_UpdatePosition</c> (<c>00462878</c>) — re-places a sound that is already running,
	/// without starting it. The looping engine hum and the flamer are what the original uses it for.
	/// </summary>
	public void UpdatePosition(int id, Vec3i position) => Place(id, position);

	/// <summary><c>Sound_Stop</c> (<c>004629c0</c>).</summary>
	public void Stop(int id) {
		if (Voice(id) is { } voice) {
			_repeatsLeft[id] = 0;
			_backend.Stop(voice);
		}
	}

	/// <summary>
	/// <c>Sound_IsPlaying</c> (<c>004629ec</c>). Used by the original to avoid restarting a loop that
	/// is already running — the torso servo does exactly that.
	/// </summary>
	public bool IsPlaying(int id) =>
		Voice(id) is { } voice && (_backend.IsPlaying(voice) || _repeatsLeft[id] > 0);

	/// <summary>
	/// <c>Sound_SetPitch</c> (<c>00463010</c>) — the playback rate as the original's 16.16 ratio.
	/// <c>FUN_004328cc</c> uses it to drop the engine loop to <see cref="SoundId.EngineLoopPitch"/>.
	/// </summary>
	public void SetPitch(int id, int ratioQ16) {
		if (Voice(id) is { } voice) {
			_backend.SetPitch(voice, ratioQ16 / (float)PitchOne);
		}
	}

	/// <summary>
	/// <c>Sound_ThrottleCheck</c> (<c>004626c4</c>) — the "one in n" gate that keeps a sound fired by
	/// many objects in the same tick from playing once per object.
	///
	/// <para>The counter is per catalog row and wraps at 0x0f, exactly as the original's runtime
	/// attribute byte does.</para>
	/// </summary>
	/// <returns>Whether this call is one of the ones allowed through.</returns>
	public bool ThrottleCheck(int id) {
		if (Entry(id) is not { } entry) {
			return false;
		}

		if (entry.ThrottleCounter == 0x0f) {
			entry.ThrottleCounter = 0;
		}

		int interval = (2 - DetailSetting) * entry.ThrottleDivisor;
		if (interval == 0) {
			return true;
		}

		entry.ThrottleCounter++;
		return entry.ThrottleCounter % interval == 0;
	}

	/// <summary>
	/// <c>Sound_SetCategoryVolume</c> (<c>00462f5c</c>) — the per-row scale in percent that every
	/// volume this row computes is multiplied through.
	/// </summary>
	public void SetCategoryVolume(int id, byte percent) {
		if (Entry(id) is not { } entry) {
			return;
		}

		entry.CategoryVolume = percent;
		SetVolume(id, SimMath.Q16Multiply(entry.Volume, SoundCatalog.VolumeTrim) * percent / 100);
	}

	/// <summary>
	/// <c>Sound_SuspendAll</c> (<c>00463078</c>) — records what is playing and stops it, so the
	/// matching <see cref="ResumeAll"/> can put back exactly that set. The original does this when
	/// the window loses focus.
	/// </summary>
	public void SuspendAll() {
		for (int id = 0; id < _voices.Length; id++) {
			var entry = _bank.Catalog.Entries[id];
			entry.WasPlaying = IsPlaying(id);
			if (Voice(id) is { } voice) {
				_backend.Stop(voice);
			}
		}
	}

	/// <summary><c>Sound_ResumeAll</c> (<c>00463134</c>) — replays what <see cref="SuspendAll"/> stopped.</summary>
	public void ResumeAll() {
		for (int id = 0; id < _voices.Length; id++) {
			var entry = _bank.Catalog.Entries[id];
			if (entry.WasPlaying) {
				Start(id, entry);
			}

			entry.WasPlaying = false;
		}
	}

	/// <summary>Stops everything at once — <c>Sfx_StopAll</c>.</summary>
	public void StopAll() {
		Array.Clear(_repeatsLeft);
		_backend.StopAll();
	}

	/// <summary>
	/// Services the finite repeat counts. Call once a frame.
	///
	/// <para>Attribute byte 0 can ask for a sound to play a fixed number of times — the three cockpit
	/// alerts all ask for five — and no backend this targets expresses that, so the repeats are
	/// re-triggered here as each pass finishes. A count of 0 is endless and is the backend's own
	/// looping flag instead, so it never reaches this.</para>
	/// </summary>
	public void Update() {
		for (int id = 0; id < _voices.Length; id++) {
			if (_repeatsLeft[id] <= 0 || _voices[id] < 0) {
				continue;
			}

			if (_backend.IsPlaying(_voices[id])) {
				continue;
			}

			_repeatsLeft[id]--;
			if (_repeatsLeft[id] > 0) {
				_backend.Play(_voices[id]);
			}
		}
	}

	/// <summary>
	/// The variation roll every play does first — <c>id + rand(count)</c> when the row stands for
	/// more than one consecutive id, which is how a single impact id picks between
	/// <c>impacts2</c>, <c>3</c> and <c>5</c>.
	/// </summary>
	private int RollVariation(int id) {
		if (Entry(id) is not { VariationCount: > 1 } entry) {
			return id;
		}

		return id + _random.NextBelow(entry.VariationCount);
	}

	private void Start(int id, SoundCatalog.Entry entry) {
		if (Voice(id) is not { } voice) {
			return;
		}

		// A finite count is tracked here; an endless one was handed to the backend at construction.
		_repeatsLeft[id] = entry.LoopCount == 0 ? 0 : entry.LoopCount;
		_backend.Play(voice);
	}

	private void SetVolume(int id, int volume0To100) {
		if (Voice(id) is { } voice) {
			_backend.SetGain(voice, Math.Clamp(volume0To100 / 100f, 0f, 1f));
		}
	}

	private void SetPan(int id, int pan0To65535) {
		if (Voice(id) is { } voice) {
			_backend.SetPan(voice, (pan0To65535 - PanCentre) / (float)PanCentre);
		}
	}

	/// <summary>The row's authored volume after the loader's headroom trim and its category scale.</summary>
	private static int AttributeVolume(SoundCatalog.Entry entry) =>
		SimMath.Q16Multiply(entry.Volume, SoundCatalog.VolumeTrim) * entry.CategoryVolume / 100;

	private bool CategoryEnabled(int id) =>
		SoundCatalog.IsMusic(id) ? MusicEnabled : EffectsEnabled;

	private SoundCatalog.Entry? Entry(int id) =>
		_bank.Catalog[id] is { HasAttributes: true } entry ? entry : null;

	private int? Voice(int id) =>
		id >= 0 && id < _voices.Length && _voices[id] >= 0 ? _voices[id] : null;

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) {
			return;
		}

		_disposed = true;
		_backend.Dispose();
	}
}
