using System.Collections.Generic;
using UnityEngine;

class ConditionalButtonsTweak : ModuleTweak
{
	private readonly Dictionary<string, string> colorblindChars = new Dictionary<string, string>()
	{
		{ "pink", "I" },
		{ "orange", "O" },
		{ "purple", "P" },
		{ "white", "W" },
		{ "blue", "B" },
		{ "yellow", "Y" },
		{ "light green", "LG" },
		{ "red", "R" },
		{ "black", "K" },
		{ "dark green", "DG" }
	};

	private readonly List<GameObject> colorblindText = new List<GameObject>();

	public ConditionalButtonsTweak(BombComponent bombComponent) : base(bombComponent, "conditionalButtons")
	{
		if (!ColorblindMode.IsActive("conditionalButtons"))
			return;

		UpdateColorblind();

		foreach (var selectable in bombComponent.GetComponent<KMSelectable>().Children)
		{
			var previous = selectable.OnInteract;
			selectable.OnInteract = () => {
				previous();
				foreach (var text in colorblindText)
					text.SetActive(false);
				return false;
			};
		}
	}

	private void UpdateColorblind()
	{
		void makeText(GameObject btn, string letter)
		{
			var text = new GameObject("ColorblindText");
			text.transform.SetParent(btn.transform, false);
			colorblindText.Add(text);

			var mesh = text.AddComponent<TextMesh>();
			mesh.transform.localPosition = new Vector3(0, 0, 0.0051f);
			mesh.transform.localEulerAngles = new Vector3(0, 180, 180);
			mesh.characterSize = letter.Length == 1 ? 0.0015f : 0.0013f;
			mesh.fontSize = 80;
			mesh.anchor = TextAnchor.MiddleCenter;
			mesh.GetComponent<MeshRenderer>().sharedMaterial.shader = Shader.Find("GUI/KT 3D Text");

			mesh.text = letter;
			mesh.color = (letter == "K" || letter == "DG") ? Color.white : Color.black;
		}

		var buttons = bombComponent.GetComponent<KMSelectable>().Children;
		for (int i = 0; i < buttons.Length; i++)
			makeText(buttons[i].gameObject, colorblindChars[buttons[i].GetComponent<MeshRenderer>().material.name.Replace(" (Instance)", "")]);
	}
}