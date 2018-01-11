using UnityEngine;
using System;
using System.Collections;
using BombInfoExtensions;
using System.Collections.Generic;
using System.Linq;

public class SkewedModule : MonoBehaviour
{
    public GameObject[] Slots;
    public KMSelectable Submit;
    public KMAudio BombAudio;
    public KMBombModule BombModule;
    public KMBombInfo BombInfo;

    int[] Numbers = new int[3];
    int[] Display = new int[3];
    int[] Solution = new int[3];
    bool moduleActivated = false;
    bool solved = false;
    bool firstSpin = true;
    string ruleLog = "(Rule Log)";
    int[] fibonacci = {1, 1, 2, 3, 5, 8, 13, 21, 34, 55};

    static int idCounter = 1;
    int moduleID;

    void LogRule(string msg)
    {
        ruleLog += "\n" + msg;
    }

    public static int Count(IEnumerable source)
    {
        int c = 0;
        var e = source.GetEnumerator();
        while (e.MoveNext())
        {
            c++;
        }

        return c;
    }

    public static bool isPrime(int number)
    {
        int boundary = (int)Math.Floor(Math.Sqrt(number));

        if (number <= 1) return false;
        if (number == 2) return true;

        for (int i = 2; i <= boundary; ++i)
        {
            if (number % i == 0) return false;
        }

        return true;
    }

    void DebugMsg(string msg)
    {
        Debug.LogFormat("[Skewed Slots #{0}] {1}", moduleID, msg);
    }

    int Random(int min, int max)
    {
        return UnityEngine.Random.Range(min, max + 1);
    }

    void ButtonPress(KMSelectable Selectable)
    {
        Selectable.AddInteractionPunch();
        BombAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
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

                    spins[slotnumber] = spins[slotnumber] - 1;
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
            LogRule("Initial State: " + Numbers[0] + ", " + Numbers[1] + ", " + Numbers[2] + ".");

            for (int slotnumber = 0; slotnumber < 3; slotnumber++)
            {
                Solution[slotnumber] = ApplyRules(Numbers[slotnumber], slotnumber);
            }

            LogRule("\nFinal State: " + Solution[0] + ", " + Solution[1] + ", " + Solution[2] + ".");
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

        Submit.OnInteract += delegate ()
        {
            ButtonPress(Submit);
            if (moduleActivated)
            {
                DebugMsg("Submitted: " + Display[0] + " " + Display[1] + " " + Display[2]);
                if (Display[0] == Solution[0] && Display[1] == Solution[1] && Display[2] == Solution[2])
                {
                    StartCoroutine(SpinSlots(2));
                }
                else
                {
                    StartCoroutine(SpinSlots(1));
                }
            }
            return false;
        };

        foreach (GameObject slot in Slots)
        {
            int slotnumber = int.Parse(slot.name.Substring(4, 1));
            KMSelectable up = slot.transform.Find("Up").gameObject.GetComponent<KMSelectable>() as KMSelectable;
            KMSelectable down = slot.transform.Find("Down").gameObject.GetComponent<KMSelectable>() as KMSelectable;
            up.OnInteract += delegate ()
            {
                if (moduleActivated)
                {
                    ButtonPress(up);
                    Display[slotnumber] = (Display[slotnumber] + 1) % 10;

                    UpdateSlots();
                }
                return false;
            };

            down.OnInteract += delegate ()
            {
                ButtonPress(down);
                if (moduleActivated)
                {
                    Display[slotnumber] = Display[slotnumber] - 1;
                    if (Display[slotnumber] == -1)
                    {
                        Display[slotnumber] = 9;
                    }
                    UpdateSlots();
                }
                return false;
            };
        }
    }

    int ApplyRules(int digit, int slotnumber)
    {
        int correct = digit;

        LogRule("\nCalculating slot #" + (slotnumber + 1) + ". Starting at: " + digit);

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
        int lit = Count(BombInfo.GetOnIndicators());
        int unlit = Count(BombInfo.GetOffIndicators());

        correct = correct + lit - unlit;

        LogRule("Added indicators (" + lit + " - " + unlit + "). New number: " + correct);

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
            else if (isPrime(correct))
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
                foreach (char c in Convert.ToString(digit, 2).ToCharArray())
                {

                    if (c.ToString() == "1")
                    {
                        total = total + 1;
                    }
                }

                correct = total;
                LogRule("Number is greater than or equal to 5. Changed the number to the total of the binary form of the original digit.");
            }
            else
            {
                LogRule("No other rules apply. Number + 1.");
                correct += 1;
            }
        }

        LogRule("After the second section: " + correct);

        while (correct > 9)
        {
            correct = correct - 10;
        }
        while (correct < 0)
        {
            correct = correct + 10;
        }

        LogRule("Final digit: " + correct);

        return correct;
    }

    void UpdateSlots()
    {
        int slotnumber = 0;
        foreach (GameObject slot in Slots)
        {
            TextMesh text = slot.transform.Find("Number").gameObject.GetComponent<TextMesh>() as TextMesh;
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

    public IEnumerator ProcessTwitchCommand(string command)
    {
        string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 4 && split[0] == "submit")
        {
            int slot0, slot1, slot2;

            if (int.TryParse(split[1], out slot0) && int.TryParse(split[2], out slot1) && int.TryParse(split[3], out slot2))
            {
                List<int> submit = new List<int>() { slot0, slot1, slot2 };
				if (submit.Any(x => x < 0 || x > 9)) yield break;

				yield return null;
                foreach (GameObject slot in Slots)
                {
                    int slotnumber = int.Parse(slot.name.Substring(4, 1));
                    KMSelectable up = slot.transform.Find("Up").gameObject.GetComponent<KMSelectable>() as KMSelectable;
                    KMSelectable down = slot.transform.Find("Down").gameObject.GetComponent<KMSelectable>() as KMSelectable;

                    int diff = Display[slotnumber] - submit[slotnumber];
                    for (int i = 0; i < Math.Abs(diff); i++)
                    {
                        (diff > 0 ? down : up).OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                }

                if (Display[0] == Solution[0] && Display[1] == Solution[1] && Display[2] == Solution[2]) yield return "solve";
                else yield return "strike";

                Submit.OnInteract();
            }
        }
    }
}
