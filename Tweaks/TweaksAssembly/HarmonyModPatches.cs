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
		private static Dictionary<string, ModInfo> InstalledModInfos;
		public static void ResetDict()
		{
			if (InstalledModInfos == null)
				return;
			ModManager.Instance.InstalledModInfos.Clear();
			foreach(var pair in InstalledModInfos)
				ModManager.Instance.InstalledModInfos.Add(pair.Key, pair.Value);
		}

		public static void Prefix(ModManager __instance)
		{
			if (HarmonyPatchInfo.ModInfoFile == "modInfo_Harmony.json")
				InstalledModInfos = __instance.InstalledModInfos.ToDictionary(p => p.Key, p => p.Value);
		}
	}

	[HarmonyPatch(typeof(ModManagerState), "ReturnToSetupState")]
	[HarmonyPriority(Priority.First)]
	internal static class SetupPatch
	{
		public static bool InitialLoadCompleted;

		public static bool Prefix()
		{
			bool cont = HarmonyPatchInfo.ToggleModInfo();
			if (cont)
			{
				InitialLoadCompleted = true;
				return true;
			}

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