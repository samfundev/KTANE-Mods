using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Assets.Scripts.BombBinder;
using Assets.Scripts.Missions;
using Assets.Scripts.Mods.Mission;
using Assets.Scripts.Platform;
using Assets.Scripts.Progression;
using Assets.Scripts.Props;
using Assets.Scripts.Settings;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TweaksAssembly.Patching;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
class Tweaks : MonoBehaviour
{
	public static Tweaks Instance;

	public static ModConfig<TweakSettings> modConfig;
	public static TweakSettings settings;
	public static TweakSettings userSettings; // This stores exactly what the user has in their settings file unlike the settings variable which includes overrides.
	public static TweakSettings setupSettings; // The settings when the user entered the setup room. Many settings don't actually change until the user enters the setup room again.

	public static bool TwitchPlaysActive => GameObject.Find("TwitchPlays_Info") != null;
	public static Mode CurrentMode => TwitchPlaysActive ? Mode.Normal : settings.Mode;
	public static bool TwitchPlaysActiveCache;
	public static Mode CurrentModeCache;

	public static KMGameInfo GameInfo;
	[HideInInspector]
	public static KMGameInfo.State CurrentState = KMGameInfo.State.Transitioning;

	private readonly HashSet<TableOfContentsMetaData> ModToCMetaData = new HashSet<TableOfContentsMetaData>();
	static GameObject SettingWarning;
	GameObject TweaksCaseGeneratorCase;

	GameObject AdvantageousWarning;
	bool AdvantageousFeaturesEnabled => settings.BombHUD == HUDMode.On || settings.ShowEdgework || CurrentMode != Mode.Normal || settings.MissionSeed != -1;

	public static Action<KMGameInfo.State, KMGameInfo.State> OnStateChanged;

	private Tweak[] AllTweaks = new Tweak[0];
	private Type[] AllModulePatches = new Type[0];

