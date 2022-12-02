using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

[ModulePatch]
public static class SkinnyWiresPatch
{
	static bool Prepare() => ReflectedTypes.SkinnyWiresCalculateMethod != null;

	static MethodBase TargetMethod() => ReflectedTypes.SkinnyWiresCalculateMethod;

	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
		ILGenerator generator)
	{
		if (ReflectedTypes.SkinnyWiresCorrectRuleField is null)
		{
			Tweaks.Log("correctRule field could not be found (Skinny Wires)");
			return instructions;
		}

		return new CodeMatcher(instructions, generator).MatchEndForward(
				new CodeMatch(OpCodes.Ldc_I4_S, (sbyte) 9),
				new CodeMatch(OpCodes.Stfld, ReflectedTypes.SkinnyWiresCorrectRuleField)).Advance(1)
			.RemoveInstruction().MatchEndForward(new CodeMatch(OpCodes.Ldarg_0)).CreateLabel(out Label jumpLabel).Insert(
				new CodeInstruction(OpCodes.Ldarg_0),
				new CodeInstruction(OpCodes.Ldfld, ReflectedTypes.SkinnyWiresCorrectRuleField),
				new CodeInstruction(OpCodes.Ldc_I4, 9),
				new CodeInstruction(OpCodes.Bne_Un, jumpLabel), new CodeInstruction(OpCodes.Ret))
			.InstructionEnumeration();

	}
}