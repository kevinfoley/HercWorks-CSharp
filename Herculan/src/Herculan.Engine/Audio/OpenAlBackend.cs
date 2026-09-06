using Silk.NET.OpenAL;

namespace Herculan.Engine.Audio;

/// <summary>
/// <see cref="IAudioBackend"/> on OpenAL, through Silk.NET — the backend docs/engine/planning.md
/// picks for audio.
///
/// <para><b>OpenAL's own 3D model is switched off.</b> <see cref="DistanceModel.None"/> is set
/// globally and every source is marked source-relative with its rolloff factor zeroed, so no
/// attenuation of OpenAL's happens at all. Gain and stereo position are computed by
/// <see cref="SoundDirector.Place"/> from the original's rules and arrive here already final; the
/// source position exists only to make OpenAL pan, and rides a unit circle around the listener at
/// the origin.</para>
///
/// <para><b>One buffer per sample, sources from a pool.</b> A play claims a free source, binds the
/// sample's buffer to it and starts it, so the same sample sounding twice occupies two sources and
/// is heard twice — the original's arrangement, where <c>Sfx_Play</c> starts a fresh
/// <c>sosDIGIStartSample</c> on every call. See docs/formats/audio.md, "A repeated play layers; it
/// does not restart".</para>
/// </summary>
public sealed unsafe class OpenAlBackend : IAudioBackend {
	/// <summary>
	/// How many playbacks can sound at once.
	///
	/// <para><b>This engine's number, not the original's.</b> Retail's ceiling is whatever its SOS
	/// driver was initialised with, and that is not recovered: the <c>sosDIGIInitDriver</c> argument
	/// block at <c>006b5614</c> is filled field by field with no header to name them, so which word
	/// is the channel count is not known. This is chosen instead against what the game asks for —
	/// 43 catalog samples and 63 <c>CVM</c> clips, of which only a handful overlap in practice —
	/// and against OpenAL Soft's own hard limit of 256 sources.</para>
	/// </summary>
	public const int ChannelCount = 64;

	private readonly AL _al;
	private readonly ALContext _alc;
	private readonly Device* _device;
	private readonly Context* _context;
	private readonly List<uint> _buffers = new();
	private readonly uint[] _sources = new uint[ChannelCount];
	private readonly int[] _generation = new int[ChannelCount];
	private int _channels;
	private int _cursor;
	private bool _disposed;

	private OpenAlBackend(AL al, ALContext alc, Device* device, Context* context) {
		_al = al;
		_alc = alc;
		_device = device;
		_context = context;
		OpenChannels();
	}

	/// <inheritdoc />
	public bool IsAvailable => !_disposed && _channels > 0;

	/// <summary>
	/// How many times <see cref="TryCreate"/> opens the device before giving up, and how long it
	/// waits between tries.
	///
	/// <para><b>A first try failing is not a missing sound card.</b> On Windows
	/// <c>alcCreateContext</c> is where OpenAL Soft's WASAPI backend calls
	/// <c>IAudioClient::Initialize</c>, and on an endpoint that is still settling that call fails
	/// intermittently — measured at roughly one launch in seven on this machine, with
	/// <c>AUDCLNT_E_DEVICE_INVALIDATED</c> (<c>0x88890004</c>) or <c>ERROR_GEN_FAILURE</c>
	/// (<c>0x8007001f</c>) reported by the driver and <c>ALC_INVALID_DEVICE</c> reaching the caller.
	/// It clears on its own, so the device is closed and reopened rather than the whole session being
	/// written off as silent.</para>
	/// </summary>
	public const int OpenAttempts = 4;

	/// <summary>How long to wait between the tries <see cref="OpenAttempts"/> allows.</summary>
	public static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(150);

