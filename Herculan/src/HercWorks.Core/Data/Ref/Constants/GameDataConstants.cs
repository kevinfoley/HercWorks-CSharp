namespace HercWorks.Core.Data.Ref.Constants;

/// <summary>An empty singleton: it defines no constants.</summary>
public sealed class GameDataConstants {
	private static GameDataConstants? _instance;

	private GameDataConstants() { }

	public static GameDataConstants GetInstance() => _instance ??= new GameDataConstants();
}
