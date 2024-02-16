using HarmonyLib;
using TweaksAssembly.Patching;

class CustomMissionTerms : Tweak
{
	public override void Setup()
	{
		Patching.EnsurePatch("custommissionterms", typeof(TermExistsPatch));
	}

	[HarmonyPatch(typeof(Localization), nameof(Localization.TermExists))]
	static class TermExistsPatch
	{
		static void Postfix(ref bool __result, string term)
		{
			if (!term.EqualsAny("mod/custom__DisplayName", "mod/custom__Description")) return;

			__result = false;
		}
	}
}