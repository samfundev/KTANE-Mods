using UnityEngine;
using Assets.Scripts.Missions;
using System.Collections.Generic;
using System.Linq;

static class BetterCasePicker
{
	public static BombCaseGenerator BombCaseGenerator;
	public static GameObject CaseParent;

	public static BombGenerator BombGenerator;

	public static void HandleCaseGeneration()
	{
		// The game sets the seed to 33 for some reason, so we have to set the seed so it doesn't pick the same values every time.
		Random.InitState((int) System.DateTime.Now.Ticks);

		BombGenerator bombGenerator = Object.FindObjectOfType<BombGenerator>();
		BombGenerator = bombGenerator;
		if (bombGenerator.BombPrefabOverride == null) // No point in doing anything if they aren't even going to use the ObjectPool.
		{
			ObjectPool prefabPool = bombGenerator.BombPrefabPool;

			// Generate a case parent
			if (CaseParent != null) Object.Destroy(CaseParent);

			// We have to parent the case to a GameObject that isn't active so it doesn't appear in the scene but itself is still active.
			CaseParent = new GameObject();
			CaseParent.SetActive(false);

			// Override any KMGameCommands
			foreach (KMGameCommands gameCommands in Object.FindObjectsOfType<KMGameCommands>())
			{
				var previousDelegate = gameCommands.OnCreateBomb;

				gameCommands.OnCreateBomb = (string missionId, KMGeneratorSetting generatorSettings, GameObject spawnTarget, string seed) =>
				{
					HandleGeneratorSetting(ModMission.CreateGeneratorSettingsFromMod(generatorSettings), prefabPool);
					return previousDelegate(missionId, generatorSettings, spawnTarget, seed);
				};
			}

			// This must happen regardless of even BetterCasePicker is enabled so that the game can't try to spawn the fake case. 
			prefabPool.Objects = prefabPool.Objects.Where(gameobject => gameobject.name != "TweaksCaseGenerator").ToList();

			if (!Tweaks.settings.BetterCasePicker && !Tweaks.CaseGeneratorSettingCache)
			{
				return;
			}

			// Try to figure out what mission we are going into
			Mission mission = null;
			if (!string.IsNullOrEmpty(GameplayState.MissionToLoad))
			{
				if (GameplayState.MissionToLoad.Equals(FreeplayMissionGenerator.FREEPLAY_MISSION_ID))
				{
					mission = FreeplayMissionGenerator.Generate(GameplayState.FreeplaySettings);
				}
				else if (GameplayState.MissionToLoad.Equals(ModMission.CUSTOM_MISSION_ID))
				{
					mission = GameplayState.CustomMission;
				}
				else
				{
					mission = MissionManager.Instance.GetMission(GameplayState.MissionToLoad);
				}
			}

			if (mission == null)
			{
				Debug.LogError("[BetterCasePicker] Unable to find the current mission");
				return;
			}

			HandleGeneratorSetting(mission.GeneratorSetting, prefabPool);
		}
	}

	static GameObject previousGeneratedCase;
	public static void HandleGeneratorSetting(GeneratorSetting generatorSetting, ObjectPool prefabPool)
	{
		bool frontFaceOnly = generatorSetting.FrontFaceOnly;
		int componentCount = generatorSetting.ComponentPools.Where(pool => pool.ModTypes == null || pool.ModTypes.Count == 0 || !(pool.ModTypes.Contains("Factory Mode") || pool.ModTypes[0].StartsWith("Multiple Bombs"))).Sum(pool => pool.Count) + 1;

		Dictionary<GameObject, int> bombcases = prefabPool.Objects
		.Where(gameobject => gameobject != null && gameobject.GetComponent<KMBomb>() != null)
		.ToDictionary(gameobject => gameobject, gameobject =>
		{
			if (!frontFaceOnly)
			{
				return gameobject.GetComponent<KMBomb>().Faces.Select(face => face.Anchors.Count).Sum();
			}
			else
			{
				return gameobject.GetComponent<KMBomb>().Faces[0].Anchors.Count;
			}
		});

		bombcases.Add(prefabPool.Default, !frontFaceOnly ? 12 : 6);

		// Generate a case using Case Generator
		if (Tweaks.CaseGeneratorSettingCache)
		{
			List<Vector2> caseSizes = new List<Vector2>();
			for (int x = 1; x <= componentCount; x++)
				for (int y = 1; y <= componentCount; y++)
					if (x >= y)
						caseSizes.Add(new Vector2(x, y));

			var caseSize = caseSizes
				.Where(size => size.y / size.x >= 0.5f && size.x * size.y * (frontFaceOnly ? 1 : 2) >= componentCount)
				.OrderBy(size => System.Math.Abs(size.x * size.y * (frontFaceOnly ? 1 : 2) - componentCount))
				.ThenByDescending(size => size.y / size.x)
				.FirstOrDefault();

			if (caseSize != default)
			{
				var caseGameObject = BombCaseGenerator.GenerateCase(caseSize);
				caseGameObject.transform.parent = CaseParent.transform;

				bombcases.Add(caseGameObject, (int) (caseSize.x * caseSize.y * (frontFaceOnly ? 1 : 2)));

				if (previousGeneratedCase != null)
					bombcases.Remove(previousGeneratedCase);

				previousGeneratedCase = caseGameObject;
			}
		}

		if (bombcases.Count == 0)
		{
			Debug.LogError("[BetterCasePicker] Unable to find any bomb cases to use");
			return;
		}

		var validBombCases = bombcases.Where(pair => pair.Value >= componentCount);
		var minBombCases = !validBombCases.Any() ?
			bombcases.Where(pair => pair.Value == bombcases.Max(pair2 => pair2.Value)) :
			validBombCases.Where(pair => pair.Value == validBombCases.Min(pair2 => pair2.Value));

		prefabPool.Objects = minBombCases.Select(x => x.Key).ToList();
	}

	static Dictionary<KMGameCommands, KMGameCommands.CreateBombDelegate> previousDelegates = new Dictionary<KMGameCommands, KMGameCommands.CreateBombDelegate>();
	static Dictionary<KMGameCommands, KMGameCommands.CreateBombDelegate> addedDelegates = new Dictionary<KMGameCommands, KMGameCommands.CreateBombDelegate>();
	public static void RestoreGameCommands()
	{
		foreach (var pair in previousDelegates)
			pair.Key.OnCreateBomb += pair.Value;

		foreach (var pair in addedDelegates)
			pair.Key.OnCreateBomb -= pair.Value;

		previousDelegates.Clear();
		addedDelegates.Clear();
	}
}
