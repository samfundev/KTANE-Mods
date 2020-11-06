using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;

public class TTKComponentSolver
{
	//When I created startTime/initialTime, my idea was that Zen mode would start at 0:01 at default, and a starting time would need to be specified via a setting.
	//Then I realized I should be using the starting time from the mission/freeplay binder. So it's not needed.
	public TTKComponentSolver(KMBombModule bombModule, Bomb bomb, float startTime)
	{
		module = bombModule;
		currentBomb = bomb;
		initialTime = startTime;

		if (Tweaks.TwitchPlaysActive) return; // Don't modify TTKs if TP is active.
		if (Tweaks.CurrentMode.Equals(Mode.Zen) && initialTime < 600) initialTime *= 10;
		_lock = (MonoBehaviour) _lockField.GetValue(module.GetComponent(_componentType));
		if (SceneManager.Instance.GameplayState.Bombs != null) _lock?.StartCoroutine(ReWriteTTK());
		module.OnActivate = OnActivate;
	}

	private bool IsTargetTurnTimeCorrect(int turnTime)
	{
		return turnTime < 0 || turnTime == (int) _targetTimeField.GetValue(module.GetComponent(_componentType));
	}

	private bool CanTurnEarlyWithoutStrike(int turnTime)
	{
		int time = (int) _targetTimeField.GetValue(module.GetComponent(_componentType));
		int timeRemaining = (int) currentBomb.GetTimer().TimeRemaining;
		if (Tweaks.CurrentMode.Equals(Mode.Zen) && timeRemaining > time) return false;
		if (Tweaks.CurrentMode.Equals(Mode.Zen))
			return (int) _targetTimeField.GetValue(module.GetComponent(_componentType)) >= time && IsTargetTurnTimeCorrect(turnTime);
		return false;
	}

	private bool OnKeyTurn(int turnTime = -1)
	{
		if (!active) return false;
		bool result = CanTurnEarlyWithoutStrike(turnTime);
		_lock.StartCoroutine(DelayKeyTurn(!result));
		return false;
	}

	private IEnumerator DelayKeyTurn(bool restoreBombTimer, bool causeStrikeIfWrongTime = true)
	{
		var passCheck = false;
		Animator keyAnimator = (Animator) _keyAnimatorField.GetValue(module.GetComponent(_componentType));
		KMAudio keyAudio = (KMAudio) _keyAudioField.GetValue(module.GetComponent(_componentType));
		int time = (int) _targetTimeField.GetValue(module.GetComponent(_componentType));

		var remaining = currentBomb.BombComponents.Where(component => component.IsSolvable && !component.IsSolved)
			.Select(component => component.GetModuleDisplayName())
			.ToList();

		if (!remaining.Exists(x => !x.Equals("Turn The Key")) && remaining.Contains("Turn The Key"))
		{
			passCheck = true;
		}
		if (!restoreBombTimer)
		{
			currentBomb.GetTimer().TimeRemaining = time + 0.5f + Time.deltaTime;
			yield return null;
		}
		else if (causeStrikeIfWrongTime && time != (int) Mathf.Floor(currentBomb.GetTimer().TimeRemaining))
		{
			module.HandleStrike();
			keyAnimator.SetTrigger("WrongTurn");
			keyAudio.PlaySoundAtTransform("WrongKeyTurnFK", module.transform);
			yield return null;
			if (passCheck) goto skip;
			if ((bool) _solvedField.GetValue(module.GetComponent(_componentType)))
			{
				yield break;
			}
		}
		skip:
		module.HandlePass();
		_keyUnlockedField.SetValue(module.GetComponent(_componentType), true);
		_solvedField.SetValue(module.GetComponent(_componentType), true);
		keyAnimator.SetBool("IsUnlocked", true);
		keyAudio.PlaySoundAtTransform("TurnTheKeyFX", module.transform);
		yield return null;
	}

	public IEnumerable<Dictionary<string, T>> QueryWidgets<T>(string queryKey, string queryInfo = null)
	{
		return currentBomb.WidgetManager.GetWidgetQueryResponses(queryKey, queryInfo).Select(str => JsonConvert.DeserializeObject<Dictionary<string, T>>(str));
	}

