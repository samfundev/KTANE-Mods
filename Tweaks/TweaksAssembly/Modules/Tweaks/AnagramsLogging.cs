using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TweaksAssembly.Modules.Tweaks
{
	internal class AnagramsLogging : ModuleLogging
	{
		public AnagramsLogging(BombComponent bombComponent) : base(bombComponent, "AnagramsModule", "Anagrams")
		{
			bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
			{
				string displayedWord = component.GetValue<TextMesh>("PuzzleDisplay").text;
				 IList<IList<string>> words = component.GetValue<IList<IList<string>>>("Words");
				List<string> possileAnswers = words.First(row => row.Contains(displayedWord)).Where(word => word != displayedWord).ToList();

				Log($"Displayed word: {displayedWord}");
				Log($"Possible answers: {possileAnswers[0]}, {possileAnswers[1]}");

				var submitButton = component.GetValue<KMSelectable>("EnterButton");
				var baseInteract = submitButton.OnInteract;
				submitButton.OnInteract = () =>
				{
					string submittedAnswer = component.GetValue<TextMesh>("AnswerDisplay").text;

					Log($"Submitted \"{submittedAnswer}\". {(possileAnswers.Contains(submittedAnswer) ? "Solving module..." : "Strike!")}");
					return baseInteract();
				};
			};
		}
	}
}
