using UnityEngine;
using Assets.Scripts.Missions;
using System.Collections.Generic;
using System.Linq;

static class BetterCasePicker
{
	public static BombCaseGenerator BombCaseGenerator;
	public static GameObject CaseParent;

	public static void PickCase()
	{
		// The game sets the seed to 33 for some reason, so we have to set the seed so it doesn't pick the same values every time.
		Random.InitState((int) System.DateTime.Now.Ticks);

		BombGenerator bombGenerator = Object.FindObjectOfType<BombGenerator>();
		if (bombGenerator.BombPrefabOverride == null) // No point in doing anything if they aren't even going to use the ObjectPool.
		{
			GameplayState gameplayState = SceneManager.Instance.GameplayState;
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

			bool frontFaceOnly = mission.GeneratorSetting.FrontFaceOnly;
			int componentCount = mission.GeneratorSetting.ComponentPools.Where(pool => pool.ModTypes == null || pool.ModTypes.Count == 0 || !(pool.ModTypes.Contains("Factory Mode") || pool.ModTypes[0].StartsWith("Multiple Bombs"))).Sum(pool => pool.Count) + 1;

			ObjectPool prefabPool = bombGenerator.BombPrefabPool;

			Dictionary<GameObject, int> bombcases = prefabPool.Objects
			.Where(gameobject => gameobject.GetComponent<KMBomb>() != null)
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
			if (Tweaks.settings.CaseGenerator)
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

				if (caseSize != default(Vector2))
				{
					if (CaseParent != null) Object.Destroy(CaseParent);

					// We have to parent the case to a GameObject that isn't active so it doesn't appear in the scene but itself is still active.
					CaseParent = new GameObject();
					CaseParent.SetActive(false);

					var caseGameObject = BombCaseGenerator.GenerateCase(caseSize);
					caseGameObject.transform.parent = CaseParent.transform;

					bombcases.Add(caseGameObject, (int) (caseSize.x * caseSize.y * (frontFaceOnly ? 1 : 2)));
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
	}
}