	public void Awake()
	{
		Instance = this;

		MainThreadQueue.Initialize();

		GameInfo = GetComponent<KMGameInfo>();
		SettingWarning = gameObject.Traverse("UI", "SettingWarning");
		AdvantageousWarning = gameObject.Traverse("UI", "AdvantageousWarning");
		Tips.TipMessage = gameObject.Traverse("UI", "TipMessage");
		BetterCasePicker.BombCaseGenerator = GetComponentInChildren<BombCaseGenerator>();
		DemandBasedLoading.LoadingScreen = gameObject.Traverse<CanvasGroup>("UI", "LoadingModules");

		modConfig = new ModConfig<TweakSettings>("TweakSettings", OnRead);
		modConfig.Migrations.Add(jObject =>
		{
			if (jObject.TryGetValue("BombHUD", out JToken bombValue) && bombValue.Type == JTokenType.Boolean)
			{
				jObject["BombHUD"] = bombValue.Value<bool>() ? nameof(HUDMode.On) : nameof(HUDMode.Off);
			}

			if (jObject.TryGetValue("DisableAdvantageous", out JToken advantageousValue) && advantageousValue.Type == JTokenType.Boolean)
			{
				jObject["DisableAdvantageous"] = advantageousValue.Value<bool>() ? nameof(AdvantageousMode.On) : nameof(AdvantageousMode.Off);
			}
		});
		UpdateSettings();
		StartCoroutine(Modes.LoadDefaultSettings());

		if (Harmony.HasAnyPatches("qkrisi.harmonymod"))
			new Harmony("qkrisi.harmonymod").UnpatchAll();

		Patching.EnsurePatch("Harmony", typeof(ModInfoPatch), typeof(WorkshopPatch), typeof(ReloadPatch), typeof(SetupPatch),
			typeof(ManualButtonPatch), typeof(InstructionPatch), typeof(ChangeButtonText));

		StartCoroutine(Repository.LoadData());

		DemandBasedLoading.EverLoadedModules = !settings.DemandBasedModLoading;

		bool changeFadeTime = settings.FadeTime >= 0;

		FreeplayDevice.MAX_SECONDS_TO_SOLVE = float.MaxValue;
		FreeplayDevice.MIN_MODULE_COUNT = 1;

		// Force the graphics quality to "Mod Quality", otherwise features might not render correctly.
		QualitySettings.SetQualityLevel(2);
		AbstractPlatformUtil.Instance.OriginalQualityLevel = 2;

		if (settings.EnableModsOnlyKey)
		{
			var lastFreeplaySettings = FreeplaySettings.CreateDefaultFreeplaySettings();
			lastFreeplaySettings.OnlyMods = true;
			ProgressionManager.Instance.RecordLastFreeplaySettings(lastFreeplaySettings);
		}

		UpdateSettingWarnings();
		AdvantageousWarning.SetActive(false);

		// Setup API/properties other mods to interact with
		GameObject infoObject = new GameObject("Tweaks_Info", typeof(TweaksProperties));
		infoObject.transform.parent = gameObject.transform;

		TweaksAPI.Setup();

		// Watch the TweakSettings file for Time Mode state being changed in the office.
		FileSystemWatcher watcher = new FileSystemWatcher(Path.Combine(Application.persistentDataPath, "Modsettings"), "TweakSettings.json")
		{
			NotifyFilter = NotifyFilters.LastWrite
		};
		watcher.Changed += (object source, FileSystemEventArgs e) =>
		{
			if (ModConfig<TweakSettings>.SerializeSettings(userSettings) == ModConfig<TweakSettings>.SerializeSettings(modConfig.Read())) return;

			UpdateSettings();
			UpdateSettingWarnings();

			MainThreadQueue.Enqueue(() => StartCoroutine(ModifyFreeplayDevice(false)));
		};

		// Setup the leaderboard controller to block the leaderboard submission requests.
		LeaderboardController.Install();

		UpdateModuleTweaksAndPatches();

		// Create a fake case with a bunch of anchors to trick the game when using CaseGenerator.
		TweaksCaseGeneratorCase = new GameObject("TweaksCaseGenerator");
		TweaksCaseGeneratorCase.transform.SetParent(transform);
		var kmBomb = TweaksCaseGeneratorCase.AddComponent<KMBomb>();
		kmBomb.IsHoldable = false;
		kmBomb.WidgetAreas = new List<GameObject>();
		kmBomb.visualTransform = transform;
		kmBomb.Faces = new List<KMBombFace>();

		TweaksCaseGeneratorCase.AddComponent<ModBomb>();

		var kmBombFace = TweaksCaseGeneratorCase.AddComponent<KMBombFace>();
		kmBombFace.Anchors = new List<Transform>();
		kmBomb.Faces.Add(kmBombFace);

		for (int i = 0; i <= 9001; i++) kmBombFace.Anchors.Add(transform);

		// Handle scene changes
		UnityEngine.SceneManagement.SceneManager.sceneLoaded += (Scene scene, LoadSceneMode _) =>
		{
			UpdateSettings();
			UpdateSettingWarnings();

			Modes.settings = Modes.modConfig.Read();
			Modes.modConfig.Write(Modes.settings);

			if ((scene.name == "mainScene" || scene.name == "gameplayScene") && changeFadeTime) SceneManager.Instance.RapidFadeInTime = settings.FadeTime;

			switch (scene.name)
			{
				case "mainScene":
					if (changeFadeTime)
					{
						SceneManager.Instance.SetupState.FadeInTime =
						SceneManager.Instance.SetupState.FadeOutTime =
						SceneManager.Instance.UnlockState.FadeInTime = settings.FadeTime;
					}

					break;
				case "gameplayLoadingScene":
					var gameplayLoadingManager = FindObjectOfType<GameplayLoadingManager>();
					if (settings.InstantSkip) gameplayLoadingManager.MinTotalLoadTime = 0;
					if (changeFadeTime)
					{
						gameplayLoadingManager.FadeInTime =
						gameplayLoadingManager.FadeOutTime = settings.FadeTime;
					}

					ReflectedTypes.UpdateTypes();

					foreach(var patchType in AllModulePatches)
						Patching.EnsurePatch(patchType.Name, patchType);

					break;
				case "gameplayScene":
					if (changeFadeTime)
					{
						SceneManager.Instance.GameplayState.FadeInTime =
						SceneManager.Instance.GameplayState.FadeOutTime = settings.FadeTime;
					}

					break;
			}
		};

		// Handle state changes
		GameInfo.OnStateChange += (KMGameInfo.State state) =>
		{
			OnStateChanged(CurrentState, state);

			// Transitioning away from another state
			if (state == KMGameInfo.State.Transitioning && CurrentState != KMGameInfo.State.Gameplay)
			{
				DemandBasedLoading.HandleTransitioning();
			}

			CurrentState = state;
			watcher.EnableRaisingEvents = state == KMGameInfo.State.Setup;

			if (state == KMGameInfo.State.Gameplay)
			{
				UpdateSettings();

				if (
					AdvantageousFeaturesEnabled &&
					GameplayState.MissionToLoad != FreeplayMissionGenerator.FREEPLAY_MISSION_ID &&
					GameplayState.MissionToLoad != ModMission.CUSTOM_MISSION_ID
				)
					StartCoroutine(ShowAdvantageousWarning());

				if (AdvantageousFeaturesEnabled)
					LeaderboardController.DisableLeaderboards();

				TwitchPlaysActiveCache = TwitchPlaysActive;
				CurrentModeCache = CurrentMode;

				BombStatus.Instance.widgetsActivated = false;
				BombStatus.Instance.HUD.SetActive(settings.BombHUD != HUDMode.Off);
				BombStatus.Instance.SolvesPrefab.gameObject.SetActive(settings.BombHUD == HUDMode.On);
				BombStatus.Instance.ConfidencePrefab.gameObject.SetActive(CurrentMode != Mode.Zen);
				BombStatus.Instance.StrikesPrefab.color = CurrentMode == Mode.Time ? Color.yellow : Color.red;

				Modes.Multiplier = Modes.settings.TimeModeStartingMultiplier;
				BombStatus.Instance.UpdateMultiplier();
				bombWrappers.Clear();
				StartCoroutine(CheckForBombs());

				if (GameplayState.BombSeedToUse == -1) GameplayState.BombSeedToUse = settings.MissionSeed;
			}
			else if (state == KMGameInfo.State.Setup)
			{
				if (ReflectedTypes.LoadedModsField.GetValue(ModManager.Instance) is Dictionary<string, Mod> loadedMods)
				{
					Mod tweaksMod = loadedMods.Values.FirstOrDefault(mod => mod.ModID == "Tweaks");
					if (tweaksMod != null && setupSettings?.CaseGenerator != settings.CaseGenerator)
					{
						if (settings.CaseGenerator)
							tweaksMod.ModObjects.Add(TweaksCaseGeneratorCase);
						else
							tweaksMod.ModObjects.Remove(TweaksCaseGeneratorCase);
					}
				}

				setupSettings = modConfig.Read();

				StartCoroutine(Tips.ShowTip());
				StartCoroutine(ModifyFreeplayDevice(true));
				StartCoroutine(ModifyHoldables());
				GetComponentInChildren<ModSelectorExtension>().FindAPI();
				TweaksAPI.SetTPProperties(!TwitchPlaysActive);

				GameplayState.BombSeedToUse = -1;

				UpdateSettingWarnings();

				UpdateBombCreator();
			}
			else if (state == KMGameInfo.State.Transitioning)
			{
				// Because the settings are checked on a scene change and there is no scene change from exiting the gameplay room,
				// we need to update the settings here in case the user changed their HideTOC settings.
				UpdateSettings();

				bool modified = false;
				var ModMissionToCs = ModManager.Instance.ModMissionToCs;
				foreach (var metaData in ModMissionToCs)
					modified |= ModToCMetaData.Add(metaData);

				var unloadedMods = (Dictionary<string, Mod>) ReflectedTypes.UnloadedModsField.GetValue(ModManager.Instance);
				if (unloadedMods != null)
					foreach (var unloadedMod in unloadedMods.Values)
					{
						var tocs = (List<ModTableOfContentsMetaData>) ReflectedTypes.TocsField.GetValue(unloadedMod);
						if (tocs != null)
							foreach (var metaData in tocs)
								modified |= ModToCMetaData.Remove(metaData);
					}

				var newToCs = ModToCMetaData.Where(metaData => !settings.HideTOC.Any(pattern => Localization.GetLocalizedString(metaData.DisplayNameTerm).Like(pattern)));
				modified |= newToCs.Count() != ModMissionToCs.Count || !newToCs.All(ModMissionToCs.Contains);
				ModMissionToCs.Clear();
				ModMissionToCs.AddRange(newToCs);

				if (modified)
				{
					SetupState.LastBombBinderTOCIndex = 0;
					SetupState.LastBombBinderTOCPage = 0;
				}
			}
		};
	}

