using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class BetterCasePicker : MonoBehaviour
{
	public Type FindType(string fullName)
	{
		return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName.Equals(fullName));
	}

	void LogError(string formatting, params object[] args)
	{
		Debug.LogAssertionFormat("[BetterCasePicker] " + formatting, args);
	}

	Type _gameplayStateType;
	FieldInfo _missionToLoadField;
	FieldInfo _freeplaySettingsField;
	FieldInfo _customMissionField;

	Type _missionManagerType;
	PropertyInfo _instanceProperty;
	MethodInfo _getMissionMethod;

	Type _missionType;
	FieldInfo _generatorSettingField;

	Type _generatorSettingType;
	MethodInfo _getComponentCountMethod;
	FieldInfo _frontFaceOnlyField;

	object bombGenerator;

	Type _bombGeneratorType;
	FieldInfo _bombPrefabOverrideField;
	FieldInfo _bombPrefabPoolField;

	Type _objectPoolType;
	FieldInfo _objectsField;
	FieldInfo _defaultField;

	void Start()
	{
		BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;
		BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

		_gameplayStateType = FindType("GameplayState");
		_missionToLoadField = _gameplayStateType.GetField("MissionToLoad", Static);
		_freeplaySettingsField = _gameplayStateType.GetField("FreeplaySettings", Static);
		_customMissionField = _gameplayStateType.GetField("CustomMission", Static);

		_missionManagerType = FindType("Assets.Scripts.Missions.MissionManager");
		_instanceProperty = _missionManagerType.GetProperty("Instance", Static);
		_getMissionMethod = _missionManagerType.GetMethod("GetMission", Public);

		_missionType = FindType("Assets.Scripts.Missions.Mission");
		_generatorSettingField = _missionType.GetField("GeneratorSetting", Public);

		_generatorSettingType = FindType("Assets.Scripts.Missions.GeneratorSetting");
		_getComponentCountMethod = _generatorSettingType.GetMethod("GetComponentCount", Public);
		_frontFaceOnlyField = _generatorSettingType.GetField("FrontFaceOnly", Public);

		_bombGeneratorType = FindType("BombGenerator");
		_bombPrefabOverrideField = _bombGeneratorType.GetField("BombPrefabOverride", Public);
		_bombPrefabPoolField = _bombGeneratorType.GetField("BombPrefabPool", Public);

		_objectPoolType = FindType("ObjectPool");
		_objectsField = _objectPoolType.GetField("Objects", Public);
		_defaultField = _objectPoolType.GetField("Default", Public);

		GetComponent<KMGameInfo>().OnStateChange += delegate (KMGameInfo.State state)
		{
			if (state == KMGameInfo.State.Gameplay)
			{
				bombGenerator = FindObjectOfType(_bombGeneratorType);

				if (_bombPrefabOverrideField.GetValue(bombGenerator) == null) // Don't replace the bomb prefab if there is already one.
				{
					int componentCount = 0;
					bool frontFaceOnly = false;
					string missionID = (string) _missionToLoadField.GetValue(null);
					if (missionID == "freeplay")
					{
						object freeplaySettings = _freeplaySettingsField.GetValue(null);
						componentCount = (int) freeplaySettings.GetType().GetField("ModuleCount", Public).GetValue(freeplaySettings);
					}
					else
					{
						object mission;
						if (missionID == "custom") mission = _customMissionField.GetValue(null);
						else mission = _getMissionMethod.Invoke(_instanceProperty.GetValue(null, null), new object[] { missionID });

						if (mission == null)
						{
							LogError("Unable to find a mission to get information from.");
							return;
						}

						object generatorSetting = _generatorSettingField.GetValue(mission);
						frontFaceOnly = (bool) _frontFaceOnlyField.GetValue(generatorSetting);

						componentCount = (int) _getComponentCountMethod.Invoke(generatorSetting, null);
					}
					
					componentCount += 1; // We need one spot for the timer as well.
					
					object prefabPool = _bombPrefabPoolField.GetValue(bombGenerator);
					List<GameObject> gameObjects = (List<GameObject>) _objectsField.GetValue(prefabPool);
					
					var bombcases = gameObjects
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

					bombcases.Add((GameObject) _defaultField.GetValue(prefabPool), (!frontFaceOnly ? 12 : 6));

					if (bombcases.Count == 0)
					{
						LogError("Unable to find any bomb cases to use.")
						return;
					}

					var validBombCases = bombcases.Where(pair => pair.Value >= componentCount);

					if (validBombCases.Count() == 0)
					{
						_bombPrefabOverrideField.SetValue(bombGenerator, PickBySize(bombcases, bombcases.Max(x => x.Value)));
					}
					else
					{
						_bombPrefabOverrideField.SetValue(bombGenerator, PickBySize(validBombCases, validBombCases.Min(x => x.Value)));
					}
				}
			}
		};
	}

	GameObject PickBySize(IEnumerable<KeyValuePair<GameObject, int>> bombCases, int size)
	{
		var matchingSizes = bombCases.Where(x => x.Value == size).Select(x => x.Key);

		return matchingSizes.ElementAt(new System.Random().Next(matchingSizes.Count()));
	}
}
