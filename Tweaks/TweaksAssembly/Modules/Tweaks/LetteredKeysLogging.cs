using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class LetteredKeysLogging : ModuleLogging
{
    public LetteredKeysLogging(BombComponent bombComponent) : base(bombComponent, "LetterKeys", "Letter Keys")
    {
		bool moduleSolve = false;

		Bomb bomb = bombComponent.Bomb;

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
        {
			moduleSolve = false;

			List<Widget> widgetList = bomb.WidgetManager.GetWidgets();

			int number = int.Parse(component.GetValue<TextMesh>("textMesh").text);

			Log("Number: " + number);

			string answer = GetAnswer(number, GetBatteryCount(widgetList), GetSerialNumber(widgetList));

			KMSelectable[] buttons = component.GetValue<KMSelectable[]>("buttons");

			foreach (KMSelectable button in buttons)
			{
				OnWrongButtonPressed(button, answer);
			}

			Log("Answer is " + answer);
		};

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
        {
			if (!moduleSolve)
			{
				moduleSolve = true;
				Log($"Module Solved");
			}
			return false;
		};
	}

	private string GetAnswer(int number, int batteryCount, string serial)
	{
		if (number == 69)
		{
			return "D";
		}
		else if (number % 6 == 0)
		{
			Log("Number is divisible 6");
			return "A";
		}
		else if (number % 3 == 0 && batteryCount >= 2)
		{
			Log("Batteries ≥ 2 and number is divisible by three");
			return "B";
		}
		else if (serial.Contains("E") || serial.Contains("C") || serial.Contains("3"))
		{
			if (number >= 22 && number <= 79)
			{
				Log("Serial number contains a 'C' 'E' or '3' and 22 ≤ number ≤ 79");
				return "B";
			}
			else
			{
				Log("Serial number contains a 'C' 'E' or '3'");
				return "C";
			}
		}
		else if (number < 46)
		{
			Log("Number < 46");

			return "D";
		}
		else
		{
			Log("No condition applies");
			return "A";
		}
	}

	private int GetBatteryCount(List<Widget> widgetList)
	{
		int batteryCount = 0;
		foreach (Widget widget in widgetList)
		{
			if (widget is BatteryWidget)
			{
				batteryCount += ((BatteryWidget) widget).GetNumberOfBatteries();
			}
		}

		return batteryCount;
	}

	private string GetSerialNumber(List<Widget> widgetList)
	{
		Widget serialNumber = widgetList.First(w => w is SerialNumber);

		return ((SerialNumber) serialNumber).SerialTextMesh.text;
	}

	private void OnWrongButtonPressed(KMSelectable sel, string answer)
	{
		var prev = sel.OnInteract;
		sel.OnInteract = delegate
		{
			string text = sel.GetComponentInChildren<TextMesh>().text;

			if (answer != text)
			{ 
				Log($"Strike! You pressed {text}");
			}

			var ret = prev();

			return ret;
		};
	}
}