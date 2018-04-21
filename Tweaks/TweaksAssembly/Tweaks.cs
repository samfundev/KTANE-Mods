using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
class Tweaks : MonoBehaviour
{
	void Awake()
	{
		ModConfig modConfig = new ModConfig("TweakSettings", typeof(TweakSettings));
		TweakSettings settings = (TweakSettings) modConfig.Settings;

		bool changeFadeTime = settings.FadeTime >= 0;
		
		FreeplayDevice.MAX_SECONDS_TO_SOLVE = float.MaxValue;
		FreeplayDevice.MIN_MODULE_COUNT = 1;
		
		UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate (Scene scene, LoadSceneMode _)
		{
			settings = (TweakSettings) modConfig.Settings;

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
			}
		};
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
}