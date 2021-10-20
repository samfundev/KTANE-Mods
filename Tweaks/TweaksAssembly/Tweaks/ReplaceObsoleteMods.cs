using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using TweaksAssembly.Patching;
using UnityEngine;

class ReplaceObsoleteMods : Tweak
{
	public override bool ShouldEnable => SteamManager.Initialized && Tweaks.settings.ReplaceObsoleteMods;

	// Everything is stored as ulong since that's what the Steam API expects.
	// The key is the obsolete mod id and value is the new mod is.
	private static readonly Dictionary<ulong, ulong> obsoleteMods = new Dictionary<ulong, ulong>()
	{
		{ 1224413364, 2037350348 },
		{ 1459883764, 2036735094 },
		{ 2502467653, 1366808675 }
	};

	public override IEnumerator OnTweaksLoadingState()
	{
		yield return new WaitUntil(() => Repository.Loaded);

		LoadObsoleteMods();

		foreach (var modInfo in ModManager.Instance.InstalledModInfos.Values)
		{
			if (modInfo.SteamInfo == null)
				continue;

			var oldModID = modInfo.SteamInfo.PublishedFileID;
			if (!obsoleteMods.TryGetValue(oldModID, out ulong newModID))
				continue;

			// Unsubscribe from the old mod.
			Utilities.UnsubscribeFromMod(oldModID, (unsubResult) =>
			{
				if (unsubResult != EResult.k_EResultOK)
				{
					Tweaks.Log($"Failed to unsubscribe from obsolete mod: {modInfo.ID} ({oldModID}) ({unsubResult})");
					return;
				}

				// Subscribe to the new mod.
				Utilities.SubscribeToMod(newModID, (result) =>
				{
					if (result != EResult.k_EResultOK)
					{
						Tweaks.Log($"Failed to subscribe to new mod: {newModID} ({result})");
						return;
					}

					Toasts.Make($"Replaced obsolete mod: {modInfo.Title}");
					Tweaks.Log($"Replaced obsolete mod: {modInfo.Title} ({oldModID} -> {newModID})");

					// If DBML is enabled, automatically disable this module.
					var newModStringID = newModID.ToString();
					if (Tweaks.settings.DemandBasedModLoading && Repository.Modules.Any(module => module.Type.EqualsAny("Module", "Needy") && module.SteamID == newModStringID))
						Utilities.DisableMod(newModStringID);
					else
						SetupPatch.ReloadMods = true;
				});
			});
		}
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