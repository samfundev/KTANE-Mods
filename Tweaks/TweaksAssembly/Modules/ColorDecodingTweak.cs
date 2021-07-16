using System.Collections.Generic;
using UnityEngine;

class ColorDecodingTweak : ModuleTweak
{
	private readonly Dictionary<Color32, string> colorToLetter = new Dictionary<Color32, string>
	{
		{ new Color32(0xFF, 0x00, 0x00, 0xFF), "R" },
		{ new Color32(0x00, 0x57, 0x00, 0xFF), "G" },
		{ new Color32(0x0F, 0x00, 0xA1, 0xFF), "B" },
		{ new Color32(0xDA, 0xEA, 0x00, 0xFF), "Y" },
		{ new Color32(0x7C, 0x11, 0x9A, 0xFF), "P" }
	};

	private readonly List<GameObject> colorblindText = new List<GameObject>();

	public ColorDecodingTweak(BombComponent bombComponent) : base(bombComponent, "ColorDecoding")
	{
		if (!ColorblindMode.IsActive("Color Decoding"))
			return;

		UpdateColorblind();

		foreach (var selectable in bombComponent.GetComponent<KMSelectable>().Children)
		{
			var previous = selectable.OnInteract;
			selectable.OnInteract = () => {
				previous();
				UpdateColorblind();
				return false;
			};
		}

		bombComponent.OnPass += (_) => {
			foreach (var text in colorblindText)
			{
				text.SetActive(false);
			}

			return false;
		};
	}

	private void UpdateColorblind()
	{
		void makeText(GameObject cell, string letter, bool display)
		{
			var text = cell.transform.Find("ColorblindText")?.gameObject;
			if (text == null)
			{
				text = new GameObject("ColorblindText");
				text.transform.SetParent(cell.transform, false);
				colorblindText.Add(text);
			}

			var mesh = text?.GetComponent<TextMesh>();
			if (mesh == null)
			{
				mesh = text.AddComponent<TextMesh>();
				mesh.transform.localPosition = Vector3.up * (display ? 1.05f : 0.95f);
				mesh.transform.localEulerAngles = new Vector3(90, 0, 0);
				mesh.characterSize = 0.225f;
				mesh.fontSize = 40;
				mesh.anchor = TextAnchor.MiddleCenter;
				mesh.GetComponent<MeshRenderer>().sharedMaterial.shader = Shader.Find("GUI/KT 3D Text");
			}

			mesh.text = letter;
			mesh.color = letter == "Y" ? Color.black : Color.white;
		}

		var IndicatorGrid = component.GetValue<GameObject[]>("IndicatorGrid");
		var DisplayGrid = component.GetValue<GameObject[]>("DisplayGrid");
		for (int row = 0; row < 4; row++) {
			for (int col = 0; col < 4; col++) {
				var cell = IndicatorGrid[row * 4 + col];
				var letter = colorToLetter[cell.GetComponent<MeshRenderer>().material.color];

				makeText(cell, letter, false);
			}
		}
		for (int row = 0; row < 6; row++) {
			for (int col = 0; col < 6; col++) {
				var cell = DisplayGrid[row * 6 + col];
				var letter = colorToLetter[cell.GetComponent<MeshRenderer>().material.color];

				makeText(cell, letter, true);
			}
		}
	}
}