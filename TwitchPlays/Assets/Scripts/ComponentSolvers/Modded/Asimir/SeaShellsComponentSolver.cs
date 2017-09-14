using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeaShellsComponentSolver : ComponentSolver
{
	public SeaShellsComponentSolver(BombCommander bombCommander, MonoBehaviour bombComponent, IRCConnection ircConnection, CoroutineCanceller canceller) :
		base(bombCommander, bombComponent, ircConnection, canceller)
	{
		_buttons = (KMSelectable[]) _buttonsField.GetValue(bombComponent.GetComponent(_componentType));
		helpMessage = "Press buttons by typing !{0} press alar llama. You can submit partial text as long it only matches one button. NOTE: Each button press is separated by a space so typing \"burglar alarm\" will press a button twice.";
	}

	protected override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		var commands = inputCommand.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (commands.Length >= 2 && commands[0].Equals("press"))
		{
			List<string> buttonLabels = _buttons.Select(button => button.GetComponentInChildren<TextMesh>().text.ToLowerInvariant()).ToList();

			if (!buttonLabels.Any(label => label == " "))
			{
				yield return null;

				IEnumerable<string> submittedText = commands.Where((_, i) => i > 0);
				List<string> fixedLabels = new List<string>();
				foreach (string text in submittedText)
				{
					IEnumerable<string> matchingLabels = buttonLabels.Where(label => label.Contains(text));

					int matchedCount = matchingLabels.Count();
					if (buttonLabels.Any(label => label.Equals(text)))
					{
						fixedLabels.Add(text);
					}
					else if (matchedCount == 1)
					{
						fixedLabels.Add(matchingLabels.First());
					}
					else if (matchedCount == 0)
					{
						yield return string.Format("sendtochat There isn't any label that contains \"{0}\".", text);
						yield break;
					}
					else
					{
						yield return string.Format("sendtochat There are multiple labels that contain \"{0}\": {1}.", text, string.Join(", ", matchingLabels.ToArray()));
						yield break;
					}
				}
				
				foreach (string fixedLabel in fixedLabels)
				{
					KMSelectable button = _buttons[buttonLabels.IndexOf(fixedLabel)];
					DoInteractionClick(button);

					yield return new WaitForSeconds(0.1f);
				}
			}
		}
	}

	static SeaShellsComponentSolver()
	{
		_componentType = ReflectionHelper.FindType("SeaShellsModule");
		_buttonsField = _componentType.GetField("buttons", BindingFlags.Public | BindingFlags.Instance);
	}

	private static Type _componentType = null;
	private static FieldInfo _buttonsField = null;

	private KMSelectable[] _buttons = null;
}
