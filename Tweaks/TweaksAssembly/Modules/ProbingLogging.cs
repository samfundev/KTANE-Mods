using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class ProbingLogging : ModuleTweak
{
	static readonly string typeName = "ProbingModule";

	[Flags]
	private enum WireValues
	{
		NONE = 0x0,
		A = 0x1,
		B = 0x2,
		C = 0x4,
		D = 0x8
	}

	Dictionary<WireValues, string> wireHz = new Dictionary<WireValues, string>()
	{
		{ WireValues.A, "10" },
		{ WireValues.B, "22" },
		{ WireValues.C, "50" },
		{ WireValues.D, "60" }
	};

	// Depending on the value in the target wire, we can deduce the rule used for both clips.
	Dictionary<WireValues, string> redClipRules = new Dictionary<WireValues, string>()
	{
		{ WireValues.A | WireValues.B | WireValues.D, "the red and white wire contains 50 Hz" },
		{ WireValues.B | WireValues.C | WireValues.D, "the red and yellow wire doesn't contain 10 Hz" },
		{ WireValues.A | WireValues.B | WireValues.C, "the other two conditions didn't apply" }
	};

	Dictionary<WireValues, string> blueClipRules = new Dictionary<WireValues, string>()
	{
		{ WireValues.A | WireValues.C | WireValues.D, "the red and yellow wire contains 10 Hz" },
		{ WireValues.A | WireValues.B | WireValues.C, "the other condition didn't apply" }
	};

	private static int idCounter = 1;
	private readonly int moduleID;
	private readonly KMBombInfo bombInfo;

	public ProbingLogging(BombComponent bombComponent) : base(bombComponent)
	{
		componentType = componentType ?? (componentType = ReflectionHelper.FindType(typeName));
		mWiresField = mWiresField ?? (mWiresField = componentType.GetField("mWires", NonPublicInstance));
		mTargetWireAField = mTargetWireAField ?? (mTargetWireAField = componentType.GetField("mTargetWireA", NonPublicInstance));
		mTargetWireBField = mTargetWireBField ?? (mTargetWireBField = componentType.GetField("mTargetWireB", NonPublicInstance));

		component = bombComponent.GetComponent(componentType);
		moduleID = idCounter++;
		bombInfo = bombComponent.GetComponent<KMBombInfo>();
		mNumStrikes = bombInfo.GetStrikes();
		bombComponent.GetComponent<KMBombModule>().OnActivate += () => bActive = true;
		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
		{
			Debug.Log($"[Probing #{moduleID}] Module solved.");
			return true;
		};

		mWires = ((Array) mWiresField.GetValue(component)).Cast<WireValues>();
		LogWires();
		bombComponent.StartCoroutine(CheckForStrikeChanges());
	}

	private int mNumStrikes;
	private bool bActive;
	private IEnumerable<WireValues> mWires;
	IEnumerator CheckForStrikeChanges()
	{
		while (true)
		{
			if (bActive && mNumStrikes != bombInfo.GetStrikes())
			{
				mNumStrikes = bombInfo.GetStrikes();

				var mWiresNew = ((Array) mWiresField.GetValue(component)).Cast<WireValues>();
				var valuesChanged = !mWires.SequenceEqual(mWiresNew);

				mWires = mWiresNew;
				if (valuesChanged)
				{
					Debug.Log($"[Probing #{moduleID}] The number of strikes changed and so did the wire values.");
					LogWires();
				}
				else
					Debug.Log($"[Probing #{moduleID}] The number of strikes changed but the wire values stayed the same.");
			}
			yield return null;
		}
	}

	string FormatWireValue(WireValues wireValue) => wireHz
		.Where(pair => (wireValue & pair.Key) != 0)
		.Select(pair => pair.Value)
		.Join("+");

	void LogWires()
	{
		var wireValues = mWires.Select(FormatWireValue);

		var missingWireValues = mWires.Select(wireValue => wireHz.First(pair => (wireValue & pair.Key) == 0).Value);

		Debug.Log($"[Probing #{moduleID}] Wire values:\n{wireValues.Take(3).Join(" | ")}\n{wireValues.Skip(3).Join(" | ")}");
		Debug.Log($"[Probing #{moduleID}] Missing wire values:\n{missingWireValues.Take(3).Join(" | ")}\n{missingWireValues.Skip(3).Join(" | ")}");

		WireValues redTargetValues = (WireValues) mTargetWireAField.GetValue(component);
		WireValues blueTargetValues = (WireValues) mTargetWireBField.GetValue(component);

		Debug.Log($"[Probing #{moduleID}] The red clip should be connected to a wire containing {FormatWireValue(redTargetValues)} because {redClipRules[redTargetValues]}.");
		Debug.Log($"[Probing #{moduleID}] The blue clip should be connected to a wire containing {FormatWireValue(blueTargetValues)} because {blueClipRules[blueTargetValues]}.");
	}

	static Type componentType;
	static FieldInfo mWiresField;
	static FieldInfo mTargetWireAField;
	static FieldInfo mTargetWireBField;

	const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
}