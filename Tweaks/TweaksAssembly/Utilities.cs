using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Settings;
using UnityEngine;

public static class Utilities
{
	public static void EnsureSteamRequestManager()
	{
		if (SteamWorkshopRequestManager.Instance != null)
			return;

		new GameObject().AddComponent<SteamWorkshopRequestManager>();
	}

	public static string SteamDirectory
	{
		get
		{
			// Mod folders
			var folders = Assets.Scripts.Services.AbstractServices.Instance.GetModFolders();
			if (folders.Count != 0)
			{
				return folders[0] + "/../../../../..";
			}

			// Relative to the game
			var relativePath = Path.GetFullPath("./../../..");
			if (new DirectoryInfo(relativePath).Name == "Steam")
			{
				return relativePath;
			}

			// Registry key
			using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
			{
				if (key?.GetValueNames().Contains("SteamPath") == true)
				{
					return key.GetValue("SteamPath")?.ToString().Replace('/', '\\') ?? string.Empty;
				}
			}

			// Guess common paths
			foreach (var path in new[]
			{
				@"Program Files (x86)\Steam",
				@"Program Files\Steam",
			})
			{
				foreach (var drive in Directory.GetLogicalDrives())
				{
					if (Directory.Exists(drive + path))
					{
						return drive + path;
					}
				}
			}

			foreach (var path in new[]
			{
				"~/Library/Application Support/Steam",
				"~/.steam/steam",
			})
			{
				if (Directory.Exists(path))
				{
					return path;
				}
			}

			return null;
		}
	}

	private static readonly List<string> steamIDs = new List<string>();

	public static void DisableMod(string steamID) => steamIDs.Add(steamID);

	public static bool FlushDisabledMods()
	{
		var newMods = false;
		var disabledMods = ModSettingsManager.Instance.ModSettings.DisabledModPaths.ToList();
		var modWorkshopPath = Path.GetFullPath(new[] { SteamDirectory, "steamapps", "workshop", "content", "341800" }.Aggregate(Path.Combine));

		foreach (var steamID in steamIDs)
		{
			var modPath = Path.Combine(modWorkshopPath, steamID);
			if (!disabledMods.Contains(modPath))
			{
				disabledMods.Add(modPath);
				newMods |= true;
			}
		}

		ModSettingsManager.Instance.ModSettings.DisabledModPaths = disabledMods.ToArray();
		ModSettingsManager.Instance.SaveModSettings();

		steamIDs.Clear();

		return newMods;
	}
}