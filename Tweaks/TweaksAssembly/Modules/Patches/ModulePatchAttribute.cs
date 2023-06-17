using System;
using HarmonyLib;

[AttributeUsage(AttributeTargets.Class)]
public class ModulePatchAttribute : HarmonyPatch
{
	public readonly string ModuleId;
	
	public ModulePatchAttribute(string moduleId)
	{
		ModuleId = moduleId;
	}
}
