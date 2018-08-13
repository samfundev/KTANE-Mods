using Assets.Scripts.Records;
using System;
using System.Reflection;
using UnityEngine;

static class ReflectedTypes
{
	public static FieldInfo GameRecordCurrentStrikeIndexField = typeof(GameRecord).GetField("currentStrikeIndex", BindingFlags.NonPublic | BindingFlags.Instance);

	public static Type FactoryRoomType;
	public static Type FactoryGameModeType;
	public static Type FiniteSequenceModeType;
	public static PropertyInfo GameModeProperty;
	public static FieldInfo _CurrentBombField;

	public static PropertyInfo AdaptationsProperty;
	public static Type FactoryGameModeAdaptationType;
	public static Type GlobalTimerAdaptationType;

	public static Type ForeignExchangeRatesType;
	public static FieldInfo CurrencyAPIEndpointField;

	public static FieldInfo IsInteractingField;
	public static FieldInfo ZenModeBool { get; set; }
    public static FieldInfo TimeModeBool { get; set; }
    
    public static void FindModeBoolean(MonoBehaviour KMModule)
	{
    	Component[] allComponents = KMModule.GetComponentsInChildren<Component>(true);
    	foreach (Component component in allComponents)
    	{
        	Type type = component.GetType();
			var modeType = KMModule.GetComponent(type);
			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
			ZenModeBool = type.GetField("TwitchZenMode", flags);
			if (ZenModeBool == null) ZenModeBool = type.GetField("ZenModeActive", flags);
			TimeModeBool = type.GetField("TwitchTimeMode", flags);
			if (TimeModeBool == null) TimeModeBool = type.GetField("TimeModeActive", flags);
			if (ZenModeBool?.GetValue(ZenModeBool.IsStatic ? null : modeType) is bool)
			Tweaks.settings.Mode == Mode.Zen ? ZenModeBool.SetValue(modeType, true) : ZenModeBool.SetValue(modeType, false);
			else if (TimeModeBool?.GetValue(TimeModeBool.IsStatic ? null : modeType) is bool)
			Tweaks.settings.Mode == Mode.Time ? TimeModeBool.SetValue(modeType, true) : TimeModebool.SetValue(modeType, false);
    	}
	}

	public static void UpdateTypes()
	{
		FactoryRoomType = ReflectionHelper.FindType("FactoryAssembly.FactoryRoom");
		FactoryGameModeType = ReflectionHelper.FindType("FactoryAssembly.FactoryGameMode");
		FiniteSequenceModeType = ReflectionHelper.FindType("FactoryAssembly.FiniteSequenceMode");
		GameModeProperty = FactoryRoomType?.GetProperty("GameMode", BindingFlags.NonPublic | BindingFlags.Instance);
		_CurrentBombField = FiniteSequenceModeType?.GetField("_currentBomb", BindingFlags.NonPublic | BindingFlags.Instance);

		AdaptationsProperty = FactoryGameModeType?.GetProperty("Adaptations", BindingFlags.NonPublic | BindingFlags.Instance);
		FactoryGameModeAdaptationType = ReflectionHelper.FindType("FactoryAssembly.FactoryGameModeAdaptation");
		GlobalTimerAdaptationType = ReflectionHelper.FindType("FactoryAssembly.GlobalTimerAdaptation");

		ForeignExchangeRatesType = ReflectionHelper.FindType("ForeignExchangeRates");
		CurrencyAPIEndpointField = ForeignExchangeRatesType?.GetField("CURRENCY_API_ENDPOINT", BindingFlags.Static | BindingFlags.NonPublic);

		IsInteractingField = typeof(InteractiveObject).GetField("isInteracting", BindingFlags.NonPublic | BindingFlags.Instance);
	}

	static ReflectedTypes()
	{
		UpdateTypes();
	}
}
