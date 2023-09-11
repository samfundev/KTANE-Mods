using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Missions;
using Assets.Scripts.Records;
using Events;
using Newtonsoft.Json;
using TweaksAssembly.Modules.Tweaks;
using UnityEngine;

class BombWrapper : MonoBehaviour
{
	readonly Dictionary<Mode, Color> ModeColors = new Dictionary<Mode, Color>()
	{
		{ Mode.Normal, Color.red },
		{ Mode.Zen, Color.cyan },
		{ Mode.Time, new Color(1, 0.5f, 0) },
		{ Mode.Steady, new Color(0, 0.8f, 0) }
	};

	float realTimeStart;

	public void Awake()
	{
		Bomb = GetComponent<Bomb>();
		holdable = Bomb.GetComponentInChildren<FloatingHoldable>();
		timerComponent = Bomb.GetTimer();
		widgetManager = Bomb.WidgetManager;

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
			Bomb.NumStrikesToLose++;
		}

		realTimeStart = Time.unscaledTime;
		BombEvents.OnBombDetonated += OnDetonate;
		BombEvents.OnBombSolved += OnSolve;

		foreach (BombComponent component in Bomb.BombComponents)
		{
			Dictionary<string, object> makeEventInfo(string type)
			{
				Dictionary<string, object> eventInfo = new Dictionary<string, object>()
				{
					{ "type", type },
					{ "moduleID", component.GetModuleID() },
					{ "bombTime", CurrentTimer },
					{ "realTime", Time.unscaledTime - realTimeStart },
				};

				if (componentIDs.TryGetValue(component, out int loggingID))
					eventInfo["loggingID"] = loggingID;

				return eventInfo;
			}

			component.OnPass += delegate
			{
				BombStatus.Instance.UpdateSolves();

				var eventInfo = makeEventInfo("PASS");
				if (Tweaks.CurrentMode == Mode.Time)
				{
					if (
						!Modes.settings.ComponentValues.TryGetValue(component.GetModuleID(), out double ComponentValue) &&
						!Modes.DefaultComponentValues.TryGetValue(component.GetModuleID(), out ComponentValue)
					)
					{
						ComponentValue = 10;
					}

					if (
						!Modes.settings.TotalModulesMultiplier.TryGetValue(component.GetModuleID(), out double totalModulesMultiplier) &&
						!Modes.DefaultTotalModulesMultiplier.TryGetValue(component.GetModuleID(), out totalModulesMultiplier)
					)
					{
						totalModulesMultiplier = 0;
					}

					var modules = Bomb.GetSolvableComponentCount();
					var points = ComponentValue + modules * totalModulesMultiplier;
					float finalMultiplier = Mathf.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier);
					float time = (float) (points * finalMultiplier * Modes.settings.TimeModePointMultiplier);
					float finalTime = Math.Max(Modes.settings.TimeModeMinimumTimeGained, time);

					// Show the alert
					string alertText = "";
					if (Math.Round(totalModulesMultiplier, 3) != 0)
					{
						alertText += $"{ComponentValue} + {totalModulesMultiplier:0.###} <size=36>x</size> {modules} mods = {points:0}\n";
					}

					string multiplierText = Math.Round(Modes.settings.TimeModePointMultiplier, 3) == 1 ? "" : $"<size=36>x</size> {Modes.settings.TimeModePointMultiplier:0.###} ";
					alertText += $"{points:0} point{(Math.Round(points) == 1 ? "" : "s")} <size=36>x</size> {finalMultiplier:0.#} {multiplierText}= {(time > 0 ? "+" : "")}{time.FormatTime()}\n";

					if (time < Modes.settings.TimeModeMinimumTimeGained)
					{
						alertText += $"Min Time Added = {(finalTime > 0 ? "+" : "")}{finalTime.FormatTime()}\n";
					}

					eventInfo["timeMode"] = alertText.TrimEnd('\n').Replace("<size=36>x</size>", "×"); // Build the logging information for time mode.

					alertText += component.GetModuleDisplayName();

					AddAlert(alertText.Replace(' ', ' '), Color.green); // Replace all spaces with nbsp since we don't want the line to wrap.

					CurrentTimer += finalTime;

					Modes.Multiplier += Modes.settings.TimeModeSolveBonus;
					BombStatus.Instance.UpdateMultiplier();
				}

				Tweaks.LogJSON("LFAEvent", eventInfo);

				return false;
			};

