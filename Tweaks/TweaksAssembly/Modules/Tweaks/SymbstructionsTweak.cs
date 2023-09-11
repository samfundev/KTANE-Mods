using System;
using UnityEngine;

class SymbstructionsTweak : ModuleTweak
{
	public SymbstructionsTweak(BombComponent bombComponent) : base(bombComponent, "Symbstructions.Behaviours.SymbstructionsScript")
	{
		// Extracts and updates the internal ModSettings as it fails with its own non-serializable class
		SymbstructionsSettings settings = new ModConfig<SymbstructionsSettings>("Symbstructions-settings").Read();
		component.GetValue<object>("Settings").SetValue("TimeLimit", settings.TimeLimit);
		component.GetValue<object>("Settings").SetValue("WiresPuzzleEnabled", settings.WiresPuzzleEnabled);
		component.GetValue<object>("Settings").SetValue("KeypadPuzzleEnabled", settings.KeypadPuzzleEnabled);
		Type timerType = ReflectionHelper.FindType("Symbstructions.Behaviours.Timer");
		Component timerComp = component.transform.GetChild(2).GetChild(2).GetComponent(timerType);
		timerComp.SetValue("TimeRemaining", settings.TimeLimit);
		timerComp.CallMethod("UpdateDisplay");
	}
}

[Serializable]
public class SymbstructionsSettings
{
	public float TimeLimit { get; set; } = 60f;
	public bool WiresPuzzleEnabled { get; set; } = true;
	public bool KeypadPuzzleEnabled { get; set; } = true;
}