	/// <summary>
	/// Opens the default device, or returns null when there is none, when the OpenAL runtime is not
	/// present, or when context creation keeps failing. A machine with no sound card is not an error
	/// — the caller falls back to <see cref="NullAudioBackend"/> and the game runs silent.
	/// </summary>
	/// <param name="failure">
	/// What went wrong, or null when nothing did. The caller puts it in its status line: a device
	/// that will not start otherwise reads identically to a machine that has none, and the two want
	/// different things done about them.
	/// </param>
	public static OpenAlBackend? TryCreate(out string? failure) {
		AL? al = null;
		ALContext? alc = null;
		failure = null;

		try {
			alc = ALContext.GetApi(soft: true);
			al = AL.GetApi(soft: true);

			for (int attempt = 1; ; attempt++) {
				var device = alc.OpenDevice("");
				if (device == null) {
					// No endpoint at all, which retrying will not conjure one of.
					failure = "no output device";
					break;
				}

				var context = alc.CreateContext(device, null);
				if (context != null && alc.MakeContextCurrent(context)) {
					// Nothing OpenAL does to gain by distance is wanted; the original's own rolloff has
					// already been applied by the time a gain reaches SetGain.
					al.DistanceModel(DistanceModel.None);
					al.SetListenerProperty(ListenerVector3.Position, 0f, 0f, 0f);
					al.SetListenerProperty(ListenerVector3.Velocity, 0f, 0f, 0f);

					var backend = new OpenAlBackend(al, alc, device, context);
					failure = attempt > 1 ? $"the device needed {attempt} tries to start" : null;

					if (backend._channels < ChannelCount) {
						failure = $"{backend._channels} of {ChannelCount} channels available";
					}

					return backend;
				}

				var error = alc.GetError(device);
				if (context != null) {
					alc.DestroyContext(context);
				}

				alc.CloseDevice(device);

				if (attempt >= OpenAttempts) {
					failure = $"the device would not start in {OpenAttempts} tries (ALC {error})";
					break;
				}

				Thread.Sleep(OpenRetryDelay);
			}
		} catch (Exception e) {
			// Silk.NET throws rather than returning null when the native library is missing, which
			// on a machine without OpenAL is the normal case and not a fault.
			failure = $"the OpenAL runtime would not load: {e.Message}";
		}

		alc?.Dispose();
		al?.Dispose();
		return null;
	}

	/// <summary>
	/// Generates the channel pool up front. A device that will not hand out the full
	/// <see cref="ChannelCount"/> is not fatal — whatever it gave is the ceiling, and
	/// <see cref="TryCreate"/> says so in its status line.
	/// </summary>
	private void OpenChannels() {
		// GetError latches, so clear whatever an earlier call left before reading this one's.
		_al.GetError();

		for (int i = 0; i < ChannelCount; i++) {
			uint source = _al.GenSource();
			if (_al.GetError() != AudioError.NoError) {
				break;
			}

			_al.SetSourceProperty(source, SourceBoolean.SourceRelative, true);
			_al.SetSourceProperty(source, SourceFloat.RolloffFactor, 0f);
			_al.SetSourceProperty(source, SourceFloat.ReferenceDistance, 1f);
			_sources[i] = source;
			_channels++;
		}
	}

	/// <inheritdoc />
	/// <remarks>
	/// Error-checked at every step, because OpenAL reports a refusal by leaving the name at zero
	/// rather than by failing loudly — and zero is a legal argument everywhere else in the API, so an
	/// unchecked failure produces a sample id that is accepted, played and silent for the rest of the
	/// mission.
	/// </remarks>
	public int CreateSample(WaveSample sample) {
		if (_disposed || sample.Samples.Length == 0) {
			return -1;
		}

		_al.GetError();

		uint buffer = _al.GenBuffer();
		if (_al.GetError() != AudioError.NoError) {
			return -1;
		}

		_al.BufferData(buffer, BufferFormat.Mono16, sample.Samples, sample.SampleRate);
		if (_al.GetError() != AudioError.NoError) {
			_al.DeleteBuffer(buffer);
			return -1;
		}

		_buffers.Add(buffer);
		return _buffers.Count - 1;
	}

	/// <inheritdoc />
	public int Start(int sample, float gain, float pan, float pitch, bool looping) {
		if (_disposed || sample < 0 || sample >= _buffers.Count) {
			return -1;
		}

		int slot = ClaimChannel();
		if (slot < 0) {
			return -1;
		}

		uint source = _sources[slot];

		// The buffer has to be detached before a different one can be attached, and the source must
		// be stopped for either to be legal.
		_al.SourceStop(source);
		_al.SetSourceProperty(source, SourceInteger.Buffer, (int)_buffers[sample]);
		_al.SetSourceProperty(source, SourceBoolean.Looping, looping);
		SetSourceGain(source, gain);
		SetSourcePan(source, pan);
		SetSourcePitch(source, pitch);
		_al.SourcePlay(source);

		return Handle(slot);
	}

