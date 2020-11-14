using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Missions;
using Assets.Scripts.Mods.Screens;
using Assets.Scripts.Settings;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TweaksAssembly.Patching;
using Random = System.Random;
using System.Reflection;

static class DemandBasedLoading
{
	public static CanvasGroup LoadingScreen;

	public static bool EverLoadedModules;
	public static int DisabledModsCount;
	static readonly List<string> fakedModules = new List<string>();
	static string modWorkshopPath;
	static readonly List<string> loadOrder = new List<string>();
	static readonly Dictionary<string, UnityEngine.Object[]> loadedObjects = new Dictionary<string, UnityEngine.Object[]>();

	static bool BombsLoaded => allBombInfo.All(pair => pair.Value.Loaded);
	static readonly Dictionary<Bomb, BombInfo> allBombInfo = new Dictionary<Bomb, BombInfo>();

	class BombInfo
	{
		public readonly List<BombComponent> Components = new List<BombComponent>();
		public readonly GeneratorSetting Settings;
		public readonly BombFace TimerFace;
		public readonly Random Rand;
		public bool EnableOriginal; // Enables the original InstantiateComponent method.
		public bool Loaded;
		public List<WidgetZone> WidgetZones;

		public BombInfo(GeneratorSetting settings, BombFace timerFace, Random rand)
		{
			Settings = settings;
			TimerFace = timerFace;
			Rand = rand;
		}
	}

