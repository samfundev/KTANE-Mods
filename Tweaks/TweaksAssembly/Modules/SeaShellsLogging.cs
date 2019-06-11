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
		mButtonsField = mButtonsField ?? (mButtonsField = SeaShellsModule.GetType().GetField("buttons", PublicInstance));
		mDisplayField = mDisplayField ?? (mDisplayField = SeaShellsModule.GetType().GetField("Display", PublicInstance));
		mStepField = mStepField ?? (mStepField = SeaShellsModule.GetType().GetField("step", NonPublicInstance));
		mStageField = mStageField ?? (mStageField = SeaShellsModule.GetType().GetField("stage", NonPublicInstance));

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
		{
			Solved = true;
			return true;
		};

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			var checker = bombComponent.StartCoroutine(RunLogger(
				fldLength: SeaShellsModule.GetType().GetField("length", NonPublicInstance),
				fldTable: SeaShellsModule.GetType().GetField("table", NonPublicInstance),
				fldKey: SeaShellsModule.GetType().GetField("key", NonPublicInstance),
				fldSwap: SeaShellsModule.GetType().GetField("swap", NonPublicInstance),
				fldKeynum: SeaShellsModule.GetType().GetField("keynum", NonPublicInstance),
				fldRow: SeaShellsModule.GetType().GetField("row", NonPublicInstance),
				fldCol: SeaShellsModule.GetType().GetField("col", NonPublicInstance)));

			var buttons = (KMSelectable[]) mButtonsField.GetValue(SeaShellsModule);
			var oldHandlers = buttons.Select(button => button.OnInteract).ToArray();
			for (int i = 0; i < buttons.Length; i++)
			{
				var j = i;
				buttons[i].OnInteract = delegate
				{
					var prevStep = (int) mStepField.GetValue(SeaShellsModule);
					var ret = oldHandlers[j]();
					if (Solved || (int) mStepField.GetValue(SeaShellsModule) > prevStep)
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

	private IEnumerator RunLogger(FieldInfo fldLength, FieldInfo fldTable, FieldInfo fldKey, FieldInfo fldSwap, FieldInfo fldKeynum, FieldInfo fldRow, FieldInfo fldCol)
	{
		var buttons = (KMSelectable[]) mButtonsField.GetValue(SeaShellsModule);
		while (true)
		{
			while (buttons.Any(b => b.GetComponentInChildren<TextMesh>().text == " "))
			{
				yield return null;
				if (Solved)
					yield break;
			}

			// this.OnPress(this.swap[j] == this.key[this.keynum, this.table[this.row, this.col, this.step]]);

			Log($"Stage: {(int) mStageField.GetValue(SeaShellsModule) + 1}");
			Log($"Phrase: {((TextMesh) mDisplayField.GetValue(SeaShellsModule)).text.Replace("\n", " ")}");
			Log($"Buttons: {buttons.Select(b => b.GetComponentInChildren<TextMesh>().text).Join(", ")}");

			var row = (int) fldRow.GetValue(SeaShellsModule);
			var col = (int) fldCol.GetValue(SeaShellsModule);
			var key = (int[,]) fldKey.GetValue(SeaShellsModule);
			var keynum = (int) fldKeynum.GetValue(SeaShellsModule);
			var table = (int[,,]) fldTable.GetValue(SeaShellsModule);
			var swap = (int[]) fldSwap.GetValue(SeaShellsModule);

			Log($@"Expected solution: {Enumerable.Range(0, ((int[,]) fldLength.GetValue(SeaShellsModule))[row, col])
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

	static FieldInfo mButtonsField;
	static FieldInfo mDisplayField;
	static FieldInfo mStepField;
	static FieldInfo mStageField;

	const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
	const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
}