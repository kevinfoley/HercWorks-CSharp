namespace Herculan.Engine.Input;

/// <summary>
/// What one of the four axis rows of a <c>prefs.cfg</c> controls block is set to. The stored byte is
/// the value directly, and it also indexes that row's three-word set in <c>CTL_ALRT.STR</c>.
///
/// <para><b>The three values mean the same thing on every row</b>, which is why one enum covers all
/// four and both machines: 0 is unassigned, 1 points the control at the <i>movement</i> pair of game
/// axes and 2 at the <i>turret</i> pair. The words differ because a given control only contributes
/// half of a pair — the RUDDER row's single axis reads DIRECTION under 1 and TURRET ROTATION under 2,
/// while the JOYSTICK row's two axes reach both halves at once and so read MOVEMENT and TURRET. A
/// RAZOR renames all of them again (PITCH / ROLL, THROTTLE / YAW …) over the same
/// numbers.</para>
/// </summary>
public enum JoystickAxisAssignment : byte {
	/// <summary>NO JOYSTICK / NO THROTTLE / NO RUDDER / NO HAT — the control is read and discarded.</summary>
	Unassigned = 0,

	/// <summary>The movement pair: game axis 0 (steering) and axis 1 (throttle).</summary>
	Movement = 1,

	/// <summary>The turret pair: game axis 2 (torso twist) and axis 3 (torso pitch).</summary>
	Turret = 2,
}
