using Herculan.Engine.Content;

namespace Herculan.Engine.Audio;

/// <summary>
/// The cockpit computer's speaking voice — <c>SYSTEM.STR</c>'s messages, read aloud from the
/// <c>CVM_nnnn.WAV</c> clips in the voice archive. See docs/formats/audio.md, "The computer's
/// messages".
///
/// <para><b>It is not part of the sound catalog and does not go through
/// <see cref="SoundDirector"/>.</b> Speech has its own channel pool in the original, opened at
/// priority <c>0xff</c> so that no catalog effect can ever evict a line mid-sentence, and it is
/// never positional — the computer is in the player's ears, not in the world. That separation is
/// kept here: this type holds its own backend voices and its own enable flag.</para>
///
/// <para><b>Only the audio half of the original's message port is here</b>: <see cref="Post"/>
/// speaks a line, <see cref="Cancel"/> withdraws one that has not started, and a line already
/// speaking runs to its end before the next begins. The port's display timing and preemption rules
/// are not decompiled, so nothing here invents them — and the on-screen text does not exist
/// yet.</para>
///
/// <para>The preferences screen offers OFF / TEXT ONLY / VOICE ONLY / TEXT-VOICE per message
/// channel; <see cref="Enabled"/> is the voice half of that for this channel.</para>
/// </summary>
public sealed class ComputerVoice {
	/// <summary>The archive folder the clips live in — the same label in all three language archives.</summary>
	public const string ResourceFolder = "SIMVOICE";

	/// <summary>
	/// How a clip number becomes a resource name. The original builds it from the literal
	/// <c>CVM_0000</c> template it keeps beside the pilot and base-command ones.
	/// </summary>
	public const string ClipNameFormat = "CVM_{0:0000}.WAV";

	private readonly GameContent _content;
	private readonly IAudioBackend _backend;
	private readonly Dictionary<int, int> _voices = new();
	private readonly Queue<int> _pending = new();

	private int _speaking = -1;

	/// <param name="content">The mounted archives. Must include a voice archive for anything to sound.</param>
	/// <param name="messages">The parsed <c>SYSTEM.STR</c>, or null to run mute.</param>
	/// <param name="backend">The mixer the clips are opened on.</param>
	public ComputerVoice(GameContent content, SystemMessages? messages, IAudioBackend backend) {
		_content = content;
		_backend = backend;
		Messages = messages;
	}

	/// <summary>The message set, or null when <c>SYSTEM.STR</c> would not load.</summary>
	public SystemMessages? Messages { get; }

	/// <summary>
	/// The voice half of the COMPUTER MESSAGE preference. False stops new messages being spoken and
	/// discards what is queued; it does not cut off a line already running.
	/// </summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Speech gain, 0 to 1. Separate from the effects volume, as the original's channel is.</summary>
	public float Volume {
		get => _volume;
		set {
			_volume = Math.Clamp(value, 0f, 1f);
			if (_speaking >= 0) {
				_backend.SetGain(_speaking, _volume);
			}
		}
	}

	private float _volume = 1f;

	/// <summary>The message currently being spoken, or null when the channel is silent.</summary>
	public int? Speaking { get; private set; }

	/// <summary>Whether a clip for <paramref name="messageId"/> could be found and opened.</summary>
	public bool CanSpeak(int messageId) => Voice(messageId) >= 0;

	/// <summary>
	/// Says message <paramref name="messageId"/> — the flat <c>SYSTEM.STR</c> id the original's own
	/// call sites pass. Speaks it now if the channel is free, otherwise queues it behind what is
	/// running.
	/// </summary>
	public void Post(int messageId) {
		if (!Enabled || Voice(messageId) < 0) {
			return;
		}

		if (Speaking == null) {
			Begin(messageId);
			return;
		}

		if (!_pending.Contains(messageId)) {
			_pending.Enqueue(messageId);
		}
	}

	/// <summary>
	/// Withdraws a message that has been posted but has not started —
	/// <c>Mech_ToggleRadarMode</c> drops both radar lines before posting the one it wants, so that
	/// flipping the mode twice quickly does not queue up a contradiction.
	///
	/// <para>A message already speaking is left to finish, which is what the original's own delete
	/// does: it takes a message out of the queue, not out of the mixer.</para>
	/// </summary>
	public void Cancel(int messageId) {
		if (!_pending.Contains(messageId)) {
			return;
		}

		var kept = _pending.Where(id => id != messageId).ToArray();
		_pending.Clear();
		foreach (int id in kept) {
			_pending.Enqueue(id);
		}
	}

	/// <summary>
	/// Services the channel: starts the next queued message once the current one has finished
	/// speaking. Call once a frame.
	/// </summary>
	public void Update() {
		if (Speaking == null) {
			if (_pending.Count > 0) {
				Begin(_pending.Dequeue());
			}

			return;
		}

		if (_speaking >= 0 && _backend.IsPlaying(_speaking)) {
			return;
		}

		Speaking = null;
		_speaking = -1;

		if (_pending.Count > 0) {
			Begin(_pending.Dequeue());
		}
	}

	/// <summary>Silences the channel and forgets what was queued.</summary>
	public void Stop() {
		if (_speaking >= 0) {
			_backend.Stop(_speaking);
		}

		_speaking = -1;
		Speaking = null;
		_pending.Clear();
	}

	private void Begin(int messageId) {
		int voice = Voice(messageId);
		if (voice < 0) {
			return;
		}

		_speaking = voice;
		Speaking = messageId;
		_backend.SetGain(voice, _volume);
		_backend.SetPan(voice, 0f);
		_backend.Play(voice);
	}

	/// <summary>
	/// The backend voice for a message's clip, opened on first use and kept.
	///
	/// <para>The original keeps five slots and evicts the least recently used, because 66 clips of
	/// 8-bit PCM did not fit its cache budget. Holding each one that is actually asked for costs a
	/// few hundred kilobytes over a mission and removes the eviction path entirely, which is the same
	/// trade <see cref="SoundBank"/> makes for the effect samples.</para>
	/// </summary>
	/// <returns>The voice handle, or -1 when the message, its clip or a device is missing.</returns>
	private int Voice(int messageId) {
		if (_voices.TryGetValue(messageId, out int cached)) {
			return cached;
		}

		int voice = -1;

		if (Messages?[messageId] is { VoiceClip: > 0 } message) {
			string name = string.Format(ClipNameFormat, message.VoiceClip);
			if (_content.Read(ResourceFolder, name) is { } bytes
					&& WaveSample.Decode(bytes) is { } sample) {
				voice = _backend.CreateVoice(sample);
				if (voice >= 0) {
					_backend.SetLooping(voice, false);
					_backend.SetPitch(voice, 1f);
				}
			}
		}

		_voices[messageId] = voice;
		return voice;
	}
}
