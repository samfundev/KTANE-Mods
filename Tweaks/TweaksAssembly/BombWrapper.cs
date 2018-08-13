using System;
using UnityEngine;
using Assets.Scripts.Records;
using Assets.Scripts.Missions;
using System.Collections.Generic;
using System.Linq;

class BombWrapper
{
	public BombWrapper(Bomb bomb)
	{
		Bomb = bomb;
		holdable = bomb.GetComponentInChildren<FloatingHoldable>();
		timerComponent = bomb.GetTimer();
		widgetManager = bomb.WidgetManager;
        //This probably doesn't belong here
        //but this is where I had this in the original code
        //TODO: Move this where it belongs
        if (Tweaks.settings.Mode == Mode.Zen)
        {
            Modes.normalRate = -timerComponent.GetRate();
            timerComponent.text.color = Color.blue;
            timerComponent.SetRateModifier(Modes.normalRate);
            Modes.initialTime = timerComponent.TimeRemaining;
            timerComponent.SetTimeRemaing(1);
            //This was in the original code to make sure the bomb didn't explode on the first strike
            bomb.NumStrikesToLose += 1;
        }

		foreach (BombComponent component in Bomb.BombComponents)
		{
			component.OnPass += delegate
			{
				BombStatus.Instance.UpdateSolves();

				if (Tweaks.settings.Mode == Mode.Time)
				{
					double ComponentValue;
					if (!Modes.settings.ComponentValues.TryGetValue(Modes.GetModuleID(component), out ComponentValue))
					{
						ComponentValue = 6;
					}

					float time = (float) (Modes.Multiplier * ComponentValue);
					CurrentTimer += Math.Max(Modes.settings.TimeModeMinimumTimeGained, time);

					Modes.Multiplier = Modes.Multiplier + Modes.settings.TimeModeSolveBonus;
				}

				return false;
			};

			component.OnStrike += delegate
			{
                //Ideally, catch this before the strikes are recorded
                if (Tweaks.settings.Mode == Mode.Zen)
                {
                    //TODO: NumStrikesToLose is read only on this page,
                    //So probably needs to be repurposed for this mod.
                    bomb.NumStrikesToLose += 1;
                    bomb.GetTimer().SetRateModifier(Modes.normalRate);
                    /*timePenalty is the amount of time gained per strike.
                    This feature was a request from Elias.
                    The function for setting timePenalty can be found in Tweaks.cs
                    The actual variable is located in Modes.cs*/
                    bomb.GetTimer().SetTimeRemaing(bomb.GetTimer().TimeRemaining + (Modes.timePenalty));
                }

				BombStatus.Instance.UpdateStrikes();

				if (Tweaks.settings.Mode == Mode.Time)
				{
					Modes.Multiplier = Math.Max(Modes.Multiplier - Modes.settings.TimeModeMultiplierStrikePenalty, Modes.settings.TimeModeMinMultiplier);
					if (CurrentTimer < (Modes.settings.TimeModeMinimumTimeLost / Modes.settings.TimeModeMultiplierStrikePenalty))
					{
						CurrentTimer -= Modes.settings.TimeModeMinimumTimeLost;
					}
					else
					{
						float timeReducer = CurrentTimer * Modes.settings.TimeModeTimerStrikePenalty;
						double easyText = Math.Round(timeReducer, 1);
						CurrentTimer -= timeReducer;
					}

					// Set strikes to 0
					Bomb.NumStrikes = 0;
                    //Moved everything here to a different method
                    //in case Zen mode can reuse any of this code.
                    StrikeRecorder();
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

            switch (component.ModuleType)
            {
                //TTK is our favorite Zen mode compatible module
                //Of course, everything here is repurposed from Twitch Plays.
                case "TurnTheKey":
                    new TTKComponentSolver(component, bomb, Tweaks.settings.Mode.Equals(Mode.Zen)? Modes.initialTime : timerComponent.TimeRemaining);
                    break;
                /*case "ButtonV2":
                    break;
                case "theSwan":
                    break;*/
				default:
					if (component.GetComponent<BombComponent>().ComponentType == ComponentTypeEnum.Mod) ReflectedTypes.FindModeBoolean(component);
            }
            
            /*if (component.GetComponent<BombComponent>().ComponentType == Assets.Scripts.Missions.ComponentTypeEnum.BigButton)
            {

            }*/

			/*
			// Setup logging
			if (loggers.ContainsKey(component.ModuleType))
			{
				loggers[component.ModuleType](component.GetComponent<BombComponent>());
			}*/
		}
	}

    void StrikeRecorder()
    {
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
		set {
			timerComponent.TimeRemaining = (value < 0) ? 0 : value;
			timerComponent.text.text = timerComponent.GetFormattedTime(value, true);
		}
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