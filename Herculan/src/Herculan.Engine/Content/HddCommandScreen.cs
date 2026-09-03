using Herculan.Engine.Numerics;
using Herculan.Engine.Render;
using Herculan.Engine.Sim;

namespace Herculan.Engine.Content;

/// <summary>
/// The command display's own state and the actions its buttons and keys perform —
/// <c>HddCommandScreen</c> (<c>FUN_0044c264</c>) minus the drawing, which is
/// <see cref="Overlay2DRenderer"/>'s.
/// </summary>
/// <remarks>
/// <para>The screen is a small state machine and the manual describes it as one: pick a pilot, pick
/// an order, pick a target on the map if the order wants one, then XMIT or CANCEL. Every transition
/// below is one of the original's — <c>FUN_0044da70</c> selects a pilot, <c>FUN_0044d9cc</c> an
/// order, <c>FUN_0044d6b8</c> resolves a map click into a unit or a gridpoint, and
/// <c>FUN_0044dbe8</c> tears the whole thing back down.</para>
///
/// <para><b>What a transmitted order does.</b> In the original it reaches the squadmate's AI, which
/// then reports back through the comm box's OBJECTIVE: line (<c>FUN_0041bac8</c> reads the machine's
/// current AI state and indexes <c>STRINGS0.STR</c> group 40 with it). There is no squad AI here
/// yet, so a transmitted order is recorded against the slot and shown on that line directly — see
/// <see cref="Objective"/>. That is this engine's stand-in, not the original's behaviour: the
/// original's line reports what the pilot is <i>doing</i>, which can differ from what they were last
/// told.</para>
/// </remarks>
public sealed class HddCommandScreen {
	/// <summary>
	/// <c>STRINGS0.STR</c> group holding the four message-row prompts: <c>SELECT PILOT</c>,
	/// <c>SELECT COMMAND</c>, <c>DESIGNATE LOCATION</c>, <c>DESIGNATE TARGET</c>. The screen picks
	/// between them with <c>FUN_0044dc44</c>'s own three-way test.
	/// </summary>
	public const int PromptGroup = 32;

	/// <summary>Prompt shown with no pilot selected.</summary>
	public const int SelectPilotPrompt = 0;

	/// <summary>With a pilot selected and no order armed.</summary>
	public const int SelectCommandPrompt = 1;

	/// <summary>With an order armed that wants a gridpoint.</summary>
	public const int DesignateLocationPrompt = 2;

	/// <summary>With one armed that wants a unit.</summary>
	public const int DesignateTargetPrompt = 3;

	/// <summary>
	/// Coarse ticks between blink toggles — <c>FUN_0044c960</c>'s <c>+ 0x1e</c> against
	/// <c>Time_GetCoarseTicks</c>, whose unit is 16 ms.
	/// </summary>
	public const int BlinkTicks = 30;

	/// <summary>
	/// What a squadmate's OBJECTIVE: line reads before anything has been transmitted: group 40's
	/// <c>FORM UP</c>, the state a squad that spawned on the player's own formation point is in. The
	/// original takes this from the machine's AI rather than assuming it.
	/// </summary>
	public const int DefaultObjective = 3;

	private readonly int[] _objectives = new int[HddLayout.PilotSlotCount];
	private double _blinkTicks;

	/// <param name="view">The map camera, sized to the herc's own map viewport.</param>
	/// <param name="raster">The mission's terrain raster, or null when the zone could not supply one.</param>
	/// <param name="squad">
	/// The squadmates the three comm boxes address, in slot order — the original's
	/// <c>DAT_004d044c</c>. Fewer than three leaves the remaining boxes empty.
	/// </param>
	public HddCommandScreen(HddMapView view, HddMapRaster? raster, IReadOnlyList<SimObject> squad) {
		View = view ?? throw new ArgumentNullException(nameof(view));
		Raster = raster;
		Squad = squad ?? Array.Empty<SimObject>();
		Array.Fill(_objectives, DefaultObjective);
	}

