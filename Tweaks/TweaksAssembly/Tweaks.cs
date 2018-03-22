using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(KMService))]
public class Tweaks : MonoBehaviour
{
	void Awake()
	{
		FreeplayDevice.MAX_SECONDS_TO_SOLVE = float.MaxValue;
		FreeplayDevice.MIN_MODULE_COUNT = 1;

		SceneManager.Instance.GameplayState.FadeInTime = 0.5f;

		UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate (Scene scene, LoadSceneMode _)
		{
			if (scene.name == "gameplayLoadingScene") {
				var loadmanager = FindObjectOfType<GameplayLoadingManager>();
				loadmanager.MinTotalLoadTime = 0;
				loadmanager.FadeInTime = 0.5f;
				loadmanager.FadeOutTime = 0.5f;
			}
		};
	}

	void OnApplicationQuit()
	{
		Debug.LogFormat("[Tweaks] [OnApplicationQuit] Found output_log: {0}", File.Exists(Path.Combine(Application.dataPath, "output_log.txt")));
	}
}