	private void UpdateModuleTweaksAndPatches()
	{
		var Types = Assembly.GetExecutingAssembly().GetTypes();
		AllTweaks = Types.Where(type => typeof(Tweak).IsAssignableFrom(type) && !type.IsAbstract)
			.Select(type => (Tweak) Activator.CreateInstance(type)).ToArray();
		AllModulePatches = Types.Where(type => type.GetCustomAttributes(typeof(ModulePatchAttribute), true).Length > 0)
			.ToArray();
	}

	// TODO: Remove this
	/*
	Vector2 scrollPosition;
	Vector2 scrollPosition2;
	GameObject inspecting;
	Dictionary<GameObject, bool> ExpandedObjects = new Dictionary<GameObject, bool>();
	void OnGUI()
	{
		GUILayout.BeginHorizontal();
		scrollPosition = GUILayout.BeginScrollView(scrollPosition);
		foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
		{
			DisplayChildren(root);
		}
		GUILayout.EndScrollView();

		if (inspecting)
		{
			scrollPosition2 = GUILayout.BeginScrollView(scrollPosition2);
			foreach (Component component in inspecting.GetComponents<Component>())
			{
				GUILayout.Label(component.GetType().Name);
				foreach (System.Reflection.FieldInfo fieldInfo in component.GetType().GetFields())
				{
					if (typeof(Array).IsAssignableFrom(fieldInfo.FieldType))
					{
						try
						{
							foreach (object obj in (Array) fieldInfo.GetValue(component))
							{
								GUILayout.Label("    * " + obj);
							}
						} catch { }
					}
					else
					{
						GUILayout.Label(" - " + fieldInfo.Name + " = " + fieldInfo.GetValue(component));
					}
				}
			}
			GUILayout.EndScrollView();
		}
		GUILayout.EndHorizontal();
	}

	void DisplayChildren(GameObject gameObj)
	{
		GUILayout.BeginHorizontal();
		ExpandedObjects.TryGetValue(gameObj, out bool expanded);
		expanded = GUILayout.Toggle(expanded, gameObj.name + (gameObj.activeInHierarchy ? " [E]" : " [X]"));
		ExpandedObjects[gameObj] = expanded;

		if (GUILayout.Button("Inspect")) inspecting = gameObj;
		GUILayout.EndHorizontal();

		if (!expanded) return;

		GUILayout.BeginHorizontal();
		GUILayout.Space(10);
		GUILayout.BeginVertical();
		foreach (Transform child in gameObj.transform)
		{
			GameObject childObj = child.gameObject;

			DisplayChildren(childObj);
		}

		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
	}*/

	public static List<BombWrapper> bombWrappers = new List<BombWrapper>();