	/// <summary>The map camera.</summary>
	public HddMapView View { get; }

	/// <summary>The terrain raster under the map.</summary>
	public HddMapRaster? Raster { get; }

	/// <summary>The squadmates the three comm boxes address.</summary>
	public IReadOnlyList<SimObject> Squad { get; }

	/// <summary>Which comm box is selected, or -1.</summary>
	public int SelectedPilot { get; private set; } = -1;

	/// <summary>Which order is armed, or null.</summary>
	public HddOrder? SelectedOrder { get; private set; }

	/// <summary>The object an armed order has been pointed at, or null.</summary>
	public SimObject? ChosenUnit { get; private set; }

	/// <summary>The gridpoint an armed order has been pointed at, or null.</summary>
	public Vec3i? ChosenPoint { get; private set; }

	/// <summary>Whether the blink is in its lit half — <c>DAT_0049d6ad</c>.</summary>
	public bool Blink { get; private set; } = true;

	/// <summary>
	/// Whether the armed order still wants something picked on the map before XMIT will take it — the
	/// state the message row's DESIGNATE prompts announce.
	/// </summary>
	public bool AwaitingPick =>
		SelectedOrder is { } order
			&& ((HddCommandState.NeedsUnit(order) && ChosenUnit == null)
				|| (HddCommandState.NeedsPoint(order) && ChosenPoint == null));

	/// <summary>The order last transmitted to <paramref name="slot"/>, as a group-40 index.</summary>
	public int Objective(int slot) =>
		slot >= 0 && slot < _objectives.Length ? _objectives[slot] : DefaultObjective;

	/// <summary>Advances the blink. Wall time, not simulation time, exactly as the original's is.</summary>
	public void Update(TimeSpan elapsed) {
		_blinkTicks += elapsed.TotalSeconds / Audio.GameAudio.CoarseTickSeconds;
		while (_blinkTicks >= BlinkTicks) {
			_blinkTicks -= BlinkTicks;
			Blink = !Blink;
		}
	}

	/// <summary>
	/// Selects a comm box, or -1 for none — <c>FUN_0044da70</c>. Selecting a different pilot drops
	/// whatever order was armed for the previous one, which is what that function's first branch does
	/// before it moves the selection.
	/// </summary>
	public void SelectPilot(int slot) {
		if (slot >= 0 && (slot >= Squad.Count || Squad[slot].Neutralised)) {
			return;
		}

		if (slot != SelectedPilot) {
			ClearOrder();
		}

		SelectedPilot = slot;
	}

	/// <summary>
	/// Arms an order, or clears it — <c>FUN_0044d9cc</c>. The four that want something picked on the
	/// map put the screen into its designate state; the other four are ready to transmit at once.
	/// </summary>
	public void SelectOrder(HddOrder? order) {
		if (order != null && SelectedPilot < 0) {
			return;
		}

		SelectedOrder = order;
		ChosenUnit = null;
		ChosenPoint = null;
	}

	/// <summary>
	/// Steps the armed order one place along the list, wrapping — the <c>,&lt;</c> and <c>.&gt;</c>
	/// keys (<c>FUN_0044ee60</c> and <c>FUN_0044ee20</c>). Does nothing with no order armed, which is
	/// both functions' own guard.
	/// </summary>
	public void StepOrder(int delta) {
		if (SelectedOrder is not { } order) {
			return;
		}

		int next = ((int)order + delta) % HddLayout.OrderCount;
		SelectOrder((HddOrder)(next < 0 ? next + HddLayout.OrderCount : next));
	}

