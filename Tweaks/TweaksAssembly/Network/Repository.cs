using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class Repository
{
	public static string RawJSON;
	public static List<KtaneModule> Modules;
	public static bool Loaded;

	public static IEnumerator LoadData()
	{
		if (RawJSON != null)
			yield break;

		var download = new DownloadText("https://ktane.timwi.de/json/raw");
		yield return download;

		var repositoryBackup = Path.Combine(Application.persistentDataPath, "RepositoryBackup.json");

		RawJSON = download.Text;
		if (RawJSON == null)
		{
			Tweaks.Log("Unable to download the repository.");

			if (File.Exists(repositoryBackup))
				RawJSON = File.ReadAllText(repositoryBackup);
			else
				Tweaks.Log("Could not find a repository backup.");
		}

		if (RawJSON == null)
		{
			Tweaks.Log("Could not get module information.");

			Modules = new List<KtaneModule>();
		}
		else
		{
			// Save a backup of the repository
			File.WriteAllText(repositoryBackup, RawJSON);

			Modules = JsonConvert.DeserializeObject<WebsiteJSON>(RawJSON).KtaneModules;
		}
		Loaded = true;
	}

	public static bool IsBossMod(this string moduleID) => Modules.Any(module => module.ModuleID == moduleID && module.Ignore != null);

#pragma warning disable CS0649
	public class WebsiteJSON
	{
		public List<KtaneModule> KtaneModules;
	}

	public class KtaneModule
	{
		public string SteamID;
		public string Name;
		public string ModuleID;
		public string Type;
		public string Compatibility;
		public Dictionary<string, object> TwitchPlays;
		public string[] ObsoleteSteamIDs;

		public List<string> Ignore;
	}
#pragma warning restore CS0649
}