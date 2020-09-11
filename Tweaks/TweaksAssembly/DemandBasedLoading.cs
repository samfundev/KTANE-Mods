using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Mods.Screens;
using Assets.Scripts.Settings;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

static class DemandBasedLoading
{
	public static bool EverLoadedModules;
	public static int DisabledModsCount;
	static readonly List<string> fakedModules = new List<string>();
	static string modWorkshopPath;
	static readonly List<string> loadOrder = new List<string>();
	static readonly Dictionary<string, UnityEngine.Object[]> loadedObjects = new Dictionary<string, UnityEngine.Object[]>();

	public static void HandleTransitioning()
	{
		// Load the modules from the website and makes the fake module
		if (!EverLoadedModules)
		{
			EverLoadedModules = true;

			Time.timeScale = 0;
			Tweaks.Instance.StartCoroutine(GetModules());
		}
		else
		{
			Tweaks.Instance.StartCoroutine(CheckForModManagerState());
		}

		// Unload any service objects as they shouldn't be needed outside the gameplay room.
		foreach (Mod mod in manuallyLoadedMods.Values)
			mod.RemoveServiceObjects();

		// Unload mods if we're we're over the limit
		UnloadTo(Tweaks.settings.DemandModLimit);
	}

	private static void UnloadTo(int limit)
	{
		while (loadOrder.Count > limit && limit >= 0)
		{
			var steamID = loadOrder[0];
			loadOrder.Remove(steamID);

			Mod mod = manuallyLoadedMods[steamID];
			mod.Unload();

			foreach (var loadedObject in loadedObjects[steamID])
			{
				// GameObjects can't be unloaded, only destroyed.
				if (loadedObject as GameObject)
				{
					UnityEngine.Object.Destroy(loadedObject);
					continue;
				}

				Resources.UnloadAsset(loadedObject);
			}

			FakeModule.loadedMods.Remove(mod.GetModPath());
			loadedObjects.Remove(steamID);
			manuallyLoadedMods.Remove(steamID);
		}

		// Remove any now null keys so that objects aren't kept around
		foreach (var key in ReflectedTypes.CachedFields.Where(pair => pair.Key == null).Select(pair => pair.Key).ToArray())
		{
			ReflectedTypes.CachedFields.Remove(key);
		}
	}

	public static IEnumerator EnterAndLeaveModManager()
	{
		SceneManager.Instance.EnterModManagerStateFromSetup();
		Tweaks.Instance.UpdateSettingWarning();
		yield return new WaitUntil(() => SceneManager.Instance?.ModManagerState != null && MenuManager.Instance?.CurrentScreen?.GetType() == typeof(ModManagerMainMenuScreen));
		ModManagerScreenManager.Instance.OpenModLoadingScreenAndReturnToGame();
	}

	static IEnumerator CheckForModManagerState()
	{
		yield return null;
		yield return null;

		if (SceneManager.Instance.CurrentState == SceneManager.State.ModManager)
		{
			if (fakedModules.Count != 0)
			{
				UnityEngine.Object.Destroy(Tweaks.Instance.transform.Find("FakeModuleParent").gameObject);

				var loadedBombComponents = ModManager.Instance.GetValue<Dictionary<string, BombComponent>>("loadedBombComponents");
				foreach (string fakedModuleID in fakedModules)
					loadedBombComponents.Remove(fakedModuleID);
				fakedModules.Clear();

				UnloadTo(0);
			}

			EverLoadedModules = !Tweaks.settings.DemandBasedModLoading;
			Tweaks.DemandBasedSettingCache = Tweaks.settings.DemandBasedModLoading;
		}
	}

#pragma warning disable CS0649
	class WebsiteJSON
	{
		public List<KtaneModule> KtaneModules;
	}

	class KtaneModule
	{
		public string SteamID;
		public string Name;
		public string ModuleID;
		public string Type;
	}
#pragma warning restore CS0649

