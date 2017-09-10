using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEngine;

public class ThreeDMazeComponentSolver : ComponentSolver
{
	public ThreeDMazeComponentSolver(BombCommander bombCommander, MonoBehaviour bombComponent, IRCConnection ircConnection, CoroutineCanceller canceller) :
		base(bombCommander, bombComponent, ircConnection, canceller)
	{
		object _component = bombComponent.GetComponent(_componentType);
		_buttonLeft = (KMSelectable) _buttonLeftField.GetValue(_component);
		_buttonRight = (KMSelectable) _buttonRightField.GetValue(_component);
		_buttonStraight = (KMSelectable) _buttonStraightField.GetValue(_component);

		helpMessage = "Move around the maze using !{0} move left forward right. Walk slowly around the maze using !{0} walk left forawrd right. Shorten forms of the directions are also acceptable.";
	}

	private string ShortenDirection(string direction)
	{
		switch (direction)
		{
			case "left":
				return "l";
			case "right":
				return "r";
			case "forward":
				return "f";
			default:
				return direction;
		}
	}
	
	protected override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		var commands = inputCommand.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (commands.Length > 1 && (commands[0].Equals("move") || commands[0].Equals("walk")))
		{
			var moves = commands.Where((_, i) => i > 0).Select(dir => ShortenDirection(dir));
			
			if (moves.All(n => n == "l" || n == "r" || n == "f"))
			{
				float moveDelay = commands[0].Equals("move") ? 0.1f : 0.4f;
				foreach (string move in moves)
				{
					KMSelectable button = null;
					switch (move)
					{
						case "l":
							button = _buttonLeft;
							break;
						case "r":
							button = _buttonRight;
							break;
						case "f":
							button = _buttonStraight;
							break;
					}

					DoInteractionStart(button);
					DoInteractionEnd(button);
					yield return new WaitForSeconds(moveDelay);
				}
			}
		}

		yield return null;
	}

	static ThreeDMazeComponentSolver()
	{
		_componentType = ReflectionHelper.FindType("ThreeDMazeModule");
		_buttonLeftField = _componentType.GetField("ButtonLeft", BindingFlags.Public | BindingFlags.Instance);
		_buttonRightField = _componentType.GetField("ButtonRight", BindingFlags.Public | BindingFlags.Instance);
		_buttonStraightField = _componentType.GetField("ButtonStraight", BindingFlags.Public | BindingFlags.Instance);
	}

	private static Type _componentType = null;
	private static FieldInfo _buttonLeftField = null;
	private static FieldInfo _buttonRightField = null;
	private static FieldInfo _buttonStraightField = null;

	private KMSelectable _buttonLeft = null;
	private KMSelectable _buttonRight = null;
	private KMSelectable _buttonStraight = null;
}
