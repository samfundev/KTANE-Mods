using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
internal class SimonServesLogging : ModuleLogging
{
	private readonly List<int> lastPeoplePressed = new List<int>();
	private int lastFoodPressed = -1;
	private readonly List<List<string[]>> answers = new List<List<string[]>>() { null, null, null, null };
	private readonly string[][] foodDisplayed = new string[][] { null, null, null, null };
	private Renderer[] food;
	private KMSelectable[] buttons;
	private int stage = -1;
	private bool moduleSolved = false;
	private readonly string[] peopleOrder = new string[] { "Riley", "Brandon", "Gabriel", "Veronica", "Wendy", "Kayle" };
	private readonly string[] peopleColorOrder = new string[] { "Red", "Blue", "Green", "Violet", "White", "Black" };
	private readonly string[] foodColors = new string[] { "Red", "White", "Blue", "Brown", "Green", "Yellow", "Orange", "Pink", "None" };

	public SimonServesLogging(BombComponent bombComponent) : base(bombComponent, "simonServesScript", "Simon Serves")
	{
		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			bombComponent.StartCoroutine(HandleLogging());
			food = component.GetValue<Renderer[]>("food");

			for (int i = 0; i < 6; i++)
			{
				int dummy = i;
				food[i].GetComponent<KMSelectable>().OnInteract += () =>
				{
					lastFoodPressed = dummy;
					return false;
				};
			}
			buttons = component.GetValue<KMSelectable[]>("buttons");

			for (int i = 0; i < 6; i++)
			{
				int dummy = i;
				buttons[i].OnInteract += () =>
				{
					lastPeoplePressed.Add(dummy);
					lastFoodPressed = -1;
					return false;
				};
			}
		};

		bombComponent.OnPass += _ =>
		{
			moduleSolved = true;
			return false;
		};

