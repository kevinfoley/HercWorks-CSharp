using Herculan.Engine.Audio;
using Herculan.Engine.Numerics;

namespace Herculan.Engine.Sim;

// Footfall detection — Mech_PlaceLegsOnGround (004195c8), the part of it that matters to anything
// ported so far. See docs/simulation/mech-locomotion.md.
public sealed partial class MechObject {
	/// <summary>
	/// How near the camera a machine has to be for its footsteps to be played at all — the
	/// original's own test, made before it calls Sound_PlayAt rather than left to the catalog row's
	/// cutoff. <c>foot2</c>'s own range would allow 20480, so this is the tighter of the two.
	/// </summary>
	public const int FootfallAudibleRange = 15000;

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
	/// <para>The plant does three things. It kicks the player's cockpit view (see
	/// <c>CockpitViewKick</c>, driven off <see cref="Footfalls"/>); it plays sound <c>0x1d</c> for
	/// any machine within <see cref="FootfallAudibleRange"/> of the camera; and — the one part still
	/// unported — it copies each leg node's world position onto a per-leg child object, the
	/// dust/debris emitter.</para>
	/// </summary>
	private void PlaceLegsOnGround(SimWorld world) {
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

				// The original tests the distance to the camera itself before it plays anything,
				// rather than leaving the cutoff to foot2's own catalog range — which at 20480 is
				// the looser of the two.
				if (Position.ApproxDistanceTo(world.ListenerPosition) > FootfallAudibleRange) {
					continue;
				}

				// At the foot, not at the machine's origin — the original passes the leg node's own
				// world position, so a walking HERC's steps pan with the leg that took them.
				var foot = NodeTransform(node);
				world.Sounds?.PlayAt(SoundId.Footfall, new Vec3i(foot.X, foot.Y, foot.Z));
			}
		}
	}
}
