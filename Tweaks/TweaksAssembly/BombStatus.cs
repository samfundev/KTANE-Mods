using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

class BombStatus : MonoBehaviour
{
	public static BombStatus Instance;

	public GameObject HUD = null;
	public GameObject Alert = null;

	public Text TimerPrefab = null;
	public Text TimerShadowPrefab = null;
	public Text StrikesPrefab = null;
	public Text SolvesPrefab = null;
	public Text NeediesPrefab = null;
	public Text ConfidencePrefab = null;

	public BombWrapper currentBomb = null;

	private CanvasGroup CanvasGroup;

	private int currentSolves;
	private int currentStrikes;
	private int currentTotalModules;
	private int currentTotalStrikes;
	internal bool widgetsActivated;

	public void Start()
	{
		Instance = this;
		GameplayState.OnLightsOnEvent += delegate { widgetsActivated = true; };

		Alert = transform.Find("Alert").gameObject;
		Alert.SetActive(false);

		CanvasGroup = GetComponent<CanvasGroup>();
		CanvasGroup.alpha = 0;

	}

	public void LateUpdate()
	{
		if (currentBomb == null)
		{
			currentBomb = Array.Find(Tweaks.bombWrappers, x => x.holdable.HoldState == FloatingHoldable.HoldStateEnum.Held);
			if (currentBomb != null)
			{
				UpdateSolves();
				UpdateStrikes();

				int needies = currentBomb.Bomb.BombComponents.Count(bombComponent => bombComponent.GetComponent<NeedyComponent>() != null);
				NeediesPrefab.gameObject.SetActive(needies > 0);
				NeediesPrefab.text = needies.ToString();

				if (Tweaks.settings.ShowEdgework && !Tweaks.TwitchPlaysActiveCache)
					SetupEdgeworkCameras();
			}
			else
			{
				foreach (GameObject edgeworkCamera in edgeworkCameras)
					Destroy(edgeworkCamera);
			}
		}

		bool enabled = currentBomb != null && (Tweaks.settings.BombHUD || BombWrapper.Alerts.Count != 0) && !Tweaks.TwitchPlaysActiveCache;
		CanvasGroup.alpha = Math.Min(Math.Max(CanvasGroup.alpha + (enabled ? 1 : -1) * 0.1f, 0), 1);
		if (!enabled) return;

		if (Tweaks.settings.BombHUD)
		{
			string formattedTime = currentBomb.GetFullFormattedTime;
			TimerPrefab.text = formattedTime;
			TimerShadowPrefab.text = formattedTime.Select(character => character == ':' ? character : '8').Join("");
			UpdateConfidence();
		}

		float y = Tweaks.settings.BombHUD ? 173 : 0;
		foreach (RectTransform alert in BombWrapper.Alerts)
		{
			alert.anchoredPosition = new Vector3(0, -y);
			y += alert.rect.height;
		}
	}

	private IEnumerator UpdateStrikesCoroutine(bool delay)
	{
		if (delay)
		{
			// Delay for a single frame if this has been called from an OnStrike method
			// Necessary since the bomb doesn't update its internal counter until all its OnStrike handlers are finished
			yield return 0;
		}
		if (currentBomb == null || Tweaks.CurrentModeCache == Mode.Time) yield break;
		currentStrikes = currentBomb.StrikeCount;
		currentTotalStrikes = currentBomb.StrikeLimit;
		string strikesText = currentStrikes.ToString().PadLeft(currentTotalStrikes.ToString().Length, '0');
		StrikesPrefab.text = Tweaks.CurrentModeCache != Mode.Zen ? $"{strikesText}<size=25>/{currentTotalStrikes}</size>" : strikesText;
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

	public void UpdateMultiplier()
	{
		if (Tweaks.CurrentModeCache == Mode.Time)
		{
			string conf = "<size=36>x</size>" + string.Format("{0:0.0}", Math.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier));
			StrikesPrefab.text = conf;
		}
	}

