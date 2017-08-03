using UnityEngine;
using System.Collections;
using System.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using BetterModSettings;

public class EventSettings
{
    public bool Enabled = true;
    public float Weight = float.NaN;
    public float MinDiff = float.NaN;
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
    static BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
    static BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;

    public GameObject UI;
    public GameObject ActiveInd;
    public GameObject SuccessInd;
    public GameObject NextEvent;

    List<PacingEvent> Events = new List<PacingEvent>();

    ModSettings BetterSettings = new ModSettings("PacingExtender", typeof(PacingSettings));
    PacingSettings Settings;
	
    static void Log(params object[] info)
    {
        Debug.Log("[PacingExtender] " + string.Join(", ", info.Select(x => x.ToString()).ToArray()));
    }

	static void Log(string format, params object[] formatting)
	{
		Log(string.Format(format, formatting));
	}

    List<object> GetIdleEvents(IList actions)
    {
        List<object> events = new List<object>(); ;
        foreach (object pacingEvent in actions)
        {
            object value = pacingEvent.GetType().GetProperty("EventType", Public).GetValue(pacingEvent, null);
            if (value.ToString() == "Idle_DoingWell")
            {
                events.Add(pacingEvent);
            }
        }

        return events;
    }

    float timeLeft = 0;
    int minTime = 20;
    int maxTime = 60;
    KMGameInfo.State GameState = KMGameInfo.State.Transitioning;