			var OnStrike = component.OnStrike;
			component.OnStrike = (BombComponent source) =>
			{
				var eventInfo = makeEventInfo("STRIKE");
				if (Tweaks.CurrentMode == Mode.Time)
				{
					float multiplier = Modes.Multiplier - Modes.settings.TimeModeMultiplierStrikePenalty;
					float finalMultiplier = Math.Max(multiplier, Modes.settings.TimeModeMinMultiplier);

					// Show the alert
					string alertText = $"TIME LOST = {Modes.settings.TimeModeTimerStrikePenalty:0.###} <size=36>x</size> {CurrentTimer.FormatTime()} = {(CurrentTimer * Modes.settings.TimeModeTimerStrikePenalty).FormatTime()}\n";
					alertText += $"MULTIPIER = {Modes.Multiplier:0.#} - {Modes.settings.TimeModeMultiplierStrikePenalty:0.#} = {multiplier:0.#}\n";

					if (multiplier < Modes.settings.TimeModeMinMultiplier)
					{
						alertText += $"REDUCED TO MIN = {finalMultiplier}\n";
					}

					eventInfo["timeMode"] = alertText.TrimEnd('\n').Replace("<size=36>x</size>", "×"); // Build the logging information for time mode.

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

				Tweaks.LogJSON("LFAEvent", eventInfo);

				bool finalStrike = OnStrike(source);

				// These mode modifications need to happen after the game handles the strike since they change the timer rate.
				if (Tweaks.CurrentMode == Mode.Zen)
				{
					Bomb.NumStrikesToLose++;
					finalStrike = false;

					ZenModeTimerRate = Mathf.Max(ZenModeTimerRate - Mathf.Abs(Modes.settings.ZenModeTimerSpeedUp), -Mathf.Abs(Modes.settings.ZenModeTimerMaxSpeed));
					timerComponent.SetRateModifier(ZenModeTimerRate);

					CurrentTimer += ZenModeTimePenalty * 60;
					ZenModeTimePenalty += Mathf.Abs(Modes.settings.ZenModeTimePenaltyIncrease);
				}

				if (Tweaks.CurrentMode == Mode.Steady)
				{
					timerComponent.SetRateModifier(1);
					CurrentTimer -= Modes.settings.SteadyModeFixedPenalty * 60 - Modes.settings.SteadyModePercentPenalty * BombStartingTimer;
				}

				BombStatus.Instance.UpdateStrikes();

				return finalStrike;
			};
		}

		var moduleTweaks = new Dictionary<string, Func<BombComponent, ModuleTweak>>()
		{
			{ "shapeshift", bombComponent => new ShapeShiftLogging(bombComponent) },
			{ "Microcontroller", bombComponent => new MicrocontrollerLogging(bombComponent) },
			{ "LetterKeys", bombComponent => new LetterKeysLogging(bombComponent) },
			{ "Emoji Math", bombComponent => new EmojiMathLogging(bombComponent) },
			{ "Probing", bombComponent => new ProbingLogging(bombComponent) },
			{ "SeaShells", bombComponent => new SeaShellsLogging(bombComponent) },
			{ "WordScrambleModule", bombComponent => new WordScramblePatch(bombComponent) },
			{ "Color Decoding", bombComponent => new ColorDecodingTweak(bombComponent) },
			{ "TurnTheKeyAdvanced", bombComponent => new TTKSTweak(bombComponent) },
			{ "draw", bombComponent => new DrawTweak(bombComponent) },
			{ "groceryStore", bombComponent => new GroceryStoreTweak(bombComponent) },
			{ "parliament", bombComponent => new ParliamentTweak(bombComponent) },
			{ "ForeignExchangeRates", bombComponent => new ForeignExchangeRatesTweak(bombComponent) },
			{ "Symbstructions", bombComponent => new SymbstructionsTweak(bombComponent) },

			{ "Wires", bombComponent => new WiresLogging(bombComponent) },
			{ "Keypad", bombComponent => new KeypadLogging(bombComponent) }
		};

