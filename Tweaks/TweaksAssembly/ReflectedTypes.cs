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
	public static Type FiniteSequenceModeType;
	public static PropertyInfo GameModeProperty;
	public static FieldInfo _CurrentBombField;

	public static PropertyInfo AdaptationsProperty;
	public static Type FactoryGameModeAdaptationType;
	public static Type GlobalTimerAdaptationType;

	public static Type ForeignExchangeRatesType;
	public static FieldInfo CurrencyAPIEndpointField;

	public static FieldInfo IsInteractingField;
	static Dictionary<MonoBehaviour, ModuleFields> CachedFields = new Dictionary<MonoBehaviour, ModuleFields>();

	class ModuleFields
	{
		public FieldInfo ZenModeBool;
		public FieldInfo TimeModeBool;
	}

    //Combination of FindTimeMode, FindZenMode, and their related properties in Twitch Plays
    public static void FindModeBoolean(MonoBehaviour KMModule)
    {
        //Everything's handled in this method, so just assign the various methods based on a list.
        //If the module has already been added to the list, we can use the predetermined types again
        //Otherwise, add some null values in case the selected module doesn't have what we're looking for
        if (!CachedFields.ContainsKey(KMModule)) CachedFields[KMModule] = new ModuleFields();

		ModuleFields fields = CachedFields[KMModule];

        //Search all of the components in the module for Zen/Time fields
        var allComponents = KMModule.GetComponentsInChildren<Component>(true).Where(component => component != null);
        foreach (Component component in allComponents)
        {
			//If we already have the method for this value, continue
			Type componentType = component.GetType();
            if (fields.ZenModeBool == null) fields.ZenModeBool = componentType.GetField("TwitchZenMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            //If we already have the method for this value, continue
            if (fields.TimeModeBool == null) fields.TimeModeBool = componentType.GetField("TimeModeActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            //Unfortunately if both values aren't in the module, we have to go through this for loop each time
            //Should probably set a variable somewhere to not go through the loop if we already know it's empty
            if (fields.ZenModeBool != null && fields.TimeModeBool != null) break;
        }
        if (fields.ZenModeBool != null && fields.ZenModeBool.FieldType == typeof(bool)) {
            //Assign the current mode to the variable. Since this is usually a boolean value, this needs to be an if/else statement
            fields.ZenModeBool.SetValue(KMModule.GetComponentInChildren(fields.ZenModeBool.FieldType), Tweaks.settings.Mode == Mode.Zen);
        }
        if (fields.TimeModeBool != null && fields.TimeModeBool.FieldType == typeof(bool))
        {
            //Since I combined Time/Zen together here, Type needs to be separated by two variables as to not cause exceptions when loading.
            //As such, I assign i * 2 to Zen types and i * 2 + 1 to Time types, assuming they would ever be different.
            //This is because many modules only include one of the variables, and the type would continue changing even after zen mode was assigned.
            fields.TimeModeBool.SetValue(KMModule.GetComponentInChildren(fields.TimeModeBool.FieldType), Tweaks.settings.Mode == Mode.Time);
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
