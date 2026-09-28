using Herculan.Engine.Sim;
using Herculan.Engine.Terrain;

namespace Herculan.Engine.Render;

/// <summary>
/// Which view the chain is in — <c>DAT_004d2572</c>.
/// </summary>
public enum ExternalViewMode : short {
	/// <summary>The free camera, which only the developer keys reach.</summary>
	Free = 1,

	/// <summary>The cockpit. Where every mission starts.</summary>
	Cockpit = 2,

	/// <summary>The outside view, [V] and the joystick's OUTSIDE VIEW.</summary>
	Outside = 3,

	/// <summary>The chase view, the joystick's CHASE VIEW.</summary>
	Chase = 4,
}

/// <summary>
/// DBSIM's chain of views: the view it is in (<c>DAT_004d2572</c>) and the one it is going to
/// (<c>DAT_004d259e</c>), the cameras behind them (<c>ViewObjectPtr</c> and the per-squad cameras at
/// <c>DAT_004d270c</c>), whether the controls drive the camera or the machine, and the cockpit view
/// manager's step to and from its view 4. The rules, and what each command does from each view, are
/// docs/simulation/external-views.md.
///
/// <para>The host calls the command methods as the dispatcher's cases would run, and
/// <see cref="Advance"/> once per simulation tick in the original's order: the camera's steer from
/// <c>Sim_PollPlayerInput</c>, the chain's <c>FUN_0045de14</c>, the placement in
/// <c>FUN_004011a0</c>, then the view manager's half of <c>Sim_EndFrame</c>.</para>
///
/// <para><b>The view manager is only the part of it the chain reaches</b>: the one command latch, the
/// armed flag <c>FUN_0045de14</c> waits on, and the two-frame cooldown, for commands 2 and 3 alone.
/// The heads-down pan and the glances keep their own state in <see cref="CockpitPan"/> and
/// <see cref="CockpitGlance"/>; a switch to the external view never waits behind one of them here,
/// where retail's single latch would make it.</para>
/// </summary>
public sealed class ExternalViewChain {
	/// <summary>
	/// The chase view needs the frame counter <c>DAT_004d25ff</c> past <c>0x31</c> — the frames
	/// <see cref="PlayerTrail"/> needs to fill.
	/// </summary>
	public const int ChaseFrameGate = PlayerTrail.Length;

	/// <summary><c>DAT_004d25a4</c>'s reload: the caption is drawn on this many frames.</summary>
	public const int CaptionFrames = 2;

	/// <summary><c>Sim_InitMissionSession</c>'s view camera, which the chain starts on.</summary>
	private readonly ViewCamera _main = new();

	/// <summary><c>DAT_004d270c</c>: one camera per member of the player's squad.</summary>
	private readonly ViewCamera[] _squadCameras;

	/// <summary><c>DAT_004d27d8</c>: the camera used once every squad camera is taken.</summary>
	private readonly ViewCamera _spare = new();

	private readonly SimObject _player;

	private ExternalViewMode _pending = ExternalViewMode.Cockpit;
	private bool _cameraControlPreferred = true;
	private SimObject _chosen;
	private bool _followChosen;
	private int _captionFrames;

	private ViewCommand _latched = ViewCommand.None;
	private bool _armed;
	private int _cooldown;

	/// <summary>
	/// Sets up as <c>Sim_InitMissionSession</c> does: one camera per member of the player's group,
	/// and the main camera attached to the player's eye.
	/// </summary>
	public ExternalViewChain(SimObject player) {
		_player = player;
		_chosen = player;
		Watched = player;

		int squadSize = player.Group?.Members.Count ?? 0;
		_squadCameras = new ViewCamera[squadSize];
		for (int i = 0; i < squadSize; i++) {
			_squadCameras[i] = new ViewCamera();
		}

		Camera = _main;
		_main.AttachTo(player);
		_main.SetMode(ViewCameraMode.Attached);
	}

	/// <summary><c>DAT_004d2572</c>.</summary>
	public ExternalViewMode Mode { get; private set; } = ExternalViewMode.Cockpit;

	/// <summary><c>ViewObjectPtr</c>: the camera the view is drawn from.</summary>
	public ViewCamera Camera { get; private set; }

	/// <summary><c>DAT_004d2708</c>: the object the view was last moved to.</summary>
	public SimObject Watched { get; private set; }

	/// <summary>The player's recorded path, which the chase view follows.</summary>
	public PlayerTrail Trail { get; } = new();

	/// <summary>
	/// <c>InputDrivesCamera</c> (<c>004d2574</c>): the controls steer the camera, and the machine gets
	/// none of the axes and does not fire.
	/// </summary>
	public bool InputDrivesCamera { get; private set; }

	/// <summary>Whether the cockpit view manager is in its view 4, the one with no cockpit.</summary>
	public bool ExternalViewUp { get; private set; }

	/// <summary><c>DAT_004d25ff</c>: frames run since the mission started.</summary>
	public int FrameCount { get; private set; }

