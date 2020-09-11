using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using BombInfoExtensions;
using System.Linq;
using Random = UnityEngine.Random;

public class CreationModule : MonoBehaviour
{
    public KMAudio BombAudio;
    public KMBombModule BombModule;
    public KMBombInfo BombInfo;
    public GameObject DayCylinder;
    public GameObject WeatherCylinder;
    public GameObject Elements;
    public GameObject Halo;

    public List<GameObject> BaseElements;

    public CreationData Data;

    static int idCounter = 1;
    int moduleID;

    KMSelectable ModuleSelectable = null;
    int Day = 1;
    int Permutation = -1;
    string Lifeform = "";
	readonly HashSet<string> Required = new HashSet<string>();
    string Weather = "";
    bool Combining = false;
    //bool FirstDay = true;
    bool Solved = false;
	readonly List<string> Weathers = new List<string>
    {
        "Clear",
        "Heat Wave",
        "Meteor Shower",
        "Rain",
        "Windy"
    };
	readonly Dictionary<string, Material> ElementData = new Dictionary<string, Material>();
	readonly Dictionary<string, Material> WeatherData = new Dictionary<string, Material>();

	readonly Dictionary<string, List<string>> Combinations = new Dictionary<string, List<string>>()
    {
        // GEN. 4
        {"Bird", new List<string> {"Egg", "Air"}},
        {"Dinosaur", new List<string> {"Earth", "Egg"}},
        {"Lizard", new List<string> {"Swamp", "Egg"}},
        {"Turtle", new List<string> {"Egg", "Water"}},
        {"Mushroom", new List<string> {"Weeds", "Earth"}},
        {"Worm", new List<string> {"Bacteria", "Swamp"}},
        {"Plankton", new List<string> {"Water", "Bacteria"}},
        {"Seeds", new List<string> {"Weeds", "Egg"}},

        // GEN. 3
        {"Bacteria", new List<string> {"Swamp", "Life"}},
        {"Egg", new List<string> {"Earth", "Life"}},
        {"Ghost", new List<string> {"Plasma", "Life"}},
        {"Weeds", new List<string> {"Water", "Life"}},

        // GEN. 2
        {"Life", new List<string> {"Energy", "Swamp"}},
        {"Plasma", new List<string> {"Energy", "Fire"}},

        // GEN. 1
        {"Swamp", new List<string> {"Water", "Earth"}},
        {"Energy", new List<string> {"Fire", "Air"}},
    };

    void DebugMsg(object msg)
    {
        Debug.LogFormat("[Creation #{0}] {1}", moduleID, msg);
    }

    float Mod(float x, float m)
    {
        return (x % m + m) % m;
    }

    float InOutCubic(float s, float e, float t) {
        return Mathf.Lerp(s, e, t < .5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1);
    }

    float wx = 0;
    float dx = 0;
    IEnumerator UpdateDisplay()
    {
		int multiplier = Random.Range(4, 8);

        float i = 0;
        float weatherx = wx;
        float dayx = dx;
        //float ws = 0;
        //float ds = 0;

        while (i < 1)
        {
            i = Math.Min(i + 0.01f, 1);

            float wang = InOutCubic(weatherx, 360 * multiplier + Weathers.IndexOf(Weather) * 72, i);
            float dang = InOutCubic(dayx, 360 * multiplier + (Day - 1) * 60, i);
            WeatherCylinder.transform.localEulerAngles = new Vector3(wang, 0, 0);
            DayCylinder.transform.localEulerAngles = new Vector3(dang, 0, 0);

            /*if (!FirstDay)
            {
                ws += wang - wx;
                if (ws >= 72)
                {
                    BombAudio.PlaySoundAtTransform("Click", transform);
                    ws %= 72;
                }

                ds += dang - dx;
                if (ds >= 60)
                {
                    BombAudio.PlaySoundAtTransform("Click", transform);
                    ds %= 60;
                }
            }*/

            wx = wang;
            dx = dang;

            yield return new WaitForSeconds(0.01f);
        }
        wx = Mod(wx, 360);
        dx = Mod(dx, 360);

        //FirstDay = false;
        Combining = false;
    }

