using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using Assets.Scripts.Missions;

class BombWrapper
{
	readonly Dictionary<Mode, Color> ModeColors = new Dictionary<Mode, Color>()
	{
		{ Mode.Normal, Color.red },
		{ Mode.Zen, Color.cyan },
		{ Mode.Time, new Color(1, 0.5f, 0) },
		{ Mode.Steady, new Color(0, 0.8f, 0) }
	};

	public BombWrapper(Bomb bomb)
	{
		Bomb = bomb;
		holdable = bomb.GetComponentInChildren<FloatingHoldable>();
		timerComponent = bomb.GetTimer();
		widgetManager = bomb.WidgetManager;

		Color modeColor = ModeColors[Tweaks.CurrentMode];
		BombStatus.Instance.TimerPrefab.color = modeColor;
		timerComponent.text.color = modeColor;
		timerComponent.StrikeIndicator.RedColour = modeColor;

		if (Tweaks.CurrentMode == Mode.Zen)
		{
			ZenModeTimePenalty = Mathf.Abs(Modes.settings.ZenModeTimePenalty);
			ZenModeTimerRate = -timerComponent.GetRate();
			timerComponent.SetRateModifier(ZenModeTimerRate);
			Modes.initialTime = timerComponent.TimeRemaining;

			//This was in the original code to make sure the bomb didn't explode on the first strike
			bomb.NumStrikesToLose++;
		}

		foreach (BombComponent component in Bomb.BombComponents)
		{
			component.OnPass += delegate
			{
				BombStatus.Instance.UpdateSolves();

				if (Tweaks.CurrentMode == Mode.Time)
				{
					if (!Modes.settings.ComponentValues.TryGetValue(Modes.GetModuleID(component), out double ComponentValue))
					{
						ComponentValue = 6;
					}

					Modes.settings.TotalModulesMultiplier.TryGetValue(Modes.GetModuleID(component), out double totalModulesMultiplier);

					float time = (float) (Mathf.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier) * (ComponentValue + Bomb.BombComponents.Count * totalModulesMultiplier));

					CurrentTimer += Math.Max(Modes.settings.TimeModeMinimumTimeGained, time);

					Modes.Multiplier += Modes.settings.TimeModeSolveBonus;
					BombStatus.Instance.UpdateMultiplier();
				}

				return false;
			};

			var OnStrike = component.OnStrike;
			component.OnStrike = (BombComponent source) =>
			{
				if (Tweaks.CurrentMode == Mode.Time)
				{
					Modes.Multiplier = Math.Max(Modes.Multiplier - Modes.settings.TimeModeMultiplierStrikePenalty, Modes.settings.TimeModeMinMultiplier);
					BombStatus.Instance.UpdateMultiplier();
					if (CurrentTimer < (Modes.settings.TimeModeMinimumTimeLost / Modes.settings.TimeModeTimerStrikePenalty))
					{
						CurrentTimer -= Modes.settings.TimeModeMinimumTimeLost;
					}
					else
					{
						CurrentTimer -= CurrentTimer * Modes.settings.TimeModeTimerStrikePenalty;
					}

					// We can safely set the number of strikes to -1 since it's going to be increased by the game after us.
					Bomb.NumStrikes = -1;
				}

				OnStrike(source);

				// These mode modifications need to happen after the game handles the strike since they change the timer rate.
				if (Tweaks.CurrentMode == Mode.Zen)
				{
					bomb.NumStrikesToLose++;

					ZenModeTimerRate = Mathf.Max(ZenModeTimerRate - Mathf.Abs(Modes.settings.ZenModeTimerSpeedUp), -Mathf.Abs(Modes.settings.ZenModeTimerMaxSpeed));
					timerComponent.SetRateModifier(ZenModeTimerRate);

					CurrentTimer += ZenModeTimePenalty * 60;
					ZenModeTimePenalty += Mathf.Abs(Modes.settings.ZenModeTimePenaltyIncrease);
				}

				if (Tweaks.CurrentMode == Mode.Steady)
				{
					timerComponent.SetRateModifier(1);
					CurrentTimer -= Modes.settings.SteadyModeFixedPenalty * 60 - Modes.settings.SteadyModePercentPenalty * bombStartingTimer;
				}

				BombStatus.Instance.UpdateStrikes();

				return false;
			};
		}

