using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Pacing;
using Assets.Scripts.Missions;

public class EventSettings
{
    public bool Enabled = true;
    public float Weight = float.NaN;
    public float MinRating = float.NaN;
}

public class PacingSettings
{
    public int Min = 120;
    public int Max = 300;
    public int AbsoluteMinimum = 45;
    public bool Debug = false;
    public Dictionary<string, EventSettings> EventSettings = new Dictionary<string, EventSettings>();
}

[RequireComponent(typeof(KMService))]
[RequireComponent(typeof(KMGameInfo))]
public class PacingExtender : MonoBehaviour
{
    public GameObject UI;
    public GameObject ActiveInd;
    public GameObject SuccessInd;
    public GameObject NextEvent;

    List<PacingEvent> Events = new List<PacingEvent>();

    ModConfig<PacingSettings> PacingSettings = new ModConfig<PacingSettings>("PacingExtender");
    PacingSettings Settings;

	static void Log(object format, params object[] formatting)
	{
		Debug.LogFormat("[PacingExtender] " + format, formatting);
	}

    List<PacingAction> GetIdleEvents(List<PacingAction> actions)
    {
        List<PacingAction> events = new List<PacingAction>();
        foreach (PacingAction pacingAction in actions)
        {
            PaceEvent value = pacingAction.EventType;
            if (value == PaceEvent.Idle_DoingWell)
            {
                events.Add(pacingAction);
            }
        }

        return events;
    }

    float timeLeft = 0;
    int minTime = 20;
    int maxTime = 60;

    class PacingEvent : MonoBehaviour
    {
        Action _legacyAction = null;
        float _executionTime = 0;
        Func<IEnumerator> _funcAction = null;
        public string _name = "";
        public float _minDiff = 0;
        public float _weight = 0;
        public float _cooldown = 0;
        public float _timeStamp = 0;

        public PacingEvent(PacingAction PacingAction, Func<string, EventSettings> GetEventSettings)
        {
            _legacyAction = PacingAction.Action;

            _name = PacingAction.Name;
            switch (_name)
            {
                case "Cut the lights":
                    _executionTime = 12.181f;
                    break;
                case "Turn on Alarm Clock":
                    _executionTime = 5;
                    break;
                default:
                    Log("Unhandled PacingAction: " + _name);
                    break;
            }

            EventSettings settings = GetEventSettings(_name);
			_minDiff = float.IsNaN(settings.MinRating) ? 0 : settings.MinRating;
			_weight = float.IsNaN(settings.Weight) ? 1 : settings.Weight;

			if (float.IsNaN(settings.MinRating))
			{
				settings.MinRating = _minDiff;
			}

			if (float.IsNaN(settings.Weight))
			{
				settings.Weight = _weight;
			}
		}

        public PacingEvent(Func<IEnumerator> Event, string name, float minDiff, float weight, float cooldown, EventSettings settings)
        {
            _funcAction = Event;
            _name = name;
            _minDiff = float.IsNaN(settings.MinRating) ? minDiff : settings.MinRating;
            _weight = float.IsNaN(settings.Weight) ? weight : settings.Weight;
            _cooldown = cooldown;

			if (float.IsNaN(settings.MinRating))
			{
				settings.MinRating = _minDiff;
			}

			if (float.IsNaN(settings.Weight))
			{
				settings.Weight = _weight;
			}
		}

