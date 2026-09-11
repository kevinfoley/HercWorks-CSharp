using Herculan.Engine.Content;

namespace Herculan.Engine.Audio;

/// <summary>
/// A squadmate's speaking voice — the <c>P&lt;bank&gt;_nnnnn.WAV</c> clips in the voice archive, the
/// other half of the five-slot speech pool <see cref="ComputerVoice"/> draws on. See
/// docs/formats/audio.md, "Speech and the comm portraits".
///
/// <para><b>The comm box opens the clip, not the port.</b> <c>CommBox_BeginMessage</c>
/// (<c>0044afc8</c>) builds the <c>.WAV</c> name and the matching <c>.SNC</c> portrait script
/// together and hands both to <c>Voice_Acquire</c> (<c>00462a98</c>), which opens the voice at
/// priority <c>0xff</c> so no catalog effect can evict it. Keeping the two in one call is what makes
/// the portrait's mouth agree with the recording.</para>
///
/// <para>The name is <c>"P" + voiceBank + "_" + 2-digit message id + 3-digit variant</c>, and the
/// three language archives all carry the <c>SIMVOICE</c> folder label, so which language is heard is
/// which archive is mounted.</para>
/// </summary>
public sealed class SquadVoice {
	/// <summary>The archive folder the clips live in — shared with <see cref="ComputerVoice"/>.</summary>
	public const string ResourceFolder = ComputerVoice.ResourceFolder;

	private readonly GameContent _content;
	private readonly IAudioBackend _backend;
	private readonly Dictionary<string, int> _samples = new(StringComparer.OrdinalIgnoreCase);

	private int _speaking = -1;

	/// <param name="content">The mounted archives. Must include a voice archive for anything to sound.</param>
	/// <param name="backend">The mixer the clips are opened on.</param>
	public SquadVoice(GameContent content, IAudioBackend backend) {
		_content = content ?? throw new ArgumentNullException(nameof(content));
		_backend = backend ?? throw new ArgumentNullException(nameof(backend));
	}

	/// <summary>Speech gain, 0 to 1.</summary>
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

	/// <summary>The clip currently playing, or null when the channel is silent.</summary>
	public string? Speaking { get; private set; }

	/// <summary>
	/// The resource name for one recording — <c>CommBox_BeginMessage</c>'s own template with the
	/// message id patched into its first two digits and the variant into its last.
	/// </summary>
	public static string ClipName(int voiceBank, int messageId, int variant) =>
		$"P{voiceBank}_{messageId:00}{variant:000}.WAV";

	/// <summary>Whether the archive actually holds that recording.</summary>
	public bool Has(int voiceBank, int messageId, int variant) =>
		_content.Contains(ResourceFolder, ClipName(voiceBank, messageId, variant));

	/// <summary>
	/// Plays one recording, cutting off anything the channel was already saying. The original's pool
	/// would let two squadmates overlap, but the port only ever has one line up at a time.
	/// </summary>
	public void Speak(int voiceBank, int messageId, int variant) {
		string name = ClipName(voiceBank, messageId, variant);
		int sample = Sample(name);
		if (sample < 0) {
			return;
		}

		Stop();
		_speaking = _backend.Start(sample, _volume, 0f, 1f, looping: false);
		Speaking = _speaking >= 0 ? name : null;
	}

	/// <summary>Notices when the running clip has finished. Call once a frame.</summary>
	public void Update() {
		if (Speaking != null && (_speaking < 0 || !_backend.IsPlaying(_speaking))) {
			Speaking = null;
			_speaking = -1;
		}
	}

	/// <summary>Silences the channel.</summary>
	public void Stop() {
		if (_speaking >= 0) {
			_backend.Stop(_speaking);
		}

		_speaking = -1;
		Speaking = null;
	}

	/// <summary>
	/// The backend sample for a clip, decoded on first use and kept — the same trade
	/// <see cref="ComputerVoice"/> makes against the original's five-slot LRU.
	/// </summary>
	private int Sample(string name) {
		if (_samples.TryGetValue(name, out int cached)) {
			return cached;
		}

		int id = -1;
		if (_content.Read(ResourceFolder, name) is { } bytes && WaveSample.Decode(bytes) is { } sample) {
			id = _backend.CreateSample(sample);
		}

		_samples[name] = id;
		return id;
	}
}
