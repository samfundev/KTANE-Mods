using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;

public class MicrocontrollerLogging : ModuleLogging
{
	public MicrocontrollerLogging(BombComponent bombComponent) : base(bombComponent, "Micro", "Microcontroller")
	{
		int[] positionTranslate = component.GetValue<int[]>("positionTranslate");

		Dictionary<string, string> pinColors = new Dictionary<string, string>();
		List<string> pins = new List<string>();

		string[] LEDMaterials = new string[] { "Black", "White", "Red", "Yellow", "Purple", "Blue", "Green" };

		List<int> LEDorder = component.GetValue<List<int>>("LEDorder");

		string dotPos = GetDotPos(positionTranslate);

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			Log("Dot pos: " + dotPos);

			int[] colorMap = component.GetValue<int[]>("colorMap");
			int[] solutionRaw = component.GetValue<int[]>("solutionRaw");
			int colorRow = GetRow(colorMap);

			Log($"Controller Type: {component.GetValue<TextMesh>("MicType").text}");
			Log($"Controller Serial: {component.GetValue<TextMesh>("MicSerial").text.Substring(4)}");
			Log($"Using Row: {colorRow}");


			List<string> ledNames = new List<string>();

			for (int i = 0; i < solutionRaw.Length; i++)
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

	private int[] ConvertLEDOrder(List<int> order, string dotpos)
	{
		switch (dotpos)
		{
			case "TL":
				return order.Count == 6 ? new int[] { 1, 2, 3, 4, 5, 6 } : order.Count == 8 ? new int[] { 1, 2, 3, 4, 5, 6, 7, 8 } : new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

			case "TR":
				return order.Count == 6 ? new int[] { 3, 2, 1, 4, 5, 6 } : order.Count == 8 ? new int[] { 4, 3, 2, 1, 5, 6, 7, 8 } : new int[] { 5, 4, 3, 2, 1, 6, 7, 8, 9, 10 };

			case "BL":
				return order.Count == 6 ? new int[] { 4, 5, 6, 3, 2, 1 } : order.Count == 8 ? new int[] { 5, 6, 7, 8, 4, 3, 2, 1 } : new int[] { 6, 7, 8, 9, 10, 5, 4, 3, 2, 1, };

		}

		return order.Count == 6 ? (new int[] { 6, 5, 4, 1, 2, 3 }) : order.Count == 8 ? (new int[] { 8, 7, 6, 5, 1, 2, 3, 4 }) : (new int[] { 10, 9, 8, 7, 6, 1, 2, 3, 4, 5 });
	}

	private string GetDotPos(int[] positionTranslate)
	{
		if (positionTranslate.SequenceEqual(new int[6] { 0, 5, 1, 4, 2, 3 }) ||
		   positionTranslate.SequenceEqual(new int[8] { 0, 7, 1, 6, 2, 5, 3, 4 }) ||
		   positionTranslate.SequenceEqual(new int[10] { 0, 9, 1, 8, 2, 7, 3, 6, 4, 5 }))
		{
			return "TL";
		}

		if (positionTranslate.SequenceEqual(new int[6] { 2, 3, 1, 4, 0, 5 }) ||
		   positionTranslate.SequenceEqual(new int[8] { 3, 4, 2, 5, 1, 6, 0, 7 }) ||
		   positionTranslate.SequenceEqual(new int[10] { 4, 5, 3, 6, 2, 7, 1, 8, 0, 9 }))
		{
			return "TR";
		}


		if (positionTranslate.SequenceEqual(new int[6] { 5, 0, 4, 1, 3, 2 }) ||
			positionTranslate.SequenceEqual(new int[8] { 7, 0, 6, 1, 5, 2, 4, 3 }) ||
			positionTranslate.SequenceEqual(new int[10] { 9, 0, 8, 1, 7, 2, 6, 3, 5, 4 }))
		{
			return "BL";
		}


		return "BR";
	}
}