        public IEnumerator ExecuteAction()
        {
            Log("Executing PacingEvent: " + _name);
            if (_legacyAction != null)
            {
                _legacyAction();
                yield return new WaitForSeconds(_executionTime);
            }
            else
            {
                IEnumerator enumerator = _funcAction();
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }
            }
        }
    }

    EventSettings GetEventSettings(string name)
    {
        if (Settings.EventSettings.ContainsKey(name))
        {
            return Settings.EventSettings[name];
        }
        else
        {
            EventSettings NewSettings = new EventSettings();
            Settings.EventSettings.Add(name, NewSettings);

            return NewSettings;
        }
    }

    public void RegisterEvent(Func<IEnumerator> Event, string name, float minDiff, float weight, float cooldown)
    {
		EventSettings settings = GetEventSettings(name);
		if (settings.Enabled)
        {
            Events.Add(new PacingEvent(Event, name, minDiff, weight, cooldown, settings));
        }
    }

	IEnumerator WaitForPaceMaker()
    {
        Settings = PacingSettings.Settings;

        // Validate config file.
        if (Settings.Min > Settings.Max)
        {
            Settings.Min = Settings.Max;
        }

        if (Settings.AbsoluteMinimum > Settings.Min)
        {
            Settings.AbsoluteMinimum = Settings.Min;
        }

        PaceMaker paceMaker = null;
        yield return new WaitUntil(() => { paceMaker = SceneManager.Instance.GameplayState.GetPaceMaker(); return paceMaker != null; });
        _paceMakerObj = paceMaker.gameObject;

        minTime = Math.Max(Math.Min(Settings.Min, Settings.Max), 0);
        maxTime = Math.Max(Settings.Max, minTime);
        timeLeft = UnityEngine.Random.Range(minTime, maxTime);

		UI.SetActive(Settings.Debug);

        List<PacingAction> actions = paceMaker.GetValue<List<PacingAction>>("actions");

		yield return new WaitUntil(() => paceMaker.GetValue<bool>("isActive"));
        paceMaker.CallMethod("PopulatePacingActions");
        bool actionsEnabled = actions.Count > 0;

        Mission mission = paceMaker.GetValue<Mission>("mission");
        if (mission.ID != "freeplay" && actionsEnabled)
        {
            activeImg.color = Color.yellow;
            yield return new WaitUntil(() =>
            {
                List<PacingAction> idle = GetIdleEvents(actions);
                eventCount.text = idle.Count.ToString();
                return idle.Count == 0;
            });
            paceMaker.CallMethod("PopulatePacingActions");
        }

        paceMaker.SetValue("isActive", false);

		if (actionsEnabled)
        {
            eventCount.text = GetIdleEvents(actions).Count.ToString();
            activeImg.color = Color.green;
        }
        else
        {
            activeImg.color = Color.gray;
        }

		Events.AddRange(GetIdleEvents(actions).Select(PacingAction => new PacingEvent(PacingAction, GetEventSettings)).Where(e => GetEventSettings(e._name).Enabled));

		yield return new WaitForSeconds(1f);
        while (_paceMakerObj != null)
        {
            if (actionsEnabled)
            {
                float success = CalculateSuccess();
                timeLeft -= success;

                if (timeLeft <= 0)
                {
                    timeLeft = UnityEngine.Random.Range(minTime, maxTime);

                    PacingEvent[] validEvents = Events.Where(e => e._timeStamp <= Time.time && e._minDiff <= success).ToArray();
                    if (validEvents.Length == 0)
                    {
                        Log("Unable to find any events to play! Skipping an event this time.");
                    }
                    else
                    {
                        // Pick a random event based on weights.
                        float targetWeight = UnityEngine.Random.Range(0, validEvents.Sum(e => e._weight));
                        float currentWeight = 0;
                        PacingEvent idleEvent = null;

                        foreach (PacingEvent e in validEvents)
                        {
                            currentWeight += e._weight;
                            if (currentWeight >= targetWeight)
                            {
                                idleEvent = e;
                                break;
                            }
                        }

                        IEnumerator enumerator = idleEvent.ExecuteAction();
                        while (enumerator.MoveNext())
                        {
                            yield return enumerator.Current;
                        }
                        idleEvent._timeStamp = Time.time + idleEvent._cooldown;
                    }
                }

                eventTime.text = timeLeft.ToString("n2");
                percent.text = Math.Round((decimal) success * 100, 0, MidpointRounding.AwayFromZero) + "%";
            }

            yield return new WaitForSeconds(1f);

            if (actionsEnabled)
            {
                activeImg.color = Color.green;
            }
		}

		OnRoundEnded();
    }

	void OnRoundEnded()
	{
		activeImg.color = Color.red;

		if (_paceMakerObj != null) // Silly PaceMaker, you can't pace after a bomb!
		{
			Destroy(_paceMakerObj);
			_paceMakerObj = null;
		}

		PacingSettings.Settings = Settings; // Any Event settings should have been added by now, so we can update the users config file.
		Events.Clear();

		UI.SetActive(false);
	}

    float CalculateSuccess()
    {
        List<Bomb> Bombs = SceneManager.Instance.GameplayState.Bombs;
        if (Bombs.Count > 0)
        {
            float worstRating = Settings.Min / (float) Settings.AbsoluteMinimum;
            float bestTimeRemaining = 0;
            float maxTimeRemaining = 0;
            int totalModules = 0;
            int totalSolved = 0;
            foreach (Bomb bomb in Bombs) totalModules += bomb.GetSolvableComponentCount();

            foreach (Bomb bomb in Bombs)
            {
                int thisSolved = bomb.GetSolvedComponentCount();
                totalSolved += thisSolved;

                TimerComponent timer = bomb.GetTimer();
                float curTime = timer.TimeRemaining / timer.GetRate();
                float maxTime = bomb.TotalTime;
                maxTimeRemaining = Math.Max(maxTimeRemaining, maxTime);

                bestTimeRemaining = Mathf.Max(bestTimeRemaining, curTime);
                worstRating = Mathf.Min(worstRating, CalculateRating(bomb.GetSolvableComponentCount() - thisSolved, totalModules, curTime, maxTime));
            }
            return Mathf.Min(worstRating, CalculateRating(totalModules - totalSolved, totalModules, bestTimeRemaining, maxTimeRemaining));
        }
        else return 1;
    }

    float CalculateRating(int remain, int total, float timeLeft, float timeTotal)
    {
        float pace = total / timeTotal;
        float curPace = remain / timeLeft;

        float rating = pace / curPace;
        return Mathf.Min(Mathf.Max(rating, 0), 1.5f);
    }

	#region Type Definitions
	GameObject _paceMakerObj;

    // Used for UI
    Image activeImg = null;
	Text eventCount = null;

	Text eventTime = null;
	Text percent = null;
	#endregion

	void Start()
    {
		#region Type Assignments
		activeImg = ActiveInd.GetComponent<Image>();
		eventCount = ActiveInd.transform.Find("Text").GetComponent<Text>();
		eventTime = NextEvent.transform.Find("Text").GetComponent<Text>();
		percent = SuccessInd.transform.Find("Text").GetComponent<Text>();
		#endregion

		Coroutine _mainCoroutine = null;

        GetComponent<KMGameInfo>().OnStateChange = (KMGameInfo.State state) =>
        {
            if (state == KMGameInfo.State.Gameplay)
            {
                _mainCoroutine = StartCoroutine(WaitForPaceMaker());
            }
            else if (_mainCoroutine != null)
            {
                StopCoroutine(_mainCoroutine);
                _mainCoroutine = null;

                OnRoundEnded();
            }
        };
    }
}
