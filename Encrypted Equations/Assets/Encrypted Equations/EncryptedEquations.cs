using KM_Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

class EncryptedEquations : MonoBehaviour
{
	public static EncryptedEquations Instance;

	static int idCounter = 1;
	int moduleID;

	bool isSolved = false;
	bool isAnimating = false;
	bool NotInteractable
	{
		get
		{
			return isSolved || isAnimating;
		}
	}

	public Texture[] ShapeTextures = null;
	public Texture[] SymbolTextures = null;
	public Texture[] OperationTextures = null;

	// The name of these textures indicates two things:
	// 1. Whether or not it applies to the "Current" side or the "Oppsite".
	// 2. The number indicates if it's on the left or right. Odds are on the right.
	public Texture[] ParenthesesTextures = null;

	public GameObject Screen = null;

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

	public static Func<decimal, decimal>[][] Symbols = {
		new Func<decimal, decimal>[] { (value) => value + 1, (value) => value - 2, (value) => value * 3, (value) => value + 3, },
		new Func<decimal, decimal>[] { (value) => value * 1.5m, (value) => value / 5, (value) => value - 1, (value) => value / 1.5m, },
		new Func<decimal, decimal>[] { (value) => value - 4, (value) => value + 2, (value) => value * 2, (value) => value * 5, },
		new Func<decimal, decimal>[] { (value) => value + 4, (value) => value / 10, (value) => value / 2, (value) => value - 3, },
	};

	public class SymbolData
	{
		public int Rotation;
		public char Character;
		public int CharacterIndex;
		public Func<decimal, decimal> Operation = value => value;
		public bool Exists;

		public void Display(GameObject Pivot)
		{
			Pivot.SetActive(Exists);
			if (!Exists) return;

			Pivot.transform.localEulerAngles = new Vector3(0, 0, Rotation * -90f);
			Pivot.Traverse<Renderer>("Symbol").material.mainTexture = Instance.SymbolTextures[CharacterIndex];
		}

		public static SymbolData Generate()
		{
			if (Random.value > 0.6f) return new SymbolData { Exists = false };

			int Rotation = Random.Range(0, 4);
			int CharacterIndex = Random.Range(0, 4);

			return new SymbolData
			{
				Rotation = Rotation,
				CharacterIndex = CharacterIndex,
				Character = "•-|◦"[CharacterIndex],
				Operation = Symbols[CharacterIndex][Rotation],
				Exists = true
			};
		}

		public string LogMessage
		{
			get
			{
				return Character + " facing " + "NESW"[Rotation];
			}
		}
	}

	public enum CornerOperation
	{
		None,
		Invert,
		AbsoluteValue,
		Square,
		Cube
	}

	public static Dictionary<CornerOperation, Func<decimal, decimal>> CornerOperations = new Dictionary<CornerOperation, Func<decimal, decimal>>
	{
		{ CornerOperation.Invert, value => -value },
		{ CornerOperation.AbsoluteValue, value => Math.Abs(value) },
		{ CornerOperation.Square, value => (decimal) Math.Pow((double) value, 2) },
		{ CornerOperation.Cube, value => (decimal) Math.Pow((double) value, 3) },
	};

	public class Equation
	{
		public class Operand
		{
			public CornerOperation Operation;
			public char Character;
			public Shape Shape;
			public SymbolData Symbol;

			public decimal Value
			{
				get
				{
					decimal value = Characters[Character](Shape.Value);
					if (Symbol.Exists) value = Symbol.Operation(value).RoundThousandths();
					if (Operation != CornerOperation.None) value = CornerOperations[Operation](value).RoundThousandths();

					return value.RoundThousandths();
				}
			}

			public void Display(GameObject Operand)
			{
				Operand.Traverse<TextMesh>("CornerOperation").text = Operation == CornerOperation.None ? "" : Operation.ToString()[0].ToString();
				Operand.GetComponent<TextMesh>().text = Character.ToString();
				Shape.Display(Operand.Traverse<Renderer>("Shape"));
				Symbol.Display(Operand.Traverse("SymbolPivot"));

				Operand.Traverse<Transform>("Shape").localPosition = (Shape.Name == "+" && Character == 'E') ? new Vector3(0.05f, -0.05f, 0) : Vector3.zero;
			}