	static readonly KtaneModule[] TranslatedModules = new[]
	{
		// Translated Vanilla
		new KtaneModule
		{
			Name = "The Button Translated",
			ModuleID = "BigButtonTranslated",
			Type = "Regular",
			SteamID = "850186070"
		},
		new KtaneModule
		{
			Name = "Who's on First Translated",
			ModuleID = "WhosOnFirstTranslated",
			Type = "Regular",
			SteamID = "850186070"
		},
		new KtaneModule
		{
			Name = "Password Translated",
			ModuleID = "PasswordsTranslated",
			Type = "Regular",
			SteamID = "850186070"
		},
		new KtaneModule
		{
			Name = "Morse Code Translated",
			ModuleID = "MorseCodeTranslated",
			Type = "Regular",
			SteamID = "850186070"
		},
		new KtaneModule
		{
			Name = "Venting Gas Translated",
			ModuleID = "VentGasTranslated",
			Type = "Needy",
			SteamID = "850186070"
		},

		// Russian Adjacent Letters
		new KtaneModule
		{
			Name = "Adjacent Letters (Russian)",
			ModuleID = "AdjacentLettersModule_Rus",
			Type = "Regular",
			SteamID = "806188270"
		},

		// Polish Colour Flash
		new KtaneModule
		{
			Name = "Colour Flash PL",
			ModuleID = "ColourFlashPL",
			Type = "Regular",
			SteamID = "2030249636"
		},
	};

	static string SteamDirectory
	{
		get
		{
			// Mod folders
			var folders = Assets.Scripts.Services.AbstractServices.Instance.GetModFolders();
			if (folders.Count != 0)
			{
				return folders[0] + "/../../../../..";
			}

			// Relative to the game
			var relativePath = Path.GetFullPath("./../../..");
			if (new DirectoryInfo(relativePath).Name == "Steam")
			{
				return relativePath;
			}

			// Registry key
			using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
			{
				if (key?.GetValueNames().Contains("SteamPath") == true)
				{
					return key.GetValue("SteamPath")?.ToString().Replace('/', '\\') ?? string.Empty;
				}
			}

			// Guess common paths
			foreach (var path in new[]
			{
				@"Program Files (x86)\Steam",
				@"Program Files\Steam",
			})
			{
				foreach (var drive in Directory.GetLogicalDrives())
				{
					if (Directory.Exists(drive + path))
					{
						return drive + path;
					}
				}
			}

			foreach (var path in new[]
			{
				"~/Library/Application Support/Steam",
				"~/.steam/steam",
			})
			{
				if (Directory.Exists(path))
				{
					return path;
				}
			}

			return null;
		}
	}

