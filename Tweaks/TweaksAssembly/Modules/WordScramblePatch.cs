using System.Reflection;

class WordScramblePatch : ModuleTweak
{
	public WordScramblePatch(BombComponent bombComponent) : base(bombComponent, "WordScrambleModule")
	{
		AnswerField = AnswerField ?? componentType.GetField("Answer", BindingFlags.NonPublic | BindingFlags.Instance);
		SolutionField = SolutionField ?? componentType.GetField("_solution", BindingFlags.NonPublic | BindingFlags.Instance);
		EnterButtonField = EnterButtonField ?? componentType.GetField("EnterButton", BindingFlags.Public | BindingFlags.Instance);

		if (AnswerField == null || SolutionField == null || EnterButtonField == null) return;

		if ((string) SolutionField.GetValue(component) == "sapper")
		{
			var enterButton = (KMSelectable) EnterButtonField.GetValue(component);
			var previousInteraction = enterButton.OnInteract;
			enterButton.OnInteract = () =>
			{
				if ((string) AnswerField.GetValue(component) == "papers")
					SolutionField.SetValue(component, "papers");

				previousInteraction();

				return false;
			};
		}
	}

	static FieldInfo AnswerField;
	static FieldInfo SolutionField;
	static FieldInfo EnterButtonField;
}