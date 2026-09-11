using Herculan.Engine.Numerics;

namespace Herculan.Engine.Content;

/// <summary>
/// The pilot and squad channel — the cockpit view's second message port, at <c>view+0x207</c>. The
/// computer's own is <see cref="MessagePort"/> at <c>view+0x20b</c>; the two are instances of the
/// same queue with different show behaviour, which in the original is a vtable and here is two
/// classes. See docs/formats/audio.md, "The port".
///
/// <para>What differs from the computer's channel:</para>
/// <list type="bullet">
/// <item>a post names a <b>speaker</b> — the squadmate saying it — because the filename, the portrait
/// and the comm box that plays it all come off that machine's own pilot;</item>
/// <item>the message set is per speaker: <c>str\PILOT&lt;bank&gt;.STR</c>, chosen by the pilot's voice
/// bank, and an id there can have several recordings, one of which is rolled for
/// (<see cref="SquadMessages.Pick"/>);</item>
/// <item>it runs <b>begin</b> and <b>end</b> callbacks. Only this channel registers any
/// (<c>MessagePort_AddBeginCallback</c>, <c>004355a8</c>): <c>CommBox_OnMessageBegin</c>
/// (<c>0044b4ec</c>) starts that speaker's clip and portrait, and <c>CommBox_OnMessageEnd</c>
/// (<c>0044b5c0</c>) closes it. That pair is what <see cref="SquadCommChannel"/> is.</item>
/// </list>
///
/// <para>The lifecycle itself is the shared base's, <c>MessagePort_Tick</c> (<c>00435610</c>), on the
/// same four latches and the same four timings out of the entry's attribute bytes — see
/// <see cref="MessagePort"/>, which documents them against the computer's file.</para>
/// </summary>
public sealed class SquadMessagePort {
	/// <inheritdoc cref="MessagePort.Capacity"/>
	public const int Capacity = MessagePort.Capacity;

	/// <inheritdoc cref="MessagePort.TicksPerTimingUnit"/>
	public const int TicksPerTimingUnit = MessagePort.TicksPerTimingUnit;

	private readonly Func<int, SquadMessages?> _catalogs;
	private readonly SimRandom? _random;
	private readonly List<Queued> _queue = new(Capacity);

	private long _now;
	private Queued? _current;
	private bool _activated;
	private bool _ready;
	private bool _cancel;
	private bool _shown;

	/// <param name="catalogs">
	/// The message set for a comm-box slot — the pilot's voice bank resolved to a
	/// <c>PILOT&lt;bank&gt;.STR</c>. Null for a slot with nobody in it.
	/// </param>
	/// <param name="random">The generator the variant roll draws on — pass the world's.</param>
	public SquadMessagePort(Func<int, SquadMessages?> catalogs, SimRandom? random = null) {
		_catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
		_random = random;
	}

	/// <summary>Whether the channel is drawn and spoken. The preferences screen's PILOT MESSAGE setting.</summary>
	public MessageChannelMode Mode { get; set; } = MessageChannelMode.TextAndVoice;

	/// <inheritdoc cref="MessagePort.PilotDisabled"/>
	public bool PilotDisabled { get; set; }

	/// <summary>Messages waiting, the one that is up included.</summary>
	public int QueueLength => _queue.Count;

	/// <summary>The line currently up, or null when the channel is quiet.</summary>
	public Queued? Current => _shown ? _current : null;

	/// <summary>
	/// Raised as a message comes due, before it goes up — the port's begin callbacks. The comm box
	/// starts its static, its clip and its portrait here.
	/// </summary>
	public event Action<Queued>? Begin;

	/// <summary>Raised as it comes down, on the matching end hook.</summary>
	public event Action<Queued>? End;

