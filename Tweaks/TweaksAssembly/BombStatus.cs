using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

class BombStatus : MonoBehaviour
{
	public static BombStatus Instance;

    public GameObject HUD = null;
    public GameObject Edgework = null;

    public Text TimerPrefab = null;
	public Text TimerShadowPrefab = null;
	public Text StrikesPrefab = null;
	public Text SolvesPrefab = null;
	public Text NeediesPrefab = null;
	public Text ConfidencePrefab = null;
	public Text EdgeworkPrefab = null;

	public BombWrapper currentBomb = null;

	private CanvasGroup CanvasGroup;

	private int currentSolves;
	private int currentStrikes;
	private int currentTotalModules;
	private int currentTotalStrikes;
	internal bool widgetsActivated;

	void Start()
	{
		Instance = this;
		GameplayState.OnLightsOnEvent += delegate { widgetsActivated = true; };

		CanvasGroup = GetComponent<CanvasGroup>();
		CanvasGroup.alpha = 0;
	}

	void LateUpdate()
	{
		if (currentBomb == null)
		{
			WidgetResponses.Clear();

			currentBomb = Array.Find(Tweaks.bombWrappers, x => x.holdable.HoldState == FloatingHoldable.HoldStateEnum.Held);
			if (currentBomb != null)
			{
				UpdateSolves();
				UpdateStrikes();

				int needies = currentBomb.Bomb.BombComponents.Count(bombComponent => bombComponent.GetComponent<NeedyComponent>() != null);
				NeediesPrefab.gameObject.SetActive(needies > 0);
				NeediesPrefab.text = needies.ToString();
			}
		}

		bool enabled = currentBomb != null && !Tweaks.TwitchPlaysActive;
		CanvasGroup.alpha = Math.Min(Math.Max(CanvasGroup.alpha + (enabled ? 1 : -1) * 0.1f, 0), 1);
		if (!enabled) return;

		string formattedTime = currentBomb.GetFullFormattedTime;
		TimerPrefab.text = formattedTime;
		TimerShadowPrefab.text = Regex.Replace(formattedTime, @"\d", "8");
		UpdateConfidence();
		UpdateWidgets();
	}

	private IEnumerator UpdateStrikesCoroutine(bool delay)
	{
		if (delay)
		{
			// Delay for a single frame if this has been called from an OnStrike method
			// Necessary since the bomb doesn't update its internal counter until all its OnStrike handlers are finished
			yield return 0;
		}
		if (currentBomb == null) yield break;
		currentStrikes = currentBomb.StrikeCount;
		currentTotalStrikes = currentBomb.StrikeLimit;
		string strikesText = currentStrikes.ToString().PadLeft(currentTotalStrikes.ToString().Length, '0');
		StrikesPrefab.text = Tweaks.CurrentMode != Mode.Zen ? $"{strikesText}<size=25>/{currentTotalStrikes}</size>" : strikesText;
	}

	public void UpdateStrikes(bool delay = false)
	{
		StartCoroutine(UpdateStrikesCoroutine(delay));
	}

	public void UpdateSolves()
	{
		if (currentBomb == null) return;
		currentSolves = currentBomb.bombSolvedModules;
		currentTotalModules = currentBomb.bombSolvableModules;
		string solves = currentSolves.ToString().PadLeft(currentTotalModules.ToString().Length, '0');
		SolvesPrefab.text = $"{solves}<size=25>/{currentTotalModules}</size>";
	}

	public void UpdateConfidence()
	{
		if (Tweaks.CurrentMode == Mode.Time)
		{
			string conf = "<size=36>x</size>" + String.Format("{0:0.0}", Math.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier));
			StrikesPrefab.color = Color.yellow;
			StrikesPrefab.text = conf;
		}
		else
		{
			StrikesPrefab.color = Color.red;
		}

