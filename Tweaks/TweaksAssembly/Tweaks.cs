using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
class Tweaks : MonoBehaviour
{
	public static ModConfig<TweakSettings> modConfig = new ModConfig<TweakSettings>("TweakSettings");
	public static TweakSettings settings = modConfig.Settings;

	public static KMGameInfo GameInfo;

	void Awake()
	{
		GameInfo = GetComponent<KMGameInfo>();

		modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.

		bool changeFadeTime = settings.FadeTime >= 0;
		
		FreeplayDevice.MAX_SECONDS_TO_SOLVE = float.MaxValue;
		FreeplayDevice.MIN_MODULE_COUNT = 1;

		// Setup API/properties other mods to interact with
		GameObject infoObject = new GameObject("Tweaks_Info", typeof(TweaksProperties));
		infoObject.transform.parent = gameObject.transform;

		// Watch the TweakSettings file for Time Mode state being changed in the office.
		FileSystemWatcher watcher = new FileSystemWatcher(Path.Combine(Application.persistentDataPath, "Modsettings"), "TweakSettings.json");
		watcher.NotifyFilter = NotifyFilters.LastWrite;
		watcher.Changed += delegate (object source, FileSystemEventArgs e)
		{
			if (settings.Equals(modConfig.Settings)) return;
			
			settings = modConfig.Settings;
			modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.

			StartCoroutine(ModifyFreeplayDevice(false));
		};

		UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate (Scene scene, LoadSceneMode _)
		{
			settings = modConfig.Settings;
			modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.

			Modes.settings = Modes.modConfig.Settings;
			Modes.UpdateComponentValues();
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

					if (ReflectedTypes.CurrencyAPIEndpointField != null)
						ReflectedTypes.CurrencyAPIEndpointField.SetValue(null, settings.FixFER ? "http://exchangeratesapi.io/api" : "http://api.fixer.io");

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
		
		GameInfo.OnStateChange += delegate (KMGameInfo.State state)
		{
			watcher.EnableRaisingEvents = state == KMGameInfo.State.Setup;

			if (state == KMGameInfo.State.Gameplay)
			{
				if (settings.BetterCasePicker) BetterCasePicker.PickCase();
				
				BombStatus.Instance.HUD.SetActive(settings.BombHUD);
				BombStatus.Instance.Edgework.SetActive(settings.ShowEdgework);

				Modes.Multiplier = Modes.settings.TimeModeStartingMultiplier;
				bombWrappers = new BombWrapper[] { };
				StartCoroutine(CheckForBombs());
			}
			else if (state == KMGameInfo.State.Setup)
			{
				StartCoroutine(ModifyFreeplayDevice(true));
			}
            
			bool disableRecords = (state == KMGameInfo.State.Gameplay && (settings.BombHUD || settings.ShowEdgework || settings.Mode != Mode.Normal));

			Assets.Scripts.Stats.StatsManager.Instance.DisableStatChanges =
			Assets.Scripts.Records.RecordManager.Instance.DisableBestRecords = disableRecords;
		};
	}

	public static BombWrapper[] bombWrappers = new BombWrapper[] { };

	public IEnumerator CheckForBombs()
	{
		yield return new WaitUntil(() => (SceneManager.Instance.GameplayState.Bombs != null && SceneManager.Instance.GameplayState.Bombs.Count > 0));
		List<Bomb> bombs = SceneManager.Instance.GameplayState.Bombs;

		Array.Resize(ref bombWrappers, bombs.Count);

		for (int i = 0; i < bombs.Count; i++)
		{
			Bomb bomb = bombs[i];
			BombWrapper bombWrapper = new BombWrapper(bomb);
			bombWrappers[i] = bombWrapper;
			bombWrapper.holdable.OnLetGo += delegate () { BombStatus.Instance.currentBomb = null; };

			if (settings.Mode == Mode.Time) bombWrapper.CurrentTimer = Modes.settings.TimeModeStartingTime * 60;
		}

		if (ReflectedTypes.FactoryRoomType != null && ReflectedTypes.FiniteSequenceModeType != null)
		{
			UnityEngine.Object factoryRoom = FindObjectOfType(ReflectedTypes.FactoryRoomType);
			if (factoryRoom)
			{
				object gameMode = ReflectedTypes.GameModeProperty.GetValue(factoryRoom, null);
				if (ReflectedTypes.FiniteSequenceModeType.IsAssignableFrom(gameMode.GetType()))
				{
					IEnumerable<object> adaptations = ((IEnumerable) ReflectedTypes.AdaptationsProperty.GetValue(gameMode, null)).Cast<object>();
					bool globalTimerEnabled = !adaptations.Any(adaptation => ReflectedTypes.GlobalTimerAdaptationType.IsAssignableFrom(adaptation.GetType()));

					Func<Component> getBomb = () => (Component) ReflectedTypes._CurrentBombField.GetValue(gameMode);

					yield return new WaitUntil(() => getBomb() != null || factoryRoom == null);
					Component currentBomb = getBomb();

					while (currentBomb != null && factoryRoom != null)
					{
						yield return new WaitUntil(() => currentBomb != getBomb() || factoryRoom == null);

						currentBomb = getBomb();

						if (currentBomb == null || factoryRoom == null) break;
					
						BombWrapper bombWrapper = new BombWrapper(currentBomb.GetComponent<Bomb>());
						bombWrappers[0] = bombWrapper;
						bombWrapper.holdable.OnLetGo += delegate () { BombStatus.Instance.currentBomb = null; };

						if (globalTimerEnabled && settings.Mode.Equals(Mode.Time))
							bombWrapper.CurrentTimer = Modes.settings.TimeModeStartingTime * 60;
					}
				}
			}
		}
	}
    
	float originalTime = 300;
	IEnumerator ModifyFreeplayDevice(bool firstTime)
	{
		yield return null;
		SetupRoom setupRoom = FindObjectOfType<SetupRoom>();
		if (setupRoom)
		{
			FreeplayDevice freeplayDevice = setupRoom.FreeplayDevice;
			ExecOnDescendants(freeplayDevice.gameObject, gameObj =>
			{
				if (gameObj.name == "FreeplayLabel" || gameObj.name == "Free Play Label")
					gameObj.GetComponent<TMPro.TextMeshPro>().text = settings.Mode == Mode.Time ? "TIME MODE" : settings.Mode == Mode.Zen ? "ZEN MODE" : "FREE PLAY";
			});
			
			freeplayDevice.CurrentSettings.Time = settings.Mode == Mode.Time ? Modes.settings.TimeModeStartingTime * 60 : originalTime;
			TimeSpan timeSpan = TimeSpan.FromSeconds(freeplayDevice.CurrentSettings.Time);
			freeplayDevice.TimeText.text = string.Format("{0}:{1:00}", (int) timeSpan.TotalMinutes, timeSpan.Seconds);

			if (!firstTime) yield break;
			if (settings.Mode == Mode.Normal) originalTime = freeplayDevice.CurrentSettings.Time;
			
			freeplayDevice.TimeIncrement.OnPush += delegate { ReflectedTypes.IsInteractingField.SetValue(freeplayDevice.TimeIncrement, true); };
			freeplayDevice.TimeIncrement.OnInteractEnded += delegate
			{
				originalTime = freeplayDevice.CurrentSettings.Time;
				if (settings.Mode != Mode.Time) return;

				Modes.settings.TimeModeStartingTime = freeplayDevice.CurrentSettings.Time / 60;
				Modes.modConfig.Settings = Modes.settings;
			};

			freeplayDevice.TimeDecrement.OnPush += delegate { ReflectedTypes.IsInteractingField.SetValue(freeplayDevice.TimeDecrement, true); };
			freeplayDevice.TimeDecrement.OnInteractEnded += delegate
			{
				originalTime = freeplayDevice.CurrentSettings.Time;
				if (settings.Mode != Mode.Time) return;

				Modes.settings.TimeModeStartingTime = freeplayDevice.CurrentSettings.Time / 60;
				Modes.modConfig.Settings = Modes.settings;
			};
		}
	}

	void OnApplicationQuit()
	{
		//Debug.LogFormat("[Tweaks] [OnApplicationQuit] Found output_log: {0}", File.Exists(Path.Combine(Application.dataPath, "output_log.txt")));
	}

	public static void Log(object format, params object[] args)
	{
		Debug.LogFormat("[Tweaks] " + format, args);
	}

	void ExecOnDescendants(GameObject gameObj, Action<GameObject> func)
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
		Log("{2}{0} - {1}", goTransform.name, goTransform.localPosition.ToString("N6"), new String('\t', depth));
		foreach (Transform child in goTransform)
		{
			LogChildren(child, depth + 1);
		}
	}
}

class TweakSettings
{
	public float FadeTime = 1f;
	public bool InstantSkip = true;
	public bool BetterCasePicker = true;
	public bool FixFER = false;
	public bool BombHUD = false;
	public bool ShowEdgework = false;
    public Mode Mode = Mode.Normal;

    public override bool Equals(object obj)
	{
		var settings = obj as TweakSettings;
		return settings != null &&
			   FadeTime == settings.FadeTime &&
			   InstantSkip == settings.InstantSkip &&
			   BetterCasePicker == settings.BetterCasePicker &&
			   FixFER == settings.FixFER &&
			   BombHUD == settings.BombHUD &&
			   ShowEdgework == settings.ShowEdgework &&
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
		hashCode = hashCode * -1521134295 + Mode.GetHashCode();
		return hashCode;
	}
}