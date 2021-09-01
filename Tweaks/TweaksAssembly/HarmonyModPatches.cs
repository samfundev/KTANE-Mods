using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using Assets.Scripts.Mods;
using Assets.Scripts.Mods.Screens;
using Assets.Scripts.Services;
using Assets.Scripts.Settings;
using HarmonyLib;
using TMPro;

namespace TweaksAssembly.Patching
{
	enum LoadingState
	{
		Normal,
		Harmony,
		Tweaks
	}

	internal static class HarmonyPatchInfo
	{
		public static string ModInfoFile = "modInfo.json";

		public static bool ToggleModInfo()
		{
			if (ModInfoFile == "modInfo.json")
			{
				ModInfoFile = "modInfo_Harmony.json";
				return false;
			}
			ModInfoFile = "modInfo.json";
			return true;
		}
	}

	[HarmonyPatch(typeof(ModManager), "GetModInfoFromPath")]
	[HarmonyPriority(Priority.First)]
	internal static class ModInfoPatch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldstr && (string) instruction.operand == "modInfo.json")
					yield return new CodeInstruction(OpCodes.Ldsfld,
						typeof(HarmonyPatchInfo).GetField("ModInfoFile", AccessTools.all));
				else yield return instruction;
			}
		}
	}

	[HarmonyPatch(typeof(ManageModsScreen), "OnEnable")]
	[HarmonyPriority(Priority.First)]
	internal static class WorkshopPatch
	{
		public static bool Prefix(ManageModsScreen __instance, ref List<ModInfo> ___installedMods, ref List<ModInfo> ___fullListOfMods, ref List<ModInfo> ___allSubscribedMods)
		{
			ModManager.Instance.ReloadModMetaData();
			___installedMods = ModManager.Instance.InstalledModInfos.Values
				.Where(info => File.Exists(Path.Combine(info.FilePath, HarmonyPatchInfo.ModInfoFile))).ToList();
			___allSubscribedMods = AbstractServices.Instance.GetSubscribedMods();
			___fullListOfMods = HarmonyPatchInfo.ModInfoFile == "modInfo.json"
				? ___installedMods.Union(___allSubscribedMods).ToList()
				: ___installedMods;
			___fullListOfMods.Sort((ModInfo a, ModInfo b) => a.Title.CompareTo(b.Title));
			Traverse.Create(__instance).Method("ShowMods").GetValue();
			return false;
		}
	}

	[HarmonyPatch(typeof(ModManager), "ReloadModMetaData")]
	[HarmonyPriority(Priority.First)]
	public static class ReloadPatch
	{
		public static bool Prefix(ModManager __instance)
		{
			if (SetupPatch.LoadingState == LoadingState.Normal)
				return true;

			foreach (ModInfo.ModSourceEnum source in new List<ModInfo.ModSourceEnum>
			{
				ModInfo.ModSourceEnum.Local,
				AbstractServices.Instance.GetModSource()
			})
			{
				foreach (string text in __instance.GetAllModPathsFromSource(source))
				{
					ModInfo modInfoFromPath = __instance.GetModInfoFromPath(text, source);
					if (modInfoFromPath != null)
					{
						__instance.InstalledModInfos[text] = modInfoFromPath;
					}
				}
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(ModManagerState), "ReturnToSetupState")]
	[HarmonyPriority(Priority.First)]
	internal static class SetupPatch
	{
		public static bool InitialLoadCompleted;
		public static LoadingState LoadingState;

		public static Action OnTweaksLoadingState;
		public static List<IEnumerator> LoadingList = new List<IEnumerator>();
		public static bool ReloadMods;

		public static bool Prefix()
		{
			switch (LoadingState)
			{
				case LoadingState.Normal:
					InitialLoadCompleted = true;
					HarmonyPatchInfo.ToggleModInfo();
					LoadingState = LoadingState.Harmony;

					// "Re-enter" the Mod Manager to manage harmony mods
					if (Tweaks.settings.ManageHarmonyMods && (!PlayerSettingsManager.Instance.PlayerSettings.UseModsAlways || InitialLoadCompleted))
					{
						ModManagerScreenManager.Instance.OpenManageInstalledModsScreen();
					}
					else
					{
						ModManagerScreenManager.Instance.OpenModLoadingScreenAndReturnToGame();
					}

					return false;
				case LoadingState.Harmony:
					HarmonyPatchInfo.ToggleModInfo();
					LoadingState = LoadingState.Tweaks;

					Tweaks.Instance.StartCoroutine(TweaksLoadingState());

					return false;
				case LoadingState.Tweaks:
					LoadingState = LoadingState.Normal;
					return true;
				default:
					throw new Exception($"Unknown loading state: {LoadingState}. This should never happen.");
			}
		}

		static IEnumerator TweaksLoadingState()
		{
			ReloadMods = false;
			LoadingList.Clear();
			OnTweaksLoadingState();

			foreach (var loadingRoutine in LoadingList)
			{
				yield return loadingRoutine;
			}

			if (ReloadMods)
			{
				ModManagerScreenManager.Instance.OpenModLoadingScreenAndReturnToGame();
			}
			else
			{
				SceneManager.Instance.ModManagerState.ReturnToSetupState();
			}
		}
	}

	[HarmonyPatch(typeof(ModManagerManualInstructionScreen), "HandleOpenManualFolder")]
	[HarmonyPriority(Priority.First)]
	internal static class ManualButtonPatch
	{
		public static bool Prefix()
		{
			return SetupPatch.Prefix();
		}
	}

	[HarmonyPatch(typeof(ModManagerState), "ShouldShowManualInstructions")]
	[HarmonyPriority(Priority.First)]
	internal static class InstructionPatch
	{
		public static bool HasShownOnce;

		public static bool Prefix(out bool __result)
		{
			if (PlayerSettingsManager.Instance.PlayerSettings.UseModsAlways || HarmonyPatchInfo.ModInfoFile == "modInfo.json")
				__result = false;
			else
			{
				__result = !HasShownOnce;
				HasShownOnce = true;
			}

			return false;
		}
	}

	[HarmonyPatch(typeof(MenuScreen), "EnterScreenComplete")]
	[HarmonyPriority(Priority.First)]
	internal static class ChangeButtonText
	{
		public static void Postfix(MenuScreen __instance)
		{
			if (HarmonyPatchInfo.ModInfoFile == "modInfo_Harmony.json" && __instance is ManageModsScreen ManagerScreen)
			{
				ManagerScreen.GetComponentInChildren<TextMeshProUGUI>().text = "Manage installed Harmony mods";
				ManagerScreen.BackButton.OnInteract = () =>
				{
					ModManagerScreenManager.Instance.OpenModLoadingScreenAndReturnToGame();
					return false;
				};
			}
		}
	}
}