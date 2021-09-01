using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Assets.Scripts.BombBinder;
using HarmonyLib;
using TweaksAssembly.Patching;

public class AlphabeticBinder : Tweak
{
	public override void Setup()
	{
		Patching.EnsurePatch("AlphabeticBinder", typeof(PageManagerPatch));
	}

	private static void SortBinder()
	{
		var pageManager = UnityEngine.Object.FindObjectOfType<SetupRoom>().BombBinder.MissionTableOfContentsPageManager;
		if (!pageManager.HasMultipleToCs())
			return;

		var tocMetaDataList = pageManager.GetValue<List<TableOfContentsMetaData>>("tocMetaDataList");

		string getDisplayName(TableOfContentsMetaData metadata) => Regex.Replace(Localization.GetLocalizedString(metadata.DisplayNameTerm) ?? "", "^The ", "");

		tocMetaDataList.Sort((a, b) =>
		{
			if (a.ID == "toc_basegame")
				return -1;
			else if (b.ID == "toc_basegame")
				return 1;

			return getDisplayName(a).CompareTo(getDisplayName(b));
		});
	}

	[HarmonyPatch(typeof(MissionTableOfContentsPageManager))]
	private static class PageManagerPatch
	{
		[HarmonyPatch("SetupTableOfContentsMetaData")]
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instruction in instructions)
			{
				yield return instruction;

				if (instruction.opcode == OpCodes.Blt)
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AlphabeticBinder), "SortBinder"));
			}
		}
	}
}