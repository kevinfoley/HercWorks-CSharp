namespace Herculan.Engine.Audio;

/// <summary>
/// The backend used when there is no audio device: a headless test, a mission editor, or a machine
/// where OpenAL failed to open.
///
/// <para>It hands out real voice handles and remembers nothing else, so
/// <see cref="SoundDirector"/> runs its whole rule set — variation rolls, throttle counters,
/// distance cutoff — exactly as it would with a device attached. That is what makes those rules
/// testable without one.</para>
///
/// <para><see cref="IsPlaying"/> always answers false, which is the one behavioural difference:
/// a finite repeat count completes immediately instead of after the sample's length.</para>
/// </summary>
public sealed class NullAudioBackend : IAudioBackend {
	private int _next;

	/// <inheritdoc />
	public bool IsAvailable => false;

	/// <inheritdoc />
	public int CreateVoice(WaveSample sample) => _next++;

	/// <inheritdoc />
	public void Play(int voice) { }

	/// <inheritdoc />
	public void Stop(int voice) { }

	/// <inheritdoc />
	public bool IsPlaying(int voice) => false;

	/// <inheritdoc />
	public void SetGain(int voice, float gain) { }

	/// <inheritdoc />
	public void SetPan(int voice, float pan) { }

	/// <inheritdoc />
	public void SetPitch(int voice, float ratio) { }

	/// <inheritdoc />
	public void SetLooping(int voice, bool looping) { }

	/// <inheritdoc />
	public void SetMasterGain(float gain) { }

	/// <inheritdoc />
	public void StopAll() { }

	/// <inheritdoc />
	public void Dispose() { }
}