			public static Operand Generate()
			{
				return new Operand
				{
					Character = Characters.Keys.PickRandom(),
					Shape = Shape.Generate(),
					Symbol = SymbolData.Generate(),
					Operation = Random.value > 0.25f ? CornerOperation.None : new[] { CornerOperation.Invert, CornerOperation.AbsoluteValue, CornerOperation.Square, CornerOperation.Cube }.PickRandom()
				};
			}

			public string LogMessage
			{
				get
				{
					decimal CharacterValue = Characters[Character](Shape.Value).RoundThousandths();
					decimal SymbolValue = Symbol.Operation(CharacterValue).RoundThousandths();

					return new[]
					{
						"Shape: " + Shape.LogMessage,
						"Applying the character (" + Character + "): " + CharacterValue.ToThousandths(),
						Symbol.Exists ? ("Applying the symbol (" + Symbol.LogMessage + "): " + SymbolValue.ToThousandths()) : "No symbol",
						Operation != CornerOperation.None ? ("Applying an " + Operation.ToString()[0] + ": " + CornerOperations[Operation](SymbolValue).RoundThousandths().ToThousandths()) : "No corner character"
					}.Join("\n");
				}
			}
		}

		public class Operator
		{
			public Texture Texture;
			public string Type;

			public decimal Evaluate(decimal left, decimal right)
			{
				switch (Type)
				{
					case "add":
						return left + right;
					case "subtract":
						return left - right;
					case "multiply":
						return left * right;
					case "divide":
						return left / right;
					default:
						throw new Exception("This should never happen.");
				}
			}

			public void Display(GameObject Operation)
			{
				Operation.GetComponent<Renderer>().material.mainTexture = Texture;
			}

			public static Operator Generate()
			{
				Texture texture = Instance.OperationTextures.PickRandom();

				return new Operator
				{
					Texture = texture,
					Type = texture.name.Remove(texture.name.Length - 1)
				};
			}

			public string LogMessage
			{
				get
				{
					return Type.Substring(0, 1).ToUpperInvariant() + Type.Substring(1);
				}
			}
		}

		public Operand LeftOperand;
		public Operator LeftOperator;
		public Operand MiddleOperand;
		public Operator RightOperator;
		public Operand RightOperand;

		public Texture ParenthesesTexture;

		public bool RightSideParentheses
		{
			get
			{
				string parenthesesName = ParenthesesTexture.name;
				// There are two digits in parenthese in the texture name which if the number is odd it means the parentheses appear on the right.
				bool rightSideParentheses = int.Parse(parenthesesName.Substring(parenthesesName.Length - 3, 2)) % 2 == 1;

				// The texture name can start with Current or Oppsite, if it's Opposite we have to switch.
				if (parenthesesName.StartsWith("Oppsite")) rightSideParentheses = !rightSideParentheses;

				return rightSideParentheses;
			}
		}

		public bool ValueUndefined;
		public decimal Value
		{
			get
			{
				decimal value = RightSideParentheses
					? LeftOperator.Evaluate(LeftOperand.Value, RightOperator.Evaluate(MiddleOperand.Value, RightOperand.Value).RoundThousandths())
					: RightOperator.Evaluate(LeftOperator.Evaluate(LeftOperand.Value, MiddleOperand.Value).RoundThousandths(), RightOperand.Value);
				return value.RoundThousandths();
			}
		}

		public void Display(GameObject Equation)
		{
			LeftOperand.Display(Equation.Traverse("Left"));
			LeftOperator.Display(Equation.Traverse("LeftOperation"));
			MiddleOperand.Display(Equation.Traverse("Middle"));
			RightOperator.Display(Equation.Traverse("RightOperation"));
			RightOperand.Display(Equation.Traverse("Right"));
			Equation.Traverse<Renderer>("Parentheses").material.mainTexture = ParenthesesTexture;
		}