	static IEnumerator GetModules()
	{
		var steamDirectory = SteamDirectory;
		UnityWebRequest request = UnityWebRequest.Get("https://ktane.timwi.de/json/raw");

		yield return request.SendWebRequest();

		if (request.isNetworkError)
		{
			Tweaks.Log("Unable to load the repository:", request.error);
		}
		else if (steamDirectory == null)
		{
			Tweaks.Log("Unable to find Steam!");
		}
		else
		{
			var disabledMods = ModSettingsManager.Instance.ModSettings.DisabledModPaths.ToList();
			modWorkshopPath = Path.GetFullPath(new[] { steamDirectory, "steamapps", "workshop", "content", "341800" }.Aggregate(Path.Combine));

			var fakeModuleParent = new GameObject("FakeModuleParent");
			fakeModuleParent.transform.parent = Tweaks.Instance.transform;
			fakeModuleParent.SetActive(false);

			var loadedBombComponents = ModManager.Instance.GetValue<Dictionary<string, BombComponent>>("loadedBombComponents");

			var json = JsonConvert.DeserializeObject<WebsiteJSON>(request.downloadHandler.text);
			var cantLoad = new List<string>();
			foreach (KtaneModule module in json.KtaneModules.Concat(TranslatedModules))
			{
				if (module.SteamID == null || !(module.Type == "Regular" || module.Type == "Needy"))
					continue;

				var modPath = Path.Combine(modWorkshopPath, module.SteamID);
				if (!Directory.Exists(modPath))
				{
					cantLoad.Add($"{module.ModuleID} ({module.SteamID})");
					continue;
				}

				// Disable mods we are going to load on demand
				if (Tweaks.settings.DisableDemandBasedMods && !disabledMods.Contains(modPath))
				{
					disabledMods.Add(modPath);
					DisabledModsCount++;
				}

				if (loadedBombComponents.ContainsKey(module.ModuleID))
					continue;

				GameObject fakeModule = new GameObject("FakeModule");
				fakeModule.transform.parent = fakeModuleParent.transform;

				if (module.Type == "Regular")
				{
					var fakeBombComponent = fakeModule.AddComponent<ModBombComponent>();
					var bombModule = fakeModule.AddComponent<KMBombModule>();
					fakeBombComponent.SetValue("module", bombModule);
					fakeBombComponent.enabled = false;
					fakeBombComponent.ComponentType = Assets.Scripts.Missions.ComponentTypeEnum.Mod;
					bombModule.ModuleType = module.ModuleID;
					bombModule.ModuleDisplayName = module.Name;
				}
				else
				{
					var fakeNeedyComponent = fakeModule.AddComponent<ModNeedyComponent>();
					var needyModule = fakeModule.AddComponent<KMNeedyModule>();
					fakeNeedyComponent.SetValue("module", needyModule);
					fakeNeedyComponent.enabled = false;
					fakeNeedyComponent.ComponentType = Assets.Scripts.Missions.ComponentTypeEnum.NeedyMod;
					needyModule.ModuleType = module.ModuleID;
					needyModule.ModuleDisplayName = module.Name;
				}

				fakeModule.gameObject.name = module.SteamID;
				fakeModule.AddComponent<FakeModule>();
				fakeModule.AddComponent<Selectable>();
				fakeModule.AddComponent<ModSource>().ModName = "Tweaks";

				loadedBombComponents[module.ModuleID] = fakeModule.GetComponent<BombComponent>();
				fakedModules.Add(module.ModuleID);
			}

			if (cantLoad.Count > 0)
				Tweaks.Log($"Can't load: {cantLoad.Join(", ")}".ChunkBy(250).Join("\n"));

			ModSettingsManager.Instance.ModSettings.DisabledModPaths = disabledMods.ToArray();
			ModSettingsManager.Instance.SaveModSettings();
		}

		Time.timeScale = 1;
	}

	public static Dictionary<string, Mod> manuallyLoadedMods = new Dictionary<string, Mod>();

	public static BombFace.ComponentSpawnPoint? GetComponentSpawnPoint(Vector3 position, Bomb bomb, out BombFace bombFace)
	{
		float minimum = float.MaxValue;
		BombFace.ComponentSpawnPoint? item = null;
		bombFace = null;
		foreach (BombFace face in bomb.Faces)
		{
			foreach (var spawnPoint in face.ComponentSpawnPoints.Concat(face.TimerSpawnPoints))
			{
				float distance = (spawnPoint.Transform.position - position).magnitude;
				if (distance < minimum)
				{
					minimum = distance;
					item = spawnPoint;
					bombFace = face;
				}
			}
		}

		return item;
	}

	class FakeModule : MonoBehaviour
	{
		GameObject realModule;
		Bomb bomb;
		BombFace timerFace;

		public static readonly Dictionary<string, Mod> loadedMods = ModManager.Instance.GetValue<Dictionary<string, Mod>>("loadedMods");
		private static List<BombComponent> emptyTimerFaceComponents = null;

