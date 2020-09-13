using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public class SeaShellsLogging : ModuleLogging
{
	private bool Solved;

	public SeaShellsLogging(BombComponent bombComponent) : base(bombComponent, "SeaShellsModule", "Sea Shells")
	{
		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
		{
			Solved = true;
			return true;
		};

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			var checker = bombComponent.StartCoroutine(RunLogger());

			var buttons = component.GetValue<KMSelectable[]>("buttons");
			var oldHandlers = buttons.Select(button => button.OnInteract).ToArray();
			for (int i = 0; i < buttons.Length; i++)
			{
				var j = i;
				buttons[i].OnInteract = delegate
				{
					var prevStep = component.GetValue<int>("step");
					var ret = oldHandlers[j]();
					if (Solved || component.GetValue<int>("step") > prevStep)
					{
						Log($"{buttons[j].GetComponentInChildren<TextMesh>().text}: correct");
						if (Solved)
						{
							Log("Module solved.");
							for (int k = 0; k < buttons.Length; k++)
								buttons[k].OnInteract = oldHandlers[k];
						}
					}
					else
						Log($"{buttons[j].GetComponentInChildren<TextMesh>().text}: WRONG!");

					return ret;
				};
			}
		};
	}

	private IEnumerator RunLogger()
	{
		var buttons = component.GetValue<KMSelectable[]>("buttons");
		while (true)
		{
			while (buttons.Any(b => b.GetComponentInChildren<TextMesh>().text == " "))
			{
				yield return null;
				if (Solved)
					yield break;
			}

			Log($"Stage: {component.GetValue<int>("stage") + 1}");
			Log($"Phrase: {component.GetValue<TextMesh>("Display").text.Replace("\n", " ")}");
			Log($"Buttons: {buttons.Select(b => b.GetComponentInChildren<TextMesh>().text).Join(", ")}");

			var row = component.GetValue<int>("row");
			var col = component.GetValue<int>("col");
			var key = component.GetValue<int[,]>("key");
			var keynum = component.GetValue<int>("keynum");
			var table = component.GetValue<int[,,]>("table");
			var swap = component.GetValue<int[]>("swap");

			Log($@"Expected solution: {Enumerable.Range(0, component.GetValue<int[,]>("length")[row, col])
				.Select(i => buttons[Array.IndexOf(swap, key[keynum, table[row, col, i]])].GetComponentInChildren<TextMesh>().text)
				.Join(", ")}");

			while (buttons.All(b => b.GetComponentInChildren<TextMesh>().text != " "))
			{
				yield return null;
				if (Solved)
					yield break;
			}
		}
	}
}