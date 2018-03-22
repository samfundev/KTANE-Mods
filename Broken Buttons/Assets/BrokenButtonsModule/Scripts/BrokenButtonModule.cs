using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

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
                ButtonPress(selectable);

                var index = Solution.IndexOf(Button);
                if (index > -1 && Pressed.Count < 5)
                {
                    if (Pressed.Count == 0 && GetTextMesh(Button).text.IndexOf("e") > -1)
                    {
                        LetterE = true;
                    }

                    Pressed.Add(GetTextMesh(Button).text);
                    GetTextMesh(Button).text = GetNewWord(GetTextMesh(Button).text);
                    LogButtons();
                    FindCorrectButtons();
                }
                else
                {
                    BombModule.HandleStrike();

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
                ButtonPress(selectable);

                bool correct = (name == "Right");
                if (correct == SubmitButton && (Solution.Count == 0 || Pressed.Count == 5) && !Solved)
                {
                    Solved = true;
                    BombModule.HandlePass();
					DebugMsg("Module solved.");
                }
                else
                {
                    BombModule.HandleStrike();
                    LogButtons();
                }

                return false;
            };
        }

        FindCorrectButtons();
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
}