	/// <summary>
	/// Finds a channel that is not sounding. The search starts where the last one left off so that
	/// slots are reused in rotation rather than the low ones churning, which keeps a handle valid for
	/// as long as the pool allows.
	/// </summary>
	/// <returns>The slot, or -1 when every channel is busy.</returns>
	private int ClaimChannel() {
		for (int i = 0; i < _channels; i++) {
			int slot = (_cursor + i) % _channels;
			_al.GetSourceProperty(_sources[slot], GetSourceInteger.SourceState, out int state);
			if ((SourceState)state == SourceState.Playing || (SourceState)state == SourceState.Paused) {
				continue;
			}

			_cursor = (slot + 1) % _channels;

			// Taking the slot invalidates whatever handle last named it, so nothing that still holds
			// one can reach across and mute, move or re-pitch the sound that replaced it.
			_generation[slot]++;
			return slot;
		}

		return -1;
	}

	/// <summary>
	/// Packs a slot and its generation into one handle, the way the original's own <c>SFX</c> layer
	/// does (<c>generation &lt;&lt; 16 | slotIndex</c>, docs/formats/audio.md). The generation is
	/// masked to fifteen bits so a handle is never negative and never collides with -1.
	/// </summary>
	private int Handle(int slot) => ((_generation[slot] & 0x7fff) << 16) | slot;

	private bool Resolve(int play, out uint source) {
		source = 0;
		if (_disposed || play < 0) {
			return false;
		}

		int slot = play & 0xffff;
		if (slot >= _channels || ((_generation[slot] & 0x7fff) << 16) != (play & 0x7fff0000)) {
			return false;
		}

		source = _sources[slot];
		return true;
	}

	/// <inheritdoc />
	public void Stop(int play) {
		if (Resolve(play, out uint source)) {
			_al.SourceStop(source);
		}
	}

	/// <inheritdoc />
	public bool IsPlaying(int play) {
		if (!Resolve(play, out uint source)) {
			return false;
		}

		_al.GetSourceProperty(source, GetSourceInteger.SourceState, out int state);
		return (SourceState)state == SourceState.Playing;
	}

	/// <inheritdoc />
	public void SetGain(int play, float gain) {
		if (Resolve(play, out uint source)) {
			SetSourceGain(source, gain);
		}
	}

	/// <inheritdoc />
	public void SetPan(int play, float pan) {
		if (Resolve(play, out uint source)) {
			SetSourcePan(source, pan);
		}
	}

	/// <inheritdoc />
	public void SetPitch(int play, float ratio) {
		if (Resolve(play, out uint source)) {
			SetSourcePitch(source, ratio);
		}
	}

	private void SetSourceGain(uint source, float gain) =>
		_al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(gain, 0f, 1f));

	private void SetSourcePan(uint source, float pan) {
		// A unit vector round the listener: x is the pan, and the remainder goes into -z so the
		// source stays one unit away and in front. Distance is irrelevant with the model off, but a
		// constant radius keeps OpenAL's own panning behaved.
		float x = Math.Clamp(pan, -1f, 1f);
		float z = -MathF.Sqrt(Math.Max(0f, 1f - x * x));
		_al.SetSourceProperty(source, SourceVector3.Position, x, 0f, z);
	}

	private void SetSourcePitch(uint source, float ratio) =>
		// OpenAL rejects a pitch of zero outright and distorts below about 0.02.
		_al.SetSourceProperty(source, SourceFloat.Pitch, Math.Clamp(ratio, 0.02f, 8f));

	/// <inheritdoc />
	public void SetMasterGain(float gain) {
		if (!_disposed) {
			_al.SetListenerProperty(ListenerFloat.Gain, Math.Clamp(gain, 0f, 1f));
		}
	}

	/// <inheritdoc />
	public void StopAll() {
		if (_disposed) {
			return;
		}

		for (int slot = 0; slot < _channels; slot++) {
			_al.SourceStop(_sources[slot]);
		}
	}

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) {
			return;
		}

		_disposed = true;

		for (int slot = 0; slot < _channels; slot++) {
			_al.SourceStop(_sources[slot]);
			_al.DeleteSource(_sources[slot]);
		}

		foreach (uint buffer in _buffers) {
			_al.DeleteBuffer(buffer);
		}

		_channels = 0;
		_buffers.Clear();

		_alc.MakeContextCurrent(null);
		_alc.DestroyContext(_context);
		_alc.CloseDevice(_device);
		_al.Dispose();
		_alc.Dispose();
	}
}