		public void Awake()
		{
			emptyTimerFaceComponents = null;

			bomb = BetterCasePicker.BombGenerator.GetValue<Bomb>("bomb");

			string SteamID = gameObject.name.Replace("(Clone)", "");
			string ModuleID = GetComponent<KMBombModule>()?.ModuleType ?? GetComponent<KMNeedyModule>()?.ModuleType;

			// Hide from Souvenir
			var bombModule = GetComponent<KMBombModule>();
			if (bombModule != null)
				DestroyImmediate(bombModule);

			if (!manuallyLoadedMods.TryGetValue(SteamID, out Mod mod))
			{
				var modPath = Path.Combine(modWorkshopPath, SteamID);
				if (!Directory.Exists(modPath))
					return;

				mod = Mod.LoadMod(modPath, Assets.Scripts.Mods.ModInfo.ModSourceEnum.Local);
				foreach (string fileName in mod.GetAssetBundlePaths())
				{
					AssetBundle mainBundle = AssetBundle.LoadFromFile(fileName);
					if (mainBundle != null)
					{
						try
						{
							mod.LoadBundle(mainBundle);
						}
						catch (Exception ex)
						{
							Debug.LogErrorFormat("Load of mod \"{0}\" failed: \n{1}\n{2}", mod.ModID, ex.Message, ex.StackTrace);
						}

						loadedObjects[SteamID] = mainBundle.LoadAllAssets<UnityEngine.Object>();

						mainBundle.Unload(false);
					}
				}

				mod.CallMethod("RemoveMissions");
				mod.CallMethod("RemoveSoundOverrides");

				manuallyLoadedMods[SteamID] = mod;
				loadedMods[modPath] = mod;
			}

			loadOrder.Remove(SteamID);
			loadOrder.Add(SteamID);

			if (mod != null)
			{
				List<string> moduleIDs = new List<string>();
				foreach (KMBombModule kmbombModule in mod.GetModObjects<KMBombModule>())
				{
					string moduleType = kmbombModule.ModuleType;
					if (moduleType == ModuleID)
						realModule = Instantiate(kmbombModule.gameObject);

					moduleIDs.Add(moduleType);
				}
				foreach (KMNeedyModule kmneedyModule in mod.GetModObjects<KMNeedyModule>())
				{
					string moduleType = kmneedyModule.ModuleType;
					if (moduleType == ModuleID)
						realModule = Instantiate(kmneedyModule.gameObject);

					moduleIDs.Add(moduleType);
				}

				if (realModule != null)
				{
					foreach (ModService original in mod.GetModObjects<ModService>())
					{
						GameObject gameObject = Instantiate(original).gameObject;
						gameObject.transform.parent = ModManager.Instance.transform;
						mod.AddServiceObject(gameObject);
					}

					BombComponent bombComponent = realModule.GetComponent<BombComponent>();

					realModule.transform.parent = bomb.visualTransform.transform;
					realModule.transform.localScale = Vector3.one;

					// Update backing
					var backing = GetComponentSpawnPoint(transform.position, bomb, out _)?.Backing;
					if (bombComponent.RequiresDeepBackingGeometry && backing != null)
					{
						backing.CurrentBacking = BombComponentBacking.BackingType.Deep;
					}

					// Look for a log message so we can remove the fake module from the Bomb's BombComponent list as soon as possible.
					OnInstantiation(() =>
					{
						bomb.BombComponents.Add(bombComponent);
						bomb.BombComponents.Remove(GetComponent<BombComponent>());

						if (bombComponent.RequiresTimerVisibility)
						{
							GetComponentSpawnPoint(bomb.GetTimer().transform.position, bomb, out timerFace);
							var bombFace = GetComponent<Selectable>().Parent.GetComponent<BombFace>();
							if (timerFace != bombFace)
							{
								bomb.visualTransform.gameObject.AddComponent<ExcludeFromStaticBatch>();
							}
						}
					});
				}
				else
				{
					Tweaks.Log($"Unable to get the real module for {ModuleID} ({SteamID}). IDs found: {moduleIDs.Select(id => $"\"{id}\"").Join(", ")}. This shouldn't happen, please contact the developer of Tweaks.");

					OnInstantiation(() => bomb.BombComponents.Remove(GetComponent<BombComponent>()));

					LeaderboardController.DisableLeaderboards();
				}
			}
		}

		private void OnInstantiation(Action callback)
		{
			void logRecieved(string logString, string _, LogType type)
			{
				if (!(logString.StartsWith("[BombGenerator] Instantiated ") && type == LogType.Log))
					return;

				Application.logMessageReceived -= logRecieved;

				callback();
			}

			Application.logMessageReceived += logRecieved;
		}