	/// <summary>
	/// Posts what <paramref name="slot"/>'s squadmate has to say — <c>Ai_PostSquadMessage</c>
	/// (<c>00420a98</c>) through the port's vtable slot 0. The id names a line in that pilot's own
	/// message set, and the recording is rolled among that id's variants here rather than at play
	/// time, so the portrait script and the clip agree.
	/// </summary>
	public void Post(int messageId, int slot, object? speaker = null) {
		if (_catalogs(slot)?.Pick(messageId, _random) is not { } message) {
			return;
		}

		var attributes = message.Attributes;
		byte Attribute(int index) => index < attributes.Length ? attributes[index] : (byte)0;

		var entry = new Queued {
			Id = message.Id,
			Variant = message.Variant,
			Slot = slot,
			Speaker = speaker,
			Text = message.Text,
			Priority = Attribute(SystemMessages.PriorityAttribute),
			MinTime = Attribute(SystemMessages.MinDisplayAttribute) * TicksPerTimingUnit,
			MaxTime = Attribute(SystemMessages.MaxDisplayAttribute) * TicksPerTimingUnit,
			MinWait = Attribute(SystemMessages.MinDelayAttribute) * TicksPerTimingUnit + _now,
			MaxWait = Attribute(SystemMessages.MaxDelayAttribute) * TicksPerTimingUnit + _now,
		};

		if (_queue.Count == Capacity) {
			_queue.RemoveAt(_queue.Count - 1);
		}

		int at = _queue.FindIndex(queued => entry.Priority < queued.Priority);
		_queue.Insert(at < 0 ? _queue.Count : at, entry);
	}

	/// <summary>
	/// Runs the port for one frame at <paramref name="coarseTicks"/> — <c>MessagePort_Tick</c>. Call
	/// once a frame, before <see cref="SquadCommChannel"/>'s own update.
	/// </summary>
	public void Update(long coarseTicks) {
		_now = coarseTicks;

		// The paint side's latch, which is what puts a frame between a message coming due and going up.
		if (_activated) {
			_ready = true;
		}

		for (int i = _queue.Count - 1; i > 0; i--) {
			if (_queue[i].MaxWait < _now) {
				_queue.RemoveAt(i);
			}
		}

		_current ??= _queue.Count > 0 ? _queue[0] : null;

		if (_current is not { } message) {
			return;
		}

		if (!_ready && !_cancel) {
			if (message.MaxWait < _now) {
				// Never got its turn inside its own window.
				Dequeue();
				_shown = false;
			} else if (message.MinWait < _now && !_activated) {
				_activated = true;
				_ready = false;
				Begin?.Invoke(message);
			}
		} else if (!_activated || _cancel) {
			bool overrun = _shown && message.MaxTime < _now;
			bool yield = _shown && message.MinTime < _now && _queue.Count > 1;

			if (_cancel || overrun || yield || PilotDisabled) {
				if (_shown) {
					End?.Invoke(message);
				}

				_shown = false;
				Dequeue();
				_activated = false;
			}
		} else {
			// The show. The pilot channel's own (PilotMessagePort_Speak, 00435d9c) draws a wrapped
			// line into the cockpit's pilot box; the voice is not started here but by the comm box,
			// which opens the clip beside its portrait script so the two stay in step.
			_shown = true;
			_activated = false;
			message.MinTime += _now;
			message.MaxTime += _now;
		}
	}

	/// <summary>Takes everything down and forgets it — leaving the cockpit, or the mission ending.</summary>
	public void Clear() {
		_queue.Clear();
		_current = null;
		_activated = false;
		_ready = false;
		_cancel = false;
		_shown = false;
	}

	private void Dequeue() {
		if (_queue.Count > 0) {
			_queue.RemoveAt(0);
		}

		_current = null;
		_cancel = false;
		_ready = false;
	}

	/// <summary>One queued line: which pilot says it, which recording, and the four timings.</summary>
	public sealed class Queued {
		/// <summary>The <c>PILOT&lt;bank&gt;.STR</c> message id — attribute byte 0.</summary>
		public int Id;

		/// <summary>Which recording of it — attribute byte 1, and the last digit of both filenames.</summary>
		public int Variant;

		/// <summary>Which comm box is speaking.</summary>
		public int Slot;

		/// <summary>Record <c>+0x02</c>: the machine the line is about, which here is the speaker.</summary>
		public object? Speaker;

		/// <summary>The line itself.</summary>
		public string Text = string.Empty;

		/// <inheritdoc cref="SystemMessages.PriorityAttribute"/>
		public int Priority;

		/// <summary>Record <c>+0x1c</c>.</summary>
		public long MinTime;

		/// <summary>Record <c>+0x20</c>.</summary>
		public long MaxTime;

		/// <summary>Record <c>+0x24</c>.</summary>
		public long MinWait;

		/// <summary>Record <c>+0x28</c>.</summary>
		public long MaxWait;
	}
}
