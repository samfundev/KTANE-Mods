using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

public class TTKComponentSolver : ModuleTweak
{
	//When I created startTime/initialTime, my idea was that Zen mode would start at 0:01 at default, and a starting time would need to be specified via a setting.
	//Then I realized I should be using the starting time from the mission/freeplay binder. So it's not needed.
	public TTKComponentSolver(BombComponent bombComponent, KMBombModule bombModule, float startTime) : base(bombComponent, "TurnKeyModule")
	{
		module = bombModule;
		currentBomb = bombComponent.Bomb;
		initialTime = startTime;

		if (Tweaks.TwitchPlaysActive) return; // Don't modify TTKs if TP is active.
		if (Tweaks.CurrentMode.Equals(Mode.Zen) && initialTime < 600) initialTime *= 10;
		if (SceneManager.Instance.GameplayState.Bombs != null) bombComponent.StartCoroutine(ReWriteTTK());
		module.OnActivate = OnActivate;
	}

	public IEnumerable<Dictionary<string, T>> QueryWidgets<T>(string queryKey, string queryInfo = null)
	{
		return currentBomb.WidgetManager.GetWidgetQueryResponses(queryKey, queryInfo).Select(str => JsonConvert.DeserializeObject<Dictionary<string, T>>(str));
	}

	private void OnActivate()
	{
		string serial = QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER).First()["serial"];
		TextMesh textMesh = component.GetValue<TextMesh>("Display");
		component.SetValue("bActivated", true);

		if (string.IsNullOrEmpty(_previousSerialNumber) || !_previousSerialNumber.Equals(serial) || _keyTurnTimes.Count == 0)
		{
			if (!string.IsNullOrEmpty(_previousSerialNumber) && _previousSerialNumber.Equals(serial))
			{
				Animator keyAnimator = component.GetValue<Animator>("KeyAnimator");
				KMAudio keyAudio = component.GetValue<KMAudio>("mAudio");
				module.HandlePass();
				component.SetValue("bUnlocked", true);
				keyAnimator.SetBool("IsUnlocked", true);
				keyAudio.PlaySoundAtTransform("TurnTheKeyFX", module.transform);
				textMesh.text = "88:88";
				return;
			}
			_keyTurnTimes.Clear();
			for (int i = Tweaks.CurrentMode.Equals(Mode.Zen) ? 45 : 3; i < (Tweaks.CurrentMode.Equals(Mode.Zen) ? initialTime : (currentBomb.GetTimer().TimeRemaining - 45)); i += 3)
			{
				_keyTurnTimes.Add(i);
			}
			if (_keyTurnTimes.Count == 0)
			{
				_keyTurnTimes.Add((int) (currentBomb.GetTimer().TimeRemaining / 2f));
			}

			_keyTurnTimes = _keyTurnTimes.Shuffle().ToList();
			_previousSerialNumber = serial;
		}
		component.SetValue("mTargetSecond", _keyTurnTimes[0]);

		string display = $"{_keyTurnTimes[0] / 60:00}:{_keyTurnTimes[0] % 60:00}";
		_keyTurnTimes.RemoveAt(0);

		textMesh.text = display;
	}

	private IEnumerator ReWriteTTK()
	{
		yield return new WaitUntil(() => component.GetValue<bool>("bActivated"));
		yield return new WaitForSeconds(0.1f);
		component.CallMethod("StopAllCoroutines");

		int expectedTime = component.GetValue<int>("mTargetSecond");
		if (Math.Abs(expectedTime - currentBomb.GetTimer().TimeRemaining) < 30)
		{
			yield return new WaitForSeconds(0.1f);
			yield break;
		}

		while (!bombComponent.IsSolved)
		{
			int time = Mathf.FloorToInt(currentBomb.GetTimer().TimeRemaining);
			if (((!Tweaks.CurrentMode.Equals(Mode.Zen) && time < expectedTime) || (Tweaks.CurrentMode.Equals(Mode.Zen) && time > expectedTime)) &&
				!component.GetValue<bool>("bUnlocked") &&
				Tweaks.CurrentModeCache != Mode.Time)
			{
				module.HandleStrike();
			}
			yield return new WaitForSeconds(2.0f);
		}
	}

	private static List<int> _keyTurnTimes = new List<int>();
	private static string _previousSerialNumber;

	private readonly KMBombModule module;
	private readonly Bomb currentBomb;
	private readonly float initialTime;
}
