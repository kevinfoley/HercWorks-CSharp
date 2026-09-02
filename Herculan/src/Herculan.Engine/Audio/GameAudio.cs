using Herculan.Engine.Content;
using Herculan.Engine.Numerics;
using Herculan.Engine.Sim;

namespace Herculan.Engine.Audio;

/// <summary>
/// The whole audio subsystem as one object for a host to hold: the device, the bank, the director,
/// and the handful of per-frame duties that belong to none of them individually.
///
/// <para>Everything here is optional. <see cref="Create"/> returns an instance even with no device
/// and no samples, so a host never has to branch on whether sound came up.</para>
/// </summary>
public sealed class GameAudio : ISoundSink, IDisposable {
	/// <summary>
	/// How long after the power-up begins the computer announces the result — the original's own
	/// <c>200 &lt; elapsed</c> against <c>Time_GetCoarseTicks</c>, whose unit is 16 ms. It lands
	/// inside <c>start3</c>'s five seconds rather than after them.
	/// </summary>
	public static readonly TimeSpan PowerUpAnnounceDelay = TimeSpan.FromMilliseconds(200 * 16);

	private readonly SoundDirector? _director;
	private MechObject? _engineLoopOwner;
	private SimWorld? _world;
	private TimeSpan _powerUpAnnounceIn = TimeSpan.MinValue;

	private GameAudio(SoundDirector? director, SoundBank? bank, ComputerVoice? voice, string status) {
		_director = director;
		Bank = bank;
		Voice = voice;
		Status = status;
	}

	/// <summary>
	/// The cockpit computer's speaking channel, or null when there is no device. Separate from
	/// <see cref="Director"/> because speech is a separate channel in the original — see
	/// <see cref="ComputerVoice"/>.
	/// </summary>
	public ComputerVoice? Voice { get; }

	/// <summary>The loaded catalog and samples, or null when <c>SOUNDS.STR</c> would not load.</summary>
	public SoundBank? Bank { get; }

	/// <summary>Why audio is in the state it is, for the debug panel and the startup log.</summary>
	public string Status { get; }

	/// <summary>Whether sound will actually be heard.</summary>
	public bool IsAvailable => _director is { IsAvailable: true };

	/// <summary>
	/// The sink to hand <see cref="SimWorld.Sounds"/>. This type is it: the two channels the
	/// simulation can reach — the effect catalog and the computer's voice — are separate objects
	/// underneath, and which one a call belongs to is not the simulation's business.
	/// </summary>
	public ISoundSink Sink => this;

	/// <inheritdoc />
	void ISoundSink.Play(int id) => _director?.Play(id);

	/// <inheritdoc />
	void ISoundSink.PlayAt(int id, Vec3i position) => _director?.PlayAt(id, position);

	/// <inheritdoc />
	void ISoundSink.Stop(int id) => _director?.Stop(id);

	/// <inheritdoc />
	void ISoundSink.MoveTo(int id, Vec3i position) => _director?.UpdatePosition(id, position);

	/// <inheritdoc />
	void ISoundSink.Say(int messageId) => Voice?.Post(messageId);

	/// <inheritdoc />
	void ISoundSink.Unsay(int messageId) => Voice?.Cancel(messageId);

	/// <summary>The rule layer, for a caller that wants to play something directly.</summary>
	public SoundDirector? Director => _director;

	/// <summary>Overall output gain, 0 to 1.</summary>
	public float MasterVolume {
		get => _director?.MasterVolume ?? 0f;
		set {
			if (_director != null) {
				_director.MasterVolume = value;
			}
		}
	}

	/// <summary>
	/// Brings audio up against the mounted archives. Never throws and never returns null: a missing
	/// <c>SIMSOUND.VOL</c>, a missing <c>SOUNDS.STR</c> or a machine with no output device all give
	/// an instance that silently does nothing, with <see cref="Status"/> saying which.
	/// </summary>
	/// <param name="content">The mounted archives. Must include <c>SIMSOUND.VOL</c> for the samples.</param>
	/// <param name="random">
	/// The generator the variation roll draws on — pass the world's, as the original does.
	/// </param>
	/// <param name="lowMemory">Select the half-rate <c>hmx</c> sample bank.</param>
	public static GameAudio Create(GameContent content, SimRandom? random = null, bool lowMemory = false) {
		SoundBank? bank;
		try {
			bank = SoundBank.Load(content, lowMemory);
		} catch (Exception e) {
			return new GameAudio(null, null, null, $"sound bank failed to load: {e.Message}");
		}

		if (bank == null) {
			return new GameAudio(null, null, null,
				$"no {SoundCatalog.ResourceName} in the mounted archives — is {SoundBank.ArchiveName} mounted?");
		}

		var backend = (IAudioBackend?)OpenAlBackend.TryCreate() ?? new NullAudioBackend();
		var director = new SoundDirector(bank, backend, random);

		var messages = SystemMessages.Load(content);
		var voice = new ComputerVoice(content, messages, backend);

		string status = backend.IsAvailable
			? $"OpenAL, {bank.Catalog.Count} catalog entries"
			: $"no audio device; {bank.Catalog.Count} catalog entries loaded but silent";

		status += messages != null
			? $", {messages.Count} computer messages"
			: $", no {SystemMessages.ResourceName} (computer voice silent)";

		if (bank.Missing.Count > 0) {
			// battle1.wav is expected: the ten music rows all name it and it ships nowhere, because
			// music is CD audio. Anything else is worth seeing.
			status += $" (no sample for: {string.Join(", ", bank.Missing)})";
		}

		return new GameAudio(director, bank, voice, status);
	}

