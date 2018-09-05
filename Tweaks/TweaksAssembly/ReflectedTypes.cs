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
        Type type = null;
        foreach (Component component in allComponents)
        {
            type = component.GetType();
            if (ZenModeBool == null)
            {
                ZenModeBool = type.GetField("TwitchZenMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (ZenModeBool == null) ZenModeBool = type.GetField("ZenModeActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            if (TimeModeBool == null) TimeModeBool = type.GetField("TimeModeActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if ((ZenModeBool != null) && (TimeModeBool != null)) break;
        }
        if (type == null) return;
        if (!(ZenModeBool?.GetValue(ZenModeBool.IsStatic ? null : KMModule.GetComponent(KMModule.GetComponentInChildren(type).GetType())) is bool)) return;
        if (Tweaks.settings.Mode == Mode.Zen) ZenModeBool.SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(type).GetType()), true);
        else ZenModeBool.SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(type).GetType()), false);
        if (!(TimeModeBool?.GetValue(TimeModeBool.IsStatic ? null : KMModule.GetComponent(KMModule.GetComponentInChildren(type).GetType())) is bool)) return;
        if (Tweaks.settings.Mode == Mode.Time) TimeModeBool.SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(type).GetType()), true);
        else TimeModeBool.SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(type).GetType()), false);
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
