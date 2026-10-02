using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

internal class EnglishTestLogging : ModuleLogging
{
	private bool moduleSolved = false;

	private KMSelectable SubmitSelectable;

	private int stage = -1;
	private int totalStages;
	private static FieldInfo currentQuestionFld;
	public static PropertyInfo QuestionTextProp;
	public static PropertyInfo AnswerTextIndexProp;
	public static PropertyInfo AnswersFldProp;
	public static PropertyInfo CorrectAnswerIndexProp;

	private string QuestionText;
	private int AnswerTextIndex;
	private List<string> Answers;
	private byte CorrectAnswerIndex;

	private bool waitForNextQuestion = false;
	private bool strike = false;

	public EnglishTestLogging(BombComponent bombComponent) : base(bombComponent, "EnglishTestModule", "English Test")
	{
		currentQuestionFld = componentType.GetField("currentQuestion", BindingFlags.NonPublic | BindingFlags.Instance);
		SubmitSelectable = component.GetValue<KMSelectable>("SubmitSelectable");
		totalStages = component.GetValue<int>("targetQuestions");

		SubmitSelectable.OnInteract += () =>
		{
			int selectedAnswerIndex = component.GetValue<int>("selectedAnswerIndex");
			bool correct = selectedAnswerIndex == CorrectAnswerIndex;
			Log($"You chose: {Answers[selectedAnswerIndex]}. This is {(correct ? "correct." : "incorrect. Strike!")}");
			if (correct)
			{
				Log(stage + 1 == totalStages ? "Module Solved." : $"Moving to question {stage + 2}.");
			}

			else
			{
				Log("Resetting module.");
				strike = true;
			}

			//wait 3 seconds bc that's how long the mod does before going to the next question
			waitForNextQuestion = true;

			return false;
		};
		bombComponent.OnPass += _ =>
		{
			moduleSolved = true;
			return false;
		};
		bombComponent.StartCoroutine(HandleLogging());
	}

	private IEnumerator HandleLogging()
	{
		//Check if module has activated
		while (!component.GetValue<bool>("activated"))
		{
			yield return new WaitForSeconds(0.1f);
		}
		while (!moduleSolved)
		{
			int oldStage = stage;
			stage = component.GetValue<int>("solvedQuestions");
			yield return new WaitForSeconds(0.1f);
			Debug.Log("25");
			if (stage != oldStage || strike)
			{
				strike = false;
				if (waitForNextQuestion)
				{
					yield return new WaitForSeconds(3.1f);
					waitForNextQuestion = false;
				}

				UpdateCurrentQuestionFields();
				Log(QuestionText.Insert(AnswerTextIndex, "_____"));
				Log("Given answers: " + string.Join(", ", Answers.ToArray()));
				Log("Expected answer: " + Answers[CorrectAnswerIndex]);
			}
		}
		yield return null;
	}

	private void UpdateCurrentQuestionFields()
	{
		var question = currentQuestionFld.GetValue(component);
		Type questionType = question.GetType();

		QuestionTextProp = questionType.GetProperty("QuestionText", BindingFlags.Public | BindingFlags.Instance);
		AnswerTextIndexProp = questionType.GetProperty("AnswerTextIndex", BindingFlags.Public | BindingFlags.Instance);
		AnswersFldProp = questionType.GetProperty("Answers", BindingFlags.Public | BindingFlags.Instance);
		CorrectAnswerIndexProp = questionType.GetProperty("CorrectAnswerIndex", BindingFlags.Public | BindingFlags.Instance);

		QuestionText = (string) QuestionTextProp.GetValue(question, null);
		AnswerTextIndex = (int) AnswerTextIndexProp.GetValue(question, null);
		Answers = (List<string>) AnswersFldProp.GetValue(question, null);
		CorrectAnswerIndex = (byte) CorrectAnswerIndexProp.GetValue(question, null);
	}
}