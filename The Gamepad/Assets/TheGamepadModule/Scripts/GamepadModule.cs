using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using BombInfoExtensions;
using Random = UnityEngine.Random;
using System.Collections.Generic;

public class GamepadModule : MonoBehaviour
{
    public GameObject Input;
    public GameObject Digits1;
    public GameObject Digits2;
    public KMSelectable[] Buttons;

    public KMAudio BombAudio;
    public KMBombModule BombModule;
    public KMBombInfo BombInfo;

    string input = "";
    string solution = null;
    bool solved = false;

    int x = 0;
    int y = 0;

    static int idCounter = 1;
    int moduleID;

    string[] correct = { "GOOD JOB!", "CORRECT!", ":)", "=)", ";)", ":D", "=D", ";D" };
    string[] incorrect = { "POOR JOB!", "INCORRECT", ":(", ";(", "=(", ">:(", "O_o", "o_o", "o_O", "O_O" };

    int[] hcn = { 1, 2, 4, 6, 12, 24, 36, 48, 60 };

    public static bool isPrime(int number)
    {
        int boundary = (int) Math.Floor(Math.Sqrt(number));

        if (number <= 1) return false;
        if (number == 2) return true;

        for (int i = 2; i <= boundary; ++i)
        {
            if (number % i == 0) return false;
        }

        return true;
    }

    bool isMultiple(int number, int multiple)
    {
        return number % multiple == 0;
    }

    bool isPerfectSquare(int number)
    {
        return Math.Sqrt(number) % 1 == 0;
    }

    string SwapCharacters(string str, int n, int n2)
    {
        char[] charArray = str.ToCharArray();
        char temp = charArray[n];
        charArray[n] = charArray[n2];
        charArray[n2] = temp;

        return new string(charArray);
    }

    void UpdateInput()
    {
        string display = input;
        if (display.Length > 4)
        {
            display = display.Substring(0, 4) + " " + display.Substring(4);
        }

        TextMesh InputMesh = Input.GetComponent<TextMesh>() as TextMesh;
        InputMesh.text = display + ("---- ----").Substring(display.Length, 9 - display.Length);
    }