	private void OnActivate()
	{
		string serial = QueryWidgets<string>(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER).First()["serial"];
		TextMesh textMesh = (TextMesh) _displayField.GetValue(module.GetComponent(_componentType));
		_activatedField.SetValue(module.GetComponent(_componentType), true);

		if (string.IsNullOrEmpty(_previousSerialNumber) || !_previousSerialNumber.Equals(serial) || _keyTurnTimes.Count == 0)
		{
			if (!string.IsNullOrEmpty(_previousSerialNumber) && _previousSerialNumber.Equals(serial))
			{
				Animator keyAnimator = (Animator) _keyAnimatorField.GetValue(module.GetComponent(_componentType));
				KMAudio keyAudio = (KMAudio) _keyAudioField.GetValue(module.GetComponent(_componentType));
				module.HandlePass();
				_keyUnlockedField.SetValue(module.GetComponent(_componentType), true);
				_solvedField.SetValue(module.GetComponent(_componentType), true);
				keyAnimator.SetBool("IsUnlocked", true);
				keyAudio.PlaySoundAtTransform("TurnTheKeyFX", module.transform);
				textMesh.text = "88:88";
				return;
			}
			_keyTurnTimes.Clear();
			for (int i = (Tweaks.CurrentMode.Equals(Mode.Zen) ? 45 : 3); i < (Tweaks.CurrentMode.Equals(Mode.Zen) ? initialTime : (currentBomb.GetTimer().TimeRemaining - 45)); i += 3)
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
		_targetTimeField.SetValue(module.GetComponent(_componentType), _keyTurnTimes[0]);

		string display = $"{_keyTurnTimes[0] / 60:00}:{_keyTurnTimes[0] % 60:00}";
		_keyTurnTimes.RemoveAt(0);

		textMesh.text = display;
		//Doesn't work
		active = true;
	}

	private IEnumerator ReWriteTTK()
	{
		yield return new WaitUntil(() => (bool) _activatedField.GetValue(module.GetComponent(_componentType)));
		yield return new WaitForSeconds(0.1f);
		_stopAllCorotinesMethod.Invoke(module.GetComponent(_componentType), null);

		((KMSelectable) _lock).OnInteract = () => OnKeyTurn();
		int expectedTime = (int) _targetTimeField.GetValue(module.GetComponent(_componentType));
		if (Math.Abs(expectedTime - currentBomb.GetTimer().TimeRemaining) < 30)
		{
			yield return new WaitForSeconds(0.1f);
			yield break;
		}

		while (!module.GetComponent<BombComponent>().IsSolved)
		{
			int time = Mathf.FloorToInt(currentBomb.GetTimer().TimeRemaining);
			if (((!Tweaks.CurrentMode.Equals(Mode.Zen) && time < expectedTime) || (Tweaks.CurrentMode.Equals(Mode.Zen) && time > expectedTime)) &&
				!(bool) _solvedField.GetValue(module.GetComponent(_componentType)) &&
				Tweaks.CurrentModeCache != Mode.Time)
			{
				module.HandleStrike();
			}
			yield return new WaitForSeconds(2.0f);
		}
	}

	static TTKComponentSolver()
	{
		_componentType = ReflectionHelper.FindType("TurnKeyModule");
		_lockField = _componentType.GetField("Lock", BindingFlags.Public | BindingFlags.Instance);
		_activatedField = _componentType.GetField("bActivated", BindingFlags.NonPublic | BindingFlags.Instance);
		_solvedField = _componentType.GetField("bUnlocked", BindingFlags.NonPublic | BindingFlags.Instance);
		_targetTimeField = _componentType.GetField("mTargetSecond", BindingFlags.NonPublic | BindingFlags.Instance);
		_stopAllCorotinesMethod = _componentType.GetMethod("StopAllCoroutines", BindingFlags.Public | BindingFlags.Instance);
		_keyAnimatorField = _componentType.GetField("KeyAnimator", BindingFlags.Public | BindingFlags.Instance);
		_displayField = _componentType.GetField("Display", BindingFlags.Public | BindingFlags.Instance);
		_keyUnlockedField = _componentType.GetField("bUnlocked", BindingFlags.NonPublic | BindingFlags.Instance);
		_keyAudioField = _componentType.GetField("mAudio", BindingFlags.NonPublic | BindingFlags.Instance);
		_keyTurnTimes = new List<int>();
	}

	private static readonly Type _componentType = null;
	private static readonly FieldInfo _lockField = null;
	private static readonly FieldInfo _activatedField = null;
	private static readonly FieldInfo _solvedField = null;
	private static readonly FieldInfo _targetTimeField = null;
	private static readonly FieldInfo _keyAnimatorField = null;
	private static readonly FieldInfo _displayField = null;
	private static readonly FieldInfo _keyUnlockedField = null;
	private static readonly FieldInfo _keyAudioField = null;
	private static readonly MethodInfo _stopAllCorotinesMethod = null;

	private static List<int> _keyTurnTimes = null;
	private static string _previousSerialNumber = null;
	private bool active = false;

	private readonly MonoBehaviour _lock = null;
	private readonly KMBombModule module;
	private readonly Bomb currentBomb;
	private readonly float initialTime;
}
