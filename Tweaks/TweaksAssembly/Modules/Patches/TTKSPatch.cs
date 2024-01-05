using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// REMAINING BUGS:
// Multiple sets of TTKS simultaneously (with Multiple Bombs) will break. There is one set of static fields updated in KMBombModule.OnActivate if the bomb has a different serial number.
// If more than 8000 TTKS spawn in one set, some will have bugged displays (which should* read 0).

[ModulePatch]
public static class TTKSPatch
{
	static bool Prepare()
	{
		if (ReflectedTypes.TTKSType == null || ReflectedTypes.TTKSLeftHandler == null || ReflectedTypes.TTKSRightHandler == null)
			return false;

		Module = Module ?? ReflectedTypes.TTKSType?.GetField("mModule", BindingFlags.Instance | BindingFlags.NonPublic);
		LeftKey = LeftKey ?? ReflectedTypes.TTKSType?.GetField("bLeftKeyTurned", BindingFlags.Instance | BindingFlags.NonPublic);
		RightKey = RightKey ?? ReflectedTypes.TTKSType?.GetField("bRightKeyTurned", BindingFlags.Instance | BindingFlags.NonPublic);
		Activated = Activated ?? ReflectedTypes.TTKSType?.GetField("bActivated", BindingFlags.Instance | BindingFlags.NonPublic);
		LeftAnimator = LeftAnimator ?? ReflectedTypes.TTKSType?.GetField("LeftKeyAnim", BindingFlags.Instance | BindingFlags.Public);
		RightAnimator = RightAnimator ?? ReflectedTypes.TTKSType?.GetField("RightKeyAnim", BindingFlags.Instance | BindingFlags.Public);
		Audio = Audio ?? ReflectedTypes.TTKSType?.GetField("mAudio", BindingFlags.Instance | BindingFlags.NonPublic);
		LeftCache = LeftCache ?? ReflectedTypes.TTKSType?.GetField("<>f__am$cache16", BindingFlags.Static | BindingFlags.NonPublic);
		RightCache = RightCache ?? ReflectedTypes.TTKSType?.GetField("<>f__am$cache19", BindingFlags.Static | BindingFlags.NonPublic);

		return true;
	}

	static FieldInfo Module, LeftKey, RightKey, Activated, LeftAnimator, RightAnimator, Audio, LeftCache, RightCache;

	static IEnumerable<MethodBase> TargetMethods()
	{
		yield return ReflectedTypes.TTKSLeftHandler;
		yield return ReflectedTypes.TTKSRightHandler;
	}

	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
	{
		// Enumerable contains (in part):
		// public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second)
		// public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second, IEqualityComparer<TSource> comparer)
		var exceptMethod = typeof(Enumerable)
			.GetMethods(BindingFlags.Static | BindingFlags.Public)
			.First(m => m.Name == nameof(Enumerable.Except) && m.GetParameters().Length == 2)
			.MakeGenericMethod(typeof(string));
		var subtractMethod = typeof(TTKSPatch)
			.GetMethod(nameof(Subtract), BindingFlags.Static | BindingFlags.NonPublic)
			.MakeGenericMethod(typeof(string));

		// The local used to store the softlocking condition
		var local = original.Name == ReflectedTypes.TTKSLeftHandler.Name ? LeftCache : RightCache;
		// The field with which to early-return
		var field = original.Name == ReflectedTypes.TTKSLeftHandler.Name ? LeftKey : RightKey;
		// The animator to animate upon a strike
		var animatorField = original.Name == ReflectedTypes.TTKSLeftHandler.Name ? LeftAnimator : RightAnimator;

		return instructions
			// Make sure every instance of a "turn the key after" module is solved, not just one
			// See: "Solved *all* ... modules."
			.MethodReplacer(exceptMethod, subtractMethod)
			// Make incorrect key turns play the animation and audio from TTK
			.AddStrikeAnimation(animatorField)
			// If any "turn the key before" modules have been solved, continue with a strike to prevent softlocks
			.FixSoftlock(local, generator)
			// Prevent turning an already-turned key
			// This removes the possibility of multi-solving the module
			.FixMultiTurn(field, generator);
	}

