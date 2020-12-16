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
		zenMode = Tweaks.CurrentMode == Mode.Zen;

		if (Tweaks.TwitchPlaysActive) return; // Don't modify TTKs if TP is active.
		if (zenMode && initialTime < 600) initialTime *= 10;
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
			for (int i = zenMode ? 45 : 3; i < (zenMode ? initialTime : (currentBomb.GetTimer().TimeRemaining - 45)); i += 3)
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
		TimerComponent timer = currentBomb.GetTimer();
		if (Math.Abs(expectedTime - timer.TimeRemaining) < 30)
		{
			yield return new WaitForSeconds(0.1f);
			yield break;
		}

		SetupLongPress(component.GetValue<KMSelectable>("Lock"), 1, () => component.CallMethod("OnKeyTurn"), () => {
			var remaining = timer.TimeRemaining;
			if (!(zenMode ? expectedTime - 75 > remaining : remaining > expectedTime + 75))
				return;

			DarkTonic.MasterAudio.MasterAudio.PlaySound3DAtTransformAndForget("bb-press-release", component.transform, 1f, null, 0f, null);

			var offset = 45 + UnityEngine.Random.Range(0, 31);
			timer.TimeRemaining = expectedTime + (zenMode ? -offset : offset);
		});

		while (!bombComponent.IsSolved)
		{
			int time = Mathf.FloorToInt(timer.TimeRemaining);
			if (((!zenMode && time < expectedTime) || (zenMode && time > expectedTime)) &&
				!component.GetValue<bool>("bUnlocked") &&
				Tweaks.CurrentModeCache != Mode.Time)
			{
				module.HandleStrike();
			}
			yield return new WaitForSeconds(2.0f);
		}
	}

	private void SetupLongPress(KMSelectable selectable, float time, Action press, Action longPress)
	{
		var held = false;
		IEnumerator LongPress()
		{
			yield return new WaitForSeconds(time);

			held = true;
			longPress();
		}

		Coroutine pressCoroutine = null;
		selectable.OnInteract = () => {
			pressCoroutine = selectable.StartCoroutine(LongPress());
			return false;
		};

		selectable.OnInteractEnded = () => {
			if (pressCoroutine == null || held)
				return;

			selectable.StopCoroutine(pressCoroutine);

			press();
		};
	}

	private static List<int> _keyTurnTimes = new List<int>();
	private static string _previousSerialNumber;

	private readonly KMBombModule module;
	private readonly Bomb currentBomb;
	private readonly float initialTime;
	private readonly bool zenMode;
}
