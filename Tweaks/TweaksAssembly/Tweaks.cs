using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Assets.Scripts.Progression;
using Assets.Scripts.Settings;
using Assets.Scripts.BombBinder;
using Assets.Scripts.Mods.Mission;
using Assets.Scripts.Leaderboards;
using Assets.Scripts.Services.Steam;

[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
class Tweaks : MonoBehaviour
{
	public static ModConfig<TweakSettings> modConfig = new ModConfig<TweakSettings>("TweakSettings");
	public static TweakSettings settings = modConfig.Settings;

	public static bool TwitchPlaysActive => GameObject.Find("TwitchPlays_Info") != null;
	public static Mode CurrentMode => TwitchPlaysActive ? Mode.Normal : settings.Mode;

	public static KMGameInfo GameInfo;
	[HideInInspector]
	public KMGameInfo.State CurrentState;

	private readonly HashSet<TableOfContentsMetaData> ModToCMetaData = new HashSet<TableOfContentsMetaData>();

	void Awake()
	{
		GameInfo = GetComponent<KMGameInfo>();

		modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.

		bool changeFadeTime = settings.FadeTime >= 0;

		FreeplayDevice.MAX_SECONDS_TO_SOLVE = float.MaxValue;
		FreeplayDevice.MIN_MODULE_COUNT = 1;

		if (settings.EnableModsOnlyKey)
		{
			var lastFreeplaySettings = FreeplaySettings.CreateDefaultFreeplaySettings();
			lastFreeplaySettings.OnlyMods = true;
			ProgressionManager.Instance.RecordLastFreeplaySettings(lastFreeplaySettings);
		}

		// Setup API/properties other mods to interact with
		GameObject infoObject = new GameObject("Tweaks_Info", typeof(TweaksProperties));
		infoObject.transform.parent = gameObject.transform;

		// Watch the TweakSettings file for Time Mode state being changed in the office.
		FileSystemWatcher watcher = new FileSystemWatcher(Path.Combine(Application.persistentDataPath, "Modsettings"), "TweakSettings.json")
		{
			NotifyFilter = NotifyFilters.LastWrite
		};
		watcher.Changed += (object source, FileSystemEventArgs e) =>
		{
			if (settings.Equals(modConfig.Settings)) return;

			UpdateSettings();

			StartCoroutine(ModifyFreeplayDevice(false));
		};

		// Setup our "service" to block the leaderboard submission requests
		ReflectedTypes.InstanceField.SetValue(null, new SteamFilterService());

		UnityEngine.SceneManagement.SceneManager.sceneLoaded += (Scene scene, LoadSceneMode _) =>
		{
			UpdateSettings();

			Modes.settings = Modes.modConfig.Settings;
			Modes.modConfig.Settings = Modes.settings;

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

					ReflectedTypes.CurrencyAPIEndpointField?.SetValue(null, settings.FixFER ? "http://api.exchangeratesapi.io" : "http://api.fixer.io");

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

		GameInfo.OnStateChange += (KMGameInfo.State state) =>
		{
			CurrentState = state;
			watcher.EnableRaisingEvents = state == KMGameInfo.State.Setup;

			if (state == KMGameInfo.State.Gameplay)
			{
				bool disableRecords = settings.BombHUD || settings.ShowEdgework || CurrentMode != Mode.Normal;

				Assets.Scripts.Stats.StatsManager.Instance.DisableStatChanges =
				Assets.Scripts.Records.RecordManager.Instance.DisableBestRecords = disableRecords;
				if (disableRecords) SteamFilterService.TargetMissionID = GameplayState.MissionToLoad;

				if (settings.BetterCasePicker) BetterCasePicker.PickCase();

				BombStatus.Instance.widgetsActivated = false;
				BombStatus.Instance.HUD.SetActive(settings.BombHUD);
				BombStatus.Instance.Edgework.SetActive(settings.ShowEdgework);
				BombStatus.Instance.ConfidencePrefab.gameObject.SetActive(CurrentMode != Mode.Zen);

				Modes.Multiplier = Modes.settings.TimeModeStartingMultiplier;
				bombWrappers = new BombWrapper[] { };
				StartCoroutine(CheckForBombs());
			}
			else if (state == KMGameInfo.State.Setup)
			{
				StartCoroutine(ModifyFreeplayDevice(true));
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

				var newToCs = ModToCMetaData.Where(metaData => !settings.HideTOC.Any(pattern => metaData.DisplayName.Like(pattern)));
				modified |= (newToCs.Count() != ModMissionToCs.Count || !newToCs.All(ModMissionToCs.Contains));
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

	public static BombWrapper[] bombWrappers = new BombWrapper[] { };

	public IEnumerator CheckForBombs()
	{
		yield return new WaitUntil(() => (SceneManager.Instance.GameplayState.Bombs?.Count > 0));
		List<Bomb> bombs = SceneManager.Instance.GameplayState.Bombs;

		void wrapInitialBombs()
		{
			Array.Resize(ref bombWrappers, bombs.Count);

			for (int i = 0; i < bombs.Count; i++)
			{
				Bomb bomb = bombs[i];
				BombWrapper bombWrapper = new BombWrapper(bomb);
				bombWrappers[i] = bombWrapper;
				bombWrapper.holdable.OnLetGo += () => BombStatus.Instance.currentBomb = null;

				if (CurrentMode == Mode.Time) bombWrapper.CurrentTimer = Modes.settings.TimeModeStartingTime * 60;
				else if (CurrentMode == Mode.Zen) bombWrapper.CurrentTimer = 0.001f;
			}
		}

		if (CurrentMode == Mode.Zen)
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

					Array.Resize(ref bombWrappers, 1);

					while (currentBomb != null && factoryRoom != null)
					{
						BombWrapper bombWrapper = new BombWrapper(currentBomb.GetComponent<Bomb>());
						bombWrappers[0] = bombWrapper;
						bombWrapper.holdable.OnLetGo += () => BombStatus.Instance.currentBomb = null;

						if (globalTimerDisabled || firstBomb)
						{
							firstBomb = false;

							if (CurrentMode == Mode.Time)
								bombWrapper.CurrentTimer = Modes.settings.TimeModeStartingTime * 60;
							else if (CurrentMode == Mode.Zen)
								bombWrapper.CurrentTimer = 0.001f;
						}

						yield return new WaitUntil(() => currentBomb != getBomb() || factoryRoom == null);

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
			bool targetState = CurrentMode != Mode.Zen && bombWrappers.Any((BombWrapper bombWrapper) => !bombWrapper.Bomb.IsSolved() && bombWrapper.CurrentTimer < 60f);
			if (targetState != lastState)
			{
				foreach (Assets.Scripts.Props.EmergencyLight emergencyLight in FindObjectsOfType<Assets.Scripts.Props.EmergencyLight>())
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

	static float originalTime = 300;
	public static IEnumerator ModifyFreeplayDevice(bool firstTime)
	{
		yield return null;
		SetupRoom setupRoom = FindObjectOfType<SetupRoom>();
		if (setupRoom)
		{
			FreeplayDevice freeplayDevice = setupRoom.FreeplayDevice;
			ExecOnDescendants(freeplayDevice.gameObject, gameObj =>
			{
				if (gameObj.name == "FreeplayLabel" || gameObj.name == "Free Play Label")
					gameObj.GetComponent<TMPro.TextMeshPro>().text = CurrentMode == Mode.Normal ? "free play" : $"{CurrentMode.ToString()} mode";
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
				Modes.modConfig.Settings = Modes.settings;
			};

			freeplayDevice.TimeDecrement.OnPush += delegate { ReflectedTypes.IsInteractingField.SetValue(freeplayDevice.TimeDecrement, true); };
			freeplayDevice.TimeDecrement.OnInteractEnded += delegate
			{
				originalTime = freeplayDevice.CurrentSettings.Time;
				if (CurrentMode != Mode.Time) return;

				Modes.settings.TimeModeStartingTime = freeplayDevice.CurrentSettings.Time / 60;
				Modes.modConfig.Settings = Modes.settings;
			};
		}
	}

	public static void UpdateSettings()
	{
		settings = modConfig.Settings;
		modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.
	}

	void OnApplicationQuit()
	{
		//Debug.LogFormat("[Tweaks] [OnApplicationQuit] Found output_log: {0}", File.Exists(Path.Combine(Application.dataPath, "output_log.txt")));
	}

	public static void Log(params object[] args) => Debug.Log("[Tweaks] " + args.Select(Convert.ToString).Join(" "));

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
		Log($"{new String('\t', depth)}{goTransform.name} - {goTransform.localPosition.ToString("N6")}");
		foreach (Transform child in goTransform)
		{
			LogChildren(child, depth + 1);
		}
	}
}

class SteamFilterService : ServicesSteam
{
	public static string TargetMissionID;

	public override void ExecuteLeaderboardRequest(LeaderboardRequest request)
	{
		LeaderboardListRequest listRequest = request as LeaderboardListRequest;
		if (listRequest?.SubmitScore == true && listRequest?.MissionID == TargetMissionID)
		{
			ReflectedTypes.SubmitFieldProperty.SetValue(listRequest, false, null);

			TargetMissionID = null;
		}

		base.ExecuteLeaderboardRequest(request);
	}
}

class TweakSettings
{
	public float FadeTime = 1f;
	public bool InstantSkip = true;
	public bool BetterCasePicker = true;
	public bool EnableModsOnlyKey = false;
	public bool FixFER = false;
	public bool BombHUD = false;
	public bool ShowEdgework = false;
	public List<string> HideTOC = new List<string>();
    public Mode Mode = Mode.Normal;

    public override bool Equals(object obj)
	{
		return obj is TweakSettings settings &&
			   FadeTime == settings.FadeTime &&
			   InstantSkip == settings.InstantSkip &&
			   BetterCasePicker == settings.BetterCasePicker &&
			   FixFER == settings.FixFER &&
			   BombHUD == settings.BombHUD &&
			   ShowEdgework == settings.ShowEdgework &&
			   HideTOC.SequenceEqual(settings.HideTOC) &&
			   Mode == settings.Mode;
	}

	public override int GetHashCode()
	{
		var hashCode = -1862006898;
		hashCode = hashCode * -1521134295 + FadeTime.GetHashCode();
		hashCode = hashCode * -1521134295 + InstantSkip.GetHashCode();
		hashCode = hashCode * -1521134295 + BetterCasePicker.GetHashCode();
		hashCode = hashCode * -1521134295 + FixFER.GetHashCode();
		hashCode = hashCode * -1521134295 + BombHUD.GetHashCode();
		hashCode = hashCode * -1521134295 + ShowEdgework.GetHashCode();
		hashCode = hashCode * -1521134295 + HideTOC.GetHashCode();
		return hashCode * -1521134295 + Mode.GetHashCode();
	}
}