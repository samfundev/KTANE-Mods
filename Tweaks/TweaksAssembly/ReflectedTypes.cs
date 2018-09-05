using Assets.Scripts.Records;
using System;
using System.Collections.Generic;
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
    public static List<FieldInfo> ZenModeBool { get; set; }
    public static List<FieldInfo> TimeModeBool { get; set; }
    public static List<MonoBehaviour> Modules { get; set; }
    private static List<Type> Type;

    //Combination of FindTimeMode, FindZenMode, and their related properties in Twitch Plays
    public static void FindModeBoolean(MonoBehaviour KMModule)
    {
        //Everything's handled in this method, so just assign the various methods based on a list.
        //If the lists haven't been initialized yet, do that now
        if (Modules == null)
        {
            ZenModeBool = new List<FieldInfo>();
            TimeModeBool = new List<FieldInfo>();
            Modules = new List<MonoBehaviour>();
            Type = new List<Type>();
        }
        //If the module has already been added to the list, we can use the predetermined types again
        //Otherwise, add some null values in case the selected module doesn't have what we're looking for
        if (!Modules.Contains(KMModule))
        {
            Modules.Add(KMModule);
            ZenModeBool.Add(null);
            TimeModeBool.Add(null);
            Type.Add(null);
            Type.Add(null);
        }
        //Keep the index of KMModule in sync with its methods
        var i = Modules.IndexOf(KMModule);
        //Search all of the components in the module for Zen/Time fields
        Component[] allComponents = KMModule.GetComponentsInChildren<Component>(true);
        foreach (Component component in allComponents)
        {
            //If we already have the method for this value, continue
            if (ZenModeBool[i] == null)
            {
                Type[i * 2] = component.GetType();
                ZenModeBool[i] = Type[i * 2].GetField("TwitchZenMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (ZenModeBool[i] == null) ZenModeBool[i] = Type[i * 2].GetField("ZenModeActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }
            //If we already have the method for this value, continue
            if (TimeModeBool[i] == null)
            {
                Type[(i * 2) + 1] = component.GetType();
                TimeModeBool[i] = Type[(i * 2) + 1].GetField("TimeModeActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (TimeModeBool[i] == null) TimeModeBool[i] = Type[(i * 2) + 1].GetField("TimeModeActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }
            //Unfortunately if both values aren't in the module, we have to go through this for loop each time
            //Should probably set a variable somewhere to not go through the loop if we already know it's empty
            if ((ZenModeBool[i] != null) && (TimeModeBool[i] != null)) break;
        }
        if (ZenModeBool[i] != null)
        {
            if (!(ZenModeBool[i]?.GetValue(ZenModeBool[i].IsStatic ? null : KMModule.GetComponent(KMModule.GetComponentInChildren(Type[i * 2]).GetType())) is bool)) return;
            //Assign the current mode to the variable. Since this is usually a boolean value, this needs to be an if/else statement
            if (Tweaks.settings.Mode == Mode.Zen) ZenModeBool[i].SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(Type[i * 2]).GetType()), true);
            else ZenModeBool[i].SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(Type[i * 2]).GetType()), false);
        }
        if (TimeModeBool[i] != null)
        {
            //Since I combined Time/Zen together here, Type needs to be separated by two variables as to not cause exceptions when loading.
            //As such, I assign i * 2 to Zen types and i * 2 + 1 to Time types, assuming they would ever be different.
            //This is because many modules only include one of the variables, and the type would continue changing even after zen mode was assigned.
            if (!(TimeModeBool[i]?.GetValue(TimeModeBool[i].IsStatic ? null : KMModule.GetComponent(KMModule.GetComponentInChildren(Type[(i * 2) + 1]).GetType())) is bool)) return;
            if (Tweaks.settings.Mode == Mode.Time) TimeModeBool[i].SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(Type[(i * 2) + 1]).GetType()), true);
            else TimeModeBool[i].SetValue(KMModule.GetComponent(KMModule.GetComponentInChildren(Type[(i * 2) + 1]).GetType()), false);
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
