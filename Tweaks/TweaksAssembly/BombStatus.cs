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

	public Text TimerPrefab = null;
	public Text TimerShadowPrefab = null;
	public Text StrikesPrefab = null;
	public Text StrikeLimitPrefab = null;
	public Text SolvesPrefab = null;
	public Text TotalModulesPrefab = null;
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
			currentBomb = Tweaks.bombWrappers.FirstOrDefault(x => x.holdable.HoldState == FloatingHoldable.HoldStateEnum.Held);
			if (currentBomb != null)
			{
				UpdateSolves();
				UpdateTotalModules();
				UpdateStrikes();
				UpdateStrikeLimit();
				EdgeworkPrefab.text = EdgeworkText;
			}
		}
		GetComponent<Canvas>().enabled = currentBomb != null && GameObject.Find("TwitchPlays_Info") == null;
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
		string strikesText = currentStrikes.ToString().PadLeft(currentTotalStrikes.ToString().Length, Char.Parse("0"));
		StrikesPrefab.text = strikesText;
	}

	public void UpdateStrikes(bool delay = false)
	{
		StartCoroutine(UpdateStrikesCoroutine(delay));
	}

	public void UpdateStrikeLimit()
	{
		if (currentBomb == null) return;
		currentTotalStrikes = currentBomb.StrikeLimit;
		string totalStrikesText = currentTotalStrikes.ToString();
		StrikeLimitPrefab.text = "/" + totalStrikesText;
	}

	public void UpdateSolves()
	{
		if (currentBomb == null) return;
		currentSolves = currentBomb.bombSolvedModules;
		string solves = currentSolves.ToString().PadLeft(currentBomb.bombSolvableModules.ToString().Length, Char.Parse("0"));
		SolvesPrefab.text = solves;
	}

	public void UpdateTotalModules()
	{
		if (currentBomb == null) return;
		currentTotalModules = currentBomb.bombSolvableModules;
		string total = currentTotalModules.ToString();
		TotalModulesPrefab.text = "/" + total;
	}

	Color yellow = new Color(1, 1, 0);
	public void UpdateConfidence()
	{
		if (Tweaks.settings.TimeMode)
		{
			string conf = "x" + String.Format("{0:0.0}", TimeMode.Multiplier);
			StrikesPrefab.color = Color.yellow;
			StrikeLimitPrefab.color = Color.yellow;
			StrikesPrefab.text = conf;
			StrikeLimitPrefab.text = "";
		}
		else
		{
			StrikesPrefab.color = Color.red;
			StrikeLimitPrefab.color = Color.red;
		}

		float success = PlayerSuccessRating;
		ConfidencePrefab.text = Mathf.Round(success * 100).ToString() + "%";
		ConfidencePrefab.color = success < 0.5f ? Color.Lerp(Color.red, yellow, success * 2) : Color.Lerp(yellow, Color.green, success * 2 - 0.5f);
	}

	public float PlayerSuccessRating
	{
		get
		{
			float solvesMax = 0.5f;
			float strikesMax = 0.3f;
			float timeMax = 0.2f;

			float timeRemaining = currentBomb.CurrentTimer;
			float totalTime = currentBomb.bombStartingTimer;

			int strikesAvailable = (currentTotalStrikes - 1) - currentStrikes; // Strikes without exploding

			float solvesCounter = (float) currentSolves / (currentTotalModules - 1);
			float strikesCounter = (float) strikesAvailable / (currentTotalStrikes - 1);
			float timeCounter = timeRemaining / totalTime;

			float solvesScore = solvesCounter * solvesMax;
			float strikesScore = strikesCounter * strikesMax;
			float timeScore = timeCounter * timeMax;

			return solvesScore + strikesScore + timeScore;
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

			edgework.Add(QueryWidgets<List<string>>(KMBombInfo.QUERYKEY_GET_PORTS).Select(x => x["presentPorts"].Select(port => portNames.ContainsKey(port) ? portNames[port] : port).OrderBy(y => y).Join(", ")).Select(x => x == "" ? "Empty" : x).Select(x => "[" + x + "]").Join(" "));

			edgework.Add(QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER).First()["serial"]);

			string edgeworkString = edgework.Where(str => str != "").Join(" // ");

			return edgeworkString;
		}
	}
}