	public void UpdateConfidence()
	{
		float success = PlayerPaceRating;
		ConfidencePrefab.text = Mathf.Round(success * 100).ToString() + "%";
		ConfidencePrefab.color = success < 0 ? Color.Lerp(Color.gray, Color.red, Mathf.Sqrt(-success)) : Color.Lerp(Color.grey, Color.green, Mathf.Sqrt(success));
	}

	public float PlayerPaceRating
	{
		get
		{
			float remaining = currentBomb.CurrentTimer;

			return Tweaks.CurrentModeCache == Mode.Time ? remaining / (Modes.settings.TimeModeStartingTime * 60) - 1 : (float) currentSolves / currentTotalModules - (currentBomb.bombStartingTimer - remaining / currentBomb.timerComponent.GetRate()) / currentBomb.bombStartingTimer;
		}
	}

	readonly List<GameObject> edgeworkCameras = new List<GameObject>();
	void SetupEdgeworkCameras()
	{
		foreach (GameObject edgeworkCamera in edgeworkCameras)
			Destroy(edgeworkCamera);

		// Create widget cameras
		var widgets = currentBomb.Bomb.WidgetManager.GetWidgets();
		var widgetTypes = new[] { "BatteryWidget", "IndicatorWidget", "EncryptedIndicator", "NumberInd", "PortWidget", "DayTimeWidget", "TwoFactorWidget", "MultipleWidgets", null, "SerialNumber", "RuleSeedWidget" };
		widgets.Sort((w1, w2) =>
		{
			var i1 = widgetTypes.IndexOf(wt => wt != null && w1.GetComponent(wt) != null);
			if (i1 == -1)
				i1 = Array.IndexOf(widgetTypes, null);
			var i2 = widgetTypes.IndexOf(wt => wt != null && w2.GetComponent(wt) != null);
			if (i2 == -1)
				i2 = Array.IndexOf(widgetTypes, null);

			if (i1 < i2)
				return -1;
			if (i1 > i2)
				return 1;

			switch (w1)
			{
				case BatteryWidget batteries:
					return batteries.GetNumberOfBatteries().CompareTo(((BatteryWidget) w2).GetNumberOfBatteries());

				case IndicatorWidget indicator:
					return indicator.Label.CompareTo(((IndicatorWidget) w2).Label);

				case PortWidget port:
					var port2 = (PortWidget) w2;
					return (
						port.IsPortPresent(PortWidget.PortType.Parallel) || port.IsPortPresent(PortWidget.PortType.Serial) ? 0 :
						port.IsPortPresent(PortWidget.PortType.DVI) || port.IsPortPresent(PortWidget.PortType.PS2) || port.IsPortPresent(PortWidget.PortType.RJ45) || port.IsPortPresent(PortWidget.PortType.StereoRCA) ? 1 : 2
					).CompareTo(
						port2.IsPortPresent(PortWidget.PortType.Parallel) || port2.IsPortPresent(PortWidget.PortType.Serial) ? 0 :
						port2.IsPortPresent(PortWidget.PortType.DVI) || port2.IsPortPresent(PortWidget.PortType.PS2) || port2.IsPortPresent(PortWidget.PortType.RJ45) || port2.IsPortPresent(PortWidget.PortType.StereoRCA) ? 1 : 2
					);
				default: return w1.name.CompareTo(w2.name);
			}
		});

		const float availableWidth = 0.6666667f;
		const float availableHeight = .08f;

		// Find out how tall the widgets would be if we arrange them in one row
		var widgetWidths = widgets.Select(w => (float) w.SizeX / w.SizeZ * Screen.height / Screen.width).ToArray();
		var totalWidth = widgetWidths.Sum();
		var widgetMiddles = new float[widgetWidths.Length];
		for (int i = 0; i < widgetWidths.Length; i++)
			widgetMiddles[i] = (i == 0 ? 0 : widgetMiddles[i - 1] + widgetWidths[i - 1] / 2) + widgetWidths[i] / 2;
		var cutOffPoints = new int[] { widgetWidths.Length };
		var rowHeight = Mathf.Min(availableWidth / totalWidth, availableHeight);

		// See if we can make them bigger by wrapping them into multiple rows
		while (true)
		{
			var n = cutOffPoints.Length + 1;
			var newCutOffPoints = new int[n];
			for (int i = 0; i < n; i++)
			{
				newCutOffPoints[i] = widgetMiddles.IndexOf(w => w > (i + 1) * totalWidth / n);
				if (newCutOffPoints[i] == -1)
					newCutOffPoints[i] = widgetWidths.Length;
			}
			var rowWidths = Enumerable.Range(0, n).Select(i => widgetWidths.Skip(i == 0 ? 0 : newCutOffPoints[i - 1]).Take(newCutOffPoints[i] - (i == 0 ? 0 : newCutOffPoints[i - 1])).Sum()).ToArray();
			var newRowHeight = Mathf.Min(availableWidth / rowWidths.Max(), availableHeight / n);
			if (newRowHeight <= rowHeight)
				break;
			cutOffPoints = newCutOffPoints;
			rowHeight = newRowHeight;
		}

		// Move all lights off layer 2 to prevent them from affecting the widgets
		foreach (Light light in FindObjectsOfType<Light>())
		{
			light.cullingMask &= ~(1 << 2);
		}

		for (int i = 0; i < widgets.Count; i++)
		{
			// Setup the camera, using layer 2
			var cameraObject = new GameObject();
			edgeworkCameras.Add(cameraObject);
			var camera = cameraObject.AddComponent<Camera>();
			var row = cutOffPoints.IndexOf(ix => ix > i);
			var totalWidthInThisRow = widgetWidths.Skip(row == 0 ? 0 : cutOffPoints[row - 1]).Take(cutOffPoints[row] - (row == 0 ? 0 : cutOffPoints[row - 1])).Sum() * rowHeight;
			var widthBeforeThis = widgetWidths.Skip(row == 0 ? 0 : cutOffPoints[row - 1]).Take(i - (row == 0 ? 0 : cutOffPoints[row - 1])).Sum() * rowHeight;
			camera.rect = new Rect(.5f - totalWidthInThisRow / 2 + widthBeforeThis, 1 - (row + 1) * rowHeight, widgetWidths[i] * rowHeight, rowHeight);
			camera.aspect = (float) widgets[i].SizeX / widgets[i].SizeZ;
			camera.depth = 99;
			camera.fieldOfView = 3.25f;
			camera.transform.SetParent(widgets[i].transform, false);
			camera.transform.localPosition = new Vector3(.001f, 2.26f / widgets[i].SizeX * widgets[i].SizeZ, 0);
			var rotate = widgets[i] is PortWidget || (widgets[i] is ModWidget mw && mw.name == "NumberInd(Clone)");
			camera.transform.localEulerAngles = new Vector3(90, rotate ? 180 : 0, 0);
			var lossyScale = camera.transform.lossyScale;
			camera.nearClipPlane = 1f * lossyScale.y;
			camera.farClipPlane = 3f / widgets[i].SizeX * widgets[i].SizeZ * lossyScale.y;
			camera.cullingMask = 1 << 2;
			camera.clearFlags = CameraClearFlags.Depth;
			camera.gameObject.SetActive(true);

			// Move the widget’s GameObjects to Layer 2
			foreach (var obj in widgets[i].gameObject.GetComponentsInChildren<Transform>(true))
				if (obj.gameObject.name != "LightGlow")
					obj.gameObject.layer = 2;

			// Add a light source
			var light = camera.gameObject.AddComponent<Light>();
			light.type = LightType.Spot;
			light.cullingMask = 1 << 2;
			light.range = (camera.transform.localPosition.y + 0.05f) * lossyScale.z;
			light.spotAngle = 7.25f;
			light.intensity = 75;
			light.enabled = true;
		}
	}
}