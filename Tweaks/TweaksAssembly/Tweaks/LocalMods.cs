using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TweaksAssembly.Patching;

class LocalMods : Tweak
{
	public override bool ShouldEnable => !SteamManager.Initialized && Tweaks.settings.LocalMods.Count != 0;

	public override void Setup()
	{
		Patching.EnsurePatch("localmods", typeof(DisabledModsPatch));
	}

	public override IEnumerator OnTweaksLoadingState()
	{
		foreach (var steamID in Tweaks.settings.LocalMods)
		{
			Tweaks.Log($"Loading local mod {steamID}...");
			yield return ModManager.Instance.LoadMod(Path.Combine(Utilities.SteamWorkshopDirectory, steamID), Assets.Scripts.Mods.ModInfo.ModSourceEnum.SteamWorkshop);
		}
	}

	[HarmonyPatch(typeof(ModManager), "GetDisabledModPaths")]
	static class DisabledModsPatch
	{
		static void Postfix(ref List<string> __result)
		{
			var loadedMods = ModManager.Instance.GetValue<Dictionary<string, Mod>>("loadedMods");
			foreach (var steamID in Tweaks.settings.LocalMods)
			{
				var path = Path.Combine(Utilities.SteamWorkshopDirectory, steamID);
				if (SetupPatch.LoadingState != LoadingState.Normal || loadedMods.ContainsKey(path))
				{
					__result.Remove(path);
				}
				else if (!__result.Contains(path))
				{
					__result.Add(path);
				}
			}
		}
	}
}