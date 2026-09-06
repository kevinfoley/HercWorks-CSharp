namespace Herculan.Engine.Audio;

/// <summary>
/// The mixer underneath <see cref="SoundDirector"/>: it holds the decoded samples, hands out a
/// playback channel when one is asked for, and does nothing but play what it is told at the gain
/// and pan it is given.
///
/// <para><b>A sample is not a channel.</b> <see cref="CreateSample"/> registers one decoded
/// recording; <see cref="Start"/> begins a playback of it and returns a handle to <i>that
/// playback</i>. Starting the same sample twice gives two handles and two concurrent copies, which
/// is what the original does — see docs/formats/audio.md, "A repeated play layers; it does not
/// restart". Channels are finite, so <see cref="Start"/> can refuse.</para>
///
/// <para><b>Deliberately not a 3D audio API.</b> Distance rolloff, panning and the audible cutoff
/// are the original's own rules and are computed in <see cref="SoundDirector.Place"/> before
/// anything reaches here — see docs/formats/audio.md. A backend that applied its own distance model
/// on top would fight them, so this interface takes a finished gain and a finished pan and no
/// listener at all.</para>
///
/// <para>The abstraction exists for the reason docs/engine/planning.md gives for the rendering
/// backend: Windows is the development target but OS-specific paths are kept behind an interface
/// from the start. It is also what lets the simulation and its tests run with no audio device
/// present, through <see cref="NullAudioBackend"/>.</para>
/// </summary>
public interface IAudioBackend : IDisposable {
	/// <summary>Whether a device actually opened. False means every call here is a no-op.</summary>
	bool IsAvailable { get; }

	/// <summary>
	/// Registers one decoded recording. Called once per catalog row that has a sample, and once per
	/// computer-voice clip on first use.
	/// </summary>
	/// <returns>A sample id for <see cref="Start"/>, or -1 when it could not be registered.</returns>
	int CreateSample(WaveSample sample);

	/// <summary>
	/// Begins a playback of <paramref name="sample"/> on a free channel.
	///
	/// <para>Gain, pan and pitch are passed in rather than set afterwards because the channel does
	/// not exist until this call: they are the caller's own per-sound state, which
	/// <see cref="SoundDirector"/> keeps exactly as the original keeps it on the voice record.</para>
	/// </summary>
	/// <param name="looping">
	/// Endless repetition. Finite repeat counts are not a backend concern —
	/// <see cref="SoundDirector"/> re-triggers those itself, because no backend this targets
	/// expresses "play exactly n times".
	/// </param>
	/// <returns>
	/// A handle naming this playback, or -1 when the sample is unknown or every channel is busy.
	/// A refusal is not an error: the original's own <c>sosDIGIStartSample</c> fails the same way
	/// and <c>Sfx_Play</c> returns 0 without complaint.
	/// </returns>
	int Start(int sample, float gain, float pan, float pitch, bool looping);

	/// <summary>Stops one playback and frees its channel. A stale handle is ignored.</summary>
	void Stop(int play);

	/// <summary>Whether <paramref name="play"/> is still sounding. A stale handle answers false.</summary>
	bool IsPlaying(int play);

	/// <summary>Sets a running playback's gain, 0 to 1.</summary>
	void SetGain(int play, float gain);

	/// <summary>Sets a running playback's stereo position: -1 hard left, 0 centre, +1 hard right.</summary>
	void SetPan(int play, float pan);

	/// <summary>Sets a running playback's rate as a ratio, 1 being the recorded rate.</summary>
	void SetPitch(int play, float ratio);

	/// <summary>Overall output gain, 0 to 1.</summary>
	void SetMasterGain(float gain);

	/// <summary>Stops every playback at once.</summary>
	void StopAll();
}
