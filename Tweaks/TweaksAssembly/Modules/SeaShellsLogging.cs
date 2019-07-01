using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class SeaShellsLogging : ModuleLogging
{
	const string typeName = "SeaShellsModule";

	private object SeaShellsModule;
	private bool Solved;

	public SeaShellsLogging(BombComponent bombComponent) : base(bombComponent, "Sea Shells")
	{
		SeaShellsModule = bombComponent.gameObject.GetComponent(typeName);
		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
		{
			Solved = true;
			return true;
		};

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			var checker = bombComponent.StartCoroutine(RunLogger());

			var buttons = SeaShellsModule.GetValue<KMSelectable[]>("buttons");
			var oldHandlers = buttons.Select(button => button.OnInteract).ToArray();
			for (int i = 0; i < buttons.Length; i++)
			{
				var j = i;
				buttons[i].OnInteract = delegate
				{
					var prevStep = SeaShellsModule.GetValue<int>("step");
					var ret = oldHandlers[j]();
					if (Solved || SeaShellsModule.GetValue<int>("step") > prevStep)
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
		var buttons = SeaShellsModule.GetValue<KMSelectable[]>("buttons");
		while (true)
		{
			while (buttons.Any(b => b.GetComponentInChildren<TextMesh>().text == " "))
			{
				yield return null;
				if (Solved)
					yield break;
			}

			Log($"Stage: {SeaShellsModule.GetValue<int>("stage") + 1}");
			Log($"Phrase: {SeaShellsModule.GetValue<TextMesh>("Display").text.Replace("\n", " ")}");
			Log($"Buttons: {buttons.Select(b => b.GetComponentInChildren<TextMesh>().text).Join(", ")}");

			var row = SeaShellsModule.GetValue<int>("row");
			var col = SeaShellsModule.GetValue<int>("col");
			var key = SeaShellsModule.GetValue<int[,]>("key");
			var keynum = SeaShellsModule.GetValue<int>("keynum");
			var table = SeaShellsModule.GetValue<int[,,]>("table");
			var swap = SeaShellsModule.GetValue<int[]>("swap");

			Log($@"Expected solution: {Enumerable.Range(0, SeaShellsModule.GetValue<int[,]>("length")[row, col])
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