		modules = new string[Bomb.Faces.Sum(face => face.Anchors.Count)];
		anchors = new decimal[Bomb.Faces.Sum(face => face.Anchors.Count)][];
		bombLogInfo = new Dictionary<string, object>()
		{
			{ "serial", JsonConvert.DeserializeObject<Dictionary<string, string>>(Bomb.WidgetManager.GetWidgetQueryResponses(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null)[0])["serial"] },
			{ "displayNames", displayNames },
			{ "ids", ids },
			{ "anchors", anchors },
			{ "modules", modules },
			{ "timestamp", DateTime.Now.ToString("O") }
		};

		modulesUnactivated = Bomb.BombComponents.Count;
		foreach (BombComponent component in Bomb.BombComponents)
		{
			KMBombModule bombModule = component.GetComponent<KMBombModule>();
			KMNeedyModule needyModule = component.GetComponent<KMNeedyModule>();
			if (bombModule != null && (bombModule.ModuleType == "TurnTheKey" || Tweaks.settings.ModuleTweaks))
			{
				switch (bombModule.ModuleType)
				{
					// TTK is our favorite Zen mode compatible module
					// Of course, everything here is repurposed from Twitch Plays.
					case "TurnTheKey":
						new TTKComponentSolver(component, bombModule, Tweaks.CurrentMode.Equals(Mode.Zen) ? Modes.initialTime : timerComponent.TimeRemaining);
						break;

					// Correct some mispositioned objects in some regular modules
					case "ForeignExchangeRates":
					case "resistors":
					case "CryptModule":
					case "LEDEnc":
						// This fixes the position of the module itself (but keeps the status light in its original location, which fixes it)
						component.transform.Find("Model").localPosition = new Vector3(0.004f, 0, 0);
						break;
					case "ColourFlash":
					case "ColourFlashES":
					case "ColourFlashPL":
						// This fixes the position of the module itself (but keeps the status light in its original location)
						component.transform.Find("ModuleBackground").localPosition = Vector3.zero;
						// This fixes the position of the status light
						component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
						break;
					case "Listening":
						// This fixes the Y-coordinate of the position of the status light
						component.transform.Find("StatusLight").localPosition = new Vector3(-0.0761f, 0.01986f, 0.075f);
						break;
					case "TwoBits":
					case "errorCodes":
					case "primeEncryption":
					case "babaIsWho":
					case "combinationLock":
						// This fixes the position of the status light
						component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
						break;
					case "matrix":
						// This fixes the position of the module itself
						foreach (Transform child in component.transform)
							child.localPosition -= new Vector3(0, 0.0053061005f, 0);
						break;
					case "mcdNatures":
					case "traffic_board":
						// This fixes the position of the module itself
						foreach (Transform child in component.transform)
							child.localPosition += new Vector3(0, 0.0053061f, 0);
						break;
					case "jackboxServerModule":
						// This fixes the position of the module itself
						foreach (Transform child in component.transform)
							child.localPosition += new Vector3(0, 0.0148f, 0);
						break;
					case "memorableButtons":
					case "strikeSolve":
						// This fixes the position of the module itself
						foreach (Transform child in component.transform)
							child.localPosition -= new Vector3(0, 0.0053061005f, 0);
						// This fixes the position of the status light
						component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
						break;
					case "vexillology":
						// This fixes the position of the module itself
						foreach (Transform child in component.transform)
							child.localPosition -= new Vector3(0, 0.0053061005f, 0);
						// This fixes the position of the status light
						component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
						// This fixes the module selectable being pass through
						component.GetComponent<Selectable>().IsPassThrough = false;
						break;
					case "tWords":
					case "moon":
					case "sun":
					case "jackOLantern":
					case "simonsStar":
					case "EncryptedMorse":
					case "FlashMemory":
					case "caesarsMaths":
						// This fixes the scale of the light components
						Light[] lights = component.GetComponentsInChildren<Light>();
						for (int i = 0; i < lights.Length; i++)
							lights[i].range *= component.transform.lossyScale.x;
						break;
					case "simonSamples":
						component.transform.Find("Scale/PlayButton/LedOn/Point light").GetComponent<Light>().range *= component.transform.lossyScale.x;
						component.transform.Find("Scale/RecordButton/LedOn/Point light").GetComponent<Light>().range *= component.transform.lossyScale.x;
						break;
					case "jewelVault":
						// This fixes the scale of the light components
						lights = component.GetComponentsInChildren<Light>();
						for (int i = 0; i < lights.Length; i++)
							lights[i].range *= component.transform.lossyScale.x;
						// This fixes the scale of the light components that could not be received using the first method
						foreach (var color in new[] { "yellow", "red", "green", "blue", "pink" })
						{
							foreach (var suffix in new[] { "Top", "1", "2", "3", "4" })
								component.transform.Find($"hinge/wheelHolder/outerWheel/centre/centralOrb/{color}Lights/{color}Light{suffix}").GetComponent<Light>().range *= component.transform.lossyScale.x;
						}
						break;
					case "sphere":
						// This fixes the scale of the light components
						lights = component.GetComponentsInChildren<Light>();
						for (int i = 0; i < lights.Length; i++)
							lights[i].range *= component.transform.lossyScale.x;
						// This fixes the scale of the light components that could not be received using the first method
						for (int i = 0; i < 11; i++)
							component.transform.Find("stageLights/Light" + (i + 1) + "/Spotlight").GetComponent<Light>().range *= component.transform.lossyScale.x;
						for (int i = 0; i < 19; i++)
							component.transform.Find("interactionLights/Light" + (i + 1) + "/Spotlight").GetComponent<Light>().range *= component.transform.lossyScale.x;
						component.transform.Find("interactionLights/Light10a/Spotlight").GetComponent<Light>().range *= component.transform.lossyScale.x;
						component.transform.Find("interactionLights/Light10b/Spotlight").GetComponent<Light>().range *= component.transform.lossyScale.x;
						break;
				}

				// This fixes the position of the highlight
				switch (bombModule.ModuleType)
				{
					case "babaIsWho":
					case "memorableButtons":
					case "strikeSolve":
					case "vexillology":
					case "jackboxServerModule":
						component.GetComponent<Selectable>().Highlight.transform.localPosition = Vector3.zero;
						break;
					case "matrix":
						component.GetComponent<Selectable>().Highlight.transform.localPosition += new Vector3(0, 0.0053061005f, 0);
						break;
				}
			}
			else if (needyModule != null && Tweaks.settings.ModuleTweaks)
			{
				// Correct some mispositioned objects in some needy modules
				switch (needyModule.ModuleType)
				{
					case "Needy Math":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("Model/ScreenBackground").gameObject.SetActive(false);
						// This fixes the positions of the question and answer displays
						component.transform.Find("Model/ScreenBackgroundQuestion").localEulerAngles = new Vector3(-2.3f, -180, 0);
						component.transform.Find("Model/ScreenBackgroundAnswer").localEulerAngles = new Vector3(-2.3f, -180, 0);
						component.transform.Find("Model/MathDisplay").localPosition = new Vector3(-0.0533f, 0.0234f, 0.0616f);
						component.transform.Find("Model/MathDisplay").localEulerAngles = new Vector3(86, -720, 0);
						component.transform.Find("Model/MathDisplayAnswer").localPosition = new Vector3(0.0542f, 0.0234f, 0.0616f);
						component.transform.Find("Model/MathDisplayAnswer").localEulerAngles = new Vector3(86, -720, 0);
						break;
					case "LightsOut":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("Screen").gameObject.SetActive(false);
						break;
					case "Filibuster":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("Model/Component_Needy_Background/Screen").gameObject.SetActive(false);
						break;
					case "needyMrsBob":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("ancillary/Component_Needy_Background/Screen").gameObject.SetActive(false);
						break;
					case "draw":
					case "rapidButtons":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("Component_Needy_Background/Screen").gameObject.SetActive(false);
						break;
					case "simonSquawks":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("Ancillary/Component_Needy_Background/Screen").gameObject.SetActive(false);
						break;
					case "triangleButtons":
						// This fixes z-fighting in the countdown screen by removing Unity's placeholder screen
						component.transform.Find("Background/Screen").gameObject.SetActive(false);
						break;
				}
			}

			ModuleTweak moduleTweak = null;
			string moduleType = bombModule != null ? bombModule.ModuleType : needyModule != null ? needyModule.ModuleType : component.ComponentType.ToString();
			if (moduleTweaks.ContainsKey(moduleType) && (!moduleType.EqualsAny("WordScrambleModule", "TurnKeyAdvancedModule") || Tweaks.settings.ModuleTweaks))
			{
				moduleTweak = moduleTweaks[moduleType](component);
			}

			component.StartCoroutine(GetModuleInformation(component, moduleTweak));

			if (component.ComponentType == ComponentTypeEnum.Mod || component.ComponentType == ComponentTypeEnum.NeedyMod)
			{
				ReflectedTypes.FindModeBoolean(component);
			}
			else if (Tweaks.settings.ModuleTweaks)
			{
				switch (component.ComponentType)
				{
					case ComponentTypeEnum.Keypad:
						Tweaks.FixKeypadButtons(((KeypadComponent) component).buttons);
						break;
					case ComponentTypeEnum.Simon:
						Tweaks.FixKeypadButtons(((SimonComponent) component).buttons);
						break;
					case ComponentTypeEnum.Password:
						Tweaks.FixKeypadButtons(component.GetComponentsInChildren<KeypadButton>());
						break;
					case ComponentTypeEnum.NeedyVentGas:
						Tweaks.FixKeypadButtons(((NeedyVentComponent) component).YesButton, ((NeedyVentComponent) component).NoButton);
						break;
				}
			}
		}
	}

	public static void AwardPoints(BombComponent bombComponent, double points)
	{
		var wrapper = Tweaks.bombWrappers.First(wrap => wrap.Bomb.BombComponents.Contains(bombComponent));

		var modules = wrapper.Bomb.GetSolvableComponentCount();
		float finalMultiplier = Mathf.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier);
		float time = (float) (points * finalMultiplier * Modes.settings.TimeModePointMultiplier);

		// Show the alert
		string multiplierText = Math.Round(Modes.settings.TimeModePointMultiplier, 3) == 1 ? "" : $"<size=36>x</size> {Modes.settings.TimeModePointMultiplier:0.###} ";
		string alertText = $"{points:0} point{(Math.Round(points) == 1 ? "" : "s")} <size=36>x</size> {finalMultiplier:0.#} {multiplierText}= {(time > 0 ? " + " : "")}{time.FormatTime()}\n";
		alertText += bombComponent.GetModuleDisplayName();

		wrapper.AddAlert(alertText.Replace(' ', ' '), Color.green); // Replace all spaces with nbsp since we don't want the line to wrap.

		wrapper.CurrentTimer += time;
	}

	public void OnDestroy()
	{
		BombEvents.OnBombDetonated -= OnDetonate;
		BombEvents.OnBombSolved -= OnSolve;
		Tweaks.bombWrappers.Remove(this);
	}

	int modulesUnactivated = 0;
	readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();
	readonly Dictionary<string, List<int>> ids = new Dictionary<string, List<int>>();
	Dictionary<string, object> bombLogInfo;
	string[] modules = new string[] { };
	decimal[][] anchors = new decimal[][] { };

	readonly Dictionary<BombComponent, int> componentIDs = new Dictionary<BombComponent, int>();

	IEnumerator GetModuleInformation(BombComponent bombComponent, ModuleTweak moduleTweak = null)
	{
		int moduleID = -1;
		KMBombModule bombModule = bombComponent.GetComponent<KMBombModule>();
		string moduleType = bombComponent.GetModuleID();

		displayNames[moduleType] = bombComponent.GetModuleDisplayName();

		if (bombModule != null)
		{
			// Try to find a module ID from a field
			ReflectionHelper.Gettable idField = ReflectedTypes.GetModuleIDNumber(bombModule, out Component targetComponent);
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
			componentIDs[bombComponent] = moduleID;
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
			Tweaks.LogJSON("LFABombInfo", bombLogInfo);
		}
	}

	public void OnDetonate()
	{
		if (Bomb.HasDetonated)
			return;

		BombEvents.OnBombDetonated -= OnDetonate;

		Tweaks.LogJSON("LFAEvent", new Dictionary<string, object>()
		{
			{ "type", "BOMB_DETONATE" },
			{ "serial", bombLogInfo["serial"] },
			{ "bombTime", CurrentTimer },
			{ "realTime", Time.unscaledTime - realTimeStart },
			{ "solves", Bomb.GetSolvedComponentCount() },
			{ "strikes", Bomb.NumStrikes + (CurrentTimer > 0 ? 1 : 0) },
		});
	}

	public void OnSolve()
	{
		if (!Bomb.IsSolved())
			return;

		BombEvents.OnBombSolved -= OnSolve;

		Tweaks.LogJSON("LFAEvent", new Dictionary<string, object>()
		{
			{ "type", "BOMB_SOLVE" },
			{ "serial", bombLogInfo["serial"] },
			{ "bombTime", CurrentTimer },
			{ "realTime", Time.unscaledTime - realTimeStart },
			{ "solves", Bomb.GetSolvedComponentCount() },
			{ "strikes", Bomb.NumStrikes },
		});
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

	// TODO: Use the new Toasts class instead of this.
	void AddAlert(string text, Color color)
	{
		var alert = Instantiate(BombStatus.Instance.Alert, BombStatus.Instance.transform, false).GetComponent<RectTransform>();
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

	public void CauseStrikesToExplosion(string reason)
	{
		for (int strikesToMake = StrikeLimit - StrikeCount; strikesToMake > 0; --strikesToMake)
		{
			CauseStrike(reason);
		}
	}

	private void CauseStrike(string reason)
	{
		StrikeSource strikeSource = new StrikeSource
		{
			ComponentType = ComponentTypeEnum.Mod,
			InteractionType = InteractionTypeEnum.Other,
			Time = CurrentTimerElapsed,
			ComponentName = reason
		};

		RecordManager recordManager = RecordManager.Instance;
		recordManager.RecordStrike(strikeSource);

		Bomb.OnStrike(null);
	}

	private float ZenModeTimePenalty;
	private float ZenModeTimerRate;

	public Bomb Bomb = null;
	public FloatingHoldable holdable;

	public TimerComponent timerComponent = null;
	public WidgetManager widgetManager = null;

	public int BombSolvableModules => Bomb.GetSolvableComponentCount();
	public int BombSolvedModules => Bomb.GetSolvedComponentCount();
	public float BombStartingTimer => timerComponent.TimeElapsed + timerComponent.TimeRemaining;

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

	public string StartingTimerFormatted => timerComponent.GetFormattedTime(BombStartingTimer, true);

	public string GetFullFormattedTime => Math.Max(CurrentTimer, 0).FormatTime();

	public string GetFullStartingTime => Math.Max(BombStartingTimer, 0).FormatTime();

	public int StrikeCount => Bomb.NumStrikes;

	public int StrikeLimit => Bomb.NumStrikesToLose;
}