	public IEnumerator CheckForBombs()
	{
		yield return new WaitUntil(() => SceneManager.Instance.GameplayState.Bombs?.Count > 0);
		yield return null;
		List<Bomb> bombs = SceneManager.Instance.GameplayState.Bombs;

		if (settings.SkipGameplayDelay) StartCoroutine(SkipGameplayDelay());

		LogJSON("LFAEvent", new Dictionary<string, object>()
		{
			{ "type", "ROUND_START" },
			{ "mission", Localization.GetLocalizedString(SceneManager.Instance.GameplayState.Mission.DisplayNameTerm) },
			{ "missionId", SceneManager.Instance.GameplayState.Mission.ID },
		});

		var snoozeButton = FindObjectOfType<AlarmClock>()?.SnoozeButton;
		if (snoozeButton != null)
			FixKeypadButtons(snoozeButton);

		void wrapInitialBombs()
		{
			for (int i = 0; i < bombs.Count; i++)
			{
				Bomb bomb = bombs[i];
				BombWrapper bombWrapper = bomb.gameObject.AddComponent<BombWrapper>();
				bombWrappers.Add(bombWrapper);

				if (CurrentMode == Mode.Time) bombWrapper.CurrentTimer = Modes.settings.TimeModeStartingTime * 60;
				else if (CurrentMode == Mode.Zen) bombWrapper.CurrentTimer = 0.001f;
			}
		}

		if (CurrentModeCache == Mode.Zen)
		{
			GameplayMusicController gameplayMusic = MusicManager.Instance.GameplayMusicController;
			gameplayMusic.StopMusic();
			var controller = gameplayMusic.GetComponent<DarkTonic.MasterAudio.PlaylistController>();
			controller.ClearQueue();
			controller.QueuePlaylistClip(controller.CurrentPlaylist.MusicSettings[0].songName, true);
		}

		if (ReflectedTypes.FactoryRoomType != null && ReflectedTypes.StaticModeType != null)
		{
			UnityEngine.Object factoryRoom = FindObjectOfType(ReflectedTypes.FactoryRoomType);
			if (factoryRoom)
			{
				if (ReflectedTypes.FactoryRoomDataType != null && ReflectedTypes.WarningTimeField != null)
				{
					var roomData = FindObjectOfType(ReflectedTypes.FactoryRoomDataType);
					if (roomData != null)
						ReflectedTypes.WarningTimeField.SetValue(roomData, CurrentMode == Mode.Zen ? 0 : 60);
				}

				object gameMode = ReflectedTypes.GameModeProperty.GetValue(factoryRoom, null);
				if (ReflectedTypes.StaticModeType != gameMode.GetType())
				{
					IEnumerable<object> adaptations = ((IEnumerable) ReflectedTypes.AdaptationsProperty.GetValue(gameMode, null)).Cast<object>();
					bool globalTimerDisabled = !adaptations.Any(adaptation => ReflectedTypes.GlobalTimerAdaptationType.IsAssignableFrom(adaptation.GetType()));

					Component getBomb() => (Component) ReflectedTypes._CurrentBombField.GetValue(gameMode);

					yield return new WaitUntil(() => getBomb() != null || factoryRoom == null);
					Component currentBomb = getBomb();
					bool firstBomb = true;

					while (currentBomb != null && factoryRoom != null)
					{
						BombWrapper bombWrapper = currentBomb.gameObject.AddComponent<BombWrapper>();
						bombWrappers.Add(bombWrapper);

						if (globalTimerDisabled || firstBomb)
						{
							firstBomb = false;

							if (CurrentMode == Mode.Time)
								bombWrapper.CurrentTimer = Modes.settings.TimeModeStartingTime * 60;
							else if (CurrentMode == Mode.Zen)
								bombWrapper.CurrentTimer = 0.001f;
						}

						yield return new WaitUntil(() => currentBomb != getBomb() || factoryRoom == null);

						bombWrappers.Remove(bombWrapper);
						currentBomb = getBomb();

						if (currentBomb == null || factoryRoom == null) break;
					}
				}
				else
				{
					wrapInitialBombs();
				}

				yield break;
			}
		}

		// This code only runs if we aren't in the Factory room.
		wrapInitialBombs();

		// If TP is enabled, let it handle managing the emergency lights.
		if (TwitchPlaysActiveCache)
			yield break;

		SceneManager.Instance.GameplayState.Room.PacingActions.RemoveAll(pacingAction => pacingAction.EventType == Assets.Scripts.Pacing.PaceEvent.OneMinuteLeft);
		UnityEngine.Object portalRoom = null;
		if (ReflectedTypes.PortalRoomType != null && ReflectedTypes.RedLightsMethod != null && ReflectedTypes.RoomLightField != null)
		{
			portalRoom = FindObjectOfType(ReflectedTypes.PortalRoomType);
		}

		bool lastState = false;
		IEnumerator portalEmergencyRoutine = null;
		while (CurrentState == KMGameInfo.State.Gameplay)
		{
			bool targetState = CurrentModeCache != Mode.Zen;
			targetState &= bombWrappers.Any((BombWrapper bombWrapper) => bombWrapper.CurrentTimer < 60f && !bombWrapper.Bomb.IsSolved());
			if (targetState != lastState)
			{
				foreach (EmergencyLight emergencyLight in FindObjectsOfType<EmergencyLight>())
				{
					if (targetState)
					{
						emergencyLight.Activate();
					}
					else
					{
						emergencyLight.Deactivate();
					}
				}
				if (portalRoom != null)
				{
					if (targetState)
					{
						portalEmergencyRoutine = (IEnumerator) ReflectedTypes.RedLightsMethod.Invoke(portalRoom, null);
						StartCoroutine(portalEmergencyRoutine);
					}
					else
					{
						StopCoroutine(portalEmergencyRoutine);
						portalEmergencyRoutine = null;
						((GameObject) ReflectedTypes.RoomLightField.GetValue(portalRoom)).GetComponent<Light>().color = new Color(0.5f, 0.5f, 0.5f);
					}
				}
				lastState = targetState;
			}
			yield return null;
		}
	}

	public IEnumerator SkipGameplayDelay()
	{
		yield return null;
		Time.timeScale = 100;
		yield return new WaitForSeconds(6);
		Time.timeScale = 1;
	}

	IEnumerator ShowAdvantageousWarning()
	{
		AdvantageousWarning.SetActive(true);

		StartCoroutine(FlashAdvantageousWarning());

		var canvasGroup = AdvantageousWarning.GetComponent<CanvasGroup>();
		foreach (float alpha in (0.75f).TimedAnimation().EaseCubic())
		{
			canvasGroup.alpha = alpha;
			yield return null;
		}

		var startTime = Time.time;
		while ((Time.time - startTime < 4 || CurrentState == KMGameInfo.State.Transitioning) && CurrentState != KMGameInfo.State.Setup)
			yield return null;

		foreach (float alpha in (0.75f).TimedAnimation().EaseCubic())
		{
			canvasGroup.alpha = 1 - alpha;
			yield return null;
		}

		AdvantageousWarning.SetActive(false);
	}

	IEnumerator FlashAdvantageousWarning()
	{
		var startTime = Time.time;
		var textComponent = AdvantageousWarning.Traverse<Text>("WarningText");
		while (AdvantageousWarning.activeSelf)
		{
			float alpha = Time.time - startTime;
			float scaledAlpha = (Mathf.Cos(alpha / 3 * 2 * Mathf.PI - Mathf.PI) + 1) / 2; // Goes smoothly between 0 and 1.

			textComponent.color = Color.Lerp(Color.white, Color.red, scaledAlpha);

			yield return null;
		}
	}

