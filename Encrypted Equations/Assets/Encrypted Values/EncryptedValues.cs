using KM_Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

class EncryptedValues : MonoBehaviour
{
	public static EncryptedValues Instance;

	static int idCounter = 1;
	int moduleID;

	bool isSolved = true;
	bool NotInteractable
	{
		get
		{
			return isSolved;
		}
	}

	public Texture[] ShapeTextures = null;

	public GameObject OperandScreen = null;
	public GameObject SubmissionScreen = null;

	// 0-9 - .
	public KMSelectable[] Numberpad = null;
	public KMSelectable ClearButton = null;
	public KMSelectable SubmitButton = null;

	public static Shape[] Shapes = {
		new Shape(0, "Triangle", 0),
		new Shape(1, "Square", 1),
		new Shape(2, "Horz. Rectangle", 2),
		new Shape(3, "X", 3),
		new Shape(4, "L. Rhombus", 4),
		new Shape(5, "Octagon", 5),
		new Shape(6, "Circle", 6),
		new Shape(7, "Trapazoid", 7),
		new Shape(8, "Pentagon", 8),
		new Shape(9, "Hexagon", 9),
		new Shape(10, "#", 10),
		new Shape(15, "+", 11),
		new Shape(20, "Oval", 12),
		new Shape(25, "R. Rhombus", 13),
		new Shape(30, "Upside-down Triangle", 14),
		new Shape(35, "Diamond", 15),
		new Shape(40, "Vert. Rectangle", 16),
		new Shape(45, "6 Pointed Star", 17),
		new Shape(50, "5 Pointed Star", 18),
		new Shape(100, "None"),
	};

	public class Shape
	{
		public int Value;
		public string Name;
		public int TextureIndex;

		public Shape(int Value, string Name, int TextureIndex = -1)
		{
			this.Value = Value;
			this.Name = Name;
			this.TextureIndex = TextureIndex;
		}

		public void Display(Renderer Shape)
		{
			if (TextureIndex >= 0) Shape.material.mainTexture = Instance.ShapeTextures[TextureIndex];
			Shape.enabled = TextureIndex >= 0;
		}

		public static Shape Generate()
		{
			return Shapes.PickRandom();
		}

		public string LogMessage
		{
			get
			{
				return Name + " (" + Value + ")";
			}
		}
	}

	public static Dictionary<char, Func<decimal, decimal>> Characters = new Dictionary<char, Func<decimal, decimal>>()
	{
		{ 'A', value => value + 1 },
		{ 'B', value => value + 3 },
		{ 'C', value => value - 2 },
		{ 'D', value => value - 4 },
		{ 'E', value => value * 2 },
		{ 'F', value => value / 2 },
		{ 'G', value => value + 2 },
		{ 'π', value => value * 1.5m },
		{ 'S', value => value / 1.5m },
		{ 'N', value => value - 6 },
		{ '#', value => value + 5 },
		{ 'H', value => value * 3 },
		{ 'O', value => value - 1 },
		{ '?', value => value * 10 },
		{ 'K', value => value / 5 },
		{ '%', value => value + 10 },
		{ 'R', value => value - 5 },
		{ '=', value => value + 4 },
		{ '/', value => value * 4 },
		{ '\\', value => value / 10 },
	};

	public class Operand
	{
		public char Character;
		public Shape Shape;

		public decimal Value
		{
			get
			{
				decimal value = Characters[Character](Shape.Value);

				return value.RoundThousandths();
			}
		}

		public void Display(GameObject Operand)
		{
			Operand.GetComponent<TextMesh>().text = Character.ToString();
			Shape.Display(Operand.Traverse<Renderer>("Shape"));

			Operand.Traverse<Transform>("Shape").localPosition = (Shape.Name == "+" && Character == 'E') ? new Vector3(0.05f, -0.05f, 0) : Vector3.zero;
		}

		public static Operand Generate()
		{
			return new Operand
			{
				Character = Characters.Keys.PickRandom(),
				Shape = Shape.Generate(),
			};
		}

		public string LogMessage
		{
			get
			{
				decimal CharacterValue = Characters[Character](Shape.Value).RoundThousandths();

				return new[]
				{
						"Shape: " + Shape.LogMessage,
						"Applying the character (" + Character + "): " + CharacterValue.ToThousandths(),
					}.Join("\n");
			}
		}
	}

	private string _customDisplayText;

	public string CustomDisplayText
	{
		set
		{
			_customDisplayText = value;
			UpdateDisplay();
		}
		get
		{
			return _customDisplayText;
		}
	}

	private string _userInput;

	public string UserInput
	{
		set
		{
			_userInput = value;
			UpdateDisplay();
		}
		get
		{
			return _userInput;
		}
	}