		public static Equation Generate()
		{
			return new Equation
			{
				LeftOperand = Operand.Generate(),
				LeftOperator = Operator.Generate(),
				MiddleOperand = Operand.Generate(),
				RightOperator = Operator.Generate(),
				RightOperand = Operand.Generate(),
				ParenthesesTexture = Instance.ParenthesesTextures.PickRandom()
			};
		}

		public string LogMessage
		{
			get
			{
				return new[]
				{
					"Left Operand:",
					LeftOperand.LogMessage.PrefixLines(" - "),

					"Left Operator: " + LeftOperator.LogMessage,

					"Middle Operand:",
					MiddleOperand.LogMessage.PrefixLines(" - "),

					"Right Operator: " + RightOperator.LogMessage,

					"Right Operand:",
					RightOperand.LogMessage.PrefixLines(" - "),

					"Parentheses: " + (RightSideParentheses ? "Right" : "Left") + " Side",

					"Final Value: " + (ValueUndefined ? "undefined" : Value.ToThousandths())
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
		Screen.Traverse("Equation").SetActive(string.IsNullOrEmpty(CustomDisplayText ?? _userInput));

		Submission.text = !string.IsNullOrEmpty(CustomDisplayText)
			? CustomDisplayText
			: (_userInput == null || _userInput.Length <= 10) ? _userInput : "..." + _userInput.Substring(_userInput.Length - 10);
	}

	Equation CurrentEquation;
	KMBombModule BombModule;
	KMAudio Audio;

	TextMesh Submission;
	Material SubmissionMaterial;

	public void Start()
	{
		Instance = this;
		BombModule = GetComponent<KMBombModule>();
		Audio = GetComponent<KMAudio>();
		Submission = Screen.Traverse<TextMesh>("Submission");
		SubmissionMaterial = Submission.GetComponent<Renderer>().material;
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

			if (string.IsNullOrEmpty(UserInput))
			{
				CustomDisplayText = "UNDEFINED";
			}

			Audio.PlaySoundAtTransform("ButtonPress", transform);
			decimal result;
			if ((CurrentEquation.ValueUndefined && string.IsNullOrEmpty(UserInput)) || (!CurrentEquation.ValueUndefined && decimal.TryParse(UserInput, out result) && result == CurrentEquation.Value))
			{
				isSolved = true;
				StartCoroutine(SolveAnimation());
			}
			else
			{
				StartCoroutine(StrikeAnimation());
			}

			return true;
		});

		// Generate up to 1000 equations until we get one that doesn't throw an exception.
		for (int i = 0; i < 1000; i++)
		{
			try
			{
				CurrentEquation = Equation.Generate();
				decimal EquationValue = CurrentEquation.Value;
			}
			catch (DivideByZeroException)
			{
				// Catch any division by zero, to mark the equation as having an undefined value.
				CurrentEquation.ValueUndefined = true;
			}
			catch (OverflowException)
			{
				// The numbers on this module can get really big, too big for the decimal type:
				// "The highest possible value for an operand is 125,000,000,000 (125 trillion), which is a "?" with no shape, a "|" facing west, and a "C" exponent.
				// If all three operands are this combination, and they're all connected by multiplication only, the total adds up to...
				// 1,953,125,000,000,000,000,000,000,000,000,000 / 1.953125 x 10 ^ 33 / 1.953125 decillion" - Lumbud84
				CurrentEquation = null;
			}
			catch (Exception exception)
			{
				Log("Unexpected exception occured, solving module: ");
				Debug.LogException(exception);
				BombModule.HandlePass();
			}

			if (CurrentEquation != null)
				break;
		}

		// If we weren't able to generate a valid equation, force solve.
		if (CurrentEquation == null)
		{
			Log("Unable to generate a valid equation, solving module.");
			BombModule.HandlePass();
		}

		Log("Equation:\n" + CurrentEquation.LogMessage);

		CurrentEquation.Display(Screen.Traverse("Equation"));
	}

	IEnumerable TimedAnimation(float length)
	{
		float startTime = Time.time;
		float alpha = 0;
		while (alpha < 1)
		{
			alpha = Mathf.Min((Time.time - startTime) / length, 1);
			yield return alpha;
		}
	}

