using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System.Linq;

public class Tuple<T, U>
{
    public T Item1 { get; private set; }
    public U Item2 { get; private set; }

    public Tuple(T item1, U item2)
    {
        Item1 = item1;
        Item2 = item2;
    }
}

public static class Tuple
{
    public static Tuple<T, U> Create<T, U>(T item1, U item2)
    {
        return new Tuple<T, U>(item1, item2);
    }
}

public class BrokenButtonModule : MonoBehaviour
{
    public GameObject[] Buttons;
    public GameObject[] SubmitButtons;
	public AudioClip SolveClip;

    public KMAudio BombAudio;
    public KMBombModule BombModule;
    public KMBombInfo BombInfo;

    string[] words = {
		// Explosion Related
		"bomb", "blast", "boom", "burst",

		// Bomb Components
		"wire", "button", "module", "light", "led", "switch",
        "RJ-45", "DVI-D", "RCA", "PS/2", "serial", "port",

		// Descriptions
		"row", "column", "one", "two", "three", "four", "five",
        "six", "seven", "eight", "size",

		// Misc
		"this", "that", "other", "submit", "abort", "drop",
        "thing", "blank", "", "broken", "too", "to", "yes",
        "see", "sea", "c", "wait", "word", "bob", "no",
        "not", "first", "hold", "late", "fail"
    };

    public IList<T> Shuffle<T>(IList<T> list)
    {
        if (list == null)
            throw new ArgumentNullException("list");
        for (int j = list.Count; j >= 1; j--)
        {
            int item = Random.Range(0, j);
            if (item < j - 1)
            {
                var t = list[item];
                list[item] = list[j - 1];
                list[j - 1] = t;
            }
        }
        return list;
    }

    List<GameObject> Solution = new List<GameObject>();
    bool SubmitButton = false;
    List<string> Pressed = new List<string>();
    bool LetterE = false;
    bool Solved = false;
	bool Animating = false;

