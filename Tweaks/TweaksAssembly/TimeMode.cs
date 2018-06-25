using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using Assets.Scripts.Missions;

static class TimeMode
{
	static float _multiplier = 9;
	public static float Multiplier
	{
		get => Math.Max(_multiplier, 1);
		set => Math.Min(_multiplier, 10);
	}

	#pragma warning disable 649
	struct ModuleInfo
	{
		public string moduleID;
		public int moduleScore;
	}
	#pragma warning restore 649

	public static Dictionary<string, int> ComponentValues = new Dictionary<string, int>();

	static void UpdateComponentValues()
	{
		ModuleInfo[] moduleJSON = JsonConvert.DeserializeObject<ModuleInfo[]>(File.ReadAllText(Path.Combine(Application.persistentDataPath, "ModuleInformation.json")));

		ComponentValues = moduleJSON.ToDictionary(modInfo => modInfo.moduleID, modInfo => modInfo.moduleScore);
	}

	static Dictionary<ComponentTypeEnum, string> VanillaModuleIDs = new Dictionary<ComponentTypeEnum, string>()
	{
		{ ComponentTypeEnum.BigButton, "ButtonComponentSolver" },
		{ ComponentTypeEnum.Keypad, "KeypadComponentSolver" },
		{ ComponentTypeEnum.Maze, "InvisibleWallsComponentSolver" },
		{ ComponentTypeEnum.Memory, "MemoryComponentSolver" },
		{ ComponentTypeEnum.Morse, "MorseCodeComponentSolver" },
		{ ComponentTypeEnum.NeedyCapacitor, "NeedyDischargeComponentSolver" },
		{ ComponentTypeEnum.NeedyKnob, "NeedyKnobComponentSolver" },
		{ ComponentTypeEnum.NeedyVentGas, "NeedyVentComponentSolver" },
		{ ComponentTypeEnum.Password, "PasswordComponentSolver" },
		{ ComponentTypeEnum.Simon, "SimonComponentSolver" },
		{ ComponentTypeEnum.Venn, "VennWireComponentSolver" },
		{ ComponentTypeEnum.WhosOnFirst, "WhosOnFirstComponentSolver" },
		{ ComponentTypeEnum.Wires, "WireSetComponentSolver" },
		{ ComponentTypeEnum.WireSequence, "WireSequenceComponentSolver" }
	};

	public static string GetModuleID(BombComponent bombComponent)
	{
		switch (bombComponent.ComponentType)
		{
			case ComponentTypeEnum.Mod:
				KMBombModule bombModule = bombComponent.GetComponent<KMBombModule>();
				if (bombModule != null)
					return bombModule.ModuleType;

				break;
			case ComponentTypeEnum.NeedyMod:
				KMNeedyModule needyModule = bombComponent.GetComponent<KMNeedyModule>();
				if (needyModule != null)
					return needyModule.ModuleType;

				break;
			case ComponentTypeEnum.Empty:
			case ComponentTypeEnum.Timer:
				break;
			default:
				string ModuleIDs;
				if (VanillaModuleIDs.TryGetValue(bombComponent.ComponentType, out ModuleIDs))
					return ModuleIDs;

				break;
		}

		return null;
	}
}