	/// <summary>
	/// What the caption along the bottom of the external view last showed: the object named after
	/// VIEW and whether CONTROL read CAMERA. Null until it is first drawn. <c>FUN_0045e1ec</c> only
	/// paints on the frames <c>DAT_004d25a4</c> counts, and what it painted stays up until it paints
	/// again.
	/// </summary>
	public (SimObject Viewed, bool CameraControl)? Caption { get; private set; }

	/// <summary>
	/// [V] (scancode <c>0x2f</c>) and the joystick's OUTSIDE VIEW: out to the outside view from the
	/// cockpit, and back to the cockpit from anything else. [V] alone refuses while the camera is
	/// locked.
	/// </summary>
	public void ToggleOutside(bool fromKeyboard) {
		if (fromKeyboard && Camera.Locked) {
			return;
		}

		_followChosen = false;
		if (Mode == ExternalViewMode.Cockpit) {
			Queue(ViewCommand.Enter);
			_pending = ExternalViewMode.Outside;
		} else {
			Queue(ViewCommand.Leave);
			_pending = ExternalViewMode.Cockpit;
		}
	}

	/// <summary>
	/// The joystick's CHASE VIEW: out to the chase view from the cockpit, and back from the chase or
	/// free view. From the outside view it only sets the pending view, which nothing applies until
	/// another command steps the view manager — so there it does nothing.
	/// </summary>
	public void ToggleChase() {
		if (FrameCount < ChaseFrameGate) {
			return;
		}

		_followChosen = false;
		switch (Mode) {
			case ExternalViewMode.Cockpit:
				Queue(ViewCommand.Enter);
				_pending = ExternalViewMode.Chase;
				break;
			case ExternalViewMode.Outside:
				_pending = ExternalViewMode.Chase;
				break;
			default:
				Queue(ViewCommand.Leave);
				_pending = ExternalViewMode.Cockpit;
				break;
		}
	}

	/// <summary>
	/// [Esc] (scancode 1), which reaches the dispatcher only while the cockpit's widgets are off —
	/// that is, from the external view.
	/// </summary>
	public void Escape() {
		if (!Camera.Locked) {
			Queue(ViewCommand.Leave);
			_pending = ExternalViewMode.Cockpit;
		}
	}

	/// <summary>
	/// [Enter] or [Tab] from the external view (<c>FUN_0045fd2c</c>): the controls go between the camera
	/// and the machine. It only changes the preference; <see cref="Advance"/> hands the controls over,
	/// and only while the outside view is on the player.
	/// </summary>
	public void ToggleCameraControl() {
		if (!ExternalViewUp) {
			return;
		}

		_cameraControlPreferred = !_cameraControlPreferred;
		if (Camera.Target == _player) {
			_captionFrames = CaptionFrames;
		}
	}

	/// <summary>
	/// [N] (scancode <c>0x31</c>) in the outside view: on to the next object in the live list that is
	/// in the player's group and not below height zero, the player included.
	/// </summary>
	public void NextSquadmate(IReadOnlyList<SimObject> objects) {
		if (Camera.Locked || !ExternalViewUp || Mode != ExternalViewMode.Outside) {
			return;
		}

		var next = Walk(objects, Watched, forward: true,
			candidate => candidate.Position.Z >= 0 && candidate.Group == _player.Group);
		if (next != null && next != Watched) {
			View(next);
			Camera.SetMode(ViewCameraMode.Orbit);
			_captionFrames = CaptionFrames;
			_chosen = next;
		}
	}

	/// <summary>
	/// The developer keys' [Ctrl+N] and [Ctrl+P]: on to the next or previous object in the live list
	/// that is not below -99000, into the outside view if the cockpit is up, with the controls on the
	/// camera.
	/// </summary>
	public void DeveloperCycle(IReadOnlyList<SimObject> objects, bool forward) {
		if (Camera.Locked) {
			return;
		}

		var next = Walk(objects, Watched, forward, candidate => candidate.Position.Z >= DeveloperFloor);
		if (next == null || next == Watched) {
			return;
		}

		_chosen = next;
		if (Mode == ExternalViewMode.Cockpit) {
			Queue(ViewCommand.Enter);
			_pending = ExternalViewMode.Outside;
		} else {
			View(next);
			_captionFrames = CaptionFrames;
		}

		InputDrivesCamera = true;
	}

	/// <summary>
	/// The developer keys' [Ctrl+F]: [V]'s step, except that the outside view goes on to the free
	/// camera and the cockpit it comes back to is the chosen object's.
	/// </summary>
	public void DeveloperFollow() {
		if (Camera.Locked) {
			return;
		}

		_followChosen = true;
		switch (Mode) {
			case ExternalViewMode.Free:
				Queue(ViewCommand.Leave);
				_pending = ExternalViewMode.Cockpit;
				break;
			case ExternalViewMode.Cockpit:
				Queue(ViewCommand.Enter);
				_pending = ExternalViewMode.Outside;
				break;
			case ExternalViewMode.Outside:
				Camera.SetMode(ViewCameraMode.Free);
				Mode = ExternalViewMode.Free;
				break;
		}
	}

