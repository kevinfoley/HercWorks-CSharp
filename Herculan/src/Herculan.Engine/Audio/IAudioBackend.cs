namespace Herculan.Engine.Audio;

/// <summary>
/// The mixer underneath <see cref="SoundDirector"/>: it owns one voice per catalog id and does
/// nothing but play what it is told, where it is told, at the gain and pan it is given.
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
	/// Creates the voice for one catalog id and binds <paramref name="sample"/> to it. Called once
	/// per id at startup, mirroring the original's one-voice-per-catalog-row arrangement: playing an
	/// id that is already sounding restarts that single voice rather than layering another copy.
	/// </summary>
	/// <returns>A handle for the other calls here, or -1 when the voice could not be created.</returns>
	int CreateVoice(WaveSample sample);

	/// <summary>Starts <paramref name="voice"/> from the beginning, restarting it if it is running.</summary>
	void Play(int voice);

	/// <summary>Stops <paramref name="voice"/> and rewinds it.</summary>
	void Stop(int voice);

	/// <summary>Whether <paramref name="voice"/> is still sounding.</summary>
	bool IsPlaying(int voice);

	/// <summary>Sets <paramref name="voice"/>'s gain, 0 to 1.</summary>
	void SetGain(int voice, float gain);

	/// <summary>Sets <paramref name="voice"/>'s stereo position: -1 hard left, 0 centre, +1 hard right.</summary>
	void SetPan(int voice, float pan);

	/// <summary>Sets <paramref name="voice"/>'s playback rate as a ratio, 1 being the recorded rate.</summary>
	void SetPitch(int voice, float ratio);

	/// <summary>
	/// Sets whether <paramref name="voice"/> repeats endlessly. Finite repeat counts are not a
	/// backend concern — <see cref="SoundDirector"/> re-triggers those itself, because no backend
	/// this targets expresses "play exactly n times".
	/// </summary>
	void SetLooping(int voice, bool looping);

	/// <summary>Overall output gain, 0 to 1.</summary>
	void SetMasterGain(float gain);

	/// <summary>Stops everything at once.</summary>
	void StopAll();
}
