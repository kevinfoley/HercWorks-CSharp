using HercWorks.Core.Data.File.Dts;
using HercWorks.Core.Data.File.Dts.Anim;
using HercWorks.Core.Data.File.Dts.Part;
using HercWorks.Core.Data.File.Dyn;
using HercWorks.Core.Data.Struct;

namespace Herculan.Engine.Sim.Anim;

/// <summary>One keyframe transform: an euler rotation and a translation, both in model space.</summary>
/// <remarks>
/// The <c>ANAnimListTransform</c> record as DBSIM reads it — 12 bytes, rotation first. Which of the
/// shape's parts a given entry belongs to is stated by the sequence, not by the entry.
/// </remarks>
public readonly record struct AnimTransform(
	short RotationX, short RotationY, short RotationZ, short X, short Y, short Z) {

	/// <summary>
	/// <c>FUN_00492600</c> — one keyframe transform blended toward another, <paramref name="q10"/>
	/// of the way (0x400 == all the way).
	///
	/// <para>Rotation is blended along the shortest arc, the same wrap
	/// <see cref="Numerics.BinaryAngle.Delta"/> performs, so a node crossing 0 turns the short way
	/// instead of unwinding the long way round. Translation is a plain lerp. Both truncate, and the
	/// arithmetic is the original's: a Q10 multiply and a signed shift, not a float mix.</para>
	/// </summary>
	public static AnimTransform Blend(in AnimTransform from, in AnimTransform to, int q10) => new(
		BlendAngle(from.RotationX, to.RotationX, q10),
		BlendAngle(from.RotationY, to.RotationY, q10),
		BlendAngle(from.RotationZ, to.RotationZ, q10),
		BlendLinear(from.X, to.X, q10),
		BlendLinear(from.Y, to.Y, q10),
		BlendLinear(from.Z, to.Z, q10));

	private static short BlendAngle(short from, short to, int q10) {
		uint start = (ushort)from;
		uint delta = (ushort)to;

		// Fold the difference into (-0x8000, 0x8000] before scaling it, exactly as the original does
		// — the bias-then-subtract keeps the whole thing in unsigned arithmetic.
		if (delta <= start) {
			delta += 0x10000;
		}

		delta -= start;
		if (delta > 0x7fff) {
			delta -= 0x10000;
		}

		unchecked {
			return (short)((short)((int)(delta * (uint)q10) >> 10) + from);
		}
	}

	// The difference is truncated to a short before it is scaled, which is the original's own
	// arithmetic rather than an accident of it: the operands are model-space shorts and the
	// subtraction is a 16-bit one.
	private static short BlendLinear(short from, short to, int q10) {
		unchecked {
			return (short)(((short)(to - from) * q10 >> 10) + from);
		}
	}
}

/// <summary>
/// A hop from one sequence into another, offered by a specific frame of the source sequence.
/// </summary>
/// <param name="Duration">How long the transition frame itself lasts, in animation ticks.</param>
/// <param name="DestinationSequence">Sequence this transition leads to.</param>
/// <param name="DestinationFrame">Frame within that sequence playback resumes at.</param>
/// <param name="TransformIndex">
/// The root motion covered while the transition plays — an index into
/// <see cref="ShapeAnimation.Transforms"/>. This is <c>ANAnimListTransition.GroundMovement</c>,
/// which despite the name is not a flag and is unrelated to <see cref="AnimSequence.GroundMovement"/>.
/// </param>
public readonly record struct AnimTransition(
	short Duration, short DestinationSequence, short DestinationFrame, short TransformIndex);

/// <summary>
/// One animation sequence: a ring of frames, each with a duration and a transform per animated part.
/// </summary>
public sealed class AnimSequence {
	internal AnimSequence(short[] frameDurations, short[] transitionCounts, short[] firstTransitions,
			short[] transformIndices, short[] partIds, bool groundMovement) {
		FrameDurations = frameDurations;
		TransitionCounts = transitionCounts;
		FirstTransitions = firstTransitions;
		TransformIndices = transformIndices;
		PartIds = partIds;
		GroundMovement = groundMovement;
	}

	/// <summary>Each frame's length in animation ticks.</summary>
	public short[] FrameDurations { get; }

	/// <summary>How many transitions each frame offers.</summary>
	public short[] TransitionCounts { get; }

	/// <summary>Index of each frame's first transition in <see cref="ShapeAnimation.Transitions"/>.</summary>
	public short[] FirstTransitions { get; }

	/// <summary>Transform index per (frame, part), row-major by frame. Part 0 is the root.</summary>
	public short[] TransformIndices { get; }

	/// <summary>
	/// Which node each column of <see cref="TransformIndices"/> belongs to, as a transform id — the
	/// same id space <see cref="ShapeAnimation.ParentTransform"/> and
	/// <see cref="ShapeAnimation.DefaultTransforms"/> are indexed by. Column 0 is the root, which is
	/// why <see cref="GroundMovement"/> only ever concerns that one.
	/// </summary>
	public short[] PartIds { get; }

