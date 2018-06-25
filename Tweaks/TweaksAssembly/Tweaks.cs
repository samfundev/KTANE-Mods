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
	public static TweakSettings settings;

	void Awake()
	{
		ModConfig<TweakSettings> modConfig = new ModConfig<TweakSettings>("TweakSettings");
		settings = modConfig.Settings;
		modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.

		bool changeFadeTime = settings.FadeTime >= 0;
		
		FreeplayDevice.MAX_SECONDS_TO_SOLVE = float.MaxValue;
		FreeplayDevice.MIN_MODULE_COUNT = 1;
		
		UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate (Scene scene, LoadSceneMode _)
		{
			settings = modConfig.Settings;
			modConfig.Settings = settings; // Write any settings that the user doesn't have in their settings file.

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

					if (ReflectedTypes.CurrencyAPIEndpointField != null) ReflectedTypes.CurrencyAPIEndpointField.SetValue(null, "http://exchangeratesapi.io/api");

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
		
		GetComponent<KMGameInfo>().OnStateChange += delegate (KMGameInfo.State state)
		{
			if (state == KMGameInfo.State.Gameplay)
			{
				if (settings.BetterCasePicker) BetterCasePicker.PickCase();
				
				BombStatus.Instance.gameObject.SetActive(settings.BombHUD);
				TimeMode.Multiplier = 9;
				bombWrappers = new BombWrapper[] { };
				StartCoroutine(CheckForBombs());
			}

			bool disableRecords = (state == KMGameInfo.State.Gameplay && (settings.BombHUD || settings.TimeMode));

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
		}

		if (ReflectedTypes.FactoryRoomType != null && ReflectedTypes.FactoryFiniteModeType != null)
		{
			UnityEngine.Object factoryRoom = FindObjectOfType(ReflectedTypes.FactoryRoomType);
			if (factoryRoom)
			{
				object gameMode = ReflectedTypes.GameModeProperty.GetValue(factoryRoom, null);
				if (ReflectedTypes.FactoryFiniteModeType.IsAssignableFrom(gameMode.GetType()))
				{
					Func<Component> getBomb = () =>  (Component) ReflectedTypes._CurrentBombField.GetValue(gameMode);

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
					}
				}
			}
		}
	}

	void OnApplicationQuit()
	{
		//Debug.LogFormat("[Tweaks] [OnApplicationQuit] Found output_log: {0}", File.Exists(Path.Combine(Application.dataPath, "output_log.txt")));
	}
}

class TweakSettings
{
	public float FadeTime = 1f;
	public bool InstantSkip = true;
	public bool BetterCasePicker = true;
	public bool BombHUD = false;
	public bool TimeMode = false;
}