		bombComponent.OnStrike += _ =>
		{
			bombComponent.StartCoroutine(HandleStrikeLogging());
			return false;
		};
	}

	private IEnumerator HandleStrikeLogging()
	{
		yield return new WaitForSeconds(0.1f);
		if (stage == 4)
		{
			Renderer[] markers = component.GetValue<Renderer[]>("marker");
			int index = markers.IndexOf(marker => marker.enabled);
			Log($"You told {(index == -1 ? "Simon" : peopleOrder[index])} to pay the bill. Strike!");
		}
		else
		{
			//served the wrong person
			if (lastFoodPressed == -1)
			{
				string wrongName = peopleOrder[lastPeoplePressed.Last()];
				string rightName = peopleOrder[GetServingOrder()[lastPeoplePressed.Count - 1]];
				Log($"Served {wrongName} instead of {rightName}. Strike!");
			}
			//gave the wrong food
			else
			{
				string[] answer = answers[stage][lastPeoplePressed.Count - 1];
				Log($"Gave {answer[0]} {foodDisplayed[stage][lastFoodPressed]}. Expected {answer[1]}. Strike!");
			}

			lastPeoplePressed.Clear();
			lastFoodPressed = -1;
			UpdateFoodDisplayed(stage);
		}
	}

	private IEnumerator HandleLogging()
	{
		while (!moduleSolved)
		{
			int oldStage = stage;
			stage = component.GetValue<int>("stage");
			yield return new WaitForSeconds(0.05f);
			if (stage != oldStage)
			{
				LogStageInformation(stage);
				lastPeoplePressed.Clear();
				lastFoodPressed = -1;
			}
		}
		yield return null;
	}

	private void LogStageInformation(int stage)
	{
		string[] stageName = new string[] { "Drinks", "Appetizer", "Main Course", "Dessert" };
		if (stage >= 0 && stage <= 3)
		{
			Log($"Course {stage + 1}: {stageName[stage]}");
			LogFlashOrder();
			LogFoodOnTable(stage);
			LogServingRule(stage);
			LogFoodRule(stage);
			LogServingOrder();
		}
		LogAnswer(stage);
	}

	private void UpdateFoodDisplayed(int stage) => foodDisplayed[stage] = GetFoodColors();

	private string[] GetFoodColors()
	{
		int[] foods = component.GetValue<int[]>("foods");
		int[] actualFoods = foods.Select(i => i == -1 ? 8 : i).ToArray();
		return actualFoods.Select(i => foodColors[i]).ToArray();
	}

	private string[] GetFlashOrder()
	{
		int[] blinkingOrder = new int[6];
		Array.Copy(component.GetValue<int[]>("blinkingOrder"), 0, blinkingOrder, 0, 6);
		return blinkingOrder.Select(x => peopleColorOrder[x]).ToArray();
	}

	private bool GetFoodRule(int stage)
	{
		string[] food = foodDisplayed[stage];
		if (stage == 0)
			return food.Contains("Red");
		else if (stage == 1)
			return food.Contains("Pink") && food.Contains("Orange");
		else if (stage == 2)
			return food.Contains("Red") && component.GetValue<bool>("forgetCocktailServed");
		else if (stage == 3)
			return food.Contains("Orange") && food.Contains("Red");
		return false;
	}

	private bool GetServingRule(int stage)
	{
		string[] flashOrder = GetFlashOrder();
		if (stage == 0)
		{
			string[] target = new[] { "Violet", "Blue", "Red" };
			for (int i = 0; i < 6; i++)
			{
				if (target.Contains(flashOrder[i % 6]) &&
					target.Contains(flashOrder[(i + 1) % 6]) &&
					target.Contains(flashOrder[(i + 2) % 6]))
					return true;
			}
		}
		else if (stage == 1)
		{
			int[] indices = new int[3];
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < 3; j++)
					indices[j] = Array.IndexOf(peopleColorOrder, flashOrder[(i + j) % 6]);
				if (indices[0] - 1 == indices[1] && indices[1] - 1 == indices[2])
					return true;
			}
		}
		else if (stage == 2)
		{
			List<int[]> pairs = new List<int[]>()
			{
				new int[] { 0 , 5 },
				new int[] { 1 , 4 },
				new int[] { 2 , 3 },

			};
			int pairCount = 0;
			for (int i = 0; i < 6; i++)
			{
				int index1 = Array.IndexOf(peopleColorOrder, flashOrder[i]);
				int index2 = Array.IndexOf(peopleColorOrder, flashOrder[(i + 1) % 6]);
				foreach (int[] pair in pairs)
				{
					if (pair.Contains(index1) && pair.Contains(index2))
					{
						pairCount++;
						if (pairCount == 2)
							return true;
					}
				}
			}
		}
		else if (stage == 3)
			return component.GetValue<bool>("mainCourseFirstPickRedWhiteGreen");
		return false;
	}

	private void LogFlashOrder() => Log("Flash Order: " + string.Join(", ", GetFlashOrder()));

	private int[] GetServingOrder() => component.GetValue<int[]>("servingOrder");

	private List<string[]> GetServingAnswers()
	{
		int[] servingOrder = GetServingOrder();
		int[] foods = component.GetValue<int[]>("foods");
		List<int> takenFoods = new List<int>();
		for (int personIndex = 0; personIndex < 6; personIndex++)
		{
			int personNum = servingOrder[personIndex];

			for (int i = 0; i < 8; i++)
			{
				int mostDesiredFood = component.GetValue<int[,,]>("people")[personNum, stage, i];

				if (foods.Contains(mostDesiredFood) && !takenFoods.Contains(mostDesiredFood))
				{
					takenFoods.Add(mostDesiredFood);
					break;
				}
			}
		}
		List<string[]> answer = new List<string[]>();
		for (int i = 0; i < 6; i++)
		{
			answer.Add(new string[] { peopleOrder[servingOrder[i]], foodColors[takenFoods[i]] });
		}
		return answer;
	}

	private void LogFoodOnTable(int stage)
	{
		string[] foodColors = GetFoodColors();
		foodDisplayed[stage] = foodColors;
		Log("Food served (clockwise): " + foodColors.Join(", "));
	}

	private void LogServingOrder() => Log($"Serving Order: " + GetServingOrder().Select(i => peopleColorOrder[i]).Join(", "));

	private void LogServingRule(int stage)
	{
		string[] trueRules = new[] { "Violet, Blue, and Red flashed consecutively", "At least 3 guests consecutively flashed in counterclockwise order", "At least 2 opposite seated pairs of people flashed consecutively", "Red, White or Green had the first pick at the main course" };
		string[] falseRules = new[] { "Violet, Blue, and Red did not flash consecutively", "At least 3 guests consecutively did not flash in counterclockwise order", "At least 2 opposite seated pairs of people did not flashed consecutively", "Neither Red, White or Green had the first pick at the main course" };
		if (GetServingRule(stage))
			Log(trueRules[stage]);
		else
			Log(falseRules[stage]);
	}

	private void LogFoodRule(int stage)
	{
		string[] trueRules = new[] { "Cruelo Juice was served", "Boolean Waffles and Morse Soup was served", "Forghetti Bombognese was served and Forget Cocktail was consumed as a drink at course one", "Bamboozling Waffles and Strike Pie were served" };
		string[] falseRules = new[] { "Cruelo Juice was not served", "Boolean Waffles and Morse Soup was not served", "Either Forghetti Bombognese was not served or Forget Cocktail was not consumed as a drink at course one", "Either Bamboozling Waffles or Strike Pie were not served" };
		if (GetFoodRule(stage))
			Log(trueRules[stage]);
		else
			Log(falseRules[stage]);
	}

	private void LogAnswer(int stage)
	{
		if (stage >= 0 && stage <= 3)
		{
			if (answers[stage] == null)
				answers[stage] = GetServingAnswers();
			Log("Answer");
			foreach (string[] s in answers[stage])
				Log($"{s[0]}: {s[1]}");
		}
		else
		{
			int payingBillIndex = GetBillIndex();
			Log($"{(payingBillIndex == -1 ? "Simon" : peopleOrder[payingBillIndex])} should pay the bill");
		}
	}

	private int GetBillIndex()
	{
		int mainCourseLastPick = component.GetValue<int>("mainCourseLastPick");
		int payingBillIndex = -1;
		string serialNumber = component.GetValue<string>("serialNumber").ToUpper();
		for (int i = 0; i < 6; i++)
		{
			if (peopleOrder[(mainCourseLastPick + i) % 6].ToUpper().Intersect(serialNumber).Any())
			{
				payingBillIndex = (mainCourseLastPick + i) % 6;
				break;
			}
		}
		return payingBillIndex;
	}
}