	/// <summary>How many parts each frame carries a transform for.</summary>
	public int PartCount => PartIds.Length;

	/// <summary>
	/// The column <paramref name="transformId"/> occupies in this sequence's frames, or -1 when the
	/// sequence does not animate that node — in which case its default transform stands.
	/// </summary>
	public int ColumnOf(int transformId) {
		for (int i = 0; i < PartIds.Length; i++) {
			if (PartIds[i] == transformId) {
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	/// Whether part 0's transform is root motion — a ground displacement to be applied to the
	/// object — rather than a pose. Walk, run and turn-in-place set it; torso and arm sequences
	/// do not.
	/// </summary>
	public bool GroundMovement { get; }

	/// <summary>Number of frames.</summary>
	public int FrameCount => FrameDurations.Length;

	/// <summary>
	/// The frame after <paramref name="frame"/>, wrapping (<c>FUN_004786d8</c>).
	/// </summary>
	/// <remarks>
	/// DBSIM reaches this through a per-class vtable slot, and both implementations found in the
	/// binary wrap. A non-wrapping pair may exist for plain (non-cyclic) sequences; it was not
	/// located, and it would not change locomotion either way — walk, run and turn-in-place are
	/// cyclic, and the stop sequences are left by transition rather than by running off the end.
	/// </remarks>
	public int NextFrame(int frame) => frame < FrameCount - 1 ? frame + 1 : 0;

	/// <summary>The frame before <paramref name="frame"/>, wrapping (<c>FUN_004786f8</c>).</summary>
	public int PreviousFrame(int frame) => frame != 0 ? frame - 1 : FrameCount - 1;
}

/// <summary>
/// A shape's animation data, flattened out of the parsed <c>ANAnimList</c> into the arrays
/// <see cref="AnimationThread"/> indexes every tick.
/// </summary>
public sealed class ShapeAnimation {
	/// <summary>Recursion guard for the shape tree walk; real mech trees are three levels deep.</summary>
	private const int MaxPartTreeDepth = 16;

	private ShapeAnimation(AnimSequence[] sequences, AnimTransform[] transforms,
			AnimTransition[] transitions, short[] defaultTransforms, int[] parentTransform,
			IReadOnlyDictionary<int, int> partTransformIds) {
		Sequences = sequences;
		Transforms = transforms;
		Transitions = transitions;
		DefaultTransforms = defaultTransforms;
		ParentTransform = parentTransform;
		PartTransformIds = partTransformIds;
	}

	/// <summary>The shape's sequences, indexed by the sequence ids the mech type record names.</summary>
	public AnimSequence[] Sequences { get; }

	/// <summary>The shared keyframe transform pool every sequence indexes into.</summary>
	public AnimTransform[] Transforms { get; }

	/// <summary>The shared transition pool frames index into.</summary>
	public AnimTransition[] Transitions { get; }

	/// <summary>
	/// The rest transform of every node, as an index into <see cref="Transforms"/>, keyed by
	/// transform id. What a node holds on any frame whose sequence does not animate it.
	/// </summary>
	public short[] DefaultTransforms { get; }

	/// <summary>
	/// Each node's parent transform id, or -1 for a root — the <c>ANAnimList</c>'s relation pairs
	/// flattened into a lookup. A node's pose in shape space is its own transform composed up this
	/// chain.
	/// </summary>
	public int[] ParentTransform { get; }

	/// <summary>
	/// Shape part id to transform id, for the parts the shape tree names. The mech type record
	/// identifies its camera node by <i>part</i> id (<c>CameraBoneId</c>), and the original resolves
	/// it through the shape's own find-by-id before indexing the transform table with the part's
	/// <c>TSBasePart.Transform</c>; this is that resolution, done once at load.
	/// </summary>
	public IReadOnlyDictionary<int, int> PartTransformIds { get; }

	/// <summary>
	/// The transform id a shape part id names, or -1 when the shape has no such part. Parts with no
	/// transform of their own are not listed.
	/// </summary>
	public int TransformIdOfPart(int partId) =>
		PartTransformIds.TryGetValue(partId, out int id) ? id : -1;

	/// <summary>
	/// Whether <paramref name="sequenceId"/> names a playable sequence — in range, and with frames
	/// and parts to play. A sequence with neither is not something a thread can hold a cursor into.
	/// </summary>
	public bool HasSequence(int sequenceId) =>
		sequenceId >= 0 && sequenceId < Sequences.Length
		&& Sequences[sequenceId].FrameCount > 0 && Sequences[sequenceId].PartCount > 0;

	/// <summary>
	/// Flattens the first <c>ANAnimList</c> found in a parsed model, or returns null when it has
	/// none. A mech <c>.DTS</c> carries exactly one, on its root shape.
	/// </summary>
	public static ShapeAnimation? FromModel(DynamixThreeSpaceModel? model) {
		if (model?.Meshes is not { } roots || FirstAnimList(roots) is not { } list) {
			return null;
		}

		var partTransforms = new Dictionary<int, int>();
		foreach (var root in roots) {
			CollectPartTransforms(root, partTransforms, 0);
		}

		return From(list, partTransforms);
	}

	/// <summary>Flattens a parsed <c>ANAnimList</c>. Null when it is missing any of its three pools.</summary>
	/// <param name="list">The parsed animation list.</param>
	/// <param name="partTransformIds">
	/// The shape tree's part-id to transform-id map, when the caller has the tree to build it from.
	/// Without it <see cref="TransformIdOfPart"/> resolves nothing, which costs only the camera node.
	/// </param>
	public static ShapeAnimation? From(ANAnimList list,
			IReadOnlyDictionary<int, int>? partTransformIds = null) {
		if (list.Sequences == null || list.Transforms == null) {
			return null;
		}

		var transforms = new AnimTransform[list.Transforms.Length];
		for (int i = 0; i < transforms.Length; i++) {
			var entry = list.Transforms[i];
			var rotation = entry.Rotation;
			var translation = entry.Translation;
			transforms[i] = new AnimTransform(
				rotation?.X ?? 0, rotation?.Y ?? 0, rotation?.Z ?? 0,
				translation?.X ?? 0, translation?.Y ?? 0, translation?.Z ?? 0);
		}

		var sourceTransitions = list.Transitions ?? Array.Empty<ANAnimListTransition>();
		var transitions = new AnimTransition[sourceTransitions.Length];
		for (int i = 0; i < transitions.Length; i++) {
			var entry = sourceTransitions[i];
			transitions[i] = new AnimTransition(
				entry.Tick, entry.DestSequence, entry.DestFrame, entry.GroundMovement);
		}

		var sequences = new AnimSequence[list.Sequences.Length];
		for (int i = 0; i < sequences.Length; i++) {
			sequences[i] = Flatten(list.Sequences[i] as ANSequence);
		}

		var defaults = list.DefaultTransforms ?? Array.Empty<short>();

		// Relations are (parent, child) transform-id pairs, with -1 standing for "no parent"; the
		// same table DtsMeshBuilder walks to place a group's geometry.
		var parents = new int[defaults.Length];
		Array.Fill(parents, -1);
		foreach (var relation in list.Relations ?? Array.Empty<Vec2Short>()) {
			if (relation.Y >= 0 && relation.Y < parents.Length) {
				parents[relation.Y] = relation.X;
			}
		}

		return new ShapeAnimation(sequences, transforms, transitions, defaults, parents,
			partTransformIds ?? new Dictionary<int, int>());
	}

	/// <summary>
	/// Walks a shape tree recording each part's transform id under its part id. The first part to
	/// claim an id keeps it: a mech's geometry groups all carry part id 0, and only the named nodes —
	/// the ones the type record's bone fields point at — are unique.
	/// </summary>
	private static void CollectPartTransforms(TSObject? node, Dictionary<int, int> map, int depth) {
		if (node == null || depth > MaxPartTreeDepth) {
			return;
		}

		if (node is TSBasePart { Transform: >= 0 } part) {
			_ = map.TryAdd(part.IdNumber, part.Transform);
		}

		if (node is TSPartList { Parts: { } children }) {
			foreach (var child in children) {
				CollectPartTransforms(child, map, depth + 1);
			}
		}
	}

	private static AnimSequence Flatten(ANSequence? sequence) {
		var frames = sequence?.Frames ?? Array.Empty<ANSequenceFrame>();
		var durations = new short[frames.Length];
		var transitionCounts = new short[frames.Length];
		var firstTransitions = new short[frames.Length];

		for (int i = 0; i < frames.Length; i++) {
			durations[i] = frames[i].Tick;
			transitionCounts[i] = frames[i].NumTransitions;
			firstTransitions[i] = frames[i].FirstTransition;
		}

		return new AnimSequence(
			durations, transitionCounts, firstTransitions,
			sequence?.TransformIndices ?? Array.Empty<short>(),
			sequence?.PartIds ?? Array.Empty<short>(),
			sequence?.GroundMovement != 0);
	}

	private static ANAnimList? FirstAnimList(IEnumerable<TSObject> chunks) {
		foreach (var chunk in chunks) {
			switch (chunk) {
				case ANShape { AnimationList: { } list }:
					return list;
				case TSPartList { Parts: { } parts }: {
					if (FirstAnimList(parts) is { } nested) {
						return nested;
					}
					break;
				}
			}
		}

		return null;
	}
}