	static float originalTime = 300;
	public static IEnumerator ModifyFreeplayDevice(bool firstTime)
	{
		yield return null;
		SetupRoom setupRoom = FindObjectOfType<SetupRoom>();
		if (setupRoom)
		{
			FreeplayDevice freeplayDevice = setupRoom.FreeplayDevice;
			if (!freeplayDevice.gameObject.activeInHierarchy) yield break;

			ExecOnDescendants(freeplayDevice.gameObject, gameObj =>
			{
				string gameObjName = gameObj.name;
				if (gameObjName == "FreeplayLabel" || gameObjName == "Free Play Label")
					gameObj.GetComponent<TMPro.TextMeshPro>().text = CurrentMode == Mode.Normal ? Localization.GetLocalizedString($"FreeplayDevice/label_free{(gameObjName == "FreeplayLabel" ? "playInnerTitle" : "PlayCover")}") : $"{CurrentMode} mode";
			});

			freeplayDevice.CurrentSettings.Time = CurrentMode == Mode.Time ? Modes.settings.TimeModeStartingTime * 60 : originalTime;
			TimeSpan timeSpan = TimeSpan.FromSeconds(freeplayDevice.CurrentSettings.Time);
			freeplayDevice.TimeText.text = string.Format("{0}:{1:00}", (int) timeSpan.TotalMinutes, timeSpan.Seconds);

			if (!firstTime) yield break;
			if (CurrentMode == Mode.Normal) originalTime = freeplayDevice.CurrentSettings.Time;

			freeplayDevice.TimeIncrement.OnPush += delegate { ReflectedTypes.IsInteractingField.SetValue(freeplayDevice.TimeIncrement, true); };
			freeplayDevice.TimeIncrement.OnInteractEnded += delegate
			{
				originalTime = freeplayDevice.CurrentSettings.Time;
				if (CurrentMode != Mode.Time) return;

				Modes.settings.TimeModeStartingTime = freeplayDevice.CurrentSettings.Time / 60;
				Modes.modConfig.Write(Modes.settings);
			};

			freeplayDevice.TimeDecrement.OnPush += delegate { ReflectedTypes.IsInteractingField.SetValue(freeplayDevice.TimeDecrement, true); };
			freeplayDevice.TimeDecrement.OnInteractEnded += delegate
			{
				originalTime = freeplayDevice.CurrentSettings.Time;
				if (CurrentMode != Mode.Time) return;

				Modes.settings.TimeModeStartingTime = freeplayDevice.CurrentSettings.Time / 60;
				Modes.modConfig.Write(Modes.settings);
			};
		}
	}

	public static IEnumerator ModifyHoldables()
	{
		yield return null;

		var holdableSettings = settings.Holdables;
		var holdables = SceneManager.Instance.SetupState
			.GetValue<SetupRoom>("setupRoom")
			.GetComponentsInChildren<FloatingHoldable>()
			.ToDictionary(holdable => holdable.name.Replace("(Clone)", "").Replace("Holdable", ""), holdable => holdable);
		var holdableSpots = holdables.ToDictionary(pair => pair.Key, pair => pair.Key);

		foreach (var pair in holdables)
		{
			var name = pair.Key;
			var holdable = pair.Value;
			if (!holdableSettings.ContainsKey(name))
			{
				userSettings.Holdables.Add(name, true);
				holdableSettings.Add(name, true);
				UpdateSettings(false);
			}

			Dictionary<string, Vector3> positionOffsets = new Dictionary<string, Vector3>()
			{
				{ "FreeplayDevice", new Vector3(0.1f, 0.0075f, -0.15f) }
			};

			Dictionary<string, Vector3> rotationOffsets = new Dictionary<string, Vector3>()
			{
				{ "FreeplayDevice", new Vector3(0, 90, -5) },
				{ "ModManager", new Vector3(0, 90, 0) }
			};

			switch (holdableSettings[name])
			{
				case bool active:
					holdable.gameObject.SetActive(active);
					break;
				case string originalTargetName when holdables.ContainsKey(originalTargetName):
					var targetName = holdableSpots[originalTargetName];
					var targetHoldable = holdables[targetName];

					var oldPosition = holdable.transform.position;
					var oldRotation = holdable.transform.rotation.eulerAngles;
					var newRotation = targetHoldable.transform.rotation.eulerAngles;

					positionOffsets.TryGetValue(targetName, out Vector3 oldPosOffset);
					positionOffsets.TryGetValue(name, out Vector3 newPosOffset);

					rotationOffsets.TryGetValue(targetName, out Vector3 oldRotOffset);
					rotationOffsets.TryGetValue(name, out Vector3 newRotOffset);

					holdable.transform.position = targetHoldable.transform.position + oldPosOffset - newPosOffset;
					holdable.transform.rotation = Quaternion.Euler(newRotation + oldRotOffset - newRotOffset);
					holdable.OrigPosition = holdable.transform.position;
					holdable.OrigRotation = holdable.transform.rotation;

					targetHoldable.transform.position = oldPosition - oldPosOffset + newPosOffset;
					targetHoldable.transform.rotation = Quaternion.Euler(oldRotation + newRotOffset - oldRotOffset);
					targetHoldable.OrigPosition = targetHoldable.transform.position;
					targetHoldable.OrigRotation = targetHoldable.transform.rotation;

					holdableSpots[name] = targetName;
					holdableSpots[targetName] = name;

					break;
				default:
					Instance.OnRead(new Exception($"Unexpected value for holdable {name}: {holdableSettings[name]}"));
					break;
			}
		}
	}