    static int idCounter = 1;
    int moduleID;

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Broken Buttons #{0}] {1}", moduleID, msg);
    }

    void ButtonPress(KMSelectable Selectable)
    {
        Selectable.AddInteractionPunch();
        BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
    }

    IEnumerator Wait(float time, Func<bool> func)
    {
        yield return new WaitForSeconds(time);
        func();
    }

    TextMesh GetTextMesh(GameObject Button)
    {
        return Button.transform.Find("ButtonText").gameObject.GetComponent<TextMesh>();
    }

    Tuple<int, int> GetPos(GameObject Button)
    {
        int index = Array.IndexOf(Buttons, Button);
        return (index < 0) ? Tuple.Create(-1, -1) : Tuple.Create(index % 3 + 1, index / 3 + 1);
    }

    List<GameObject> GetButtons(string Text)
    {
        List<GameObject> Found = new List<GameObject>();
        foreach (GameObject button in Buttons)
        {
            if (GetTextMesh(button).text == Text)
            {
                Found.Add(button);
            }
        }

        return Found;
    }

    GameObject GetButtonPos(Tuple<int, int> Position)
    {
        foreach (GameObject button in Buttons)
        {
            Tuple<int, int> pos = GetPos(button);
            if (Position.Item1 == pos.Item1 && Position.Item2 == pos.Item2)
            {
                return button;
            }
        }

        return null;
    }

    List<GameObject> GetDuplicates()
    {
        Dictionary<string, List<GameObject>> Duplicates = new Dictionary<string, List<GameObject>>();
        foreach (GameObject button in Buttons)
        {
            string text = GetTextMesh(button).text;
            if (!Duplicates.ContainsKey(text))
            {
                Duplicates.Add(text, new List<GameObject>());
            }

            Duplicates[text].Add(button);
        }

        List<GameObject> DuplicateButtons = new List<GameObject>();
        foreach (GameObject button in Buttons)
        {
            string text = GetTextMesh(button).text;
            if (Duplicates[text].Count > 1)
            {
                foreach (GameObject dupbutton in Duplicates[text])
                {
                    DuplicateButtons.Add(dupbutton);
                }
            }
        }

        return DuplicateButtons;
    }

    string GetNewWord(string last)
    {
        Shuffle(words);
        int n = 0;
        string word = words[0];
        while (word == last || (GetButtons(word).Count > 1 && GetDuplicates().Count > 1))
        {
            n++;
            word = words[n];
        }

        return word;
    }

    void FindCorrectButtons()
    {
        if (Pressed.Count == 5)
		{
			Solution = new List<GameObject>();
			DebugMsg("Solution: Press the correct submit button because you've pressed 5 buttons. (" + (SubmitButton ? "Right" : "Left") + " submit button.)");
        }
        else
        {
            List<GameObject> LetterT = new List<GameObject>();
            for (int x = 1; x <= 3; x++)
            {
                GameObject button = GetButtonPos(Tuple.Create(x, 1));
                string text = GetTextMesh(button).text;
                if (text.Length > 0 && text[0].ToString() == "t")
                {
                    LetterT.Add(button);
                }

                button = GetButtonPos(Tuple.Create(x, 3));
                text = GetTextMesh(button).text;
                if (text.Length > 0 && text[0].ToString() == "t")
                {
                    LetterT.Add(button);
                }
            }

            List<GameObject> LessThan3 = new List<GameObject>();
            foreach (GameObject button in Buttons)
            {
                string text = GetTextMesh(button).text;
                if (text.Length < 3)
                {
                    LessThan3.Add(button);
                }
            }

            List<GameObject> Ports = new List<GameObject>();
            foreach (GameObject button in Buttons)
            {
                string text = GetTextMesh(button).text;
                if (text == "RJ-45" || text == "DVI-D" || text == "RCA" || text == "PS/2" || text == "serial")
                {
                    Ports.Add(button);
                }
            }

            var Sea = GetButtons("sea");
            var One = GetButtons("one");
            var Blank = GetButtons("");
            var Other = GetButtons("other");
            var Column = GetButtons("column");
            var Boom = GetButtons("boom");
            var Duplicates = GetDuplicates();

            if (Sea.Count > 0)
            {
                Solution = Sea;
                DebugMsg("Step: Press the word sea.");
            }
            else if (LetterT.Count > 0)
            {
                Solution = LetterT;
                DebugMsg("Step: Any word in the third or first row that starts with T.");
            }
            else if (GetButtons("submit").Count > 0 && One.Count > 0)
            {
                SubmitButton = false;
                Solution = One;
                DebugMsg("Step: Press the word one.");
            }
            else if (Blank.Count > 0)
            {
                Solution = Blank;
                DebugMsg("Step: Press any button that is completely blank.");
            }
            else if (Other.Count > 0)
            {
                SubmitButton = !SubmitButton;
                Solution = Other;
                DebugMsg("Step: Press the word other.");
            }
            else if (Duplicates.Count > 0)
            {
                Solution = Duplicates;
                DebugMsg("Step: Press any duplicate words.");
            }
            else if ((GetButtons("port").Count > 0 || GetButtons("module").Count > 0) && Ports.Count > 0)
            {
                Solution = Ports;
                DebugMsg("Step: Press a port name.");
            }
            else if (LessThan3.Count > 0)
            {
                Solution = LessThan3;
                DebugMsg("Step: Press any button that has less than three letters.");
            }
            else if (GetButtons("bomb").Count > 0 && Boom.Count > 0)
            {
                Solution = Boom;
                DebugMsg("Step: Press the word boom.");
            }
            else if (GetButtons("submit").Count > 0 && GetButtons("button").Count > 0)
            {
                Solution = new List<GameObject>();
                DebugMsg("Solution: Press the correct submit button. (" + (SubmitButton ? "Right" : "Left") + " submit button.)");
            }
            else if ((GetButtons("seven").Count > 0 || GetButtons("two").Count > 0) && Column.Count > 0)
            {
                List<GameObject> sol = new List<GameObject>();
                foreach (GameObject button in Column)
                {
                    int z = GetPos(button).Item2;
                    for (int x = 1; x <= 3; x++)
                    {
                        sol.Add(GetButtonPos(Tuple.Create(x, z)));
                    }
                }

                Solution = sol;
                DebugMsg("Step: Press any row that has a column button in it.");
            }
            else if (Pressed.Count == 0)
            {
                Solution = new List<GameObject>();
                Solution.Add(GetButtonPos(Tuple.Create(3, 2)));
                DebugMsg("Step: Press the third button in the second row.");
            }
            else if (LetterE)
            {
                Solution = new List<GameObject>();
                SubmitButton = true;
                DebugMsg("Solution: Press the correct submit button. (Right submit button.)");
            }
            else
            {
                Solution = new List<GameObject>();
                DebugMsg("Solution: Press the correct submit button. (" + (SubmitButton ? "Right" : "Left") + " submit button.)");
            }

            if (Solution.Count > 0)
            {
                string Text = "Press: ";
                Solution.ForEach(delegate (GameObject button)
                {
                    var pos = GetPos(button);
                    Text += "\"" + GetTextMesh(button).text.ToUpper() + "\" at " + pos.Item1 + ", " + pos.Item2 + "\n";
                });
                DebugMsg(Text.Substring(0, Text.Length - 1));
            }
        }
    }

    void LogButtons()
    {
        string Text = "Buttons:\n";
        for (int i = 0; i < Buttons.Length; i++)
        {
            Text += "[" + (GetTextMesh(Buttons[i]).text.ToUpper()) + "]";

            if ((i + 1) % 3 == 0)
            {
                Text += "\n";
            }
            else
            {
                Text += " ";
            }
        }

        DebugMsg(Text.Substring(0, Text.Length - 1));
    }

    void Start()
    {
        moduleID = idCounter++;

        foreach (GameObject button in Buttons)
        {
            GameObject Button = button;
            GetTextMesh(Button).text = GetNewWord("-");
            KMSelectable selectable = button.GetComponent<KMSelectable>() as KMSelectable;
            selectable.OnInteract += delegate ()
			{
				if (Animating) return false;
				ButtonPress(selectable);

                var index = Solution.IndexOf(Button);
                if (index > -1 && Pressed.Count < 5)
                {
                    if (Pressed.Count == 0 && GetTextMesh(Button).text.IndexOf("e") > -1)
                    {
                        LetterE = true;
                    }

                    Pressed.Add(GetTextMesh(Button).text);
					StartCoroutine(SwapButtonText(Button));
				}
                else
                {
                    BombModule.HandleStrike();
					StartCoroutine(StrikeButtonAnimation(Button));

                    if (Pressed.Count == 5)
                    {
                        DebugMsg("Strike: Press the correct submit button because you've pressed 5 buttons.");
                    }
                }

                return false;
            };
        }
        LogButtons();

        foreach (GameObject button in SubmitButtons)
        {
            string name = button.name;
            KMSelectable selectable = button.GetComponent<KMSelectable>() as KMSelectable;
            selectable.OnInteract += delegate ()
            {
				if (Animating) return false;
                ButtonPress(selectable);

                bool correct = (name == "Right");
                if (correct == SubmitButton && (Solution.Count == 0 || Pressed.Count == 5) && !Solved)
                {
                    Solved = true;
                    BombModule.HandlePass();
					DebugMsg("Module solved.");
					StartCoroutine(SolveAnimation());
                }
                else
                {
                    BombModule.HandleStrike();
					StartCoroutine(StrikeButtonAnimation(button));

                    LogButtons();
                }

                return false;
            };
        }

        FindCorrectButtons();
    }

	float SharpHitCurve(float alpha)
	{
		return Mathf.Round(Mathf.Exp(-Mathf.Pow(alpha - 0.5f, 2) / (2f * Mathf.Pow(0.09319812f, 2))) * 10000) / 10000; // Rounded to make sure it ends at 0.
	}

	IEnumerable TimeBasedAnimation(float length)
	{
		float startTime = Time.time;
		float alpha = 0;
		while (alpha < 1)
		{
			alpha = Mathf.Min((Time.time - startTime) / length, 1);
			yield return alpha;
		}
	}

	IEnumerator StrikeButtonAnimation(GameObject Button)
	{
		Animating = true;
		
		Vector2 textureScale = new Vector2(Random.Range(0.1f, 1f), Random.Range(0.1f, 1f));

		TextMesh textMesh = GetTextMesh(Button);
		foreach (float alpha in TimeBasedAnimation(0.3f))
		{
			float curvedAlpha = Mathf.Min(-Mathf.Pow(alpha - 0.5f, 2) / (0.25f / 1.5f) + 1.5f, 1);
			textMesh.color = Color.Lerp(Color.black, Color.red, curvedAlpha);
			Material mat = textMesh.GetComponent<Renderer>().material;
			mat.mainTextureScale = Vector2.Lerp(Vector2.one, textureScale, curvedAlpha);

			yield return null;
		}

		Animating = false;
	}

	IEnumerator SwapButtonText(GameObject Button)
	{
		Animating = true;
		BombAudio.PlaySoundAtTransform("Correct", transform);
		bool swapped = false;
		TextMesh textMesh = GetTextMesh(Button);

		Vector2 textureOffset = new Vector2(Random.Range(0f, 2f), Random.Range(0f, 2f));
		Vector2 textureScale = new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f));

		foreach (float alpha in TimeBasedAnimation(0.3f))
		{
			float curvedAlpha = SharpHitCurve(alpha);
			Material mat = textMesh.GetComponent<Renderer>().material;
			mat.mainTextureScale = Vector2.Lerp(Vector2.one, textureScale, curvedAlpha);
			mat.mainTextureOffset = Vector2.Lerp(Vector2.zero, textureOffset, curvedAlpha);

			if (!swapped && alpha > 0.5f)
			{
				swapped = true;

				textMesh.text = GetNewWord(textMesh.text);
				LogButtons();
				FindCorrectButtons();
			}

			yield return null;
		}

		Animating = false;
	}

	IEnumerator SolveAnimation()
	{
		Animating = true;
		BombAudio.PlaySoundAtTransform("Solve", transform);

		float[] samples = new float[SolveClip.samples * SolveClip.channels];
		SolveClip.GetData(samples, 0);

		float max = samples.Max(sample => Mathf.Abs(sample));
		samples = samples.Select(sample => sample / max).ToArray();

		Vector2[] scaleVectors = new[] { new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)), new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) };
		Vector2[] offsetVectors = new[] { new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f)), new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f)) };

		float prevAlpha = 0;
		foreach (float alpha in TimeBasedAnimation(SolveClip.length))
		{
			int startSample = (int) (prevAlpha * (samples.Length - 1));
			int endSample = (int) (alpha * (samples.Length - 1));
			float sample = samples.Skip(startSample).Take(endSample - startSample + 1).Max(s => Mathf.Abs(s));

			for (int i = 0; i < 2; i++) {
				TextMesh textMesh = GetTextMesh(SubmitButtons[i]);
				Material mat = textMesh.GetComponent<Renderer>().material;
				mat.mainTextureScale = Vector2.one + Vector2.Lerp(Vector2.zero, scaleVectors[i], sample);
				mat.mainTextureOffset = Vector2.Lerp(Vector2.zero, offsetVectors[i], sample * 0.1f);
				textMesh.color = Color.HSVToRGB(Random.value, 1, Math.Abs(sample));
			}

			prevAlpha = alpha;

			yield return null;
		}

		Animating = false;
	}

    #pragma warning disable 414
    private string TwitchHelpMessage = "Press the button by name with !{0} press \"this\". Press the button in column 2 row 3 with !{0} press 2 3. Press the right submit button with !{0} submit right.";
    #pragma warning restore 414

    public KMSelectable[] ProcessTwitchCommand(string command)
    {
        string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length == 2)
        {
            if (split[0] == "press")
            {
                foreach (GameObject button in Buttons)
                {
                    if ("\"" + GetTextMesh(button).text.ToLowerInvariant() + "\"" == split[1])
                    {
                        return new KMSelectable[] { button.GetComponent<KMSelectable>() };
                    }
                }
            }
            else if (split[0] == "submit")
            {
                if (split[1] == "l" || split[1] == "left")
                {
                    return new KMSelectable[] { SubmitButtons[0].GetComponent<KMSelectable>() };
                }
                else if (split[1] == "r" || split[1] == "right")
                {
                    return new KMSelectable[] { SubmitButtons[1].GetComponent<KMSelectable>() };
                }
            }
        }
        else if (split.Length == 3 && split[0] == "press")
        {
            int x = 0;
            int y = 0;
            if (int.TryParse(split[1], out x) && int.TryParse(split[2], out y))
            {
                GameObject button = GetButtonPos(Tuple.Create(x, y));
                if (button)
                {
                    return new KMSelectable[] { button.GetComponent<KMSelectable>() };
                }
            }
        }

        return null;
    }

	IEnumerator TwitchHandleForcedSolve()
	{
		while (Solution.Count > 0)
		{
			Solution[Random.Range(0, Solution.Count - 1)].GetComponent<KMSelectable>().OnInteract();
			yield return new WaitForSeconds(0.1f);
		}

		SubmitButtons.First(button => button.name == "Right" == SubmitButton).GetComponent<KMSelectable>().OnInteract();
		yield return new WaitForSeconds(0.1f);
	}
}
