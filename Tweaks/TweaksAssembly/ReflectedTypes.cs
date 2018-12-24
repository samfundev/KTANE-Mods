using Assets.Scripts.Records;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

static class ReflectedTypes
{
	public static FieldInfo GameRecordCurrentStrikeIndexField = typeof(GameRecord).GetField("currentStrikeIndex", BindingFlags.NonPublic | BindingFlags.Instance);

	public static Type FactoryRoomType;
	public static Type FactoryGameModeType;
	public static Type StaticModeType;
	public static PropertyInfo GameModeProperty;
	public static FieldInfo _CurrentBombField;
	public static Type FactoryRoomDataType;
	public static FieldInfo WarningTimeField;

	public static PropertyInfo AdaptationsProperty;
	public static Type FactoryGameModeAdaptationType;
	public static Type GlobalTimerAdaptationType;

	public static Type ForeignExchangeRatesType;
	public static FieldInfo CurrencyAPIEndpointField;

	public static Type PortalRoomType;
	public static MethodInfo RedLightsMethod;
	public static FieldInfo RoomLightField;

	public static FieldInfo UnloadedModsField;
	public static FieldInfo TocsField;

	public static FieldInfo IsInteractingField;
	static Dictionary<MonoBehaviour, ModuleFields> CachedFields = new Dictionary<MonoBehaviour, ModuleFields>();

	class ModuleFields
	{
		public FieldInfo ZenModeBool;
		public FieldInfo TimeModeBool;
	}

	//Combination of FindTimeMode, FindZenMode, and their related properties in Twitch Plays
	static readonly BindingFlags modeFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
	public static void FindModeBoolean(MonoBehaviour KMModule)
    {
		ModuleFields fields;

		if (!CachedFields.ContainsKey(KMModule))
		{
			fields = CachedFields[KMModule] = new ModuleFields();
			//Search all of the components in the module for Zen/Time fields
			var allComponents = KMModule.GetComponentsInChildren<Component>(true).Where(component => component != null);
			foreach (Component component in allComponents)
			{
				Type componentType = component.GetType();
				if (fields.ZenModeBool == null) fields.ZenModeBool = componentType.GetField("TwitchZenMode", modeFieldFlags) ?? componentType.GetField("ZenModeActive", modeFieldFlags);
				if (fields.TimeModeBool == null) fields.TimeModeBool = componentType.GetField("TwitchTimeMode", modeFieldFlags) ?? componentType.GetField("TimeModeActive", modeFieldFlags);
				
				if (fields.ZenModeBool != null && fields.TimeModeBool != null) break;
			}
		}
		else
			fields = CachedFields[KMModule];

		if (fields.ZenModeBool != null && fields.ZenModeBool.FieldType == typeof(bool))
            fields.ZenModeBool.SetValue(KMModule.GetComponentInChildren(fields.ZenModeBool.ReflectedType), Tweaks.CurrentMode == Mode.Zen);

        if (fields.TimeModeBool != null && fields.TimeModeBool.FieldType == typeof(bool))
            fields.TimeModeBool.SetValue(KMModule.GetComponentInChildren(fields.TimeModeBool.ReflectedType), Tweaks.CurrentMode == Mode.Time);
    }

	public static void UpdateTypes()
	{
		FactoryRoomType = ReflectionHelper.FindType("FactoryAssembly.FactoryRoom");
		FactoryGameModeType = ReflectionHelper.FindType("FactoryAssembly.FactoryGameMode");
		StaticModeType = ReflectionHelper.FindType("FactoryAssembly.StaticMode");
		GameModeProperty = FactoryRoomType?.GetProperty("GameMode", BindingFlags.NonPublic | BindingFlags.Instance);
		_CurrentBombField = ReflectionHelper.FindType("FactoryAssembly.FiniteSequenceMode")?.GetField("_currentBomb", BindingFlags.NonPublic | BindingFlags.Instance);
		FactoryRoomDataType = ReflectionHelper.FindType("FactoryAssembly.FactoryRoomData");
		WarningTimeField = FactoryRoomDataType?.GetField("WarningTime", BindingFlags.Public | BindingFlags.Instance);

		AdaptationsProperty = FactoryGameModeType?.GetProperty("Adaptations", BindingFlags.NonPublic | BindingFlags.Instance);
		FactoryGameModeAdaptationType = ReflectionHelper.FindType("FactoryAssembly.FactoryGameModeAdaptation");
		GlobalTimerAdaptationType = ReflectionHelper.FindType("FactoryAssembly.GlobalTimerAdaptation");

		ForeignExchangeRatesType = ReflectionHelper.FindType("ForeignExchangeRates");
		CurrencyAPIEndpointField = ForeignExchangeRatesType?.GetField("CURRENCY_API_ENDPOINT", BindingFlags.Static | BindingFlags.NonPublic);

		PortalRoomType = ReflectionHelper.FindType("PortalRoom", "HexiBombRoom");
		RedLightsMethod = PortalRoomType?.GetMethod("RedLight", BindingFlags.Instance | BindingFlags.Public);
		RoomLightField = PortalRoomType?.GetField("RoomLight", BindingFlags.Instance | BindingFlags.Public);

		UnloadedModsField = typeof(ModManager).GetField("unloadedMods", BindingFlags.Instance | BindingFlags.NonPublic);
		TocsField = typeof(Mod).GetField("tocs", BindingFlags.Instance | BindingFlags.NonPublic);

		IsInteractingField = typeof(InteractiveObject).GetField("isInteracting", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	static ReflectedTypes()
	{
		UpdateTypes();
	}
}
