using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class LetteredKeysLogging : ModuleLogging
{
    public LetteredKeysLogging(BombComponent bombComponent) : base(bombComponent, "LetterKeys", "Letter Keys")
    {
		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
        {
			Log($"Lettered Keys logging has been initialized");

			int number = int.Parse(component.GetValue<TextMesh>("textMesh").text);

			Log("Number: " + number);

			/*
			Bomb bomb = bombComponent.transform.parent.GetComponent<Bomb>();

			List<Widget> widgetList = bomb.WidgetManager.GetWidgets();

			

			KMSelectable[] buttons = component.GetValue<KMSelectable[]>("buttons");

			foreach (KMSelectable button in buttons)
			{
				TextMesh texthMesh = button.GetComponentInChildren<TextMesh>();
				Log(texthMesh.text + " ");
			}
			

			Log($"# of batteries: {GetBatteryCount(widgetList)}");
			*/
		};

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
        {
			Log($"You solved the Lettered Keys module");
			return false;
		};

		bombComponent.GetComponent<KMBombModule>().OnStrike += () =>
        {
			Log($"You struck on the Lettered Keys module");
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
			return "A";
		}
		else if (number % 3 == 0 && batteryCount >= 2)
		{
			return "B";
		}
		else if (serial.Contains("E") || serial.Contains("C") || serial.Contains("3"))
		{
			if (number >= 22 && number <= 79)
			{
				return "B";
			}
			else
			{
				return "C";
			}
		}
		else if (number < 46)
		{
			return "D";
		}
		else
		{
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
		int batteryCount = 0;
		foreach (Widget widget in widgetList)
		{
			if (widget is BatteryWidget)
			{
				batteryCount += ((BatteryWidget) widget).GetNumberOfBatteries();
			}
		}

		return "";
	}



}