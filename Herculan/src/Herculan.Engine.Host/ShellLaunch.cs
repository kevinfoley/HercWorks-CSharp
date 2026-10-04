namespace Herculan.Engine.Host;

/// <summary>
/// A mission handed over by the shell — <c>Rock &amp; Roll &gt;</c> or a training launch — or by
/// <see cref="MissionFileLaunch"/>: the <c>script.dat</c> the handoff was written beside,
/// and the folder the simulator's own settings are read from and written back to.
/// </summary>
sealed record ShellLaunch(string ScriptPath, string DataDirectory);