	void UpdateDisplay()
	{
		Submission.text = !string.IsNullOrEmpty(CustomDisplayText)
			? CustomDisplayText
			: (_userInput == null || _userInput.Length <= 4) ? _userInput : "..." + _userInput.Substring(_userInput.Length - 4);
	}

	void SetDisplayVisiblity(bool visible)
	{
		OperandGameObject.SetActive(visible);
		Submission.gameObject.SetActive(visible);
	}

	Operand CurrentOperand;
	KMNeedyModule NeedyModule;
	KMAudio Audio;

	GameObject OperandGameObject;
	TextMesh Submission;

	public void Start()
	{
		Instance = this;
		NeedyModule = GetComponent<KMNeedyModule>();
		Audio = GetComponent<KMAudio>();

		OperandGameObject = OperandScreen.Traverse("Operand");
		Submission = SubmissionScreen.Traverse<TextMesh>("Submission");
		moduleID = idCounter++;

		// Setup the buttons
		for (int i = 0; i < 12; i++)
		{
			KMSelectable selectable = Numberpad[i];
			selectable.AddInteract(() =>
			{
				if (NotInteractable) return false;

				Audio.PlaySoundAtTransform("ButtonPress", transform);
				UserInput += selectable.gameObject.Traverse<TextMesh>("ButtonText").text;

				return true;
			});
		}

		ClearButton.AddInteract(() =>
		{
			if (NotInteractable) return false;

			Audio.PlaySoundAtTransform("ButtonPress", transform);
			UserInput = null;
			return true;
		});

		SubmitButton.AddInteract(() =>
		{
			if (NotInteractable) return false;

			decimal result;
			if (decimal.TryParse(UserInput, out result) && result == CurrentOperand.Value)
			{
				Audio.PlaySoundAtTransform("Solve", transform);
			}
			else
			{
				Audio.PlaySoundAtTransform("Strike", transform);
				NeedyModule.HandleStrike();
			}
			isSolved = true;
			NeedyModule.HandlePass();
			SetDisplayVisiblity(false);

			return true;
		});

		NeedyModule.OnNeedyActivation = () =>
		{
			isSolved = false;
			UserInput = null;

			// Generate up to 1000 operands until we get one that doesn't throw an exception.
			for (int i = 0; i < 1000; i++)
			{
				try
				{
					CurrentOperand = Operand.Generate();
					decimal OperandValue = CurrentOperand.Value;
				}
				catch (OverflowException)
				{
					// An overflow probably can't happen with a single operand, but it doesn't hurt.
					CurrentOperand = null;
				}
				catch (Exception exception)
				{
					Log("Unexpected exception occured, solving module: ");
					Debug.LogException(exception);
					NeedyModule.HandlePass();
					return;
				}

				if (CurrentOperand != null)
					break;
			}

			// If we weren't able to generate a valid operand, force solve.
			if (CurrentOperand == null)
			{
				Log("Unable to generate a valid operand, solving module.");
				NeedyModule.HandlePass();
				return;
			}

			Log("Operand:\n" + CurrentOperand.LogMessage.PrefixLines(" - "));

			CurrentOperand.Display(OperandGameObject);
			SetDisplayVisiblity(true);
		};

		NeedyModule.OnNeedyDeactivation = () => SetDisplayVisiblity(false);
		NeedyModule.OnTimerExpired = () =>
		{
			NeedyModule.HandleStrike();
			SetDisplayVisiblity(false);
		};
	}

	void Log(params object[] values)
	{
		Debug.LogFormat("[Encrypted Values #{0}] {1}", moduleID, values.Select(Convert.ToString).Join());
	}

	public readonly string TwitchHelpMessage = "Submit the final value using !{0} submit 1234";

	public IEnumerable<KMSelectable> ProcessTwitchCommand(string command)
	{
		List<KMSelectable> buttons = new List<KMSelectable>();
		string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (split[0].EqualsAny("submit", "press", "enter", "answer", "s", "p", "e", "a"))
			split = split.Skip(1).ToArray();

		buttons.Add(ClearButton);

		foreach (string subcommand in split)
		{
			foreach (char character in subcommand)
			{
				KMSelectable button = Array.Find(Numberpad, selectable => selectable.gameObject.Traverse<TextMesh>("ButtonText").text.ToLowerInvariant()[0] == character);
				if (button == null)
					return null;

				buttons.Add(button);
			}
		}

		buttons.Add(SubmitButton);

		return buttons;
	}

	public IEnumerator TwitchHandleForcedSolve()
	{
		while (true)
		{
			if (isSolved)
			{
				yield return new WaitForSeconds(0.1f);
				continue;
			}

			foreach (KMSelectable selectable in ProcessTwitchCommand("submit " + CurrentOperand.Value.ToThousandths()))
			{
				selectable.OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
		}
	}
}