using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Records;
using UnityEngine;
using static ReflectionHelper;

static class ReflectedTypes
{
	public static FieldInfo _SizeField = typeof(List<Transform>).GetField("_size", BindingFlags.Instance | BindingFlags.NonPublic);

	public static FieldInfo GameRecordCurrentStrikeIndexField = typeof(GameRecord).GetField("currentStrikeIndex", BindingFlags.NonPublic | BindingFlags.Instance);
	public static FieldInfo HighlightField = typeof(Highlightable).GetField("highlight", BindingFlags.NonPublic | BindingFlags.Instance);

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
		public FieldInfo TimeModeAward;
	}

	//Combination of FindTimeMode, FindZenMode, and their related properties in Twitch Plays
	static readonly BindingFlags modeFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
	public static void FindModeBoolean(BombComponent KMModule)
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
				if (fields.TimeModeAward == null) fields.TimeModeAward = componentType.GetField("TimeModeAwardPoints", modeFieldFlags);

				if (fields.ZenModeBool != null && fields.TimeModeBool != null) break;
			}
		}
		else
			fields = CachedFields[KMModule];

		if (fields.ZenModeBool != null && fields.ZenModeBool.FieldType == typeof(bool))
			fields.ZenModeBool.SetValue(KMModule.GetComponentInChildren(fields.ZenModeBool.ReflectedType), Tweaks.CurrentMode == Mode.Zen);

		if (fields.TimeModeBool != null && fields.TimeModeBool.FieldType == typeof(bool))
			fields.TimeModeBool.SetValue(KMModule.GetComponentInChildren(fields.TimeModeBool.ReflectedType), Tweaks.CurrentMode == Mode.Time);

		if (fields.TimeModeAward != null && fields.TimeModeAward.FieldType == typeof(Action<double>))
			fields.TimeModeAward.SetValue(KMModule.GetComponentInChildren(fields.TimeModeAward.ReflectedType), (Action<double>) ((points) =>
			{
				if (Tweaks.CurrentMode != Mode.Time)
					return;

				BombWrapper.AwardPoints(KMModule, points);
			}));
	}

	public static Gettable GetModuleIDNumber(MonoBehaviour KMModule, out Component targetComponent)
	{
		foreach (Component component in KMModule.GetComponents<Component>())
		{
			if (component == null)
				continue;
			foreach (Gettable gettable in component.GetType().GetAllGettables(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				var name = gettable.Name.ToLowerInvariant();
				if (gettable.Type == typeof(int) && (name.EndsWith("moduleid") || string.Equals(name, "thisloggingid") || name.StartsWith("lognum")))
				{
					targetComponent = component;
					return gettable;
				}
			}
		}

		targetComponent = null;
		return null;
	}

	public static void UpdateTypes()
	{
		FactoryRoomType = FindType("FactoryAssembly.FactoryRoom");
		FactoryGameModeType = FindType("FactoryAssembly.FactoryGameMode");
		StaticModeType = FindType("FactoryAssembly.StaticMode");
		GameModeProperty = FactoryRoomType?.GetProperty("GameMode", BindingFlags.NonPublic | BindingFlags.Instance);
		_CurrentBombField = FindType("FactoryAssembly.FiniteSequenceMode")?.GetField("_currentBomb", BindingFlags.NonPublic | BindingFlags.Instance);
		FactoryRoomDataType = FindType("FactoryAssembly.FactoryRoomData");
		WarningTimeField = FactoryRoomDataType?.GetField("WarningTime", BindingFlags.Public | BindingFlags.Instance);

		AdaptationsProperty = FactoryGameModeType?.GetProperty("Adaptations", BindingFlags.NonPublic | BindingFlags.Instance);
		FactoryGameModeAdaptationType = FindType("FactoryAssembly.FactoryGameModeAdaptation");
		GlobalTimerAdaptationType = FindType("FactoryAssembly.GlobalTimerAdaptation");

		ForeignExchangeRatesType = FindType("ForeignExchangeRates");
		CurrencyAPIEndpointField = ForeignExchangeRatesType?.GetField("CURRENCY_API_ENDPOINT", BindingFlags.Static | BindingFlags.NonPublic);

		PortalRoomType = FindType("PortalRoom", "HexiBombRoom");
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
