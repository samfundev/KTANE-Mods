using UnityEngine;
using System.Reflection;
using System.Linq;

public class ProbingLogging : ModuleLogging
{
	new public string moduleType = "ProbingModule";
	static string typeName = "ProbingModule";

	private enum WireValues
	{
		NONE = 0x0,
		A = 0x1,
		B = 0x2,
		C = 0x4,
		D = 0x8
	}

	public ProbingLogging(BombComponent bombComponent) : base(bombComponent)
	{
		component = bombComponent.GetComponent(componentType);

		LogWires();
	}

	void LogWires()
	{
		string message = "";

		WireValues[] wires = (WireValues[]) mWiresField.GetValue(component);
		for (int i = 0; i < 6; i++)
		{
			message += wires[i];
		}
	}

	static FieldInfo mWiresField;

	static ProbingLogging()
	{
		componentType = ReflectionHelper.FindType(typeName);
		mWiresField = componentType.GetField("mWires", BindingFlags.NonPublic | BindingFlags.Instance);
	}
}