	/// <summary>
	/// Resolves a click in the map viewport, at <paramref name="artX"/>/<paramref name="artY"/> device
	/// pixels inside it. With an order armed that wants a target this is the pick
	/// (<c>FUN_0044d6b8</c>); otherwise it is a pilot selection, since clicking a squadmate's marker
	/// is one of the three ways the manual gives for choosing who to talk to (<c>FUN_0044d804</c>).
	/// </summary>
	/// <param name="objects">Everything live, for the unit hit test.</param>
	/// <returns>Whether the click resolved to anything.</returns>
	public bool ClickMap(float artX, float artY, IEnumerable<SimObject> objects) {
		ArgumentNullException.ThrowIfNull(objects);
		int worldX = View.ToWorldX(artX);
		int worldY = View.ToWorldY(artY);
		var hit = HitTest(worldX, worldY, objects);

		if (SelectedOrder is { } order && SelectedPilot >= 0) {
			// ATTACK ENEMY takes a hostile, DEFEND POSITION a friendly; either one falling on nothing
			// eligible drops through to the gridpoint, which is what the original does with it too.
			if (HddCommandState.NeedsUnit(order) && hit != null
				&& (hit.Side == World.MissionSide.Cybrid) == (order == HddOrder.AttackEnemy)) {
				ChosenUnit = hit;
				ChosenPoint = null;
				return true;
			}

			if (HddCommandState.NeedsUnit(order) || HddCommandState.NeedsPoint(order)) {
				ChosenUnit = null;
				ChosenPoint = new Vec3i(worldX, worldY, 0);
				return true;
			}
		}

		if (hit != null && HddMap.SquadSlotOf(Squad, hit) is var slot && slot >= 0) {
			SelectPilot(slot);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Sends the armed order — the XMIT button and [X]. Refuses, as the original does, when there is
	/// no pilot, no order, or the order still wants something picked; the caller plays the accepted or
	/// rejected blip on the result.
	/// </summary>
	public bool Transmit() {
		if (SelectedPilot < 0 || SelectedOrder is not { } order
			|| (HddCommandState.NeedsUnit(order) && ChosenUnit == null)
			|| (HddCommandState.NeedsPoint(order) && ChosenPoint == null)) {
			return false;
		}

		_objectives[SelectedPilot] = ObjectiveFor(order);
		ClearOrder();
		SelectPilot(-1);
		return true;
	}

	/// <summary>
	/// Drops the whole transmission — CANCEL and [Backspace], which is <c>FUN_0044dbe8</c>: the order,
	/// the pick and the pilot selection all go together.
	/// </summary>
	public void Cancel() {
		ClearOrder();
		SelectedPilot = -1;
	}

	/// <summary>This frame's snapshot for the renderer.</summary>
	/// <param name="player">The machine the player is flying.</param>
	/// <param name="objects">Everything live, in the order the marker list wants it.</param>
	/// <param name="route">The player squad's route, for the numbered waypoint markers.</param>
	/// <param name="strings">For the message row and the comm boxes' text.</param>
	public HddCommandState Build(SimObject? player, IReadOnlyList<SimObject> objects,
			IReadOnlyList<Vec3i>? route, SimStringTable? strings) {
		ArgumentNullException.ThrowIfNull(objects);
		if (player != null) {
			View.Follow(player.Position);
		}

		var markers = HddMap.Markers(objects, player, route, Squad);
		int chosen = -1;
		if (ChosenUnit != null) {
			for (int i = 0; i < markers.Count; i++) {
				if (markers[i].WorldX == ChosenUnit.Position.X && markers[i].WorldY == ChosenUnit.Position.Y) {
					chosen = i;
					break;
				}
			}
		}

		var pilots = new HddPilotSlot[HddLayout.PilotSlotCount];
		for (int i = 0; i < pilots.Length; i++) {
			if (i >= Squad.Count) {
				pilots[i] = new HddPilotSlot(false, string.Empty, 0, DefaultObjective);
				continue;
			}

			var mate = Squad[i];
			pilots[i] = new HddPilotSlot(
				Occupied: true,
				Name: PilotName(i, mate),
				ConditionIndex: ConditionOf(mate),
				OrderIndex: _objectives[i]);
		}

		return new HddCommandState(View, Raster, markers, pilots, SelectedPilot, SelectedOrder,
			chosen, ChosenPoint, strings?.Text(PromptGroup, PromptIndex), Blink);
	}

	/// <summary>
	/// Which of the four prompts the message row shows — <c>FUN_0044dc44</c>'s own derivation, which
	/// asks only whether a pilot is selected and, if an order is armed, which of the two picks it
	/// wants.
	/// </summary>
	public int PromptIndex {
		get {
			if (SelectedPilot < 0) {
				return SelectPilotPrompt;
			}

			if (SelectedOrder is not { } order) {
				return SelectCommandPrompt;
			}

			return order == HddOrder.AttackEnemy ? DesignateTargetPrompt
				: HddCommandState.NeedsPoint(order) || order == HddOrder.DefendPosition
					? DesignateLocationPrompt
					: SelectCommandPrompt;
		}
	}

	/// <summary>
	/// The group-40 line a transmitted order leaves on the comm box. Engine-side: see this class's
	/// remarks for why the original does not need such a mapping. SCAN FOR HOSTILES and EMCON change
	/// only the pilot's radar, so they leave the objective alone.
	/// </summary>
	private int ObjectiveFor(HddOrder order) => order switch {
		HddOrder.Disengage => 5,
		HddOrder.AttackEnemy => 0,
		HddOrder.DefendPosition => 4,
		HddOrder.PatrolGridpoint => 2,
		HddOrder.GotoGridpoint => 1,
		HddOrder.JoinOnMe => 3,
		_ => _objectives[SelectedPilot],
	};

	private void ClearOrder() {
		SelectedOrder = null;
		ChosenUnit = null;
		ChosenPoint = null;
	}

	/// <summary>
	/// The nearest live object within the marker's own click radius of a world point —
	/// <c>FUN_0044d860</c>, which converts the click to world units and tests each object against a
	/// radius scaled from <c>5 &lt;&lt; XCoordShift</c> device pixels.
	/// </summary>
	private SimObject? HitTest(int worldX, int worldY, IEnumerable<SimObject> objects) {
		int radius = HddMap.UnitMarkerSize / 2 * View.Scale >> HddMapView.ScaleShift;
		SimObject? best = null;
		long bestDistance = long.MaxValue;

		foreach (var subject in objects) {
			if (subject.Removed || subject.AwaitingDeployment || subject.TargetClass == TargetClass.None) {
				continue;
			}

			long dx = subject.Position.X - (long)worldX;
			long dy = subject.Position.Y - (long)worldY;
			long distance = dx * dx + dy * dy;
			if (Math.Abs(dx) <= radius && Math.Abs(dy) <= radius && distance < bestDistance) {
				bestDistance = distance;
				best = subject;
			}
		}

		return best;
	}

	/// <summary>
	/// The name across a comm box. The original stores a pointer per gauge, filled from the pilot
	/// roster the player's save carries — which this engine does not read, since that file is
	/// VSHELL's. The machine's own type name stands in, so the box has something true to draw.
	/// </summary>
	private static string PilotName(int slot, SimObject mate) =>
		mate is MechObject mech ? mech.Name.ToUpperInvariant() : $"WING {slot + 1}";

	/// <summary>
	/// <c>HddGauge_ConditionIndex</c>: the mean of the machine's structural readings, bucketed into
	/// group 28's five conditions by the same bands the MFD status screen uses.
	/// </summary>
	private static int ConditionOf(SimObject mate) {
		if (mate.Neutralised) {
			return 4;
		}

		if (mate is not MechObject { Damage: { } damage }) {
			return 0;
		}

		int total = 0;
		int count = Math.Min(HddLayout.StructuralRowCount, damage.Count);
		for (int i = 0; i < count; i++) {
			total += damage.DamagePercent(i);
		}

		return MfdStatusSubject.ConditionFromDamage(count == 0 ? 0 : total / count);
	}
}
