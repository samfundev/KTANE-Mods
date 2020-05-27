using Assets.Scripts.Leaderboards;
using Assets.Scripts.Records;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

static class ReflectedTypes
{
	public static FieldInfo _SizeField = typeof(List<Transform>).GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic);

	public static FieldInfo GameRecordCurrentStrikeIndexField = typeof(GameRecord).GetField("currentStrikeIndex", BindingFlags.NonPublic | BindingFlags.Instance);
	public static FieldInfo HighlightField = typeof(Highlightable).GetField("highlight", BindingFlags.NonPublic | BindingFlags.Instance);
	public static PropertyInfo SubmitFieldProperty = typeof(LeaderboardListRequest).GetProperty("SubmitScore", BindingFlags.Public | BindingFlags.Instance);

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

	public static FieldInfo LoadedModsField;
	public static FieldInfo UnloadedModsField;
	public static FieldInfo TocsField;

	public static FieldInfo IsInteractingField;

	public static FieldInfo KeypadButtonHeightField = typeof(KeypadButton).GetField("buttonHeight", BindingFlags.NonPublic | BindingFlags.Instance);

	public static readonly Dictionary<MonoBehaviour, ModuleFields> CachedFields = new Dictionary<MonoBehaviour, ModuleFields>();

	public class ModuleFields
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

    public static IEnumerable<FieldInfo> GetAllFields(Type t, BindingFlags bindingFlags)
    {
        if (t == null)
            return Enumerable.Empty<FieldInfo>();

        BindingFlags flags = bindingFlags |
                             BindingFlags.DeclaredOnly;
        return t.GetFields(flags).Concat(GetAllFields(t.BaseType, bindingFlags));
    }

    public static FieldInfo GetModuleIDNumber(MonoBehaviour KMModule, out Component targetComponent)
    {
        foreach (Component component in KMModule.GetComponents<Component>())
        {
            foreach (FieldInfo fieldInfo in GetAllFields(component.GetType(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (fieldInfo.FieldType == typeof(int) && (fieldInfo.Name.EndsWith("moduleid", StringComparison.InvariantCultureIgnoreCase) || string.Equals(fieldInfo.Name, "thisloggingid", StringComparison.InvariantCultureIgnoreCase)))
                {
                    targetComponent = component;
                    return fieldInfo;
                }
            }
        }

        targetComponent = null;
        return null;
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

		LoadedModsField = typeof(ModManager).GetField("loadedMods", BindingFlags.Instance | BindingFlags.NonPublic);
		UnloadedModsField = typeof(ModManager).GetField("unloadedMods", BindingFlags.Instance | BindingFlags.NonPublic);
		TocsField = typeof(Mod).GetField("tocs", BindingFlags.Instance | BindingFlags.NonPublic);

		IsInteractingField = typeof(InteractiveObject).GetField("isInteracting", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	static ReflectedTypes()
	{
		UpdateTypes();
	}
}
