using System.Collections.Generic;
using Assets.Scripts.Missions;
using HarmonyLib;
using TweaksAssembly.Patching;
using static Assets.Scripts.Missions.ComponentPool;

class LogAllowedSources : Tweak
{
	public override void Setup()
	{
		Patching.EnsurePatch("LogAllowedSources", typeof(ToStringPatch));
	}

	[HarmonyPatch(typeof(ComponentPool), "ToString")]
	static class ToStringPatch
	{
		static void Postfix(ComponentPool __instance, ref string __result)
		{
			if (__instance.SpecialComponentType == SpecialComponentTypeEnum.None) return;

			var allowedSources = __instance.AllowedSources;

			var sources = new List<string>();
			if ((allowedSources & ComponentSource.Base) == ComponentSource.Base) sources.Add("Vanilla");
			if ((allowedSources & ComponentSource.Mods) == ComponentSource.Mods) sources.Add("Modded");

			__result += $", Sources: {sources.Join(", ")}";
		}
	}
}