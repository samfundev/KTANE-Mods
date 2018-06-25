using System;
using UnityEngine;
using Assets.Scripts.Records;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

class BombWrapper
{
	public BombWrapper(Bomb bomb)
	{
		Bomb = bomb;
		holdable = bomb.GetComponentInChildren<FloatingHoldable>();
		timerComponent = bomb.GetTimer();
		widgetManager = bomb.WidgetManager;

		// UI
		foreach (BombComponent component in Bomb.BombComponents)
		{
			component.OnPass += delegate
			{
				BombStatus.Instance.UpdateSolves();

				if (Tweaks.settings.TimeMode)
				{
					int ComponentValue;
					if (!TimeMode.ComponentValues.TryGetValue(TimeMode.GetModuleID(component), out ComponentValue))
					{
						ComponentValue = 6;
					}

					float time = TimeMode.Multiplier * ComponentValue;
					if (time < 20)
					{
						CurrentTimer = CurrentTimer + 20;
					}
					else
					{
						CurrentTimer = CurrentTimer + time;
					}
					TimeMode.Multiplier += 0.1f;
				}

				return false;
			};

			component.OnStrike += delegate
			{
				BombStatus.Instance.UpdateStrikes();

				if (Tweaks.settings.TimeMode)
				{
					TimeMode.Multiplier -= 1.5f;
					if (CurrentTimer < (15 / 0.25f))
					{
						CurrentTimer = CurrentTimer - 15;
					}
					else
					{
						float timeReducer = CurrentTimer * 0.25f;
						double easyText = Math.Round(timeReducer, 1);
						CurrentTimer = CurrentTimer - timeReducer;
					}

					// Set strikes to 0
					Bomb.NumStrikes = 0;
					int strikeLimit = StrikeLimit;
					int strikeCount = Math.Min(StrikeCount, StrikeLimit);

					RecordManager RecordManager = RecordManager.Instance;
					GameRecord GameRecord = RecordManager.GetCurrentRecord();
					StrikeSource[] Strikes = GameRecord.Strikes;
					if (Strikes.Length != strikeLimit)
					{
						StrikeSource[] newStrikes = new StrikeSource[Math.Max(strikeLimit, 1)];
						Array.Copy(Strikes, newStrikes, Math.Min(Strikes.Length, newStrikes.Length));
						GameRecord.Strikes = newStrikes;
					}

					Debug.Log(string.Format("[Bomb] Strike from Tweaks! {0} / {1} strikes", StrikeCount, StrikeLimit));
					ReflectedTypes.GameRecordCurrentStrikeIndexField.SetValue(GameRecord, strikeCount);
					timerComponent.SetRateModifier(1);
					Bomb.StrikeIndicator.StrikeCount = strikeCount;
				}

				return false;
			};
		}

		Dictionary<string, Func<BombComponent, ModuleLogging>> loggers = new Dictionary<string, Func<BombComponent, ModuleLogging>>()
		{
			{ "Probing", bombComponent => new ProbingLogging(bombComponent) }
		};

		foreach (KMBombModule component in bomb.BombComponents.Select(x => x.GetComponent<KMBombModule>()).Where(x => x != null))
		{
			// Correct the position of the status light
			if (component.ModuleType == "ForeignExchangeRates" || component.ModuleType == "resistors" || component.ModuleType == "CryptModule")
			{
				component.transform.Find("Model").transform.localPosition = new Vector3(0.004f, 0, 0);
			}
			else if (component.ModuleType == "TwoBits")
			{
				component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
			}

			/*
			// Setup logging
			if (loggers.ContainsKey(component.ModuleType))
			{
				loggers[component.ModuleType](component.GetComponent<BombComponent>());
			}*/
		}
	}

	void LogChildren(Transform goTransform, int depth = 0)
	{
		Debug.LogFormat("{2}{0} - {1}", goTransform.name, goTransform.localPosition.ToString("N6"), new String('\t', depth));
		foreach (Transform child in goTransform)
		{
			LogChildren(child, depth + 1);
		}
	}

	public Bomb Bomb = null;
	public FloatingHoldable holdable;

	public TimerComponent timerComponent = null;
	public WidgetManager widgetManager = null;

	public int bombSolvableModules { get => Bomb.GetSolvableComponentCount(); }
	public int bombSolvedModules { get => Bomb.GetSolvedComponentCount(); }
	public float bombStartingTimer { get => timerComponent.TimeElapsed + timerComponent.TimeRemaining; }

	public float CurrentTimerElapsed => timerComponent.TimeElapsed;

	public float CurrentTimer
	{
		get => timerComponent.TimeRemaining;
		set => timerComponent.TimeRemaining = (value < 0) ? 0 : value;
	}

	public string CurrentTimerFormatted => timerComponent.GetFormattedTime(CurrentTimer, true);

	public string StartingTimerFormatted => timerComponent.GetFormattedTime(bombStartingTimer, true);
	
	public string GetFullFormattedTime => Math.Max(CurrentTimer, 0).FormatTime();

	public string GetFullStartingTime => Math.Max(bombStartingTimer, 0).FormatTime();

	public int StrikeCount
	{
		get => Bomb.NumStrikes;
	}

	public int StrikeLimit
	{
		get => Bomb.NumStrikesToLose;
	}

	public int NumberModules => bombSolvableModules;
}