    public static Type FindType(string qualifiedTypeName)
    {
        Type t = Type.GetType(qualifiedTypeName);

        if (t != null)
        {
            return t;
        }
        else
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(qualifiedTypeName);
                if (t != null)
                    return t;
            }
            return null;
        }
    }

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

        public PacingEvent(object PacingAction, Func<string, EventSettings> GetEventSettings)
        {
            Type _pacingActionType = PacingAction.GetType();
            _legacyAction = (Action) _pacingActionType.GetField("Action", Public).GetValue(PacingAction);

            _name = (string) _pacingActionType.GetProperty("Name", Public).GetValue(PacingAction, null);
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
            _weight = float.IsNaN(settings.Weight) ? 1 : settings.Weight;
        }

        public PacingEvent(Func<IEnumerator> Event, string name, float minDiff, float weight, float cooldown, EventSettings settings)
        {
            _funcAction = Event;
            _name = name;
            _minDiff = float.IsNaN(settings.MinDiff) ? minDiff : settings.MinDiff;
            _weight = float.IsNaN(settings.Weight) ? weight : settings.Weight;
            _cooldown = cooldown;
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
        if (GetEventSettings(name).Enabled)
        {
            Events.Add(new PacingEvent(Event, name, minDiff, weight, cooldown, GetEventSettings(name)));
        }
    }

	IEnumerator WaitForPaceMaker()
    {
        Settings = (PacingSettings) BetterSettings.Settings;

        // Validate config file.
        if (Settings.Min > Settings.Max)
        {
            Settings.Min = Settings.Max;
        }

        if (Settings.AbsoluteMinimum > Settings.Min)
        {
            Settings.AbsoluteMinimum = Settings.Min;
        }
		
        yield return new WaitUntil(() => { _paceMakerObj = GameObject.Find("PaceMaker"); return _paceMakerObj != null; });
		Log("paceMakerObj");

        minTime = Math.Max(Math.Min(Settings.Min, Settings.Max), 0);
        maxTime = Math.Max(Settings.Max, minTime);

        UI.SetActive(Settings.Debug);

        timeLeft = UnityEngine.Random.Range(minTime, maxTime);

        object paceMaker = _paceMakerObj.GetComponent("PaceMaker");
        _paceMakerType = paceMaker.GetType();

        FieldInfo isActive = _paceMakerType.GetField("isActive", NonPublic);
        MethodInfo populatePacingEvents = _paceMakerType.GetMethod("PopulatePacingActions", NonPublic);
        IList actions = (IList) _paceMakerType.GetField("actions", NonPublic).GetValue(paceMaker);

        yield return new WaitUntil(() => (bool) isActive.GetValue(paceMaker));
		Log("isActive");
        populatePacingEvents.Invoke(paceMaker, null);
        bool actionsEnabled = actions.Count > 0;

        object mission = _paceMakerType.GetField("mission", NonPublic).GetValue(paceMaker);
        if ((string) mission.GetType().GetProperty("ID", Public).GetValue(mission, null) != "freeplay" && actionsEnabled)
        {
            activeImg.color = Color.yellow;
			Log("Original");
            yield return new WaitUntil(() =>
            {
                List<object> idle = GetIdleEvents(actions);
                eventCount.text = idle.Count.ToString();
                return idle.Count == 0;
            });
			Log("Finished");
			populatePacingEvents.Invoke(paceMaker, null);
        }

        isActive.SetValue(paceMaker, false);

        if (actionsEnabled)
        {
            eventCount.text = GetIdleEvents(actions).Count.ToString();
            activeImg.color = Color.green;
        }
        else
        {
            activeImg.color = Color.gray;
        }

        Events.AddRange(GetIdleEvents(actions).Select((object PacingAction) => new PacingEvent(PacingAction, GetEventSettings)).Where(e => GetEventSettings(e._name).Enabled));

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

		Log("Extender Finished");
		OnRoundEnded();
    }

	void OnRoundEnded()
	{
		activeImg.color = Color.red;

		if (_paceMakerObj != null) // Silly PaceMaker, you can't pace after a bomb!
		{
			Log("Destroyed");
			Destroy(_paceMakerObj);
			_paceMakerObj = null;
		}

		BetterSettings.Settings = Settings; // Any Event settings should have been added by now, so we can update the users config file.
		Events.Clear();

		UI.SetActive(false);
	}

    float CalculateSuccess()
    {
        object[] Bombs = FindObjectsOfType(_bombType);
        if (Bombs.Length > 0)
        {
            float worstRating = Settings.Min / (float) Settings.AbsoluteMinimum;
            float bestTimeRemaining = 0;
            float maxTimeRemaining = 0;
            int totalModules = 0;
            int totalSolved = 0;
            foreach (object bomb in Bombs) totalModules += (int) _getSolvableMethod.Invoke(bomb, null);

            foreach (object bomb in Bombs)
            {
                int thisSolved = (int) _getSolvedMethod.Invoke(bomb, null);
                totalSolved += thisSolved;

                object timer = _timer.GetValue(bomb);
                float curTime = (float) _timeRemaining.GetValue(timer) / (float) _rateModifier.GetValue(timer);
                float maxTime = (float) _totalTime.GetValue(bomb);
                maxTimeRemaining = Math.Max(maxTimeRemaining, maxTime);

                bestTimeRemaining = Mathf.Max(bestTimeRemaining, curTime);
                worstRating = Mathf.Min(worstRating, CalculateRating((int) _getSolvableMethod.Invoke(bomb, null) - thisSolved, totalModules, curTime, maxTime));
            }
            worstRating = Mathf.Min(worstRating, CalculateRating(totalModules - totalSolved, totalModules, bestTimeRemaining, maxTimeRemaining));
            return worstRating;
        }
        else return 1;
    }

    float CalculateRating(int remain, int total, float timeLeft, float timeTotal)
    {
        float pace = total / timeTotal;
        float curPace = remain / timeLeft;

        float rating = pace / curPace;
        rating = Mathf.Min(Mathf.Max(rating, 0), 1.5f);

        return rating;
    }

	// TODO: Get all types here instead of in the coroutine.
	Type _paceMakerType;
	GameObject _paceMakerObj;

	// Use in CalculateSuccess()
	Type _bombType;
    MethodInfo _getSolvableMethod;
    MethodInfo _getSolvedMethod;
    FieldInfo _numStrikes;
    FieldInfo _numStrikesToLose;
    FieldInfo _totalTime;
    FieldInfo _timer;
    Type _timerType;
    FieldInfo _timeRemaining;
    FieldInfo _rateModifier;
	
	// Used in WaitForPaceMaker()
	Image activeImg = null;
	Text eventCount = null;

	Text eventTime = null;
	Text percent = null;

	void Start()
    {
		_paceMakerType = FindType("Assets.Scripts.Pacing.PaceMaker");

		_bombType = FindType("Bomb");
        _getSolvableMethod = _bombType.GetMethod("GetSolvableComponentCount", Public);
        _getSolvedMethod = _bombType.GetMethod("GetSolvedComponentCount", Public);
        _totalTime = _bombType.GetField("TotalTime", Public);
        _timer = _bombType.GetField("timer", NonPublic);
        _timerType = FindType("TimerComponent");
        _timeRemaining = _timerType.GetField("TimeRemaining", Public);
        _rateModifier = _timerType.GetField("rateModifier", NonPublic);
		
		activeImg = ActiveInd.GetComponent<Image>();
		eventCount = ActiveInd.transform.Find("Text").GetComponent<Text>();
		eventTime = NextEvent.transform.Find("Text").GetComponent<Text>();
		percent = SuccessInd.transform.Find("Text").GetComponent<Text>();

		// TODO: Make Coroutine stoppable instead of handling it inside the coroutine.
		Coroutine _mainCoroutine = null;

        GetComponent<KMGameInfo>().OnStateChange = delegate (KMGameInfo.State state)
        {
            if (state == KMGameInfo.State.Gameplay)
			{
				_mainCoroutine = StartCoroutine(WaitForPaceMaker());
			} else if (_mainCoroutine != null)
			{
				StopCoroutine(_mainCoroutine);
				_mainCoroutine = null;

				OnRoundEnded();
			}
        };
    }
}
