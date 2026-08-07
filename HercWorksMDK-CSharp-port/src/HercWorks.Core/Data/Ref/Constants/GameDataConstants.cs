namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>
/// Ported from org.hercworks.core.data.ref.constants.GameDataConstants. The Java original is an
/// otherwise-empty singleton shell (no fields/constants defined yet) — ported as-is.
/// </summary>
public sealed class GameDataConstants {
	private static GameDataConstants? _instance;

	private GameDataConstants() { }

	public static GameDataConstants GetInstance() => _instance ??= new GameDataConstants();
}
