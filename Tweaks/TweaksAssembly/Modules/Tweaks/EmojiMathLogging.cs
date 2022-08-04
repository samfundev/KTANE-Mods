using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class EmojiMathLogging : ModuleLogging
{
    public EmojiMathLogging(BombComponent bombComponent) : base(bombComponent, "EmojiMathModule", "Emoji Math")
    {
        mPuzzle = componentType?.GetField("Puzzle", BindingFlags.NonPublic | BindingFlags.Instance);
        mButtons = componentType?.GetField("Buttons", BindingFlags.Public | BindingFlags.Instance);
        mSign = componentType?.GetField("Sign", BindingFlags.NonPublic | BindingFlags.Instance);
        mAnswer = componentType?.GetField("Answer", BindingFlags.NonPublic | BindingFlags.Instance);
        mDisplayText = componentType?.GetField("DisplayText", BindingFlags.Public | BindingFlags.Instance);

        if (componentType == null || component == null || mPuzzle == null || mButtons == null || mSign == null || mAnswer == null || mDisplayText == null)
        {
            Log($"Logging failed (1): {new object[] { componentType, component, mPuzzle, mButtons, mSign, mAnswer, mDisplayText }.Select(obj => obj == null ? "<NULL>" : "(not null)").Join(", ")}.");
            return;
        }

        bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
        {
            var buttons = (KMSelectable[]) mButtons.GetValue(component);
            for (int i = 0; i < buttons.Length; i++)
                BindButtonPress(buttons[i], i);

            var puzzle = mPuzzle.GetValue(component);
            GetPuzzleFieldsMethods(puzzle.GetType());
            var op1 = (int) mOperand1.GetValue(puzzle);
            var op2 = (int) mOperand2.GetValue(puzzle);
            var op = (string) mGetOperationString.Invoke(null, new object[] { mOperator.GetValue(puzzle) });
            Log($"Puzzle on module: “{((TextMesh) mDisplayText.GetValue(component)).text}”");
            Log($"Decoded puzzle: “{op1} {op} {op2}”");
            Log($"Expected answer: “{(op == "+" ? op1 + op2 : op1 - op2)}”");
        };
    }

    private void BindButtonPress(KMSelectable sel, int i)
    {
        var prev = sel.OnInteract;
        sel.OnInteract = delegate
        {
            var prevAnswer = (string) mAnswer.GetValue(component);
            var ret = prev();
            switch (i)
            {
                case 10:    // Minus button
                    Log($"You pressed Minus. Sign is now {(mSign.GetValue(component).Equals(1) ? "positive." : "negative. Remember that this sticks even across strikes!")}");
                    break;

                case 11:    // Enter button
                    Log($"You submitted “{(mSign.GetValue(component).Equals(1) ? "" : "-")}{prevAnswer}”. This is {(mCheckAnswer.Invoke(mPuzzle.GetValue(component), new object[] { prevAnswer, mSign.GetValue(component) }).Equals(true) ? "correct. Module solved." : "wrong. Strike!")}");
                    break;

                default:    // Digit
                    Log($"You pressed {i}. Input is now “{(mSign.GetValue(component).Equals(1) ? "" : "-")}{mAnswer.GetValue(component)}”.");
                    break;
            }
            return ret;
        };
    }

    private void GetPuzzleFieldsMethods(Type puzzleType)
    {
        mCheckAnswer = puzzleType.GetMethod("CheckAnswer", BindingFlags.Public | BindingFlags.Instance);
        mGetOperationString = puzzleType.GetMethod("GetOperationString", BindingFlags.Public | BindingFlags.Static);
        mOperand1 = puzzleType.GetField("Operand1", BindingFlags.Public | BindingFlags.Instance);
        mOperand2 = puzzleType.GetField("Operand2", BindingFlags.Public | BindingFlags.Instance);
        mOperator = puzzleType.GetField("Operator", BindingFlags.Public | BindingFlags.Instance);

        if (mCheckAnswer == null || mGetOperationString == null || mOperand1 == null || mOperand2 == null || mOperator == null)
            Log($"Logging failed (2): {new object[] { mCheckAnswer, mGetOperationString, mOperand1, mOperand2, mOperator }.Select(obj => obj == null ? "<NULL>" : "(not null)").Join(", ")}.");
    }

    // Fields on the component type
    static FieldInfo mPuzzle;
    static FieldInfo mButtons;
    static FieldInfo mSign;
    static FieldInfo mAnswer;
    static FieldInfo mDisplayText;

    // Fields/methods on the Puzzle type
    static MethodInfo mCheckAnswer;
    static MethodInfo mGetOperationString;
    static FieldInfo mOperand1;
    static FieldInfo mOperand2;
    static FieldInfo mOperator;
}