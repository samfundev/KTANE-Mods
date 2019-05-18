using UnityEngine;

public abstract class ModuleLogging : ModuleTweak
{
	private static int idCounter = 1;
	private readonly int moduleID;
	private readonly string logName;

	protected ModuleLogging(BombComponent bombComponent, string logName) : base(bombComponent)
	{
		moduleID = idCounter++;
		this.logName = logName;
	}

	internal void Log(string message)
	{
		Debug.Log($"[{logName} #{moduleID}] {message}");
	}
}