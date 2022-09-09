using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LetterKeysLogging : ModuleLogging
{
	public LetterKeysLogging(BombComponent bombComponent) : base(bombComponent, "LetterKeys", "Letter Keys")
	{
		bool moduleSolve = false;

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			int number = component.GetValue<int>("magicNum");
			Log("Number: " + number);

			List<Widget> widgetList = bombComponent.Bomb.WidgetManager.GetWidgets();
			int batteryCount = widgetList
				.Select(widget => widget is BatteryWidget battery ? battery.GetNumberOfBatteries() : 0)
				.Sum();
			var serialNumber = (SerialNumber) widgetList.First(w => w is SerialNumber);

			string answer = GetAnswer(number, batteryCount, serialNumber.GetSerialString());

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
				Log("Module Solved");
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