	internal static void FixKeypadButtons(params KeypadButton[] buttons) => FixKeypadButtons((IEnumerable<KeypadButton>) buttons);
	internal static void FixKeypadButtons(IEnumerable<KeypadButton> buttons)
	{
		foreach (var button in buttons)
		{
			if (button.ButtonHeightOverride == 0)
			{
				button.ButtonHeightOverride = (float) ReflectedTypes.KeypadButtonHeightField.GetValue(button);
			}
		}
	}

	public static void UpdateSettings(bool readSettings = true)
	{
		if (readSettings)
			userSettings = modConfig.Read();

		modConfig.Write(userSettings); // Write any settings that the user doesn't have in their settings file.

		// Apply overrides
		settings = modConfig.Read();

		bool mission = settings.DisableAdvantageous == AdvantageousMode.Missions &&
			CurrentState == KMGameInfo.State.Gameplay &&
			!GameplayState.MissionToLoad.EqualsAny(FreeplayMissionGenerator.FREEPLAY_MISSION_ID, ModMission.CUSTOM_MISSION_ID);
		if (settings.DisableAdvantageous == AdvantageousMode.On || mission)
		{
			settings.BombHUD = settings.BombHUD == HUDMode.On ? HUDMode.Partial : settings.BombHUD;
			settings.MissionSeed = -1;
			settings.Mode = Mode.Normal;
			settings.ShowEdgework = false;
		}

		UpdateBombCreator();
	}

	private static void UpdateBombCreator()
	{
		var bombCreator = GameObject.Find("BombCreator(Clone)");
		if (bombCreator == null)
			return;

		bombCreator.Traverse("TimeSetting", "TwitchModeButton").SetActive(TwitchPlaysActive || settings.DisableAdvantageous != AdvantageousMode.On);
	}

	public void Update()
	{
		MainThreadQueue.ProcessQueue();

		if (CurrentState == KMGameInfo.State.Setup && SettingWarningEnabled && Input.GetKeyDown(KeyCode.F2))
		{
			StartCoroutine(DemandBasedLoading.EnterAndLeaveModManager());
		}
	}

	public void UpdateSettingWarnings() => MainThreadQueue.Enqueue(() =>
	{
		if (SettingWarning == null || setupSettings == null)
			return;

		var changedSettings = new List<string>();
		if (setupSettings.DemandBasedModLoading != settings.DemandBasedModLoading) changedSettings.Add("DemandBasedModLoading");
		if (setupSettings.ReplaceObsoleteMods != settings.ReplaceObsoleteMods && settings.ReplaceObsoleteMods) changedSettings.Add("ReplaceObsoleteMods");
		if (setupSettings.SubscribeToNewMods != settings.SubscribeToNewMods && settings.SubscribeToNewMods) changedSettings.Add("SubscribeToNewMods");
		if (setupSettings.CaseGenerator != settings.CaseGenerator) changedSettings.Add("CaseGenerator");
		if (setupSettings.Holdables.Count != settings.Holdables.Count || setupSettings.Holdables.Except(settings.Holdables).Any()) changedSettings.Add("Holdables");

		if (CurrentState != KMGameInfo.State.Setup || !modConfig.SuccessfulRead || changedSettings.Count == 0)
		{
			SettingWarning.SetActive(false);
			return;
		}

		var settingsSentence = new System.Text.StringBuilder();
		for (int i = 0; i < changedSettings.Count; i++)
		{
			settingsSentence.Append(changedSettings[i]);

			if (i != changedSettings.Count - 1)
				settingsSentence.Append(changedSettings.Count != 2 ? ", " : " ");

			if (i == changedSettings.Count - 2)
				settingsSentence.Append("and ");
		}

		SettingWarning.SetActive(true);
		SettingWarning.Traverse<Text>("WarningText").text = $"The change to the {(changedSettings.Count == 1 ? "setting" : "settings")} {settingsSentence} will only take effect once you re-enter the setup room. <i>Press F2 to do that automatically!</i>";
	});

	public static bool SettingWarningEnabled => SettingWarning.activeSelf;

	private void OnRead(Exception exception)
	{
		var invalidSettings = gameObject.Traverse("UI", "InvalidSettings");
		invalidSettings.SetActive(exception != null);

		if (exception != null)
		{
			var error = exception.Message;

			Dictionary<string, string> replacements = new Dictionary<string, string>()
			{
				{ @"([A-Z][a-z]+\.){1,}", "" },
				{ @"List`1\[(.+)\]", "$1 Array" },
				{ @"\. ", "\n" }
			};

			foreach (var replacement in replacements)
				error = Regex.Replace(error, replacement.Key, replacement.Value);

			invalidSettings.Traverse<Text>("Details", "Text").text = error;
		}

		// Update the status of any Tweaks, their setting might have been changed.
		foreach (var tweak in AllTweaks)
		{
			tweak.UpdateEnabled();
		}
	}

	public void OnApplicationQuit()
	{
		string logName = Application.platform == RuntimePlatform.WindowsPlayer ? "output_log.txt" : "Player.log";
		string newLogName = Application.platform == RuntimePlatform.WindowsPlayer ? "output_log_2.txt" : "Player_2.log";

		string path = Path.Combine(Application.persistentDataPath, logName);
		if (!File.Exists(path))
		{
			Log("Unable to save output log since it couldn't be found.");
			return;
		}

		File.Copy(
			path,
			Path.Combine(Application.persistentDataPath, newLogName),
			true
		);
	}

	public static void FixRNGSeed()
	{
		// The game sets the seed to 33 for some reason, so we have to set the seed so it doesn't pick the same values every time.
		UnityEngine.Random.InitState((int) DateTime.Now.Ticks);
	}

	public static void Log(params object[] args) => Debug.Log("[Tweaks] " + args.Select(Convert.ToString).Join(" "));