    void RandomizeWeather()
    {
        string weather = Weathers[Random.Range(0, Weathers.Count)];
        while (weather == Weather)
        {
            weather = Weathers[Random.Range(0, Weathers.Count)];
        }
        DebugMsg("It's now Day " + Day + ". The current weather conditions are " + weather + ".");

        Weather = weather;
        StartCoroutine(UpdateDisplay());

        if (Permutation == -1)
        {
            Dictionary<string, List<int>> chart = new Dictionary<string, List<int>>()
            {
                {"Water", new List<int> {2, 1, 4, 3 }},
                {"Air", new List<int> {1, 2, 3, 4}},
                {"Earth", new List<int> {4, 3, 1, 2}},
                {"Fire", new List<int> {3, 4, 2, 1}}
            };

            Dictionary<string, string> references = new Dictionary<string, string>()
            {
                {"Rain", "Water"},
                {"Windy", "Air"},
                {"Meteor Shower", "Earth"},
                {"Heat Wave", "Fire"}
            };

            if (Weather == "Clear")
            {
                Permutation = 0;
            } else
            {
                string refelement = references[Weather];
                List<string> positions = new List<string>();
                foreach (GameObject element in BaseElements)
                {
                    positions.Add(element.GetComponent<Renderer>().sharedMaterial.name);
                }

                int index = positions.IndexOf(refelement);
                Permutation = chart[refelement][index];
                DebugMsg(references[weather] + " is in the " + (index < 2 ? "upper" : "bottom") + (index % 2 == 0 ? "-left" : "-right") + " corner.");
            }

            DebugMsg("Permutation: " + Permutation);
        }
    }

    void GetTarget()
    {
        int holder = BombInfo.GetBatteryHolderCount();
        if (holder >= 3)
        {
            string[] Lifeforms = new string[] {"Bird", "Dinosaur", "Turtle", "Lizard", "Worm"};
            int Offset;
            if (BombInfo.GetOnIndicators().Any())
            {
                if (BombInfo.GetBatteryCount(KMBombInfoExtensions.KnownBatteryType.AA) == BombInfo.GetBatteryCount())
                {
                    Offset = 0;
                }
                else
                {
                    Offset = 1;
                }
            } else if (BombInfo.GetOffIndicators().Any())
            {
                if (BombInfo.GetBatteryCount(KMBombInfoExtensions.KnownBatteryType.D) == BombInfo.GetBatteryCount())
                {
                    Offset = 2;
                }
                else
                {
                    Offset = 3;
                }
            } else
            {
                Offset = 4;
            }

            Lifeform = Lifeforms[(Permutation + Offset) % 5];
        }
        else if (holder <= 2)
        {
            bool duplicates = false;
            foreach (string port in BombInfo.GetPorts())
            {
                if (BombInfo.GetPortCount(port) > 1)
                {
                    duplicates = true;
                }
            }

            if (BombInfo.GetPortPlateCount() > BombInfo.GetBatteryHolderCount())
            {
                switch (Permutation)
                {
                    case 0: case 4: Lifeform = "Ghost"; break;
                    case 1: Lifeform = "Plankton"; break;
                    case 2: Lifeform = "Seeds"; break;
                    case 3: Lifeform = "Mushroom"; break;
                }
            } else if (duplicates)
            {
                switch (Permutation)
                {
                    case 0: case 4: Lifeform = "Plankton"; break;
                    case 1: Lifeform = "Seeds"; break;
                    case 2: Lifeform = "Mushroom"; break;
                    case 3: Lifeform = "Ghost"; break;
                }
            }
            else if (BombInfo.GetOffIndicators().Count() > BombInfo.GetOnIndicators().Count())
            {
                switch (Permutation)
                {
                    case 0: case 4: Lifeform = "Seeds"; break;
                    case 1: Lifeform = "Mushroom"; break;
                    case 2: Lifeform = "Ghost"; break;
                    case 3: Lifeform = "Plankton"; break;
                }
            } else
            {
                switch (Permutation)
                {
                    case 0: case 4: Lifeform = "Mushroom"; break;
                    case 1: Lifeform = "Ghost"; break;
                    case 2: Lifeform = "Plankton"; break;
                    case 3: Lifeform = "Seeds"; break;
                }
            }
        }
    }

