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
/// <para>One source and one buffer per catalog id, created up front, which is the arrangement the
/// original has: playing an id that is already sounding restarts that one voice.</para>
/// </summary>
public sealed unsafe class OpenAlBackend : IAudioBackend {
	private readonly AL _al;
	private readonly ALContext _alc;
	private readonly Device* _device;
	private readonly Context* _context;
	private readonly List<uint> _sources = new();
	private readonly List<uint> _buffers = new();
	private bool _disposed;

	private OpenAlBackend(AL al, ALContext alc, Device* device, Context* context) {
		_al = al;
		_alc = alc;
		_device = device;
		_context = context;
	}

	/// <inheritdoc />
	public bool IsAvailable => !_disposed;

	/// <summary>
	/// Opens the default device, or returns null when there is none, when the OpenAL runtime is not
	/// present, or when context creation fails. A machine with no sound card is not an error — the
	/// caller falls back to <see cref="NullAudioBackend"/> and the game runs silent.
	/// </summary>
	public static OpenAlBackend? TryCreate() {
		AL? al = null;
		ALContext? alc = null;

		try {
			alc = ALContext.GetApi(soft: true);
			al = AL.GetApi(soft: true);

			var device = alc.OpenDevice("");
			if (device == null) {
				alc.Dispose();
				al.Dispose();
				return null;
			}

			var context = alc.CreateContext(device, null);
			if (context == null || !alc.MakeContextCurrent(context)) {
				alc.CloseDevice(device);
				alc.Dispose();
				al.Dispose();
				return null;
			}

			// Nothing OpenAL does to gain by distance is wanted; the original's own rolloff has
			// already been applied by the time a gain reaches SetGain.
			al.DistanceModel(DistanceModel.None);
			al.SetListenerProperty(ListenerVector3.Position, 0f, 0f, 0f);
			al.SetListenerProperty(ListenerVector3.Velocity, 0f, 0f, 0f);

			return new OpenAlBackend(al, alc, device, context);
		} catch (Exception) {
			// Silk.NET throws rather than returning null when the native library is missing, which
			// on a machine without OpenAL is the normal case and not a fault.
			alc?.Dispose();
			al?.Dispose();
			return null;
		}
	}

	/// <inheritdoc />
	public int CreateVoice(WaveSample sample) {
		if (_disposed || sample.Samples.Length == 0) {
			return -1;
		}

		uint buffer = _al.GenBuffer();
		_al.BufferData(buffer, BufferFormat.Mono16, sample.Samples, sample.SampleRate);

		uint source = _al.GenSource();
		_al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);
		_al.SetSourceProperty(source, SourceBoolean.SourceRelative, true);
		_al.SetSourceProperty(source, SourceFloat.RolloffFactor, 0f);
		_al.SetSourceProperty(source, SourceFloat.ReferenceDistance, 1f);
		_al.SetSourceProperty(source, SourceFloat.Gain, 1f);
		_al.SetSourceProperty(source, SourceVector3.Position, 0f, 0f, -1f);

		_buffers.Add(buffer);
		_sources.Add(source);
		return _sources.Count - 1;
	}

	/// <inheritdoc />
	public void Play(int voice) {
		if (!Resolve(voice, out uint source)) {
			return;
		}

		// Restart rather than stack: the original has one voice per catalog id, so firing the same
		// sound again while it sounds retriggers it from the top.
		_al.SourceStop(source);
		_al.SetSourceProperty(source, SourceInteger.SampleOffset, 0);
		_al.SourcePlay(source);
	}

	/// <inheritdoc />
	public void Stop(int voice) {
		if (Resolve(voice, out uint source)) {
			_al.SourceStop(source);
		}
	}

	/// <inheritdoc />
	public bool IsPlaying(int voice) {
		if (!Resolve(voice, out uint source)) {
			return false;
		}

		_al.GetSourceProperty(source, GetSourceInteger.SourceState, out int state);
		return (SourceState)state == SourceState.Playing;
	}

	/// <inheritdoc />
	public void SetGain(int voice, float gain) {
		if (Resolve(voice, out uint source)) {
			_al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(gain, 0f, 1f));
		}
	}

	/// <inheritdoc />
	public void SetPan(int voice, float pan) {
		if (!Resolve(voice, out uint source)) {
			return;
		}

		// A unit vector round the listener: x is the pan, and the remainder goes into -z so the
		// source stays one unit away and in front. Distance is irrelevant with the model off, but a
		// constant radius keeps OpenAL's own panning behaved.
		float x = Math.Clamp(pan, -1f, 1f);
		float z = -MathF.Sqrt(Math.Max(0f, 1f - x * x));
		_al.SetSourceProperty(source, SourceVector3.Position, x, 0f, z);
	}

	/// <inheritdoc />
	public void SetPitch(int voice, float ratio) {
		if (Resolve(voice, out uint source)) {
			// OpenAL rejects a pitch of zero outright and distorts below about 0.02.
			_al.SetSourceProperty(source, SourceFloat.Pitch, Math.Clamp(ratio, 0.02f, 8f));
		}
	}

	/// <inheritdoc />
	public void SetLooping(int voice, bool looping) {
		if (Resolve(voice, out uint source)) {
			_al.SetSourceProperty(source, SourceBoolean.Looping, looping);
		}
	}

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

		foreach (uint source in _sources) {
			_al.SourceStop(source);
		}
	}

	private bool Resolve(int voice, out uint source) {
		if (_disposed || voice < 0 || voice >= _sources.Count) {
			source = 0;
			return false;
		}

		source = _sources[voice];
		return true;
	}

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) {
			return;
		}

		_disposed = true;

		foreach (uint source in _sources) {
			_al.SourceStop(source);
			_al.DeleteSource(source);
		}

		foreach (uint buffer in _buffers) {
			_al.DeleteBuffer(buffer);
		}

		_sources.Clear();
		_buffers.Clear();

		_alc.MakeContextCurrent(null);
		_alc.DestroyContext(_context);
		_alc.CloseDevice(_device);
		_al.Dispose();
		_alc.Dispose();
	}
}
