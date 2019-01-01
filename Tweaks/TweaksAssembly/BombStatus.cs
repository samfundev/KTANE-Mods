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

	private int currentSolves;
	private int currentStrikes;
	private int currentTotalModules;
	private int currentTotalStrikes;

	void Start()
	{
		Instance = this;
	}

	void LateUpdate()
	{
		if (currentBomb == null)
		{
			currentBomb = Array.Find(Tweaks.bombWrappers, x => x.holdable.HoldState == FloatingHoldable.HoldStateEnum.Held);
			if (currentBomb != null)
			{
				UpdateSolves();
				UpdateStrikes();
				EdgeworkPrefab.text = EdgeworkText;

				int needies = currentBomb.Bomb.BombComponents.Count(bombComponent => bombComponent.GetComponent<NeedyComponent>() != null);
				NeediesPrefab.enabled = needies > 0;
				NeediesPrefab.text = needies.ToString();
			}
		}
		GetComponent<Canvas>().enabled = currentBomb != null && !Tweaks.TwitchPlaysActive;
		if (currentBomb == null) return;

		string formattedTime = currentBomb.GetFullFormattedTime;
		TimerPrefab.text = formattedTime;
		TimerShadowPrefab.text = Regex.Replace(formattedTime, @"\d", "8");
		UpdateConfidence();
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

	public string EdgeworkText
	{
		get
		{
			if (currentBomb == null) return null;

			List<string> edgework = new List<string>();
			Dictionary<string, string> portNames = new Dictionary<string, string>()
			{
				{ "RJ45", "RJ" },
				{ "StereoRCA", "RCA" }
			};

			var batteries = QueryWidgets<int>(KMBombInfo.QUERYKEY_GET_BATTERIES);
			edgework.Add(string.Format("{0}B {1}H", batteries.Sum(x => x["numbatteries"]), batteries.Count()));

			edgework.Add(QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_INDICATOR).OrderBy(x => x["label"]).Select(x => (x["on"] == "True" ? "*" : "") + x["label"]).Join());

			edgework.Add(QueryWidgets<List<string>>(KMBombInfo.QUERYKEY_GET_PORTS).Select(x => x["presentPorts"].Select(port => portNames.ContainsKey(port) ? portNames[port] : port).OrderBy(y => y).Join(", ")).Select(x => x?.Length == 0 ? "Empty" : x).Select(x => "[" + x + "]").Join(" "));

			edgework.Add(QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER).First()["serial"]);

			string edgeworkString = edgework.Where(str => str != "").Join(" // ");

			return edgeworkString;
		}
	}
}