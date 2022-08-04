using System;
using HarmonyLib;

[AttributeUsage(AttributeTargets.Class)]
public class ModulePatchAttribute : HarmonyPatch
{
}