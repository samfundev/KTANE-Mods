using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;

public class MicrocontrollerLogging : ModuleLogging
{
	public MicrocontrollerLogging(BombComponent bombComponent) : base(bombComponent, "Micro", "Microcontroller")
	{
		Dictionary<string, string> pinColors = new Dictionary<string, string>();
		List<string> pins = new List<string>();

		string[] LEDMaterials = new string [] { "Black", "White", "Red", "Yellow", "Purple", "Blue", "Green" };

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			int[] colorMap = component.GetValue<int[]>("colorMap");
			int[] solutionRaw = component.GetValue<int[]>("solutionRaw");
			int colorRow = GetRow(colorMap);

			Log($"Controller Type: {component.GetValue<TextMesh>("MicType").text}");
			Log($"Controller Serial: {component.GetValue<TextMesh>("MicSerial").text.Substring(4)}");
			//Log($"Dot Pos: {GetDotPos(component.GetValue<int>("dotPos"))}");
			Log($"Using Row: {colorRow}");


			List<string> ledNames = new List<string>();

			for(int i = 0; i < solutionRaw.Length; i++)
			{ 
				string pinName = GetPinName(solutionRaw[i]);

				pins.Add(pinName);

				if (!pinColors.ContainsKey(pinName))
				{
					pinColors.Add(pinName, GetColor(colorRow, pinName));
				}

				Log($"{i + 1}. {pinName} = {pinColors[pinName]}");
			}
		};

		bombComponent.GetComponent<KMBombModule>().OnStrike += () =>
		{
			int materialID = component.GetValue<int>("materialID");

			int currentIndex = component.GetValue<int>("currentLEDIndex");

			Log($"Strike! Selected {LEDMaterials[materialID].ToLower()} on {pins[currentIndex]}");

			return false;
		};
	}

	private string GetPinName(int i)
	{
		switch (i)
		{
			case 0:
				return "GND";

			case 1:
				return "VCC";

			case 2:
				return "AIN";

			case 3:
				return "DIN";

			case 4:
				return "PWN";

			default:
				return "RST";
		}
	}

	private int GetRow(int[] colorMap)
	{
		if (colorMap.SequenceEqual(new int[] { 1, 3, 4, 6, 5, 2 }))
		{
			return 1;
		}

		if (colorMap.SequenceEqual(new int[] { 1, 3, 2, 4, 6, 5 }))
		{
			return 2;
		}

		if (colorMap.SequenceEqual(new int[] { 1, 2, 4, 6, 5, 3 }))
		{
			return 3;
		}

		if (colorMap.SequenceEqual(new int[] { 1, 2, 5, 3, 6, 4 }))
		{
			return 4;
		}

		return 5;
	}

	private string GetColor(int colorRow, string pinName)
	{
		if (colorRow == 1)
		{
			switch (pinName)
			{
				case "VCC":
					return "Yellow";

				case "AIN":
					return "Magenta";

				case "DIN":
					return "Green";

				case "PWN":
					return "Blue";

				case "RST":
					return "Red";

				default:
					return "White";

			}
		}

		if (colorRow == 2)
		{
			switch (pinName)
			{
				case "VCC":
					return "Yellow";

				case "AIN":
					return "Red";

				case "DIN":
					return "Magenta";

				case "PWN":
					return "Green";

				case "RST":
					return "Blue";

				default:
					return "White";

			}
		}

		if (colorRow == 3)
		{
			switch (pinName)
			{
				case "VCC":
					return "Red";

				case "AIN":
					return "Magenta";

				case "DIN":
					return "Green";

				case "PWN":
					return "Blue";

				case "RST":
					return "Yellow";

				default:
					return "White";

			}
		}

		if (colorRow == 4)
		{
			switch (pinName)
			{
				case "VCC":
					return "Red";

				case "AIN":
					return "Blue";

				case "DIN":
					return "Yellow";

				case "PWN":
					return "Green";

				case "RST":
					return "Magenta";

				default:
					return "White";

			}
		}

		else
		{
			switch (pinName)
			{
				case "VCC":
					return "Green";

				case "AIN":
					return "Red";

				case "DIN":
					return "Yellow";

				case "PWN":
					return "Blue";

				case "RST":
					return "Magenta";

				default:
					return "White";

			}
		}


	}
}