using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Missions;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

public enum Mode
{
    Normal,
    Time,
    //VS,
    Zen,
	Steady
}

static class Modes
{
	public static ModConfig<ModeSettings> modConfig = new ModConfig<ModeSettings>("ModeSettings");
	public static ModeSettings settings = modConfig.Settings;
	public static float Multiplier = settings.TimeModeStartingMultiplier;
    public static float timePenalty = 1.0f;
    public static float initialTime;

	public static Dictionary<string, double> DefaultComponentValues = new Dictionary<string, double>();
	public static Dictionary<string, double> DefaultTotalModulesMultiplier = new Dictionary<string, double>();

	#pragma warning disable 649
	struct ModuleInfo
	{
		public string moduleID;
		public int moduleScore;
	}
	#pragma warning restore 649

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
			default:
				return bombComponent.ComponentType.ToString();
		}

		return null;
	}

	public static IEnumerator LoadDefaultSettings()
	{
		UnityWebRequest www = UnityWebRequest.Get("https://spreadsheets.google.com/feeds/list/16lz2mCqRWxq__qnamgvlD0XwTuva4jIDW1VPWX49hzM/1/public/values?alt=json");

		yield return www.SendWebRequest();

		if (!www.isNetworkError && !www.isHttpError)
		{
			bool missingWarning = false;
			foreach (var entry in JObject.Parse(www.downloadHandler.text)["feed"]["entry"])
			{
				bool score = !string.IsNullOrEmpty(entry["gsx$resolvedscore"]["$t"]?.Value<string>().Trim());
				bool multiplier = !string.IsNullOrEmpty(entry["gsx$resolvedbosspointspermodule"]["$t"]?.Value<string>().Trim());

				if (entry["gsx$moduleid"] == null)
				{
					if ((score || multiplier) && !missingWarning)
					{
						missingWarning = true;
						Tweaks.Log("An entry on the spreadsheet is missing it's module ID. You should contact the spreadsheet maintainers about this.");
					}

					continue;
				}

				string moduleID = entry["gsx$moduleid"].Value<string>("$t");
				if (string.IsNullOrEmpty(moduleID) || moduleID == "ModuleID")
					continue;

				if (score)
					DefaultComponentValues[moduleID] = entry["gsx$resolvedscore"].Value<double>("$t");
				if (multiplier)
					DefaultTotalModulesMultiplier[moduleID] = entry["gsx$resolvedbosspointspermodule"].Value<double>("$t");
			}
		}
		else
		{
			Tweaks.Log("Failed to load the default time mode values.");
		}
	}
}

class ModeSettings
{
    public float ZenModeTimePenalty = 0;
	public float ZenModeTimePenaltyIncrease = 0;
	public float ZenModeTimerSpeedUp = 0.25f;
	public float ZenModeTimerMaxSpeed = 2;
	public float SteadyModeFixedPenalty = 2;
	public float SteadyModePercentPenalty = 0;
	public float TimeModeStartingTime = 5;
	public float TimeModeStartingMultiplier = 9.0f;
	public float TimeModeMaxMultiplier = 10.0f;
	public float TimeModeMinMultiplier = 1.0f;
	public float TimeModeSolveBonus = 0.1f;
	public float TimeModeMultiplierStrikePenalty = 1.5f;
	public float TimeModeTimerStrikePenalty = 0.25f;
	public float TimeModeMinimumTimeLost = 15;
	public float TimeModeMinimumTimeGained = 20;
	public float TimeModePointMultiplier = 1;
    public Dictionary<string, double> ComponentValues = new Dictionary<string, double>();
	public Dictionary<string, double> TotalModulesMultiplier = new Dictionary<string, double>();
}