    void FindElements(string element)
    {
        if (Combinations.ContainsKey(element))
        {
            List<string> subs = Combinations[element];
            foreach (string sub in subs)
            {
                Required.Add(sub);
                FindElements(sub);
            }
        }
    }

    List<string> Replace(List<string> List, string replace, string replacer)
    {
        List<string> newList = new List<string>(List);
        int index = newList.IndexOf(replace);
        if (index > -1)
        {
            newList[index] = replacer;
        }

        return newList;
    }

    void UpdateSelectable()
    {
        List<KMSelectable> children = new List<KMSelectable>();
        foreach (Transform child in Elements.transform)
        {
            GameObject element = child.gameObject;
            if (element != Halo)
            {
                children.Add(element.activeSelf ? element.GetComponent<KMSelectable>() : null);
            }
        }

        ModuleSelectable.Children = children.ToArray();
        ModuleSelectable.UpdateChildren();
    }

    IEnumerator CreateElement(string element, GameObject[] bases)
    {
        Combining = true;
        GameObject NewElement = null;
        foreach (Transform child in Elements.transform)
        {
            if (!child.gameObject.activeSelf && child.gameObject != Halo && element != Lifeform || child.gameObject.name == "FinalElement")
            {
                NewElement = child.gameObject;
                break;
            }
        }

        Dictionary<GameObject, Vector3> Origin = new Dictionary<GameObject, Vector3>();
        foreach (GameObject baseelement in bases)
        {
            Origin[baseelement] = baseelement.transform.localPosition;
        }
        Origin[NewElement] = NewElement.transform.localPosition;

        Halo.transform.localPosition = NewElement.transform.localPosition + new Vector3(0, 2, 0);

        float i = 0.01f;
        bool Played = false;
        while (i < 1)
        {
            i = Math.Min(i * 1.35f, 1f);
            if (i > 0.65 && !Played)
            {
                Played = true;
                BombAudio.PlaySoundAtTransform("NewElement", transform);
            }

            Halo.GetComponent<Light>().intensity = i * 2f;
            foreach (GameObject baseelement in bases)
            {
                baseelement.transform.localPosition = Vector3.Lerp(Origin[baseelement], Origin[NewElement], i);
            }

            yield return new WaitForSeconds(0.05f);
        }

        NewElement.GetComponent<Renderer>().sharedMaterial = ElementData[element];
        NewElement.SetActive(true);

        while (i > 0.01)
        {
            i = Math.Max(i / 1.35f, 0f);

            Halo.GetComponent<Light>().intensity = i * 2f;
            foreach (GameObject baseelement in bases)
            {
                baseelement.transform.localPosition = Vector3.Lerp(Origin[baseelement], Origin[NewElement], i);
            }

            yield return new WaitForSeconds(0.05f);
        }

        Halo.GetComponent<Light>().intensity = 0;
        foreach (GameObject baseelement in bases)
        {
            baseelement.transform.localPosition = Vector3.Lerp(Origin[baseelement], Origin[NewElement], 0);
        }

        if (element == Lifeform)
        {
            KMSelectable selectable = NewElement.GetComponent<KMSelectable>();
            ModuleSelectable.Children = new KMSelectable[1] { selectable };
            ModuleSelectable.UpdateChildren(selectable);
            BombAudio.PlaySoundAtTransform("Solved", transform);
            BombModule.HandlePass();
            Solved = true;

            Halo.transform.localPosition = new Vector3(0, 4, 0);
            Halo.GetComponent<Light>().range = transform.lossyScale.x * 0.2f;

            i = 0.01f;
            while (i < 1)
            {
                i = Math.Min(i * 1.25f, 1f);

                Halo.GetComponent<Light>().intensity = i * 0.5f;
                foreach (Transform child in Elements.transform)
                {
                    child.localScale = Vector3.Lerp(new Vector3(0.3f, 1, 0.3f), new Vector3(0, 1, 0), Math.Min(i * 2.5f, 1));
                }

                NewElement.transform.localPosition = Vector3.Lerp(Origin[NewElement], new Vector3(0, 0.04f, 0), i);
                NewElement.transform.localScale = Vector3.Lerp(new Vector3(0.3f, 1, 0.3f), new Vector3(0.85f, 1, 0.85f), i);

                yield return new WaitForSeconds(0.05f);
            }

            yield return new WaitForSeconds(0.75f);

            while (i > 0.01)
            {
                i = Math.Max(i / 1.05f, 0f);
                Halo.GetComponent<Light>().intensity = i * 0.5f;

                yield return new WaitForSeconds(0.05f);
            }

            Halo.GetComponent<Light>().intensity = i;
        } else
        {
            UpdateSelectable();
            Day++;
            RandomizeWeather();
        }
    }

