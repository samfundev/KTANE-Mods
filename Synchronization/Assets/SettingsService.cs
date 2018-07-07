using UnityEngine;

public class SettingsService : MonoBehaviour {
	private void Awake()
	{
		new ModConfig<SynchronizationModule.TestSettings>("SynchronizationSettings").ToString(); // This should create the settings file.
		DestroyImmediate(GetComponent<KMService>()); //Hide from Mod Selector
	}
}
