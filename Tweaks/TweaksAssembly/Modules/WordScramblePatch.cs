using System.Reflection;

class WordScramblePatch : ModuleTweak
{
	public WordScramblePatch(BombComponent bombComponent) : base(bombComponent)
	{
		component = bombComponent.GetComponent(componentType);
		if (AnswerField == null || _solutionField == null || EnterButtonField == null) return;

		if ((string) _solutionField.GetValue(component) == "sapper")
		{
			var enterButton = (KMSelectable) EnterButtonField.GetValue(component);
			var previousInteraction = enterButton.OnInteract;
			enterButton.OnInteract = () =>
			{
				if ((string) AnswerField.GetValue(component) == "papers")
					_solutionField.SetValue(component, "papers");

				previousInteraction();

				return false;
			};
		}
	}

	static readonly FieldInfo AnswerField;
	static readonly FieldInfo _solutionField;
	static readonly FieldInfo EnterButtonField;

	static WordScramblePatch()
	{
		componentType = ReflectionHelper.FindType("WordScrambleModule");
		AnswerField = componentType?.GetField("Answer", BindingFlags.NonPublic | BindingFlags.Instance);
		_solutionField = componentType?.GetField("_solution", BindingFlags.NonPublic | BindingFlags.Instance);
		EnterButtonField = componentType?.GetField("EnterButton", BindingFlags.Public | BindingFlags.Instance);
	}
}