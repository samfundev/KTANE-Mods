using System.Collections.Generic;
using UnityEngine;

class LEDMathTweak : ModuleTweak
{
	private readonly string[] colorblindChars = { "R", "B", "G", "Y" };

	private readonly List<GameObject> colorblindText = new List<GameObject>();

	public LEDMathTweak(BombComponent bombComponent) : base(bombComponent, "LEDMathScript")
	{
		if (!ColorblindMode.IsActive("lgndLEDMath"))
			return;

		UpdateColorblind();

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
		void makeText(GameObject led, string letter)
		{
			var text = new GameObject("ColorblindText");
			text.transform.SetParent(led.transform, false);
			colorblindText.Add(text);

			var mesh = text.AddComponent<TextMesh>();
			mesh.transform.localPosition = new Vector3(0, 0.501f, 0);
			mesh.transform.localEulerAngles = new Vector3(90, 0, 0);
			mesh.characterSize = 0.08f;
			mesh.fontSize = 80;
			mesh.anchor = TextAnchor.MiddleCenter;
			mesh.GetComponent<MeshRenderer>().sharedMaterial.shader = Shader.Find("GUI/KT 3D Text");

			mesh.text = letter;
			mesh.color = Color.black;
		}

		var ledA = component.GetValue<MeshRenderer>("ledA").gameObject;
		var ledB = component.GetValue<MeshRenderer>("ledB").gameObject;
		var ledOp = component.GetValue<MeshRenderer>("ledOp").gameObject;
		makeText(ledA, colorblindChars[component.GetValue<int>("ledAIndex")]);
		makeText(ledB, colorblindChars[component.GetValue<int>("ledBIndex")]);
		makeText(ledOp, colorblindChars[component.GetValue<int>("ledOpIndex")]);
	}
}