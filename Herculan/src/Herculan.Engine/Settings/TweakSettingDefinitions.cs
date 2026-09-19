namespace Herculan.Engine.Settings;

/// <summary>
/// The catalog of tweak settings: one static, shared <see cref="TweakSettingDefinition{T}"/>
/// per setting. This is schema only (ID, category, default, hidden) — the values a player has chosen
/// live in a <see cref="TweakSettings"/> instance instead, keyed by the definition.
/// </summary>
public static class TweakSettingDefinitions {
	#region COSMETIC
	/// <summary>
	/// Fix issues where HERC or weapon stats were displayed in the UI with incorrect
	/// values in vanilla.
	/// <para>CATEGORY: Cosmetic</para>
	/// <para>Changes:</para>
	/// <list type="bullet">
	/// <item><description>Display correct speed for Outlaw in VSHELL (100 kph, not 80 kph)</description></item>
	/// <item><description>Display correct hardpoints for Raptor II in VSHELL (5, not 4)</description></item>
	/// </list>
	///
	/// </summary>
	public static readonly TweakSettingDefinition<bool> ShowCorrectStats = new("tweak.show_correct_stats", TweakCategory.Cosmetic, false);

	/// <summary>
	/// Show accurate movement speed on HUD. Retail shows an inaccurate reading when the
	/// player's HERC is in walking stride.
	/// </summary>
	public static readonly TweakSettingDefinition<bool> ShowAccurateSpeed = new("tweak.show_accurate_speed", TweakCategory.Cosmetic, false);

	/// <summary>
	/// On the MFD, show target distance in meters on the MFD F5 TARGET screen. Retail
	/// shows distance in engine units on this screen only.
	/// </summary>
	public static readonly TweakSettingDefinition<bool> ShowTargetDistanceInMeters = new("tweak.target_distance_meters", TweakCategory.Cosmetic, false);

	/// <summary>
	/// A hit on the right nacelle of the Razor will draw the impact effect at the
	/// correct side instead of at the left nacelle.
	/// </summary>
	public static readonly TweakSettingDefinition<bool> FixNacelleImpactEffectPosition = new("tweak.fix_nacelle_impact_position", TweakCategory.Cosmetic, false);

	/// <summary>
	/// Fix new sound effects overwriting the volume and position of previous sound
	/// effects of the same type.
	/// </summary>
	public static readonly TweakSettingDefinition<bool> PreserveSoundPosition = new("tweak.preserve_sound_position", TweakCategory.Cosmetic, false);

	/// <summary>
	/// When a friendly unit is destroyed by the player while targeted by the player,
	/// play "Friendly Target Destroyed". Retail plays "Enemy Target Destroyed".
	/// </summary>
	public static readonly TweakSettingDefinition<bool> FriendlyTargetDestroyedMessage = new("tweak.friendly_target_destroyed", TweakCategory.Cosmetic, false);

	#endregion

	#region FUNCTIONAL
	/// <summary>
	/// Enable smoother turret movement when aiming with a joystick or other analog
	/// input. Retail rounds the turret rotation values, causing noticeable stutter
	/// when aiming at slow speeds. Irrelevant if controlling the turret with a
	/// keyboard.
	/// </summary>
	public static readonly TweakSettingDefinition<bool> SmootherTurretMovement = new("tweak.smoother_turret_movement", TweakCategory.Functional, false);

	#endregion

	/// <summary>Every defined <c>bool</c> tweak setting, keyed by ID for <see cref="TweakSettings"/> save/load.</summary>
	public static readonly IReadOnlyList<TweakSettingDefinition<bool>> All = new[] {
		ShowCorrectStats, ShowAccurateSpeed, ShowTargetDistanceInMeters, FixNacelleImpactEffectPosition,
		PreserveSoundPosition, FriendlyTargetDestroyedMessage, SmootherTurretMovement,
	};
}
