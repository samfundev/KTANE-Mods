using System.Collections.Generic;
using Assets.Scripts.Missions;

public enum Mode
{
    Normal,
    Time,
    //VS,
    Zen
}

static class Modes
{
	public static ModConfig<ModeSettings> modConfig = new ModConfig<ModeSettings>("ModeSettings");
	public static ModeSettings settings = modConfig.Settings;
	public static float Multiplier = settings.TimeModeStartingMultiplier;
    public static float timePenalty = 1.0f;
    public static float initialTime;

	#pragma warning disable 649
	struct ModuleInfo
	{
		public string moduleID;
		public int moduleScore;
	}
	#pragma warning restore 649
	
	public static void UpdateComponentValues()
	{
		foreach (KMGameInfo.KMModuleInfo info in Tweaks.GameInfo.GetAvailableModuleInfo())
		{
			if (!settings.ComponentValues.ContainsKey(info.ModuleId))
				settings.ComponentValues[info.ModuleId] = 6;
		}
	}

	public static string GetModuleID(BombComponent bombComponent)
	{
		switch (bombComponent.ComponentType)
		{
			case ComponentTypeEnum.Mod:
				KMBombModule bombModule = bombComponent.GetComponent<KMBombModule>();
				if (bombModule != null)
					return bombModule.ModuleType;

				break;
			case ComponentTypeEnum.NeedyMod:
				KMNeedyModule needyModule = bombComponent.GetComponent<KMNeedyModule>();
				if (needyModule != null)
					return needyModule.ModuleType;

				break;
			case ComponentTypeEnum.Empty:
			case ComponentTypeEnum.Timer:
				break;
			default:
				return bombComponent.ComponentType.ToString();
		}

		return null;
	}
}

class ModeSettings
{
    public float ZenModeTimePenalty = 0;
	public float ZenModeTimePenaltyIncrease = 0;
	public float ZenModeTimerSpeedUp = 0.25f;
	public float ZenModeTimerMaxSpeed = 2;
	public float TimeModeStartingTime = 5;
	public float TimeModeStartingMultiplier = 9.0f;
	public float TimeModeMaxMultiplier = 10.0f;
	public float TimeModeMinMultiplier = 1.0f;
	public float TimeModeSolveBonus = 0.1f;
	public float TimeModeMultiplierStrikePenalty = 1.5f;
	public float TimeModeTimerStrikePenalty = 0.25f;
	public float TimeModeMinimumTimeLost = 15;
	public float TimeModeMinimumTimeGained = 20;
    public Dictionary<string, double> ComponentValues = new Dictionary<string, double>();
	public Dictionary<string, double> TotalModulesMultiplier = new Dictionary<string, double>();
}