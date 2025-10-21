using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BombInfoExtensions;
using UnityEngine;

public class SkewedModule : MonoBehaviour
{
    public GameObject[] Slots;
    public KMSelectable Submit;
    public KMAudio BombAudio;
    public KMBombModule BombModule;
    public KMBombInfo BombInfo;

	readonly int[] Numbers = new int[3];
	readonly int[] Display = new int[3];
	readonly int[] Solution = new int[3];
    bool moduleActivated;
    bool solved;
    string ruleLog = "(Rule Log)";
	readonly int[] fibonacci = {1, 1, 2, 3, 5, 8, 13, 21, 34, 55};

    static int idCounter = 1;
    int moduleID;

    void LogRule(string msg, params object[] args)
    {
        ruleLog += "\n" + string.Format(msg, args);
    }

    public static bool IsPrime(int number)
    {
        if (number <= 1) return false;
        if (number == 2) return true;

		int boundary = (int) Math.Floor(Math.Sqrt(number));
		for (int i = 2; i <= boundary; ++i)
        {
            if (number % i == 0) return false;
        }

        return true;
    }

    void DebugMsg(string msg, params object[] args)
    {
        Debug.LogFormat("[Skewed Slots #{0}] {1}", moduleID, string.Format(msg, args));
    }

    int Random(int min, int max)
    {
        return UnityEngine.Random.Range(min, max + 1);
    }

	string Join<T>(IEnumerable<T> objects, string separator = " ")
	{
		StringBuilder stringBuilder = new StringBuilder();
		IEnumerator<T> enumerator = objects.GetEnumerator();
		if (enumerator.MoveNext()) stringBuilder.Append(enumerator.Current); else return "";

		while (enumerator.MoveNext()) stringBuilder.Append(separator).Append(enumerator.Current);

		return stringBuilder.ToString();
	}

	void SetupInteraction(KMSelectable Selectable, Action action)
    {
		Selectable.OnInteract += () =>
		{
			Selectable.AddInteractionPunch();
			BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
			if (moduleActivated && !solved) action(); // !solved added to hide the warning

			throw new Exception("wow");

			return false;
		};
    }

    private IEnumerator SpinSlots(int state)
    {
        // State:
        // 0 - Module was just activated.
        // 1 - Mistake was made and will HandleStrike()
        // 2 - Module was solved and will HandlePass()

        moduleActivated = false;
        ruleLog = "(Rule Log)";
        int[] spins = {40 + Random(-5, 5), 60 + Random(-5, 5), 80 + Random(-5, 5) };
        while (spins[2] > 0)
        {
            for (int slotnumber = 0; slotnumber < 3; slotnumber++)
            {
                if (spins[slotnumber] > 0)
                {
                    int number = (Display[slotnumber] + 1) % 10;

                    Numbers[slotnumber] = number;
                    Display[slotnumber] = number;

					spins[slotnumber]--;
                }

                if (spins[slotnumber] == 0 && state == 2) {
                    Display[slotnumber] = 10;
                }
            }

            UpdateSlots();

            yield return new WaitForSeconds(.03f);
        }

        if (state < 2)
        {
            LogRule("Initial State: {0}.", Join(Numbers, " "));

            for (int slotnumber = 0; slotnumber < 3; slotnumber++)
            {
                Solution[slotnumber] = ApplyRules(Numbers[slotnumber], slotnumber);
            }

            LogRule("\nFinal State: {0}.", Join(Solution, " "));
            DebugMsg(ruleLog);

            moduleActivated = true;

            if (state == 1)
            {
                BombModule.HandleStrike();
            }
        }
        else
        {
            solved = true;
            BombModule.HandlePass();
        }
    }

    void Start()
    {
        BombModule.OnActivate += ActivateModule;

		SetupInteraction(Submit, () =>
		{
			DebugMsg("Submitted: {0}", Join(Display, " "));
			if (Display.SequenceEqual(Solution))
			{
				StartCoroutine(SpinSlots(2));
			}
			else
			{
				StartCoroutine(SpinSlots(1));
			}
		});

        foreach (GameObject slot in Slots)
        {
            int slotnumber = int.Parse(slot.name.Substring(4, 1));
            KMSelectable up = slot.transform.Find("Up").GetComponent<KMSelectable>();
            KMSelectable down = slot.transform.Find("Down").GetComponent<KMSelectable>();
			SetupInteraction(up, () =>
			{
				Display[slotnumber] = (Display[slotnumber] + 1) % 10;
				UpdateSlots();
			});

			SetupInteraction(down, () =>
			{
				Display[slotnumber]--;
				if (Display[slotnumber] == -1) Display[slotnumber] = 9;
				UpdateSlots();
			});
        }
    }

