namespace Herculan.Engine.Sim;

// Footfall detection — Mech_PlaceLegsOnGround (004195c8), the part of it that matters to anything
// ported so far. See docs/simulation/mech-locomotion.md.
public sealed partial class MechObject {
	/// <summary>The gait the original's own leg thresholds are indexed by.</summary>
	private const int GaitWalking = 0;
	private const int GaitReversing = 1;
	private const int GaitRunning = 2;

	/// <summary>Per-leg arming flags — <c>mech+0x2b4</c>, one byte a leg.</summary>
	private bool[] _legArmed = System.Array.Empty<bool>();

	/// <summary>
	/// How many times a foot has planted. The host watches it for the edge rather than a flag, so a
	/// tick that plants two feet at once is still two footfalls.
	/// </summary>
	public int Footfalls { get; private set; }

	/// <summary>
	/// <c>Mech_PlaceLegsOnGround</c> (<c>004195c8</c>) — runs at the end of every movement tick and
	/// works out, per leg, whether the foot has just planted.
	///
	/// <para>The test is on the leg node's <b>fore/aft</b> position in the machine's own frame, not
	/// its height: a foot swings forward, past the gait's arming figure
	/// (<see cref="MechTypeRecord.FootfallRearm"/>), and plants as it comes back through the trigger
	/// (<see cref="MechTypeRecord.FootfallTrigger"/>). Reversing flips both comparisons, since the
	/// foot travels the other way, and it has its own pair of figures.</para>
	///
	/// <para>The original does two more things here that are not ported: it copies each leg node's
	/// world position onto a per-leg child object (the dust/debris emitters), and it plays sound
	/// <c>0x1d</c> on the plant, for any machine within 15000 units of the camera. What <i>is</i>
	/// ported is the third: the player's own footfalls kick the cockpit view (see
	/// <c>CockpitViewKick</c>), which is the visible half.</para>
	/// </summary>
	private void PlaceLegsOnGround() {
		if (Thread is not { } thread || Animation is not { } animation) {
			return;
		}

		var type = Type;
		int legs = type.LegCount;
		if (legs <= 0) {
			return;
		}

		if (_legArmed.Length < legs) {
			System.Array.Resize(ref _legArmed, legs);
		}

		// The gait is read off the sequence playback is heading for, falling back to the one running.
		int sequence = thread.TargetSequence >= 0 ? thread.TargetSequence : thread.Sequence;

		int gait;
		if (sequence == type.WalkSequence || sequence == type.TurnInPlaceSequence
			|| sequence == type.StopForwardSequence || sequence == type.StopReverseSequence) {
			gait = AnimRate < 1 ? GaitReversing : GaitWalking;
		} else if (sequence == type.RunSequence) {
			gait = GaitRunning;
		} else {
			// Any other sequence — a death or a jump — has no walk cycle to take steps from.
			return;
		}

		// Reversing runs both comparisons the other way round; the original carries it as a flag
		// rather than negating the figures, because the figures are already signed for the gait.
		bool forward = gait != GaitReversing;
		short trigger = type.FootfallTrigger(gait);
		short rearm = type.FootfallRearm(gait);

		for (int leg = 0; leg < legs; leg++) {
			if (type.LegKind(leg) != 0) {
				continue;
			}

			int node = animation.TransformIdOfPart(type.LegPartId(leg));
			if (node < 0) {
				continue;
			}

			int position = NodeTransform(node).Y;

			if (forward ? rearm < position : position < rearm) {
				_legArmed[leg] = true;
			}

			if (_legArmed[leg] && (forward ? position < trigger : trigger < position)) {
				_legArmed[leg] = false;
				Footfalls++;
			}
		}
	}
}