	/// <summary>The developer keys' [Ctrl+T]: the controls onto the camera or back.</summary>
	public void DeveloperHandOff() => InputDrivesCamera = !InputDrivesCamera;

	/// <summary>
	/// One tick of the chain. <paramref name="steer"/>, <paramref name="throttle"/> and
	/// <paramref name="trigger"/> are the tick's steering and throttle axes and trigger, which reach
	/// the camera only while <paramref name="controlsDriveCamera"/>; otherwise it is steered with
	/// nothing, which lets its rates run down.
	/// </summary>
	public void Advance(short steer, short throttle, bool trigger, bool controlsDriveCamera,
			HeightGrid? terrain) {
		if (controlsDriveCamera) {
			Camera.Steer(steer, throttle, trigger);
		} else {
			Camera.Steer(0, 0, false);
		}

		ApplyPending();

		Trail.Record(_player);
		Camera.Update(terrain, Trail);

		StepViewManager();
		FrameCount++;
	}

	// FUN_0045de14: while the view manager is mid-step, move to the pending view; otherwise keep
	// the controls where the preference puts them, while the outside view is on the player.
	private void ApplyPending() {
		if (!_armed) {
			if (Mode == ExternalViewMode.Outside && Watched == _player
					&& InputDrivesCamera != _cameraControlPreferred) {
				InputDrivesCamera = _cameraControlPreferred;
			}
		} else if (_pending != Mode) {
			Mode = _pending;
			switch (Mode) {
				case ExternalViewMode.Free:
					Camera.SetMode(ViewCameraMode.Free);
					InputDrivesCamera = _cameraControlPreferred;
					break;
				case ExternalViewMode.Cockpit:
					View(_followChosen ? _chosen : _player);
					Camera.SetMode(ViewCameraMode.Attached);
					InputDrivesCamera = false;
					break;
				case ExternalViewMode.Outside:
					View(_chosen);
					Camera.SetMode(ViewCameraMode.Orbit);
					_captionFrames = CaptionFrames;
					break;
				case ExternalViewMode.Chase:
					View(_player);
					Camera.SetMode(ViewCameraMode.Chase);
					_captionFrames = CaptionFrames;
					InputDrivesCamera = false;
					break;
			}
		}

		if (_captionFrames != 0) {
			Caption = (Camera.Target ?? _player, InputDrivesCamera);
			_captionFrames--;
		}
	}

	// FUN_0045df18: onto the camera that already views the object, or onto a free squad camera
	// seeded from the current one and attached afresh, or failing both onto the spare the same way.
	private void View(SimObject target) {
		ViewCamera? next = null;
		foreach (var camera in _squadCameras) {
			if (camera.Target == target) {
				next = camera;
				next.SetMode(ViewCameraMode.Orbit);
				break;
			}

			if (camera.Target == null) {
				next = Seeded(camera, target);
				break;
			}
		}

		Camera = next ?? Seeded(_spare, target);
		Watched = target;
		InputDrivesCamera = target != _player || _cameraControlPreferred;

		ViewCamera Seeded(ViewCamera camera, SimObject subject) {
			camera.CopyFrom(Camera);
			camera.AttachTo(subject);
			return camera;
		}
	}

	// CockpitView_QueueViewCommand's commands 2 and 3: one latch, taken only from the view each
	// leaves.
	private void Queue(ViewCommand command) {
		if (_latched != ViewCommand.None) {
			return;
		}

		if (command == ViewCommand.Enter ? !ExternalViewUp : ExternalViewUp) {
			_latched = command;
		}
	}

	// Sim_EndFrame's two calls, in its order. CockpitView_StepViewTransition finishes a command the
	// last frame armed, and CockpitView_ProcessViewCommand arms a latched one unless cooling down —
	// so the chain moves on the tick after the key, and the view with it.
	private void StepViewManager() {
		if (_armed) {
			ExternalViewUp = _latched == ViewCommand.Enter;
			_latched = ViewCommand.None;
			_armed = false;
			_cooldown = 2;
		}

		if (_cooldown > 0) {
			_cooldown--;
			return;
		}

		if (_latched != ViewCommand.None) {
			_armed = true;
		}
	}

	// FUN_00411e40 / FUN_00411e60 round the live list, skipping what fails the test. The walk stops
	// at the start if nothing passes, where the original's would go round for ever.
	private static SimObject? Walk(IReadOnlyList<SimObject> objects, SimObject from, bool forward,
			Func<SimObject, bool> accept) {
		int count = objects.Count;
		int start = from.ListIndex;
		if (count == 0 || start < 0) {
			return null;
		}

		int step = forward ? 1 : count - 1;
		for (int i = (start + step) % count; ; i = (i + step) % count) {
			var candidate = objects[i];
			if (!candidate.Removed && accept(candidate)) {
				return candidate;
			}

			if (i == start) {
				return null;
			}
		}
	}

	/// <summary>Below this height [Ctrl+N]/[Ctrl+P] step past an object — where a destroyed flyer is sent.</summary>
	private const int DeveloperFloor = -99000;

	private enum ViewCommand {
		None,
		Enter,
		Leave,
	}
}
