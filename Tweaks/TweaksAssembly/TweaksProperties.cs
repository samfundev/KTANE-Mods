class TweaksProperties : PropertiesBehaviour
{
	public TweaksProperties()
	{
		AddProperty("Mode", new Property(() => Tweaks.settings.Mode, value =>
		{
			Tweaks.settings.Mode = (Mode) value;
			Tweaks.modConfig.Settings = Tweaks.settings;
		}));
		AddProperty("TimeModeStartingTime", new Property(() => Modes.settings.TimeModeStartingTime, value =>
		{
			Modes.settings.TimeModeStartingTime = (float) value;
			Modes.modConfig.Settings = Modes.settings;
		}));
        AddProperty("ZenModeTimePenalty", new Property(() => Modes.settings.ZenModeTimePenalty, value =>
        {
            Modes.settings.ZenModeTimePenalty = (string) value;
            Modes.modConfig.Settings = Modes.settings;
        }));
	}
}