    int ApplyRules(int digit, int slotnumber)
    {
        int correct = digit;

        LogRule("\nCalculating slot #{0}. Starting at: {1}", slotnumber + 1, digit);

        // All digits
        switch (correct)
        {
            case 2:
                correct = 5;
                LogRule("2 is actually 5.");
                break;
            case 7:
                correct = 0;
                LogRule("7 is actually 0.");
                break;
        }

        string serial = BombInfo.GetSerialNumber();
        int lit = BombInfo.GetOnIndicators().Count();
        int unlit = BombInfo.GetOffIndicators().Count();

        correct = correct + lit - unlit;

        LogRule("Added indicators ({0} - {1}). New number: {2}", lit, unlit, correct);

        if (correct % 3 == 0)
        {
            LogRule("Number is a multiple of 3. Number + 4");
            correct += 4;
        }
        else if (correct > 7)
        {
            LogRule("Number is greater than 7. Number * 2");
            correct *= 2;
        }
        else if (correct < 3 && correct % 2 == 0)
        {
            LogRule("Number is even and less than 3. Number / 2");
            correct /= 2;
        }
        else if (BombInfo.IsPortPresent("StereoRCA") || BombInfo.IsPortPresent("PS2"))
        {
            LogRule("RCA or PS/2 port present. Skip this section.");
            // Skip the rest of the rules
        }
        else
        {
            LogRule("Added battery count to the original number for new number. (" + BombInfo.GetBatteryCount() + ")");
            correct = digit + BombInfo.GetBatteryCount();
        }

        LogRule("After the the first section: " + correct);

        // Specific digits
        if (slotnumber == 0)
        {
            if (correct % 2 == 0 && correct > 5)
            {
                LogRule("Number is even and greater than 5. # / 2.");
                correct /= 2;
            }
            else if (IsPrime(correct))
            {
                LogRule("Number is prime. Added rightmost serial number.");
                correct += int.Parse(serial[serial.Length - 1].ToString());
            }
            else if (BombInfo.IsPortPresent("Parallel"))
            {
                LogRule("Parallel port present. Number * -1.");
                correct *= -1;
            }
            else if (Numbers[1] % 2 == 1)
            {
                LogRule("Second slot was originally odd. Leave the number unchanged.");
                // Leave the digit unchanged.
            }
            else
            {
                LogRule("No other rules apply. Number - 2.");
                correct -= 2;
            }
        }
        else if (slotnumber == 1)
        {
            int index = Array.IndexOf(fibonacci, correct);
            if (BombInfo.IsIndicatorOff("BOB"))
            {
                LogRule("Bob helped you out. Leave the number unchanged.");
                // Leave the digit unchanged.
            }
            else if (correct == 0)
            {
                LogRule("The number is 0. Add the original digit in the first slot.");
                correct += Numbers[0];
            }
            else if (index > -1)
            {
                LogRule("Number is in the fibonacci sequence. Added the next digit: " + fibonacci[index + 1]);
                correct += fibonacci[index + 1];
            }
            else if (correct >= 7)
            {
                LogRule("Number greater than or equal to 7. Number + 4.");
                correct += 4;
            }
            else
            {
                LogRule("No other rules apply. Number * 3.");
                correct *= 3;
            }
        }
        else if (slotnumber == 2)
        {
            if (BombInfo.IsPortPresent("Serial"))
            {
                int largest = 0;
                foreach (char c in serial)
                {
                    int value;
                    if (int.TryParse(c.ToString(), out value))
                    {
                        if (value > largest)
                        {
                            largest = value;
                        }
                    }
                }

                correct += largest;
                LogRule("Serial port present. Added the largest serial number: " + largest);
            }
            else if (digit == Numbers[0] || digit == Numbers[1])
            {
                LogRule("The original digit is the same as another. Leave the number unchanged.");
                // Leave the digit unchanged.
            }
            else if (correct >= 5)
            {
                int total = 0;
                foreach (char c in Convert.ToString(digit, 2))
                {
                    if (c.ToString() == "1")
                    {
						total++;
                    }
                }

                correct = total;
                LogRule("Number is greater than or equal to 5. Changed the number to the total of the binary form of the original digit.");
            }
            else
            {
                LogRule("No other rules apply. Number + 1.");
				correct++;
            }
        }

        LogRule("After the second section: " + correct);

        while (correct > 9)
        {
            correct -= 10;
        }
        while (correct < 0)
        {
            correct += 10;
        }

        LogRule("Final digit: {0}", correct);

        return correct;
    }

    void UpdateSlots()
    {
        int slotnumber = 0;
        foreach (GameObject slot in Slots)
        {
            TextMesh text = slot.transform.Find("Number").GetComponent<TextMesh>();
            if (Display[slotnumber] < 10)
            {
                text.text = Display[slotnumber].ToString();
            } else {
                text.text = "!";
            }
            slotnumber++;
        }
    }

    void ActivateModule()
    {
        moduleID = idCounter++;

        for (int slotnumber = 0; slotnumber < 3; slotnumber++)
        {
            int number = Random(0, 9);

            Numbers[slotnumber] = number;
            Display[slotnumber] = number;
        }

        StartCoroutine(SpinSlots(0));
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = "Submit the correct response with !{0} submit 1 2 3.";
    #pragma warning restore 414

    public IEnumerator ProcessTwitchCommand(string command)
    {
		List<string> split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
		if (split[0] == "submit") split.RemoveAt(0); // Make submit be optional

		split = split.SelectMany(numbers => numbers.Select(num => num.ToString())).ToList();

        int slot0, slot1, slot2;
        if (split.Count == 3 && int.TryParse(split[0], out slot0) && int.TryParse(split[1], out slot1) && int.TryParse(split[2], out slot2))
        {
            int[] submit = new int[3] { slot0, slot1, slot2 };
			if (submit.Any(x => x < 0 || x > 9)) yield break;

			yield return null;
            foreach (GameObject slot in Slots)
            {
                int slotnumber = int.Parse(slot.name.Substring(4, 1));
                KMSelectable up = slot.transform.Find("Up").GetComponent<KMSelectable>();
                KMSelectable down = slot.transform.Find("Down").GetComponent<KMSelectable>();

                int diff = Display[slotnumber] - submit[slotnumber];
                for (int i = 0; i < Math.Abs(diff); i++)
                {
                    (diff > 0 ? down : up).OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
			}
			yield return new WaitForSeconds(0.25f);

			if (submit.SequenceEqual(Solution)) yield return "solve";
            else yield return "strike";

            Submit.OnInteract();
        }
    }

	IEnumerator TwitchHandleForcedSolve()
	{
		yield return ProcessTwitchCommand(string.Concat(Solution[0], Solution[1], Solution[2]));
		while (!solved) yield return true;
	}
}