	IEnumerator SubmitAnimation()
	{
		StringBuilder stringBuilder = new StringBuilder(Submission.text);
		float y = 0;
		float changeCharacter = 0;

		KMAudio.KMAudioRef audioRef = Audio.PlaySoundAtTransformWithRef("Submit", transform);
		foreach (float alpha in TimedAnimation(4))
		{
			float curvedAlpha = Mathf.Pow(alpha, 4) / 5;
			changeCharacter += alpha;

			while (changeCharacter >= 0.5f)
			{
				changeCharacter -= 0.5f;
				stringBuilder[Random.Range(0, stringBuilder.Length)] = (char) Random.Range(32, 126);
				CustomDisplayText = stringBuilder.ToString();
			}

			SubmissionMaterial.mainTextureOffset = new Vector2(0, y += curvedAlpha);
			yield return null;
		}

		audioRef.StopSound();
	}

	IEnumerator SolveAnimation()
	{
		isAnimating = true;

		yield return SubmitAnimation();

		CustomDisplayText = "CORRECT";
		SubmissionMaterial.mainTextureOffset = Vector2.zero;
		Audio.PlaySoundAtTransform("Solve", transform);

		BombModule.HandlePass();

		yield return new WaitForSeconds(3);
		CustomDisplayText = null;

		isAnimating = false;
	}

	IEnumerator StrikeAnimation()
	{
		isAnimating = true;

		yield return SubmitAnimation();

		CustomDisplayText = "INCORRECT";
		SubmissionMaterial.mainTextureOffset = Vector2.zero;
		Audio.PlaySoundAtTransform("Strike", transform);

		BombModule.HandleStrike();

		yield return new WaitForSeconds(1);
		CustomDisplayText = null;

		isAnimating = false;
	}

	void Log(params object[] values)
	{
		Debug.LogFormat("[Encrypted Equations #{0}] {1}", moduleID, values.Select(Convert.ToString).Join());
	}

	public readonly string TwitchHelpMessage = "Submit the final value using !{0} submit 1234. Clear the submitted value using !{0} clear.";

	public IEnumerable<KMSelectable> ProcessTwitchCommand(string command)
	{
		List<KMSelectable> buttons = new List<KMSelectable>();
		string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (split[0].EqualsAny("submit", "press", "enter", "answer", "s", "p", "e", "a"))
			split = split.Skip(1).ToArray();

		if (split.Length == 1 && split[0] == "clear")
		{
			return new[] { ClearButton };
		}

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
		foreach (KMSelectable selectable in ProcessTwitchCommand("submit " + (CurrentEquation.ValueUndefined ? "" : CurrentEquation.Value.ToThousandths())))
		{
			selectable.OnInteract();
			yield return new WaitForSeconds(0.1f);
		}
	}
}

public static class GeneralExtensions
{
	public static string PrefixLines(this string lines, string prefix)
	{
		return Regex.Replace(lines, "^", prefix, RegexOptions.Multiline);
	}

	public static decimal RoundThousandths(this decimal value)
	{
		return Math.Round(value, 3, MidpointRounding.AwayFromZero);
	}

	public static string ToThousandths(this decimal value)
	{
		return value.ToString("0.###");
	}

	public static GameObject Traverse(this GameObject currentObject, params string[] names)
	{
		Transform currentTransform = currentObject.transform;
		foreach (string name in names)
		{
			currentTransform = currentTransform.Find(name);
		}

		return currentTransform.gameObject;
	}

	public static T Traverse<T>(this GameObject currentObject, params string[] names)
	{
		Transform currentTransform = currentObject.transform;
		foreach (string name in names)
		{
			currentTransform = currentTransform.Find(name);
		}

		return currentTransform.GetComponent<T>();
	}

	public static void AddInteract(this KMSelectable selectable, Func<bool> action)
	{
		selectable.OnInteract += () =>
		{
			if (action())
			{
				selectable.AddInteractionPunch(0.5f);
			}

			return false;
		};
	}
}