    void Restart()
    {
        foreach (Transform child in Elements.transform)
        {
            GameObject element = child.gameObject;
            if (element.name != "BaseElement" && element != Halo)
            {
                element.SetActive(false);
            }
        }
        UpdateSelectable();

        Day = 1;
        Permutation = -1;
        foreach (GameObject element in BaseElements)
        {
            var other = BaseElements[Random.Range(0, BaseElements.Count)].GetComponent<Renderer>();
            var temp = element.GetComponent<Renderer>().sharedMaterial;
            element.GetComponent<Renderer>().sharedMaterial = other.sharedMaterial;
            other.sharedMaterial = temp;
        }

        RandomizeWeather();
        GetTarget();

        Required.Clear();
        Required.Add(Lifeform);
        FindElements(Lifeform);
        DebugMsg("Target Lifeform: " + Lifeform + ", requiring " + (Required.Count - 1).ToString() + " elements.");
    }

    void Start()
    {
        moduleID = idCounter++;

		Halo.GetComponent<Light>().range *= transform.lossyScale.x;

        ModuleSelectable = gameObject.GetComponent<KMSelectable>();
        foreach (Material mat in Data.Icons)
        {
            WeatherData[mat.name] = mat;
        }

        foreach (Material mat in Data.Elements)
        {
            ElementData[mat.name] = mat;
        }

        /*List<string> Lifeforms = new List<string>
        {
            "Bird",
            "Dinosaur",
            "Turtle",
            "Lizard",
            "Worm",
            "Ghost",
            "Plankton",
            "Mushroom",
            "Seeds"
        };

        string log = "";
        foreach (string lifeform in Lifeforms)
        {
            Required = new HashSet<string>();
            FindElements(lifeform);
            if (Required.Count > 9)
            {
                log += lifeform + " (" + Required.Count.ToString() + ")\n";
            } else
            {
                DebugMsg(lifeform + " " + Required.Count.ToString());
            }
        }
        DebugMsg(log);*/

        Restart();

        GameObject selected = null;
        foreach (Transform child in Elements.transform)
        {
            GameObject element = child.gameObject;
            if (element != Halo)
            {
                GameObject glow = child.Find("Glow").gameObject;
                KMSelectable Selectable = element.GetComponent<KMSelectable>();
                Selectable.OnInteract = () =>
				{
					if (!Combining && !Solved)
					{
						Selectable.AddInteractionPunch(0.5f);
						BombAudio.PlaySoundAtTransform("Select", child);

						if (selected == null)
						{
							selected = element;
							glow.SetActive(true);
						}
						else if (selected == element)
						{
							selected = null;
							glow.SetActive(false);
						}
						else
						{
							string name = selected.GetComponent<Renderer>().sharedMaterial.name;
							string name2 = element.GetComponent<Renderer>().sharedMaterial.name;

							string created = null;
							foreach (string step in Required)
							{
								if (Combinations.ContainsKey(step))
								{
									List<string> combinations = Combinations[step];
									switch (Weather)
									{
										case "Rain": combinations = Replace(combinations, "Water", "Fire"); break;
										case "Windy": combinations = Replace(combinations, "Air", "Earth"); break;
										case "Heat Wave": combinations = Replace(combinations, "Fire", "Water"); break;
										case "Meteor Shower": combinations = Replace(combinations, "Earth", "Air"); break;
										case "Clear": break;
									}

									if (combinations.Contains(name) && combinations.Contains(name2))
									{
										created = step;
									}
								}
							}

							foreach (Transform ele in Elements.transform)
							{
								if (ele.gameObject != Halo)
								{
									ele.Find("Glow").gameObject.SetActive(false);
								}
							}

							if (created != null)
							{
								DebugMsg("Combining " + name + " and " + name2 + " to create " + created + ".");
								Required.Remove(created);

								StartCoroutine(CreateElement(created, new GameObject[] { element, selected }));
							}
							else
							{
								DebugMsg("Combining " + name + " and " + name2 + " is wrong. Restarting module...");
								BombModule.HandleStrike();
								Restart();
							}

							selected = null;
						}
					}

					return false;
				};
            }
        }
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "Combine two elements with !{0} combine water fire.";
    #pragma warning restore 414

    public IEnumerator ProcessTwitchCommand(string command)
    {
        string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 3 && split[0] == "combine" && split[1] != split[2])
        {
            List<GameObject> elements = new List<GameObject>();
            foreach (Transform child in Elements.transform)
            {
                GameObject element = child.gameObject;
                if (element.activeInHierarchy && element != Halo)
                {
                    string name = element.GetComponent<Renderer>().sharedMaterial.name.ToLowerInvariant();
                    if (name == split[1] || name == split[2])
                    {
                        elements.Add(element);
                    }
                }
            }

            if (elements.Count == 2)
            {
				yield return null;

                foreach (GameObject element in elements)
                {
                    element.GetComponent<KMSelectable>().OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }

                yield return new WaitUntil(() => !Combining || Solved);
            }
        }
    }