	static IEnumerable<T> Subtract<T>(this IEnumerable<T> first, IEnumerable<T> second)
	{
		var l = first.ToList();
		foreach (var t in second)
			l.Remove(t);
		return l;
	}

	static IEnumerable<CodeInstruction> AddStrikeAnimation(this IEnumerable<CodeInstruction> instructions, FieldInfo field)
	{
		var strikeMethod = typeof(KMBombModule).GetMethod(nameof(KMBombModule.HandleStrike), BindingFlags.Instance | BindingFlags.Public);

		var en = instructions.GetEnumerator();
		while (en.MoveNext())
		{
			var instruction = en.Current;
			if (instruction.Calls(strikeMethod))
			{
				yield return instruction;
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, field);
				yield return new CodeInstruction(OpCodes.Ldstr, "WrongTurn");
				yield return CodeInstruction.Call(typeof(Animator), "SetTrigger", new System.Type[] { typeof(string) });
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, Audio);
				yield return new CodeInstruction(OpCodes.Ldstr, "WrongKeyTurnFX");
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return CodeInstruction.Call(typeof(Component), "get_transform", new System.Type[] { });
				yield return CodeInstruction.Call(typeof(KMAudio), "PlaySoundAtTransform", new System.Type[] { typeof(string), typeof(Transform) });
			}
			else
			{
				yield return instruction;
			}
		}
	}

	static IEnumerable<CodeInstruction> FixSoftlock(this IEnumerable<CodeInstruction> instructions, FieldInfo lambdaCache, ILGenerator generator)
	{
		var en = instructions.GetEnumerator();
		bool seek = false;
		object local = null;
		while (en.MoveNext())
		{
			var instruction = en.Current;
			if (instruction.Is(OpCodes.Ldloc_S, local))
			{
				// Skip this load and subsequent branch, to be done later
				en.MoveNext();
			}
			// Conveniently, only one Stfld exists in each method and in a nice spot
			else if (instruction.opcode == OpCodes.Stfld)
			{
				var label = generator.DefineLabel();
				yield return instruction;
				yield return new CodeInstruction(OpCodes.Ldloc_S, local);
				yield return new CodeInstruction(OpCodes.Brtrue, label);
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, Module);
				yield return new CodeInstruction(OpCodes.Callvirt, typeof(KMBombModule).GetMethod("HandleStrike", BindingFlags.Instance | BindingFlags.Public));
				en.MoveNext();
				var i = en.Current;
				i.labels.Add(label);
				yield return i;
			}
			else
			{
				// We need to grab the local from where it's set because the ldloc.s
				// we're looking for might use a number or a LocalBuilder (thanks, Harmony)
				if (instruction.Is(OpCodes.Ldsfld, lambdaCache))
					seek = true;
				if (seek && instruction.opcode == OpCodes.Stloc_S)
				{
					seek = false;
					local = instruction.operand;
				}

				yield return instruction;
			}
		}
	}

	static IEnumerable<CodeInstruction> FixMultiTurn(this IEnumerable<CodeInstruction> instructions, FieldInfo field, ILGenerator generator)
	{
		var en = instructions.GetEnumerator();
		while (en.MoveNext())
		{
			var instruction = en.Current;
			if (instruction.LoadsField(Activated))
			{
				var label = generator.DefineLabel();

				// If not activated, return
				yield return instruction;
				en.MoveNext();
				yield return new CodeInstruction(OpCodes.Brfalse, label);

				// If this key has been turned, return
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Ldfld, field);
				yield return new CodeInstruction(OpCodes.Brtrue, label);
				yield return new CodeInstruction(OpCodes.Br, en.Current.operand);
				en.MoveNext();
				var i = en.Current;
				i.labels.Add(label);
				yield return i;

				// return false;
				// label:
			}
			else
			{
				yield return instruction;
			}
		}
	}
}