using System.Linq;
using UnityEngine;

public class LetteredKeysLogging : ModuleLogging
{
    public LetteredKeysLogging(BombComponent bombComponent) : base(bombComponent, "LetterKeys", "Letter Keys")
    {

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
        {
			Log($"Lettered Keys logging has been initialized");

			TextMesh number = component.GetValue<TextMesh>("textMesh");

			Log("Number: " + number.text);

			KMSelectable[] buttons = component.GetValue<KMSelectable[]>("buttons");

			foreach (KMSelectable button in buttons)
			{
				TextMesh texthMesh = button.GetComponentInChildren<TextMesh>();
				Log(texthMesh.text + " ");
			}

        };

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
        {
			Log($"You solved the lettered keys module");
			return false;
		};

		bombComponent.GetComponent<KMBombModule>().OnStrike += () =>
        {
			Log($"You struck on the lettered keys module");
			return false;
        };
	}


}