	IEnumerable MakeElement(string targetElement)
	{
		if (!Combinations.ContainsKey(targetElement)) yield break;

		var neededElements = Combinations[targetElement];
		foreach (string neededElement in neededElements)
			if (Required.Contains(neededElement))
				foreach (object obj in MakeElement(neededElement)) yield return obj;

		switch (Weather)
		{
			case "Rain": neededElements = Replace(neededElements, "Water", "Fire"); break;
			case "Windy": neededElements = Replace(neededElements, "Air", "Earth"); break;
			case "Heat Wave": neededElements = Replace(neededElements, "Fire", "Water"); break;
			case "Meteor Shower": neededElements = Replace(neededElements, "Earth", "Air"); break;
			case "Clear": break;
		}

		foreach (Transform child in Elements.transform)
		{
			GameObject element = child.gameObject;
			if (element.activeInHierarchy && element != Halo)
			{
				if (neededElements.Contains(element.GetComponent<Renderer>().sharedMaterial.name))
				{
					element.GetComponent<KMSelectable>().OnInteract();
					yield return new WaitForSeconds(0.1f);
				}
			}
		}

		while (Combining && !Solved) yield return true;
	}

	IEnumerator TwitchHandleForcedSolve()
	{
		foreach (object obj in MakeElement(Lifeform)) yield return obj;
	}
}
