using System;
using UnityEngine;
using Assets.Scripts.Records;
using Assets.Scripts.Missions;
using System.Collections.Generic;
using System.Linq;

class BombWrapper
{
	readonly Dictionary<Mode, Color> ModeColors = new Dictionary<Mode, Color>()
	{
		{ Mode.Normal, Color.red },
		{ Mode.Zen, Color.cyan },
		{ Mode.Time, new Color(1, 0.5f, 0) },
		{ Mode.Steady, new Color(0, 0.8f, 0) }
	};

	public BombWrapper(Bomb bomb)
	{
		Bomb = bomb;
		holdable = bomb.GetComponentInChildren<FloatingHoldable>();
		timerComponent = bomb.GetTimer();
		widgetManager = bomb.WidgetManager;

		Color modeColor = ModeColors[Tweaks.CurrentMode];
		BombStatus.Instance.TimerPrefab.color = modeColor;
		timerComponent.text.color = modeColor;
		timerComponent.StrikeIndicator.RedColour = modeColor;

		if (Tweaks.CurrentMode == Mode.Zen)
        {
			ZenModeTimePenalty = Mathf.Abs(Modes.settings.ZenModeTimePenalty);
			ZenModeTimerRate = -timerComponent.GetRate();
			timerComponent.SetRateModifier(ZenModeTimerRate);
            Modes.initialTime = timerComponent.TimeRemaining;

			//This was in the original code to make sure the bomb didn't explode on the first strike
			bomb.NumStrikesToLose++;
		}

		foreach (BombComponent component in Bomb.BombComponents)
		{
			component.OnPass += delegate
			{
				BombStatus.Instance.UpdateSolves();

				if (Tweaks.CurrentMode == Mode.Time)
				{
					if (!Modes.settings.ComponentValues.TryGetValue(Modes.GetModuleID(component), out double ComponentValue))
					{
						ComponentValue = 6;
					}

					Modes.settings.TotalModulesMultiplier.TryGetValue(Modes.GetModuleID(component), out double totalModulesMultiplier);

					float time = (float) (Mathf.Min(Modes.Multiplier, Modes.settings.TimeModeMaxMultiplier) * (ComponentValue + Bomb.BombComponents.Count * totalModulesMultiplier));

					CurrentTimer += Math.Max(Modes.settings.TimeModeMinimumTimeGained, time);

					Modes.Multiplier += Modes.settings.TimeModeSolveBonus;
					BombStatus.Instance.UpdateMultiplier();
				}

				return false;
			};

			var OnStrike = component.OnStrike;
			component.OnStrike = (BombComponent source) =>
			{
				if (Tweaks.CurrentMode == Mode.Time)
				{
					Modes.Multiplier = Math.Max(Modes.Multiplier - Modes.settings.TimeModeMultiplierStrikePenalty, Modes.settings.TimeModeMinMultiplier);
					BombStatus.Instance.UpdateMultiplier();
					if (CurrentTimer < (Modes.settings.TimeModeMinimumTimeLost / Modes.settings.TimeModeTimerStrikePenalty))
					{
						CurrentTimer -= Modes.settings.TimeModeMinimumTimeLost;
					}
					else
					{
						CurrentTimer -= CurrentTimer * Modes.settings.TimeModeTimerStrikePenalty;
					}

					// We can safely set the number of strikes to -1 since it's going to be increased by the game after us.
					Bomb.NumStrikes = -1;
				}

				OnStrike(source);

				// These mode modifications need to happen after the game handles the strike since they change the timer rate.
				if (Tweaks.CurrentMode == Mode.Zen)
				{
					bomb.NumStrikesToLose++;

					ZenModeTimerRate = Mathf.Max(ZenModeTimerRate - Mathf.Abs(Modes.settings.ZenModeTimerSpeedUp), -Mathf.Abs(Modes.settings.ZenModeTimerMaxSpeed));
					timerComponent.SetRateModifier(ZenModeTimerRate);

					CurrentTimer += ZenModeTimePenalty * 60;
					ZenModeTimePenalty += Mathf.Abs(Modes.settings.ZenModeTimePenaltyIncrease);
				}

				if (Tweaks.CurrentMode == Mode.Steady)
				{
					timerComponent.SetRateModifier(1);
					CurrentTimer -= Modes.settings.SteadyModeFixedPenalty * 60 - Modes.settings.SteadyModePercentPenalty * bombStartingTimer;
				}

				BombStatus.Instance.UpdateStrikes();

				return false;
			};
		}

		var moduleTweaks = new Dictionary<string, Func<BombComponent, ModuleTweak>>()
		{
            { "Emoji Math", bombComponent => new EmojiMathLogging(bombComponent) },
			{ "Probing", bombComponent => new ProbingLogging(bombComponent) },
			{ "switchModule", bombComponent => new SwitchesLogging(bombComponent) },
			{ "WordScrambleModule", bombComponent => new WordScramblePatch(bombComponent) }
		};

        foreach (KMBombModule component in bomb.BombComponents.Select(x => x.GetComponent<KMBombModule>()).Where(x => x != null))
        {
            switch (component.ModuleType)
            {
                //TTK is our favorite Zen mode compatible module
                //Of course, everything here is repurposed from Twitch Plays.
                case "TurnTheKey":
                    new TTKComponentSolver(component, bomb, Tweaks.CurrentMode.Equals(Mode.Zen) ? Modes.initialTime : timerComponent.TimeRemaining);
                    break;

				// Correct the position of the status light
				case "ForeignExchangeRates":
				case "resistors":
				case "CryptModule":
				case "LEDEnc":
					component.transform.Find("Model").transform.localPosition = new Vector3(0.004f, 0, 0);
					break;
				case "TwoBits":
					component.GetComponentInChildren<StatusLightParent>().transform.localPosition = new Vector3(0.075167f, 0.01986f, 0.076057f);
					break;
			}

			if (component.GetComponent<BombComponent>().ComponentType == ComponentTypeEnum.Mod)
			{
				ReflectedTypes.FindModeBoolean(component);
			}

			// Setup module tweaks
			if (moduleTweaks.ContainsKey(component.ModuleType))
			{
				moduleTweaks[component.ModuleType](component.GetComponent<BombComponent>());
			}
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

	private float ZenModeTimePenalty;
	private float ZenModeTimerRate;

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