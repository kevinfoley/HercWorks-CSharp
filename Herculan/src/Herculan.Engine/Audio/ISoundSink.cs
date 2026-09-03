using Herculan.Engine.Numerics;

namespace Herculan.Engine.Audio;

/// <summary>
/// What the simulation talks to when something makes a noise. <see cref="SoundDirector"/> is the
/// real implementation; a world with none attached simply runs silent.
///
/// <para>The simulation holds no audio state of its own and never asks a question back — it says
/// "this happened, here", exactly as DBSIM's own call sites do, and every rule about whether that is
/// audible, how loud, and where in the stereo field belongs to the director. That keeps
/// <see cref="Sim.SimWorld"/> tickable by a headless test or a mission editor with no device
/// present, which is the same split docs/engine/planning.md draws for rendering.</para>
/// </summary>
public interface ISoundSink {
	/// <summary>
	/// Plays a catalog id with no position — a cockpit tone, which is in the player's ears wherever
	/// the machine is. <c>Sound_Play</c> (<c>0046272c</c>).
	/// </summary>
	void Play(int id);

	/// <summary>
	/// Plays a catalog id at a world point. <c>Sound_PlayAt</c> (<c>004627dc</c>).
	/// </summary>
	void PlayAt(int id, Vec3i position);

	/// <summary>Stops a catalog id. <c>Sound_Stop</c> (<c>004629c0</c>).</summary>
	void Stop(int id);

	/// <summary>
	/// Moves a sound that is already running. <c>Sound_UpdatePosition</c> (<c>00462878</c>) — what
	/// the looping engine hum and the flamer use to follow their machine.
	/// </summary>
	void MoveTo(int id, Vec3i position);

	/// <summary>
	/// Sets a running sound's playback rate, 16.16 with <c>0x10000</c> as its recorded pitch —
	/// <c>Sound_SetPitch</c> (<c>00463010</c>). The flyer's engine hum is the one thing that varies
	/// it continuously; the cockpit power-up sets it once.
	/// </summary>
	void SetPitch(int id, int rate);

	/// <summary>
	/// Posts one of the cockpit computer's messages by its flat <c>SYSTEM.STR</c> id — the vtable
	/// call the original makes on the cockpit's message port, <c>view+0x20b</c>. See
	/// <see cref="Content.SystemMessages"/> for the ids and <see cref="ComputerVoice"/> for what
	/// becomes of one.
	/// </summary>
	void Say(int messageId);

	/// <summary>
	/// Withdraws a posted message that has not been said yet — <c>FUN_00435ac8</c>, which the radar
	/// toggle uses on both of its own lines before posting the one it wants.
	/// </summary>
	void Unsay(int messageId);
}