	public static void HandleTransitioning()
	{
		// Load the modules from the website and makes the fake module
		if (!EverLoadedModules)
		{
			EverLoadedModules = true;

			Time.timeScale = 0;
			Tweaks.Instance.StartCoroutine(GetModules());

			Patching.EnsurePatch("DBML", typeof(GameplayStatePatches), typeof(GeneratorPatches), typeof(WidgetGeneratorPatch), typeof(MultipleBombsPatch));
			FactoryPatches.PatchAll();
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

			loadedMods.Remove(mod.GetModPath());
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
		Tweaks.Instance.UpdateSettingWarnings();
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
			foreach (KtaneModule module in json.KtaneModules)
			{
				// Don't load anything that:
				// Doesn't have a Steam ID.
				// Isn't a module.
				// Is on the user's exclude list.
				if (
					module.SteamID == null ||
					!(module.Type == "Regular" || module.Type == "Needy") ||
					Tweaks.settings.DemandBasedModsExcludeList.Any(name => module.Name.Like(name))
					)
					continue;

				var modPath = Path.Combine(modWorkshopPath, module.SteamID);
				if (!Directory.Exists(modPath))
				{
					cantLoad.Add($"{module.ModuleID} ({module.SteamID})");
					continue;
				}

				// Disable mods we are going to load on demand
				if (!disabledMods.Contains(modPath))
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

	// Loading a module
	public static int modsLoading = 0;
	public static readonly Dictionary<string, Mod> loadedMods = ModManager.Instance.GetValue<Dictionary<string, Mod>>("loadedMods");
	private static readonly Dictionary<string, bool> modLoading = new Dictionary<string, bool>();

	private static int totalModules = 0;

	private static IEnumerator LoadModule(BombComponent fakeModule, BombInfo bombInfo)
	{
		// To preserve the order that the BombComponents were picked in, insert a null value and keep track of it's index to be replaced later.
		int componentIndex = bombInfo.Components.Count;
		bombInfo.Components.Add(null);

		modsLoading++;
		Time.timeScale = 0;

		yield return null;

		totalModules = modsLoading;

		UpdateLoadingScreen();

		yield return null;

		string SteamID = fakeModule.gameObject.name.Replace("(Clone)", "");
		string ModuleID = fakeModule.GetComponent<KMBombModule>()?.ModuleType ?? fakeModule.GetComponent<KMNeedyModule>()?.ModuleType;

		if (modLoading.ContainsKey(SteamID))
		{
			yield return new WaitUntil(() => !modLoading[SteamID]);
		}

		if (!manuallyLoadedMods.TryGetValue(SteamID, out Mod mod))
		{
			var modPath = Path.Combine(modWorkshopPath, SteamID);
			if (!Directory.Exists(modPath))
				yield break;

			modLoading[SteamID] = true;

			mod = Mod.LoadMod(modPath, Assets.Scripts.Mods.ModInfo.ModSourceEnum.Local);
			foreach (string fileName in mod.GetAssetBundlePaths())
			{
				var bundleRequest = AssetBundle.LoadFromFileAsync(fileName);

				yield return bundleRequest;

				var mainBundle = bundleRequest.assetBundle;

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
			modLoading[SteamID] = false;
		}

		loadOrder.Remove(SteamID);
		loadOrder.Add(SteamID);

		if (mod == null)
			yield break;

		List<string> moduleIDs = new List<string>();
		BombComponent realModule = null;
		foreach (KMBombModule kmbombModule in mod.GetModObjects<KMBombModule>())
		{
			string moduleType = kmbombModule.ModuleType;
			if (moduleType == ModuleID)
				realModule = kmbombModule.GetComponent<BombComponent>();

			moduleIDs.Add(moduleType);
		}
		foreach (KMNeedyModule kmneedyModule in mod.GetModObjects<KMNeedyModule>())
		{
			string moduleType = kmneedyModule.ModuleType;
			if (moduleType == ModuleID)
				realModule = kmneedyModule.GetComponent<BombComponent>();

			moduleIDs.Add(moduleType);
		}

		if (realModule != null)
		{
			foreach (ModService original in mod.GetModObjects<ModService>())
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(original).gameObject;
				gameObject.transform.parent = ModManager.Instance.transform;
				mod.AddServiceObject(gameObject);
			}

			bombInfo.Components[componentIndex] = realModule.GetComponent<BombComponent>();
		}
		else
		{
			Tweaks.Log($"Unable to get the real module for {ModuleID} ({SteamID}). IDs found: {moduleIDs.Select(id => $"\"{id}\"").Join(", ")}. This shouldn't happen, please contact the developer of Tweaks.");

			LeaderboardController.DisableLeaderboards();
		}

		modsLoading--;
		UpdateLoadingScreen();

		if (modsLoading == 0)
			Time.timeScale = 1;
	}

	private static void UpdateLoadingScreen()
	{
		var screen = LoadingScreen;
		screen.gameObject.SetActive(modsLoading != 0);
		screen.alpha = (float) modsLoading / totalModules;
		screen.gameObject.Traverse<Text>("LoadingText").text = $"Loading {modsLoading} module{(modsLoading == 1 ? "" : "s")}...";
	}

	private static IEnumerator InstantiateComponents(Bomb bomb)
	{
		yield return new WaitUntil(() => modsLoading == 0);

		var bombGenerator = SceneManager.Instance.GameplayState.GetValue<BombGenerator>("bombGenerator");
		bombGenerator.SetValue("bomb", bomb);

		var validBombFaces = new List<BombFace>(bomb.Faces.Where(face => face.ComponentSpawnPoints.Count != 0));
		bombGenerator.SetValue("validBombFaces", validBombFaces);

		List<KMBombInfo> knownBombInfos = new List<KMBombInfo>();
		foreach (KMBombInfo item in UnityEngine.Object.FindObjectsOfType<KMBombInfo>())
		{
			knownBombInfos.Add(item);
		}

		var bombInfo = allBombInfo[bomb];
		var timerFace = bombInfo.TimerFace;
		var setting = bombInfo.Settings;
		bombInfo.EnableOriginal = true;
		bombGenerator.SetValue("rand", bombInfo.Rand); // Bring back the real Random object.
		UnityEngine.Random.InitState(bomb.Seed); // Loading AudioClips triggers RNG calls so we need to reset the RNG to before that happened.
		foreach (var bombComponent in bombInfo.Components.OrderByDescending(component => component.RequiresTimerVisibility))
		{
			BombFace face = null;
			if (bombComponent.RequiresTimerVisibility && timerFace.ComponentSpawnPoints.Count != 0)
				face = timerFace;

			if (face == null && validBombFaces.Count != 0)
				face = validBombFaces[bombInfo.Rand.Next(0, validBombFaces.Count)];

			if (face == null)
			{
				Tweaks.Log("No valid faces remain to instantiate:", bombComponent.name);
				continue;
			}

			bombGenerator.CallMethod("InstantiateComponent", face, bombComponent, setting);
		}

		while (validBombFaces.Count > 0)
			bombGenerator.CallMethod("InstantiateComponent", validBombFaces[0], bombGenerator.emptyComponentPrefab, setting);

		// To ensure that the widgets get placed in the right position, we need to temporarily revert the bomb's size.
		bomb.visualTransform.localScale = Vector3.one;

		WidgetGenerator generator = bombGenerator.GetComponent<WidgetGenerator>();
		generator.SetValue("zones", bombInfo.WidgetZones);
		generator.GenerateWidgets(bomb.WidgetManager, setting.OptionalWidgetCount);

		bomb.visualTransform.localScale = new Vector3(bomb.Scale, bomb.Scale, bomb.Scale);

		bombInfo.Loaded = true;

		HookUpMultipleBombs(bomb, knownBombInfos);

		if (BombsLoaded)
		{
			SceneManager.Instance.GameplayState.Bombs.AddRange(allBombInfo.Keys);
			allBombInfo.Clear();

			// This fixes the bomb not getting picked up correctly if clicked on before loading is finished.
			var holdable = KTInputManager.Instance.SelectableManager.GetCurrentFloatingHoldable();
			if (holdable)
				holdable.Defocus(false, false);
		}
	}

	private static void HookUpMultipleBombs(Bomb bomb, List<KMBombInfo> knownBombInfos)
	{
		var type = ReflectionHelper.FindType("MultipleBombsAssembly.MultipleBombs");
		if (type == null)
			return;

		var multipleBombs = UnityEngine.Object.FindObjectOfType(type);
		if (multipleBombs == null)
			return;

		multipleBombs.CallMethod("redirectPresentBombInfos", bomb, knownBombInfos);
		multipleBombs.CallMethod("processBombEvents", bomb);
	}

	// TODO: We don't need to add a component to know which modules are the fake ones, we can just have a list.
	class FakeModule : MonoBehaviour
	{
	}

	private class FakeRandom : Random
	{
		// chosen by fair dice roll.
		// guaranteed to be random.
		public override int Next(int minValue, int maxValue)
		{
			return 0;
		}
	}

	#pragma warning disable IDE0051, RCS1213
	// Most mods look at the Bombs field of the GameplayState to see when the bombs have finished spawning.
	// These Harmony patches will make the Bombs field be an empty list until modules have finished loading.
	[HarmonyPatch(typeof(GameplayState))]
	static class GameplayStatePatches
	{
		[HarmonyPatch("SpawnBomb")]
		static bool Prefix(GeneratorSetting generatorSetting, HoldableSpawnPoint spawnPoint, int seed, ref Bomb __result)
		{
			var gameplayState = SceneManager.Instance.GameplayState;

			__result = gameplayState.GetValue<BombGenerator>("bombGenerator").CreateBomb(generatorSetting, spawnPoint, seed, BombTypeEnum.Default);

			if (modsLoading == 0)
				gameplayState.Bombs.Add(__result);

			return false;
		}

		[HarmonyPatch("Bomb", MethodType.Getter)]
		static bool Prefix(ref Bomb __result)
		{
			if (!BombsLoaded)
				__result = allBombInfo.Keys.First();

			return BombsLoaded;
		}

		[HarmonyPatch("Bombs", MethodType.Getter)]
		static bool Prefix(ref List<Bomb> __result)
		{
			if (!BombsLoaded)
				__result = new List<Bomb>();

			return BombsLoaded;
		}
	}

	// Patches Factory to wait for DBML and fixes a bug with SetSelectableLayer.
	static class FactoryPatches
	{
		static readonly List<Bomb> NotActivated = new List<Bomb>();

		static bool QuickDelayCoroutine(Action delayCallable, ref IEnumerator __result)
		{
			__result = BombDelayCoroutine(delayCallable);
			return false;
		}

		static IEnumerator BombDelayCoroutine(Action delayCallable)
		{
			// Do a WaitUntil and then wait 2 frames just like the original one.
			yield return new WaitUntil(() => BombsLoaded);
			yield return null;
			yield return null;

			NotActivated.AddRange(SceneManager.Instance.GameplayState.Bombs);

			delayCallable();
		}

		static bool ActivateBomb(object __instance)
		{
			var bomb = __instance.GetValue<Bomb>("InternalBomb");
			if (NotActivated.Contains(bomb))
			{
				NotActivated.Remove(bomb);
				return true;
			}
			else
			{
				return false;
			}
		}

		// If there is a _selectableArea, run the original method.
		static bool SetSelectableLayer(object ____selectableArea) => ____selectableArea != null;

		public static void PatchAll()
		{
			PatchMethod(ReflectedTypes.FactoryRoomType, "QuickDelayCoroutine");
			PatchMethod(ReflectionHelper.FindType("FactoryAssembly.FactoryBomb"), "SetSelectableLayer");
			PatchMethod(ReflectionHelper.FindType("FactoryAssembly.FactoryBomb"), "ActivateBomb");
		}

		static void PatchMethod(Type type, string method)
		{
			if (type == null)
				return;

			var original = AccessTools.DeclaredMethod(type, method);

			var patches = Harmony.GetPatchInfo(original);
			if (patches == null) // Patch if it hasn't been patched.
				Patching.ManualInstance("Factory").Patch(original, new HarmonyMethod(typeof(FactoryPatches), method));
		}
	}

	[HarmonyPatch]
	static class MultipleBombsPatch
	{
		static MethodBase method;

		static bool Prepare()
		{
			var type = ReflectionHelper.FindType("MultipleBombsAssembly.MultipleBombs");
			method = type?.GetMethod("processBombEvents", BindingFlags.NonPublic | BindingFlags.Instance);
			return method != null;
		}

		static MethodBase TargetMethod() => method;

		static bool Prefix(Bomb bomb)
		{
			return allBombInfo[bomb].EnableOriginal;
		}
	}

	// Patches some stuff related to BombGenerator so we can handle instantiating components when DBML has finished loading them.
	[HarmonyPatch]
	static class GeneratorPatches
	{
		[HarmonyPatch(typeof(BombGenerator), "InstantiateComponent")]
		static bool Prefix(BombGenerator __instance, BombFace selectedFace, BombComponent bombComponentPrefab, GeneratorSetting settings)
		{
			var bomb = __instance.GetValue<Bomb>("bomb");
			var type = bombComponentPrefab.ComponentType;

			// Let the timer component spawn normally and record the bomb's information.
			if (type == ComponentTypeEnum.Timer)
			{
				allBombInfo.Add(bomb, new BombInfo(settings, selectedFace, __instance.GetValue<Random>("rand")));

				void logCallback(string condition, string _, LogType __)
				{
					if (!condition.StartsWith("[BombGenerator] Bomb component list: "))
						return;

 					// Replace the Random object with a fake one, so that we can make the consistent RNG calls later.
					__instance.SetValue("rand", new FakeRandom());

					Application.logMessageReceived -= logCallback;
				}

				Application.logMessageReceived += logCallback;

				return true;
			}

			// If we don't have information about a bomb, just let it go through.
			if (!allBombInfo.TryGetValue(bomb, out BombInfo bombInfo))
				return true;

			// Once we're ready to instantiate the components, this allows us to call the original method again.
			if (bombInfo.EnableOriginal)
				return true;

			// If the generator is trying to fill the bomb with empty components, clear the valid faces to skip over it.
			if (type == ComponentTypeEnum.Empty)
			{
				__instance.GetValue<List<BombFace>>("validBombFaces").Clear();
				return false;
			}

			// Start loading any fake modules.
			if (bombComponentPrefab.GetComponent<FakeModule>() != null)
				__instance.StartCoroutine(LoadModule(bombComponentPrefab, bombInfo));
			else
				bombInfo.Components.Add(bombComponentPrefab);

			return false;
		}

		[HarmonyPatch(typeof(Bomb), "SetTotalTime")]
		static void Prefix(Bomb __instance) => __instance.StartCoroutine(InstantiateComponents(__instance));
	}

	[HarmonyPatch(typeof(WidgetGenerator))]
	static class WidgetGeneratorPatch
	{
		[HarmonyPatch("Init")]
		static void Postfix(List<GameObject> areas, WidgetGenerator __instance)
		{
			var info = allBombInfo.First(pair => pair.Key.WidgetAreas == areas).Value;
			info.WidgetZones = __instance.GetValue<List<WidgetZone>>("zones");
		}

		[HarmonyPatch("GenerateWidgets")]
		static bool Prefix(WidgetManager widgetManager)
		{
			return allBombInfo.Any(pair => pair.Key.WidgetManager == widgetManager && pair.Value.EnableOriginal);
		}
	}
	#pragma warning restore IDE0051, RCS1213
}