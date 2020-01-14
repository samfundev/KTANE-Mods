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

		holdable.OnLetGo += () => BombStatus.Instance.currentBomb = null;

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
					if (
						!Modes.settings.ComponentValues.TryGetValue(Modes.GetModuleID(component), out double ComponentValue) &&
						!Modes.DefaultComponentValues.TryGetValue(Modes.GetModuleID(component), out ComponentValue)
					)
					{
						ComponentValue = 10;
					}

					if (
						!Modes.settings.TotalModulesMultiplier.TryGetValue(Modes.GetModuleID(component), out double totalModulesMultiplier) &&
						!Modes.DefaultTotalModulesMultiplier.TryGetValue(Modes.GetModuleID(component), out totalModulesMultiplier)
					)
					{
						totalModulesMultiplier = 0;
					}

					var points = ComponentValue + Bomb.BombComponents.Count * totalModulesMultiplier;
					float finalMultiplier = Mathf.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier);
					float time = (float) (points * finalMultiplier * Modes.settings.TimeModePointMultiplier);
					float finalTime = Math.Max(Modes.settings.TimeModeMinimumTimeGained, time);

					// Show the alert
					string alertText = "";
					if (Math.Round(totalModulesMultiplier, 3) != 0)
					{
						alertText += $"{ComponentValue} + {totalModulesMultiplier.ToString("0.###")} <size=36>x</size> {Bomb.BombComponents.Count} mods = {points.ToString("0")}\n";
					}

					string multiplierText = Math.Round(Modes.settings.TimeModePointMultiplier, 3) == 1 ? "" : $"<size=36>x</size> {Modes.settings.TimeModePointMultiplier.ToString("0.###")} ";
					alertText += $"{points.ToString("0")} points <size=36>x</size> {finalMultiplier.ToString("0.#")} {multiplierText}= {(time > 0 ? "+" : "")}{time.FormatTime()}\n";

					if (time < Modes.settings.TimeModeMinimumTimeGained)
					{
						alertText += $"Min Time Added = {(finalTime > 0 ? "+" : "")}{finalTime.FormatTime()}\n";
					}

					alertText += component.GetModuleDisplayName();

					AddAlert(alertText.Replace(' ', ' '), Color.green); // Replace all spaces with nbsp since we don't want the line to wrap.

					CurrentTimer += finalTime;

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
					float multiplier = Modes.Multiplier - Modes.settings.TimeModeMultiplierStrikePenalty;
					float finalMultiplier = Math.Max(multiplier, Modes.settings.TimeModeMinMultiplier);

					// Show the alert
					string alertText = $"TIME LOST = {Modes.settings.TimeModeTimerStrikePenalty.ToString("0.###")} <size=36>x</size> {CurrentTimer.FormatTime()} = {(CurrentTimer * Modes.settings.TimeModeTimerStrikePenalty).FormatTime()}\n";
					alertText += $"MULTIPIER = {Modes.Multiplier.ToString("0.#")} - {Modes.settings.TimeModeMultiplierStrikePenalty.ToString("0.#")} = {multiplier.ToString("0.#")}\n";

					if (multiplier < Modes.settings.TimeModeMinMultiplier)
					{
						alertText += $"REDUCED TO MIN = {finalMultiplier}\n";
					}

					alertText += component.GetModuleDisplayName();
					AddAlert(alertText.Replace(' ', ' '), Color.red);

					Modes.Multiplier = finalMultiplier;
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
		anchors = new decimal[bomb.Faces.Sum(face => face.Anchors.Count)][];
		bombLogInfo = new Dictionary<string, object>()
		{
			{ "serial", JsonConvert.DeserializeObject<Dictionary<string, string>>(bomb.WidgetManager.GetWidgetQueryResponses(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null)[0])["serial"] },
			{ "displayNames", displayNames },
			{ "ids", ids },
			{ "anchors", anchors },
			{ "modules", modules }
		};

		modulesUnactivated = bomb.BombComponents.Count;
		foreach (BombComponent component in bomb.BombComponents)
		{
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

			ModuleTweak moduleTweak = null;
			string moduleType = bombModule != null ? bombModule.ModuleType : component.ComponentType.ToString();
			if (moduleTweaks.ContainsKey(moduleType))
			{
				moduleTweak = moduleTweaks[moduleType](component);
			}

			component.StartCoroutine(GetModuleInformation(component, moduleTweak));

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
	readonly decimal[][] anchors = new decimal[][] { };

	IEnumerator GetModuleInformation(BombComponent bombComponent, ModuleTweak moduleTweak = null)
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

		// From logging implemented by Tweaks
		if (moduleTweak is ModuleLogging moduleLogging)
		{
			moduleID = moduleLogging.moduleID;
		}

		if (moduleID != -1)
		{
			if (!ids.ContainsKey(moduleType)) ids[moduleType] = new List<int>();
			ids[moduleType].Add(moduleID);
		}

		// Find the index and position of the module's anchor
		var allAnchors = Bomb.Faces.SelectMany(face => face.Anchors).ToList();
		if (allAnchors.Count != 0) // Prevents .First() from being a problem later if there was somehow no anchors.
		{
			Transform moduleAnchor = allAnchors.OrderBy(anchor => (anchor.position - bombComponent.transform.position).magnitude).First();
			int index = allAnchors.IndexOf(moduleAnchor);

			modules[index] = moduleID != -1 ? $"{moduleType} {moduleID}" : $"{moduleType} -";
			var position = Quaternion.Euler(-Bomb.transform.rotation.eulerAngles) * ((moduleAnchor.position - Bomb.transform.position) / Bomb.Scale);
			anchors[index] = new decimal[] { Math.Round((decimal) position.x, 3), Math.Round((decimal) position.z, 3) }; // Round using a decimal to make the JSON a bit cleaner.
		}

		modulesUnactivated--;
		if (modulesUnactivated == 0)
		{
			LogJSON("LFABombInfo", bombLogInfo);
		}
	}

	void LogJSON(string tag, object json)
	{
		string[] chunks = JsonConvert.SerializeObject(json).ChunkBy(250).ToArray();
		Tweaks.Log(tag, chunks.Length + "\n" + chunks.Join("\n"));
	}

	void LogChildren(Transform goTransform, int depth = 0)
	{
		Debug.LogFormat("{2}{0} - {1}", goTransform.name, goTransform.localPosition.ToString("N6"), new string('\t', depth));
		foreach (Transform child in goTransform)
		{
			LogChildren(child, depth + 1);
		}
	}

	public static List<RectTransform> Alerts = new List<RectTransform>();

	void AddAlert(string text, Color color)
	{
		var alert = UnityEngine.Object.Instantiate(BombStatus.Instance.Alert, BombStatus.Instance.transform, false).GetComponent<RectTransform>();
		var textComponent = alert.gameObject.Traverse<UnityEngine.UI.Text>("Background", "Text");
		textComponent.text = text;
		textComponent.color = color;

		var size = alert.sizeDelta;
		size.y = textComponent.preferredHeight + 16; // Add 16 to account for margin.
		alert.sizeDelta = size;
		alert.gameObject.Traverse<RectTransform>("Background").sizeDelta = size;

		Alerts.Add(alert);
		BombStatus.Instance.StartCoroutine(AnimateAlert(alert));
	}

	IEnumerator AnimateAlert(RectTransform alert)
	{
		alert.gameObject.SetActive(true);

		float originalHeight = alert.rect.height;
		foreach (float alpha in 0.75f.TimedAnimation().EaseCubic())
		{
			alert.sizeDelta = new Vector2(alert.sizeDelta.x, originalHeight * alpha);
			yield return null;
		}

		yield return new WaitForSeconds(5);

		foreach (float alpha in 0.75f.TimedAnimation().EaseCubic())
		{
			alert.sizeDelta = new Vector2(alert.sizeDelta.x, originalHeight * (1 - alpha));
			yield return null;
		}

		Alerts.Remove(alert);
		UnityEngine.Object.Destroy(alert.gameObject);
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