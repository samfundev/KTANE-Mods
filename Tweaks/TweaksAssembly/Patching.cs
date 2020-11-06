using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TweaksAssembly.Patching
{
	static class Patching
	{
		private static readonly Dictionary<string, Harmony> instances = new Dictionary<string, Harmony>();

		public static bool PatchClasses(string id, out Harmony instance, params Type[] types)
		{
			instance = new Harmony($"samfundev.tweaks.{id}");
			var success = false;

			foreach (Type type in types)
			{
				var result = instance.CreateClassProcessor(type).Patch();
				success |= result?.Count > 0;
			}

			return success;
		}

		public static void EnsurePatch(string id, params Type[] types)
		{
			if (instances.ContainsKey(id))
				return;

			if (PatchClasses(id, out Harmony instance, types))
				instances.Add(id, instance);
		}

		public static Harmony ManualInstance(string id)
		{
			if (!instances.ContainsKey(id))
				instances.Add(id, new Harmony($"samfundev.tweaks.{id}"));

			return instances[id];
		}
	}

	#pragma warning disable IDE0051
	[HarmonyPatch]
	static class LogfileUploaderPatch
	{
		static MethodBase method;

		static bool Prepare()
		{
			var type = ReflectionHelper.FindType("LogfileUploader");
			method = type?.GetMethod("HandleLog", BindingFlags.NonPublic | BindingFlags.Instance);
			return method != null;
		}

		static MethodBase TargetMethod() => method;

		static void Postfix(object __instance, string stackTrace)
		{
			if (string.IsNullOrEmpty(stackTrace) || !__instance.GetValue<bool>("loggingEnabled"))
				return;

			__instance.SetValue("Log", __instance.GetValue<string>("Log") + stackTrace + "\n");
			__instance.SetValue("LastBombLog", __instance.GetValue<string>("LastBombLog") + stackTrace + "\n");
		}
	}
	#pragma warning restore IDE0051
}