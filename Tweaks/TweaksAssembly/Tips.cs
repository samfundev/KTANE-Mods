using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class Tips
{
	public static GameObject TipMessage;

	static TweakSettings Settings => Tweaks.settings;

	static readonly Tip[] tips = new Tip[]
	{
		new Tip("Change how long it takes to fade in and out using the \"FadeTime\" setting.", () => Settings.FadeTime == 1, "settings-FadeTime"),
		new Tip("Skip sitting in the dark before a bomb starts using the \"SkipGameplayDelay\" setting.", () => !Settings.SkipGameplayDelay, "settings-SkipGameplayDelay"),
		new Tip("Enable the mods only key by default using the \"EnableModsOnlyKey\" setting.", () => !Settings.EnableModsOnlyKey, "settings-EnableModsOnlyKey"),
		new Tip("Load modules on demand to save on bootup time and memory using the \"DemandBasedModLoading\" setting.", () => !Settings.DemandBasedModLoading, "settings-DemandBasedModLoading"),
		new Tip("Automatically disable modules that are loaded on demand using the \"DisableDemandBasedMods\" setting.", () => !Settings.DisableDemandBasedMods, "settings-DisableDemandBasedMods"),
		new Tip("Fix Foreign Exchange Rates not loading exchange rates using the \"FixFER\" setting.", () => !Settings.FixFER, "settings-FixFER"),
		new Tip("See the current status of the bomb using the \"BombHUD\" setting.", () => !Settings.BombHUD, "settings-BombHUD"),
		new Tip("See a bomb's edgework on screen using the \"ShowEdgework\" setting.", () => !Settings.ShowEdgework, "settings-ShowEdgework"),
		new Tip("Temporarily disable advantageous features using the \"DisableAdvantageous\" setting.", () => !Settings.DisableAdvantageous, "settings-DisableAdvantageous"),
		new Tip("Hide unnecessary table of contents entries using the \"HideTOC\" setting.", () => Settings.HideTOC.Count == 0, "settings-HideTOC"),
		new Tip("Try out different modes for defusing bombs using the \"Mode\" setting.", () => Settings.Mode == Mode.Normal, "settings-Mode"),
		new Tip("Generate the same bomb consistently using the \"MissionSeed\" setting.", () => Settings.MissionSeed == -1, "settings-MissionSeed"),
		new Tip("Give generated cases a new splash of color using the \"CaseColors\" setting.", () => Settings.CaseGenerator && Settings.CaseColors.Count == 0, "settings-CaseColors"),
		new Tip("Pin frequently used settings using the pin icon in the Mod Settings app.", () => Settings.PinnedSettings.Count == 0),
	};

	public class Tip
	{
		public string Text;
		public Func<bool> Check;
		public string DocID;

		public Tip(string text, Func<bool> check, string docID = null)
		{
			Text = text;
			Check = check;
			DocID = docID;
		}
	}

	static Tip GetTip() => tips.Where(tip => tip.Check()).Shuffle().FirstOrDefault();

	static bool tipShown;
	public static IEnumerator ShowTip()
	{
		Tweaks.FixRNGSeed();

		var tip = GetTip();
		if (tip == null || tipShown || Tweaks.SettingWarningEnabled)
			yield break;

		tipShown = true;

		Text tipText = TipMessage.Traverse<Text>("TipText");
		tipText.text = $"Tweaks Tip: {tip.Text}";

		if (tip.DocID != null)
		{
			tipText.text += "\n(Click to learn more, opens website)";
			TipMessage.GetComponent<Button>().onClick.AddListener(() => Process.Start($"https://samfun123.github.io/KTANE-Mods/tweaks.html#{tip.DocID}"));
		}

		yield return new WaitUntil(() => Application.isFocused);
		TipMessage.SetActive(true);

		var canvasGroup = TipMessage.GetComponent<CanvasGroup>();
		foreach (float alpha in (0.75f).TimedAnimation().EaseCubic())
		{
			canvasGroup.alpha = alpha;
			yield return null;
		}

		var startTime = Time.time;
		while ((Time.time - startTime < 8 || Tweaks.CurrentState == KMGameInfo.State.Transitioning) && Tweaks.CurrentState == KMGameInfo.State.Setup)
			yield return null;

		foreach (float alpha in (0.75f).TimedAnimation().EaseCubic())
		{
			canvasGroup.alpha = 1 - alpha;
			yield return null;
		}

		TipMessage.SetActive(false);
	}
}
