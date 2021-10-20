using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using TweaksAssembly.Patching;
using UnityEngine;

class SubscribeToNewMods : Tweak
{
	public override bool ShouldEnable => Tweaks.settings.SubscribeToNewMods;

	private static bool subscribed;

	public override IEnumerator OnTweaksLoadingState()
	{
		if (subscribed)
			yield break;

		yield return new WaitUntil(() => Repository.Loaded);

		var disabledMods = new List<ulong>();

		var installedIDs = ModManager.Instance.InstalledModInfos.Values
			.Where(module => module.SteamInfo != null)
			.Select(module => module.SteamInfo.PublishedFileID);
		foreach (var module in Repository.Modules)
		{
			if (!module.Type.EqualsAny("Regular", "Needy"))
				continue;

			if (!ulong.TryParse(module.SteamID, out ulong steamID))
				continue;

			if (installedIDs.Contains(steamID))
				continue;

			Utilities.EnsureSteamRequestManager();

			// Subscribe to the new mod.
			SteamWorkshopRequestManager.Instance.SubscribeToItem(new PublishedFileId_t(steamID), (subResult) =>
			{
				if (subResult != EResult.k_EResultOK)
				{
					Tweaks.Log($"Failed to subscribe to new mod: {steamID} ({module.Name}) ({subResult})");
					return;
				}

				Toasts.Make($"Subscribed to new mod: {module.Name} ({steamID})");

				// If DBML is enabled, automatically disable this module.
				if (Tweaks.settings.DemandBasedModLoading)
					Utilities.DisableMod(steamID.ToString());

				SetupPatch.ReloadMods |= true;
			});
		}

		Utilities.FlushDisabledMods();

		subscribed = true;
	}
}