    void DebugMsg(object msg)
    {
        Debug.LogFormat("[The Gamepad #{0}] {1}", moduleID, msg);
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

    void Start()
    {
        moduleID = idCounter++;

        BombModule.OnActivate += ActivateModule;

        x = Random.Range(1, 99);
        y = Random.Range(1, 99);

        Digits1.GetComponent<TextMesh>().text = x.ToString("D2");
        Digits2.GetComponent<TextMesh>().text = y.ToString("D2");

        TextMesh InputMesh = Input.GetComponent<TextMesh>() as TextMesh;

        foreach (KMSelectable button in Buttons)
        {
            string text = button.gameObject.GetComponentInChildren<TextMesh>().text;

            button.OnInteract += delegate ()
            {
                ButtonPress(button);
                int length = input.Length;
                switch (text)
                {
                    case "←":
                        if (length > 0)
                        {
                            input = input.Substring(0, length - 1);
                            UpdateInput();
                        }
                        break;
                    case "↵":
                        if (!solved)
                        {
                            DebugMsg("Submitted: " + input);
                            if (input == solution)
                            {
                                //InputMesh.text = "GOOD JOB!";
                                DebugMsg("Module solved!");
                                InputMesh.text = correct[Random.Range(0, correct.Length)];
                                BombModule.HandlePass();
                                solved = true;

                                StartCoroutine(Wait(2, () =>
                                {
                                    UpdateInput();
                                    return true;
                                }));
                            }
                            else
                            {
                                //InputMesh.text = "INCORRECT";
                                DebugMsg("Inputted " + input + " but expected " + solution);
                                InputMesh.text = incorrect[Random.Range(0, incorrect.Length)];
                                BombModule.HandleStrike();

                                StartCoroutine(Wait(2, () =>
                                {
                                    UpdateInput();
                                    return true;
                                }));
                            }
                        }
                        break;
                    default:
                        if (length < 8)
                        {
                            input = input + text;
                            UpdateInput();
                        }
                        break;
                }

                return false;
            };
        }
    }

    void ActivateModule()
    {
        string serial = BombInfo.GetSerialNumber();

        int serialn = int.Parse(serial[serial.Length - 1].ToString());

        int a = x / 10;
        int b = a * -10 + x;
        int c = y / 10;
        int d = c * -10 + y;

        DebugMsg("Initial State: " + x.ToString("D2") + ":" + y.ToString("D2"));
        // Left Commands
        if (isPrime(x))
        {
            solution = "▲▲▼▼";
        }
        else if (isMultiple(x, 12))
        {
            solution = "▲A◀◀";
        }
        else if (a + b == 10 && isMultiple(serialn, 2) == false)
        {
            solution = "AB◀▶";
        }
        else if (x % 6 == 3 || x % 10 == 5)
        {
            solution = "▼◀A▶";
        }
        else if (isMultiple(x, 7) && !isMultiple(y, 7))
        {
            solution = "◀◀▲B";
        }
        else if (x == c * d)
        {
            solution = "A▲◀◀";
        }
        else if (isPerfectSquare(x))
        {
            solution = "▶▶A▼";
        }
        else if (x % 3 == 2 || BombInfo.IsIndicatorOff("SND"))
        {
            solution = "▶AB▲";
        }
        else if (60 <= x && x < 90 && BombInfo.GetBatteryCount() == 0)
        {
            solution = "BB▶◀";
        }
        else if (isMultiple(x, 6))
        {
            solution = "ABA▶";
        }
        else if (isMultiple(x, 4))
        {
            solution = "▼▼◀▲";
        }
        else
        {
            solution = "A◀B▶";
        }

        // Right Commands
        if (isPrime(y))
        {
            solution += "◀▶◀▶";
        }
        else if (isMultiple(y, 8))
        {
            solution += "▼▶B▲";
        }
        else if (c - d == 4 && BombInfo.IsPortPresent("StereoRCA"))
        {
            solution += "▶A▼▼";
        }
        else if (y % 4 == 2 || BombInfo.IsIndicatorOn("FRQ"))
        {
            solution += "B▲▶A";
        }
        else if (isMultiple(y, 7) && !isMultiple(x, 7))
        {
            solution += "◀◀▼A";
        }
        else if (isPerfectSquare(y))
        {
            solution += "▲▼B▶";
        }
        else if (y == a * b)
        {
            solution += "A▲◀▼";
        }
        else if (y % 4 == 3 || BombInfo.IsPortPresent("PS2"))
        {
            solution += "▲BBB";
        }
        else if (c > d && BombInfo.GetBatteryCount() >= 2)
        {
            solution += "AA▲▼";
        }
        else if (isMultiple(y, 5))
        {
            solution += "BAB◀";
        }
        else if (isMultiple(y, 3))
        {
            solution += "▶▲▲◀";
        }
        else
        {
            solution += "B▲A▼";
        }

        DebugMsg("Solution (Before Overrides): " + solution.Substring(0, 4) + " " + solution.Substring(4, 4));
        // Global Override
        if (isMultiple(x, 11))
        {
            DebugMsg(x + " is a multiple of 11. Swapping 1 <-> 2 & 5 <-> 7.");
            solution = SwapCharacters(SwapCharacters(solution, 4, 6), 0, 1);
        }

        if (a == 1 + d)
        {
            DebugMsg(a + " = 1 + " + d + ". Swapping 3 <-> 4 & 5 <-> 7.");
            solution = SwapCharacters(SwapCharacters(solution, 5, 7), 2, 3);
        }

        if (hcn.Contains(x) || hcn.Contains(y))
        {
            if (hcn.Contains(x) && hcn.Contains(y))
            {
                DebugMsg(x + " & " + y + " is a highly composite number.");
            }
            else if (hcn.Contains(x))
            {
                DebugMsg(x + " is a highly composite number.");
            }
            else
            {
                DebugMsg(y + " is a highly composite number.");
            }

            DebugMsg("Swapping the 1st and 2nd sequence.");

            solution = solution.Substring(4, 4) + solution.Substring(0, 4);
        }

        if (isPerfectSquare(x) && isPerfectSquare(y))
        {
            DebugMsg(x + " & " + y + " are perfect squares. Reversing entire sequence.");
            char[] charArray = solution.ToCharArray();
            Array.Reverse(charArray);
            solution = new string(charArray);
        }

        DebugMsg("Solution: " + solution.Substring(0, 4) + " " + solution.Substring(4, 4));
    }

    // TPK Support
    public KMSelectable[] ProcessTwitchCommand(string command)
    {
        string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 9 && split[0] == "submit")
        {
            List<KMSelectable> inputs = new List<KMSelectable>();
            List<string> positions = new List<string>()
            {
                "l", "r", "u", "d", "b", "a",
                "left", "right", "up", "down", "beta", "alpha"
            };

            if (input.Length > 0)
            {
                for (var i = 0; i < 8; i++)
                {
                    inputs.Add(Buttons[6]);
                }
            }

            foreach (string button in split.Skip(1))
            {
                int index = positions.IndexOf(button);
                if (index > -1)
                {
                    inputs.Add(Buttons[index % 6]);
                }
                else
                {
                    return null;
                }
            }
            inputs.Add(Buttons[7]); // Submit button

            return inputs.ToArray();
        }

        return null;
    }
}