		var moduleTweaks = new Dictionary<string, Func<BombComponent, ModuleTweak>>()
		{
			{ "Emoji Math", bombComponent => new EmojiMathLogging(bombComponent) },
			{ "Probing", bombComponent => new ProbingLogging(bombComponent) },
			{ "SeaShells", bombComponent => new SeaShellsLogging(bombComponent) },
			{ "switchModule", bombComponent => new SwitchesLogging(bombComponent) },
			{ "WordScrambleModule", bombComponent => new WordScramblePatch(bombComponent) },

			{ "Wires", bombComponent => new WiresLogging(bombComponent) },
			{ "Keypad", bombComponent => new KeypadLogging(bombComponent) },
		};

		modules = new string[bomb.Faces.Sum(face => face.Anchors.Count)];
		bombLogInfo = new Dictionary<string, object>()
		{
			{ "serial", JsonConvert.DeserializeObject<Dictionary<string, string>>(bomb.WidgetManager.GetWidgetQueryResponses(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null)[0])["serial"] },
			{ "displayNames", displayNames },
			{ "ids", ids },
			{ "case", bomb.gameObject.name.Replace("(Clone)", "") },
			{ "modules", modules }
		};

		modulesUnactivated = bomb.BombComponents.Count;
		foreach (BombComponent component in bomb.BombComponents)
		{
			component.StartCoroutine(GetModuleInformation(component));

			KMBombModule bombModule = component.GetComponent<KMBombModule>();
			if (bombModule != null)
			{
				switch (bombModule.ModuleType)
				{
					//TTK is our favorite Zen mode compatible module
					//Of course, everything here is repurposed from Twitch Plays.
					case "TurnTheKey":
						new TTKComponentSolver(bombModule, bomb, Tweaks.CurrentMode.Equals(Mode.Zen) ? Modes.initialTime : timerComponent.TimeRemaining);
						break;

					// Correct some mispositioned objects in older modules
					case "ForeignExchangeRates":
					case "resistors":
					case "CryptModule":
					case "LEDEnc":
						// This fixes the position of the module itself (but keeps the status light in its original location, which fixes it)
						component.transform.Find("Model").transform.localPosition = new Vector3(0.004f, 0, 0);
						break;
					case "Listening":
						// This fixes the Y-coordinate of the position of the status light
						component.transform.Find("StatusLight").transform.localPosition = new Vector3(-0.0761f, 0.01986f, 0.075f);
						break;
					case "TwoBits":
					case "errorCodes":
						// This fixes the position of the status light
						component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
						break;
				}
			}

			string moduleType = bombModule != null ? bombModule.ModuleType : component.ComponentType.ToString();
			if (moduleTweaks.ContainsKey(moduleType))
			{
				moduleTweaks[moduleType](component);
			}

			if (component.ComponentType == ComponentTypeEnum.Mod)
			{
				ReflectedTypes.FindModeBoolean(component);
			}
		}
	}

	int modulesUnactivated = 0;
	readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();
	readonly Dictionary<string, List<int>> ids = new Dictionary<string, List<int>>();
	readonly Dictionary<string, object> bombLogInfo;
	readonly string[] modules = new string[] { };

	IEnumerator GetModuleInformation(BombComponent bombComponent)
	{
		int moduleID = -1;
		KMBombModule bombModule = bombComponent.GetComponent<KMBombModule>();
		string moduleType = bombModule != null ? bombModule.ModuleType : bombComponent.ComponentType.ToString();

		displayNames[moduleType] = bombComponent.GetModuleDisplayName();

		if (bombModule != null)
		{
			// Try to find a module ID from a field
			System.Reflection.FieldInfo idField = ReflectedTypes.GetModuleIDNumber(bombModule, out Component targetComponent);

			if (idField != null)
			{
				// Find the module ID from reflection
				float startTime = Time.time;
				yield return new WaitUntil(() =>
				{
					moduleID = (int) idField.GetValue(targetComponent);
					return moduleID != 0 || Time.time - startTime > 30; // Check to see if the field has been initialized with an ID or fail out after 30 seconds.
				});
			}

			// From the object name.
			string prefix = bombModule.ModuleDisplayName + " #";
			if (moduleID == -1 && bombModule.gameObject.name.StartsWith(prefix) && !int.TryParse(bombModule.gameObject.name.Substring(prefix.Length), out moduleID))
			{
				moduleID = -1;
			}
		}

		// These component types shouldn't try to get the ID from the logger property. Used below.
		var blacklistedComponents = new[]
		{
			ComponentTypeEnum.Empty,
			ComponentTypeEnum.Mod,
			ComponentTypeEnum.NeedyMod,
			ComponentTypeEnum.Timer,
		};

		// From the logger property of vanilla components
		string loggerName = bombComponent.GetValue<object>("logger")?.GetValue<object>("Logger")?.GetValue<string>("Name");
		if (moduleID == -1 && !blacklistedComponents.Contains(bombComponent.ComponentType) && loggerName != null && !int.TryParse(loggerName.Substring(loggerName.IndexOf('#') + 1), out moduleID))
		{
			moduleID = -1;
		}

		// TODO: Handle logging implemented by Tweaks

		if (moduleID != -1)
		{
			if (!ids.ContainsKey(moduleType)) ids[moduleType] = new List<int>();
			ids[moduleType].Add(moduleID);
		}
		else
		{
			Tweaks.Log(bombComponent.GetModuleDisplayName(), "has no module id.");
		}

		// Find anchor index
		int index = 0;
		foreach (BombFace face in Bomb.Faces)
		{
			foreach (Transform anchor in face.Anchors)
			{
				if ((anchor.position - bombComponent.transform.position).magnitude < 0.05)
				{
					modules[index] = moduleID != -1 ? $"{moduleType} {moduleID}" : $"{moduleType} -";

					break;
				}

				index++;
			}
		}

		modulesUnactivated--;
		if (modulesUnactivated == 0)
		{
			string[] chunks = JsonConvert.SerializeObject(bombLogInfo).ChunkBy(250).ToArray();
			Tweaks.Log("LFABombInfo", chunks.Length + "\n" + chunks.Join("\n"));
		}
	}

	void LogChildren(Transform goTransform, int depth = 0)
	{
		Debug.LogFormat("{2}{0} - {1}", goTransform.name, goTransform.localPosition.ToString("N6"), new string('\t', depth));
		foreach (Transform child in goTransform)
		{
			LogChildren(child, depth + 1);
		}
	}

	private float ZenModeTimePenalty;
	private float ZenModeTimerRate;

	public Bomb Bomb = null;
	public FloatingHoldable holdable;

	public TimerComponent timerComponent = null;
	public WidgetManager widgetManager = null;

	public int bombSolvableModules { get => Bomb.GetSolvableComponentCount(); }
	public int bombSolvedModules { get => Bomb.GetSolvedComponentCount(); }
	public float bombStartingTimer { get => timerComponent.TimeElapsed + timerComponent.TimeRemaining; }

	public float CurrentTimerElapsed => timerComponent.TimeElapsed;

	public float CurrentTimer
	{
		get => timerComponent.TimeRemaining;
		set
		{
			timerComponent.TimeRemaining = (value < 0) ? 0 : value;
			timerComponent.text.text = timerComponent.GetFormattedTime(value, true);
		}
	}

	public string CurrentTimerFormatted => timerComponent.GetFormattedTime(CurrentTimer, true);

	public string StartingTimerFormatted => timerComponent.GetFormattedTime(bombStartingTimer, true);

	public string GetFullFormattedTime => Math.Max(CurrentTimer, 0).FormatTime();

	public string GetFullStartingTime => Math.Max(bombStartingTimer, 0).FormatTime();

	public int StrikeCount
	{
		get => Bomb.NumStrikes;
	}

	public int StrikeLimit
	{
		get => Bomb.NumStrikesToLose;
	}

	public int NumberModules => bombSolvableModules;
}