		float success = PlayerPaceRating;
		ConfidencePrefab.text = Mathf.Round(success * 100).ToString() + "%";
		ConfidencePrefab.color = success < 0 ? Color.Lerp(Color.gray, Color.red, Mathf.Sqrt(-success)) : Color.Lerp(Color.grey, Color.green, Mathf.Sqrt(success));
	}

	public float PlayerPaceRating
	{
		get
		{
            float remaining = currentBomb.CurrentTimer;

			return Tweaks.CurrentMode == Mode.Time ? remaining / (Modes.settings.TimeModeStartingTime * 60) - 1 : (float) currentSolves / currentTotalModules - (currentBomb.bombStartingTimer - remaining / currentBomb.timerComponent.GetRate()) / currentBomb.bombStartingTimer;
		}
	}

	// Edgework
	public IEnumerable<Dictionary<string, T>> QueryWidgets<T>(string queryKey, string queryInfo = null)
	{
		return currentBomb.widgetManager.GetWidgetQueryResponses(queryKey, queryInfo).Select(str => Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, T>>(str));
	}

	private Dictionary<string, string> WidgetResponses = new Dictionary<string, string>();

	private void UpdateWidgets()
	{
		var ind = KMBombInfo.QUERYKEY_GET_INDICATOR;
		const string tf = "twofactor";
		const string man = "manufacture";
		const string day = "day";
		const string time = "time";
		var newResponses = new Dictionary<string, string>
		{
			{ ind, QueryWidgets<string>(ind).Select(x => x.Values).Join() },
			{ tf, QueryWidgets<string>(tf).Select(x => x["twofactor_key"]).Join() },
			{ man, QueryWidgets<string>(man).Select(x => x.Values).Join() },
			{ day, QueryWidgets<string>(day).Select(x => x.Values).Join() },
			{ time, QueryWidgets<string>(time).Select(x => x.Values).Join() }
		};
		if (!widgetsActivated)
			EdgeworkPrefab.text = null;
		if (widgetsActivated && (WidgetResponses.Count < 1 || !newResponses.SequenceEqual(WidgetResponses)))
		{
			WidgetResponses = new Dictionary<string, string>(newResponses);
			EdgeworkPrefab.text = EdgeworkText;
		}
	}

	public string EdgeworkText
	{
		get
		{
			if (currentBomb == null) return null;

			List<string> edgework = new List<string>();
			Dictionary<string, string> portNames = new Dictionary<string, string>()
			{
				{ "RJ45", "RJ" },
				{ "StereoRCA", "RCA" },
				{ "ComponentVideo", "Component" },
				{ "CompositeVideo", "Composite" }
			};

			//Move vanilla indicators here to combine all indicators in the same entry
			var indicators = QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_INDICATOR).Where(x => !x.ContainsKey("display"));
			var coloredIndicators = QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_INDICATOR + "Color");
			foreach (Dictionary<string, string> indicator in coloredIndicators)
			{
				foreach (Dictionary<string, string> vanillaIndicator in indicators)
				{
					if ((indicator["label"] != vanillaIndicator["label"]) ||
						(vanillaIndicator["on"] != (indicator["color"] == "Black" ? "False" : "True")) ||
						vanillaIndicator.ContainsKey("color")) continue;
					//Prefix colors for Multiple Widgets. ToLowerInvariant to match number indicators.
					//Also includes "Black" and "White" due to those colors being sent by the widget.
					vanillaIndicator["on"] = $"({indicator["color"].ToLowerInvariant()}) ";
				}
			}

			var batteries = QueryWidgets<int>(KMBombInfo.QUERYKEY_GET_BATTERIES);
			edgework.Add(string.Format("{0}B {1}H", batteries.Sum(x => x["numbatteries"]), batteries.Count()));

			//Support for Encrypted Indicators, Number Indicators, and Multiple Widgets
			edgework.Add(
				indicators
					.OrderBy(x => x["label"])
					.Select(x => (x["on"] == "False" ? "" : x["on"] == "True" ? "*" : x["on"]) + (x.ContainsKey("display") ? x.ContainsKey("color") ? $"{x["display"]} ({x["color"]})" : x["display"] : x["label"]))
					.Join()
			);

			edgework.Add(
				QueryWidgets<List<string>>(KMBombInfo.QUERYKEY_GET_PORTS)
					.Select(x => x["presentPorts"]
						.Select(port => portNames.ContainsKey(port) ? portNames[port] : port)
						.OrderBy(y => y)
						.Join(", ")
					)
					.Select(x => x?.Length == 0 ? "[Empty]" : $"[{x}]")
					.Join(" ")
			);

			//New "Daytime" widget, designed for league usage to help limit bomb inconsistencies
			//Also includes two other widgets, for fun
			edgework.Add(QueryWidgets<string>("manufacture").Select(x => x["month"] + " - " + x["year"]).Join());

			edgework.Add(
				QueryWidgets<string>("day")
					.Select(x => $"{x["day"]} ({x["daycolor"]}) {(x["monthcolor"] == "0" ? $"{x["month"]}-{x["date"]} (Orange-Cyan)" : $"{x["date"]}-{x["month"]} (Cyan-Orange)")}")
					.Join()
			);

			edgework.Add(QueryWidgets<string>("time").Select(x => {
				var str1 = x["time"].Substring(0, 2);
				var str2 = x["time"].Substring(2, 2);
				var str3 = x["am"] == "True" ? "am" : x["pm"] == "True" ? "pm" : "";
				if ((str3 == "am" || str3 == "pm") && int.Parse(str1) < 10) str1 = str1.Substring(1, 1);
				return str1 + ":" + str2 + str3;
			}).Join(", "));

			edgework.Add(QueryWidgets<int>("twofactor").Select(x => x["twofactor_key"]).Join());

			edgework.Add(QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER).First()["serial"]);

			string edgeworkString = edgework.Where(str => str != "").Join(" // ");

			return edgeworkString;
		}
	}
}