		public void Start()
		{
			if (realModule == null)
			{
				Destroy(gameObject);
				return;
			}

			BombComponent bombComponent = realModule.GetComponent<BombComponent>();

			if (bombComponent.RequiresTimerVisibility)
			{
				GetTimerFaceComponents();

				GetComponent<BombComponent>().RequiresTimerVisibility = true;

				var bombFace = GetComponent<Selectable>().Parent.GetComponent<BombFace>();
				var myFaceSelectable = GetComponent<Selectable>().Parent;

				if (timerFace != bombFace)
				{
					var timerFaceSelectable = timerFace.GetComponent<Selectable>();
					var swapTarget = timerFaceSelectable.Children
						.Where(child => child != null)
						.Select(child => child.GetComponent<BombComponent>())
						.Where(component => !component.RequiresTimerVisibility)
						.Concat(emptyTimerFaceComponents)
						.Shuffle()
						.FirstOrDefault();

					if (swapTarget != null)
					{
						var swapPosition = swapTarget.transform.position;
						var swapRotation = swapTarget.transform.rotation;
						var myPosition = transform.position;
						var myRotation = transform.rotation;

						transform.position = swapPosition;
						transform.rotation = swapRotation;

						swapTarget.transform.position = myPosition;
						swapTarget.transform.rotation = myRotation;

						if (emptyTimerFaceComponents.Contains(swapTarget))
						{
							emptyTimerFaceComponents.Remove(swapTarget);

							var swapIndex = Array.IndexOf(timerFaceSelectable.Children, swapTarget.GetComponent<Selectable>());
							timerFaceSelectable.Children[swapIndex] = GetComponent<Selectable>();
							GetComponent<Selectable>().Parent = timerFaceSelectable;
						}
						else
						{
							var swapIndex = Array.IndexOf(timerFaceSelectable.Children, swapTarget.GetComponent<Selectable>());
							var myIndex = Array.IndexOf(GetComponent<Selectable>().Parent.Children, GetComponent<Selectable>());

							timerFaceSelectable.Children[swapIndex] = GetComponent<Selectable>();
							GetComponent<Selectable>().Parent.Children[myIndex] = swapTarget.GetComponent<Selectable>();

							GetComponent<Selectable>().Parent = timerFaceSelectable;
							swapTarget.GetComponent<Selectable>().Parent = myFaceSelectable;
						}

						renderSelectable = realModule.GetComponent<Selectable>();
					}
				}
			}

			realModule.transform.position = transform.position;
			realModule.transform.rotation = transform.rotation;

			Selectable component3 = realModule.GetComponent<Selectable>();
			if (component3 != null)
			{
				Selectable component4 = GetComponent<Selectable>().Parent;
				int selectableIndex = Array.IndexOf(component4.Children, GetComponent<Selectable>());
				component4.Children[selectableIndex] = component3;
				component3.Parent = component4;
			}

			bombComponent.OnStrike = (StrikeEvent) Delegate.Combine(bombComponent.OnStrike, new StrikeEvent(bomb.OnStrike));
			bombComponent.OnPass = (PassEvent) Delegate.Combine(bombComponent.OnPass, new PassEvent(bomb.OnPass));
			bombComponent.Bomb = bomb;

			NeedyComponent needyComponent = realModule.GetComponent<NeedyComponent>();
			if (needyComponent != null)
			{
				needyComponent.SecondsBeforeForcedActivation = GetComponent<NeedyComponent>().SecondsBeforeForcedActivation;
			}

			Destroy(gameObject);
		}

		private void GetTimerFaceComponents()
		{
			if (emptyTimerFaceComponents != null)
				return;

			emptyTimerFaceComponents = new List<BombComponent>();

			GetComponentSpawnPoint(bomb.GetTimer().transform.position, bomb, out BombFace timerFace);
			foreach (BombComponent bombComponent in bomb.BombComponents)
			{
				if (bombComponent.ComponentType != Assets.Scripts.Missions.ComponentTypeEnum.Empty)
					continue;

				GetComponentSpawnPoint(bombComponent.transform.position, bomb, out BombFace ourFace);

				if (timerFace == ourFace)
					emptyTimerFaceComponents.Add(bombComponent);
			}
		}
	}
}