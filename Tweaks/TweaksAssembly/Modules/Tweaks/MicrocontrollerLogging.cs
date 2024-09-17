using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MicrocontrollerLogging : ModuleLogging
{
	public MicrocontrollerLogging(BombComponent bombComponent) : base(bombComponent, "Micro", "Microcontroller")
	{
		string[] LEDMaterials = new string[] { "Black", "White", "Red", "Yellow", "Magenta", "Blue", "Green" };

		List<int> LEDorder = component.GetValue<List<int>>("LEDorder");
		var positionTranslate = component.GetValue<int[]>("positionTranslate");

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


			var pinNames = new[] { "GND", "VCC", "AIN", "DIN", "PWM", "RST" };
			for (int i = 0; i < solutionRaw.Length; i++)
			{
				string pinName = pinNames[solutionRaw[i]];
				string pinColor = LEDMaterials[colorMap[solutionRaw[i]]];

				Log($"{i + 1}. {pinName} = {pinColor}");
			}

			var buttonOK = component.GetValue<KMSelectable>("buttonOK");
			var baseInteract = buttonOK.OnInteract;
			buttonOK.OnInteract = () =>
			{
				var currentLEDIndex = component.GetValue<int>("currentLEDIndex");
				var materialID = component.GetValue<int>("materialID");

				int pinNumber = positionTranslate[LEDorder[currentLEDIndex]];
				string pinColor = LEDMaterials[materialID].ToLower();
				string expected = LEDMaterials[colorMap[solutionRaw[pinNumber]]].ToLower();
				Log($"Set pin {pinNumber + 1} to {pinColor}{(pinColor == expected ? "" : $" (expected {expected})")}");

				return baseInteract();
			};
		};
	}

	private int GetRow(int[] colorMap)
	{
		if (colorMap.SequenceEqual(new int[] { 1, 3, 4, 6, 5, 2 }))
		{
			return 1;
		}
		else if (colorMap.SequenceEqual(new int[] { 1, 3, 2, 4, 6, 5 }))
		{
			return 2;
		}
		else if (colorMap.SequenceEqual(new int[] { 1, 2, 4, 6, 5, 3 }))
		{
			return 3;
		}
		else if (colorMap.SequenceEqual(new int[] { 1, 2, 5, 3, 6, 4 }))
		{
			return 4;
		}

		return 5;
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