	public static void LogJSON(string tag, object json)
	{
		string[] chunks = Newtonsoft.Json.JsonConvert.SerializeObject(json).ChunkBy(250).ToArray();
		Log(tag, chunks.Length + "\n" + chunks.Join("\n"));
	}

	static void ExecOnDescendants(GameObject gameObj, Action<GameObject> func)
	{
		foreach (Transform child in gameObj.transform)
		{
			GameObject childObj = child.gameObject;
			func(childObj);

			ExecOnDescendants(childObj, func);
		}
	}

	void LogChildren(Transform goTransform, int depth = 0)
	{
		Log($"{new string('\t', depth)}{goTransform.name} - {goTransform.localPosition:N6}");
		foreach (Transform child in goTransform)
		{
			LogChildren(child, depth + 1);
		}
	}

	public static Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
	{
		new Dictionary<string, object>
		{
			{ "Filename", "TweakSettings.json" },
			{ "Name", "Tweaks" },
			{ "Listings", new List<Dictionary<string, object>>
				{
					new Dictionary<string, object> { { "Text", "Gameplay" }, { "Type", "Section" } },
					new Dictionary<string, object>
					{
						{ "Key", "Mode" },
						{ "Description", "Sets the mode for the next round." },
						{ "Type", "Dropdown" },
						{ "DropdownItems", new List<object> { "Normal", "Time", "Zen", "Steady" } }
					},
					new Dictionary<string, object> {
						{ "Key", "DisableAdvantageous" },
						{ "Text", "Disable Advantageous Features" },
						{ "Description", "Disables advantageous features like the Bomb\nHUD, Show Edgework, custom Modes, etc." },
						{ "Type", "Dropdown" },
						{ "DropdownItems", new List<object> { "Off", "Missions", "On" } }
					},
					new Dictionary<string, object> { { "Key", "MissionSeed" }, { "Text", "Mission Seed" }, { "Description", "Seeds the random numbers for the mission which should make the bomb\ngenerate consistently." } },

					new Dictionary<string, object> { { "Text", "HUDs" }, { "Type", "Section" } },
					new Dictionary<string, object> {
						{ "Key", "BombHUD" },
						{ "Text", "Bomb HUD" },
						{ "Description", "Adds a HUD in the top right corner showing information about the currently\nselected bomb." },
						{ "Type", "Dropdown" },
						{ "DropdownItems", new List<object> { "Off", "Partial", "On" } }
					},
					new Dictionary<string, object> { { "Key", "ShowEdgework" }, { "Text", "Show Edgework" }, { "Description", "Adds a HUD to the top of the screen showing the edgework for the currently selected bomb." } },

					new Dictionary<string, object> { { "Text", "Cases" }, { "Type", "Section" } },
					new Dictionary<string, object> { { "Key", "BetterCasePicker" }, { "Text", "Better Case Picker" }, { "Description", "Chooses the smallest case that fits instead of a random one." } },
					new Dictionary<string, object> { { "Key", "CaseGenerator" }, { "Text", "Case Generator" }, { "Description", "Generates a case to best fit the bomb which can be one of the colors defined by CaseColors." } },
					new Dictionary<string, object> { { "Key", "CaseColors" }, { "Text", "Case Colors" }, { "Description", "Controls the color of the cases that are generated with Case Generator." } },

					new Dictionary<string, object> { { "Text", "Tweaks" }, { "Type", "Section" } },
					new Dictionary<string, object> { { "Key", "HideTOC" }, { "Text", "Hide TOC" }, { "Description", "Hides table of contents entries based on patterns." } },
					new Dictionary<string, object> { { "Key", "EnableModsOnlyKey" }, { "Text", "Enable Mods Only Key" }, { "Description", "Turns the Mods Only key to be on by default." } },
					new Dictionary<string, object> { { "Key", "FadeTime" }, { "Text", "Fade Time" }, { "Description", "The number seconds should it take to fade in and out of scenes." } },
					new Dictionary<string, object> { { "Key", "InstantSkip" }, { "Text", "Instant Skip" }, { "Description", "Skips the gameplay loading screen as soon as possible." } },
					new Dictionary<string, object> { { "Key", "SkipGameplayDelay" }, { "Text", "Skip Gameplay Delay" }, { "Description", "Skips the delay at the beginning of a round when the lights are out." } },
					new Dictionary<string, object> { { "Key", "ModuleTweaks" }, { "Text", "Module Tweaks" }, { "Description", "Controls all module related tweaks like fixing status light positions." } },
					new Dictionary<string, object> { { "Key", "ShowTips" }, { "Text", "Show Tips" }, { "Description", "Shows tips about Tweaks features that you may not know about." } },
					new Dictionary<string, object> { { "Key", "PinnedSettings" }, { "Type", "Hidden" } },

					new Dictionary<string, object> { { "Text", "Mod Management" }, { "Type", "Section" } },
					new Dictionary<string, object> { { "Key", "ReplaceObsoleteMods" }, { "Text", "Replace Obsolete Mods" }, { "Description", "Replaces obsolete mods with their updated version." } },
					new Dictionary<string, object> { { "Key", "SubscribeToNewMods" }, { "Text", "Subscribe to New Mods" }, { "Description", "Subscribes to all new modules that are on the repository." } },
					new Dictionary<string, object> { { "Key", "DemandBasedModLoading" }, { "Text", "Demand-based Mod Loading" }, { "Description", "Load only the modules on a bomb instead of loading all of them when starting up." } },
					new Dictionary<string, object> { { "Key", "DemandModLimit" }, { "Text", "Demand Mod Limit" }, { "Description", "Sets the limit of how many mods will be kept loaded after the bomb\nis over. Negative numbers will keep all mods loaded." } },
					new Dictionary<string, object> { { "Key", "DemandBasedModsExcludeList" }, { "Text", "Exclude Demand-based Mods" }, { "Description", "Exclude mods from being loaded on demand based on module name." } },
					new Dictionary<string, object> { { "Key", "ExcludeModuleMissions" }, { "Text", "Exclude Module Missions" }, { "Description", "Automatically adds mods that have missions to the DBML exclude list." } },
					new Dictionary<string, object> { { "Key", "ManageHarmonyMods" }, { "Text", "Manage Harmony Mods" }, { "Description", "Enables the Harmony mod manager when loading mods." } },
					new Dictionary<string, object> { { "Key", "LocalMods" }, { "Text", "Local Mods" }, { "Description", "Loads a mod from Steam when running the game locally." } },
				}
			}
		},
		new Dictionary<string, object>
		{
			{ "Filename", "ModeSettings.json" },
			{ "Name", "Mode Settings" },
			{ "Listings", new List<Dictionary<string, object>>
				{
					new Dictionary<string, object> { { "Text", "Zen Mode" }, { "Type", "Section" } },
					new Dictionary<string, object> { { "Key", "ZenModeTimePenalty" }, { "Text", "Time Penalty" }, { "Description", "The base amount of minutes to be penalized for getting a strike." } },
					new Dictionary<string, object> { { "Key", "ZenModeTimePenaltyIncrease" }, { "Text", "Time Penalty Increase" }, { "Description", "The number of minutes to add to the penalty each time you get\na strike after the first." } },
					new Dictionary<string, object> { { "Key", "ZenModeTimerSpeedUp" }, { "Text", "Timer Speed Up" }, { "Description", "The rate the timer speeds up when you get a strike." } },
					new Dictionary<string, object> { { "Key", "ZenModeTimerMaxSpeed" }, { "Text", "Timer Max Speed" }, { "Description", "The maximum rate the timer can be set to.\nFor example, 2 is twice as fast as the normal timer." } },

					new Dictionary<string, object> { { "Text", "Steady Mode" }, { "Type", "Section" } },
					new Dictionary<string, object> { { "Key", "SteadyModeFixedPenalty" }, { "Text", "Fixed Penalty" }, { "Description", "The number of minutes subtracted from the time when you get a strike." } },
					new Dictionary<string, object> { { "Key", "SteadyModePercentPenalty" }, { "Text", "Percent Penalty" }, { "Description", "The factor of the starting time the remaining time is reduced by." } },

					new Dictionary<string, object> { { "Text", "Time Mode" }, { "Type", "Section" } },
					new Dictionary<string, object> { { "Key", "TimeModeStartingTime" }, { "Text", "Starting Time" }, { "Description", "The number of minutes on the timer when you start a bomb." } },
					new Dictionary<string, object> { { "Key", "TimeModeStartingMultiplier" }, { "Text", "Starting Multiplier" }, { "Description", "The initial multiplier." } },
					new Dictionary<string, object> { { "Key", "TimeModeMaxMultiplier" }, { "Text", "Max Multiplier" }, { "Description", "The highest the multiplier can go." } },
					new Dictionary<string, object> { { "Key", "TimeModeMinMultiplier" }, { "Text", "Min Multiplier" }, { "Description", "The lowest the multiplier can go." } },
					new Dictionary<string, object> { { "Key", "TimeModeSolveBonus" }, { "Text", "Solve Bonus" }, { "Description", "The amount added to the multiplier when you solve a module." } },
					new Dictionary<string, object> { { "Key", "TimeModeMultiplierStrikePenalty" }, { "Text", "Multiplier Strike Penalty" }, { "Description", "The amount subtracted from the multiplier when you get a\nstrike." } },
					new Dictionary<string, object> { { "Key", "TimeModeTimerStrikePenalty" }, { "Text", "Timer Strike Penalty" }, { "Description", "The factor the time is reduced by when getting a strike." } },
					new Dictionary<string, object> { { "Key", "TimeModeMinimumTimeLost" }, { "Text", "Min Time Lost" }, { "Description", "Lowest amount of time that you can lose when you get a strike." } },
					new Dictionary<string, object> { { "Key", "TimeModeMinimumTimeGained" }, { "Text", "Min Time Gained" }, { "Description", "Lowest amount of time you can gain when you solve a module." } },
					new Dictionary<string, object> { { "Key", "TimeModePointMultiplier" }, { "Text", "Point Multiplier" }, { "Description", "The multiplier for the number of points you get for solving a module." } },
				}
			}
		}
	};
}

