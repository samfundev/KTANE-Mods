using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using TweaksAssembly.Patching;
using UnityEngine;
using static Assets.Scripts.Mods.ModInfo;

class ReplaceObsoleteMods : Tweak
{
	public override bool Enabled => Tweaks.settings.ReplaceObsoleteMods;

	// Everything is stored as ulong since that's what the Steam API expects.
	private static readonly Dictionary<ulong, ulong> obsoleteMods = new Dictionary<ulong, ulong>()
	{
		{ 1224413364, 2037350348 },
		{ 1459883764, 2036735094 },
		{ 2502467653, 1366808675 }
	};

	private static bool removedModules;

	public override IEnumerator OnTweaksLoadingState()
	{
		if (removedModules)
			yield break;

		yield return new WaitUntil(() => Repository.Loaded);

		LoadObsoleteMods();

		foreach (var modInfo in ModManager.Instance.InstalledModInfos.Values)
		{
			if (modInfo.ModSource != ModSourceEnum.SteamWorkshop)
				continue;

			var oldModID = modInfo.SteamInfo.PublishedFileID;
			if (!obsoleteMods.TryGetValue(oldModID, out ulong newModID))
				continue;

			Utilities.EnsureSteamRequestManager();

			// Unsubscribe from the old mod.
			SteamWorkshopRequestManager.Instance.UnsubscribeFromItem(new PublishedFileId_t(oldModID), (unsubResult) =>
			{
				if (unsubResult != EResult.k_EResultOK)
				{
					Tweaks.Log($"Failed to unsubscribe from obsolete mod: {modInfo.ID} ({oldModID}) ({unsubResult})");
					return;
				}

				// Subscribe to the new mod.
				SteamWorkshopRequestManager.Instance.SubscribeToItem(new PublishedFileId_t(newModID), (subResult) =>
				{
					if (subResult != EResult.k_EResultOK)
					{
						Tweaks.Log($"Failed to subscribe to new mod: {newModID} ({subResult})");
						return;
					}

					Toasts.Make($"Replaced obsolete mod: {modInfo.Title} ({oldModID} -> {newModID})");

					// If DBML is enabled, automatically disable this module.
					var newModStringID = newModID.ToString();
					if (Tweaks.settings.DemandBasedModLoading && Repository.Modules.Any(module => module.Type.EqualsAny("Module", "Needy") && module.SteamID == newModStringID))
						Utilities.DisableMod(newModStringID);

					SetupPatch.ReloadMods |= true;
				});
			});
		}

		Utilities.FlushDisabledMods();

		removedModules = true;
	}

	private void LoadObsoleteMods()
	{
		foreach (var module in Repository.Modules)
		{
			if (module.ObsoleteSteamIDs == null)
				continue;

			if (!ulong.TryParse(module.SteamID, out ulong newSteamID))
			{
				Tweaks.Log($"Unable to parse Steam ID: {module.SteamID}");
				continue;
			}

			foreach (var stringID in module.ObsoleteSteamIDs)
			{
				if (!ulong.TryParse(stringID, out ulong oldSteamID))
				{
					Tweaks.Log($"Unable to parse Steam ID: {stringID}");
					continue;
				}

				if (obsoleteMods.TryGetValue(oldSteamID, out ulong existing) && existing != newSteamID)
				{
					continue;
				}

				obsoleteMods[oldSteamID] = newSteamID;
			}
		}
	}
}