using System.Collections;
using System.Collections.Generic;
using Steamworks;
using TweaksAssembly.Patching;
using UnityEngine;

class SubscribeToNewMods : Tweak
{
	public override bool ShouldEnable => SteamManager.Initialized && Tweaks.settings.SubscribeToNewMods;

	public override IEnumerator OnTweaksLoadingState()
	{
		yield return new WaitUntil(() => Repository.Loaded);

		var disabledMods = new List<ulong>();

		foreach (var module in Repository.Modules)
		{
			if (!module.Type.EqualsAny("Regular", "Needy"))
				continue;

			if (!ulong.TryParse(module.SteamID, out ulong steamID))
				continue;

			if (Utilities.IsInstalled(steamID))
				continue;

			// Subscribe to the new mod.
			Utilities.SubscribeToMod(steamID, (subResult) =>
			{
				if (subResult != EResult.k_EResultOK)
				{
					Tweaks.Log($"Failed to subscribe to new mod: {steamID} ({module.Name}) ({subResult})");
					return;
				}

				Toasts.Make($"Subscribed to new mod: {module.Name}");
				Tweaks.Log($"Subscribed to new mod: {module.Name} ({steamID})");

				// If DBML is enabled, automatically disable this module.
				if (Tweaks.settings.DemandBasedModLoading)
					Utilities.DisableMod(steamID.ToString());
				else
					SetupPatch.ReloadMods = true;
			});
		}
	}
}