enum HUDMode
{
	Off,
	Partial,
	On,
}

enum AdvantageousMode
{
	Off,
	Missions,
	On,
}

#pragma warning disable CS0649
class TweakSettings
{
	public float FadeTime = 1f;
	public bool InstantSkip = true;
	public bool ManageHarmonyMods = false;
	public bool SkipGameplayDelay = false;
	public bool BetterCasePicker = true;
	public bool EnableModsOnlyKey = false;
	public bool DemandBasedModLoading = false;
	public List<string> DemandBasedModsExcludeList = new List<string>();
	public int DemandModLimit = -1;
	public bool ExcludeModuleMissions = false;
	public bool ReplaceObsoleteMods = true;
	public bool SubscribeToNewMods;
	public HUDMode BombHUD = HUDMode.Off;
	public bool ShowEdgework = false;
	public AdvantageousMode DisableAdvantageous = AdvantageousMode.Off;
	public bool ShowTips = true;
	public List<string> HideTOC = new List<string>();
	public Mode Mode = Mode.Normal;
	public int MissionSeed = -1;
	public bool CaseGenerator = true;
	public bool ModuleTweaks = true;
	public List<string> CaseColors = new List<string>();
	public Dictionary<string, object> Holdables = new Dictionary<string, object>();
	[JsonConverter(typeof(LocalModsConverter))]
	public List<string> LocalMods = new List<string>();
	public HashSet<string> PinnedSettings = new HashSet<string>();
}