	/// <summary>
	/// Attaches this as <paramref name="world"/>'s sink, so everything the simulation does is heard,
	/// and remembers the world so <see cref="SetListener"/> can keep
	/// <see cref="SimWorld.ListenerPosition"/> in step with the director's.
	/// </summary>
	public void Attach(SimWorld world) {
		_world = world;
		world.Sounds = Sink;
	}

	/// <summary>
	/// Where the player's ears are. The original's listener is the camera, so this takes the camera's
	/// own position and facing rather than the machine's.
	///
	/// <para>The world gets the position too: its own range gates — the footfall's and the
	/// missile-inbound warning's — measure against the camera, and having one setter for both is what
	/// stops them drifting apart.</para>
	/// </summary>
	/// <param name="position">Camera position, in world units.</param>
	/// <param name="heading">
	/// Camera facing as a binary angle in the <b>simulation's</b> convention, 0 being <c>+Y</c>. A
	/// host holding a <c>Camera</c> must negate its <c>Yaw</c>, which runs the other way.
	/// </param>
	public void SetListener(Vec3i position, int heading) {
		if (_world != null) {
			_world.ListenerPosition = position;
		}

		if (_director == null) {
			return;
		}

		_director.ListenerPosition = position;
		_director.ListenerHeading = heading;
	}

	/// <summary>
	/// <c>FUN_004328cc</c> — the cockpit's power-up, played when the player takes a machine. Plays
	/// the start-up sequence, and for a flyer also starts the engine hum and drops it to the pitch
	/// the original sets.
	///
	/// <para><b>The hum is the flyer's, not the walker's</b>, despite the sample being called
	/// <c>herceng1</c>. The original gates it on the type record's <c>+0x50</c> — file offset 78,
	/// <c>InputFlagFlyer</c>, set on the RAZOR alone — so a HERC powers up without one and its
	/// running noise is its footsteps. See docs/simulation/mech-locomotion.md's type-record table.</para>
	/// </summary>
	public void PowerUp(MechObject pilot) {
		if (_director == null) {
			return;
		}

		_director.Play(SoundId.PowerUp);
		_powerUpAnnounceIn = PowerUpAnnounceDelay;

		if (!pilot.Type.IsFlyer) {
			_engineLoopOwner = null;
			return;
		}

		_engineLoopOwner = pilot;
		_director.PlayAt(SoundId.EngineLoop, pilot.Position);
		_director.SetPitch(SoundId.EngineLoop, SoundId.EngineLoopPitch);
	}

	/// <summary>Stops the engine hum — leaving the cockpit, or the machine dying.</summary>
	public void PowerDown() {
		_engineLoopOwner = null;
		_powerUpAnnounceIn = TimeSpan.MinValue;
		_director?.Stop(SoundId.EngineLoop);
	}

	/// <summary>
	/// Per-frame service: keeps the engine hum on its machine, lets finite repeat counts run, and
	/// runs the speech channel. Call once a frame, after the listener has been set.
	/// </summary>
	/// <param name="elapsed">Wall time since the last call, for the power-up announcement's delay.</param>
	public void Update(TimeSpan elapsed = default) {
		AnnouncePowerUp(elapsed);
		Voice?.Update();

		if (_director == null) {
			return;
		}

		// The hum is positional and its machine moves, so it is re-placed rather than left where it
		// started — Sound_UpdatePosition is exactly what the original uses it for.
		if (_engineLoopOwner is { Removed: false, Destroyed: false } owner) {
			_director.UpdatePosition(SoundId.EngineLoop, owner.Position);
		} else if (_engineLoopOwner != null) {
			PowerDown();
		}

		_director.Update();
	}

	/// <summary>
	/// <c>FUN_00432924</c>'s tail — the cockpit's power-up sequence announcing itself once
	/// <see cref="PowerUpAnnounceDelay"/> has passed since the sequence began.
	///
	/// <para><b>Always the nominal line.</b> The original chooses between it and
	/// <see cref="SystemMessages.PowerUpDamaged"/> by walking the ten heads-down gauges and testing
	/// each one's reading against 0x5a, through two accessors (<c>FUN_0041b514</c> and
	/// <c>FUN_00438700</c>) that are not decompiled — so what that reading is a percentage <i>of</i>
	/// is not known, and the threshold is not transcribed rather than guessed at. A machine taken at
	/// the start of a mission is undamaged and gets the nominal line either way.</para>
	/// </summary>
	private void AnnouncePowerUp(TimeSpan elapsed) {
		if (_powerUpAnnounceIn == TimeSpan.MinValue) {
			return;
		}

		_powerUpAnnounceIn -= elapsed;
		if (_powerUpAnnounceIn > TimeSpan.Zero) {
			return;
		}

		_powerUpAnnounceIn = TimeSpan.MinValue;
		Voice?.Post(SystemMessages.PowerUpNominal);
	}

	/// <summary>
	/// Silences everything without tearing the device down — for a pause or a lost window. Speech is
	/// cut rather than remembered: <see cref="Resume"/> can only restart a clip from its beginning,
	/// and half a sentence twice is worse than none.
	/// </summary>
	public void Suspend() {
		Voice?.Stop();
		_director?.SuspendAll();
	}

	/// <summary>Puts back what <see cref="Suspend"/> stopped.</summary>
	public void Resume() => _director?.ResumeAll();

	/// <inheritdoc />
	public void Dispose() => _director?.Dispose();
}
