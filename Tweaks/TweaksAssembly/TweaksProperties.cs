class TweaksProperties : PropertiesBehaviour
{
	public TweaksProperties()
	{
		AddProperty("Mode", new Property(() => Tweaks.settings.Mode, value =>
		{
			Tweaks.settings.Mode = (Mode) value;
			Tweaks.modConfig.Settings = Tweaks.settings;
		}));
        //Compatibility for mods looking for bool values for each mode
        AddProperty("TimeMode", new Property(() => Tweaks.settings.Mode.Equals(Mode.Time), value =>
        {
            if ((bool)value) Tweaks.settings.Mode = Mode.Time;
            //If statement here in case Zen Mode was already set
            else if (Tweaks.settings.Mode != Mode.Zen) Tweaks.settings.Mode = Mode.Normal;
        }));
        AddProperty("ZenMode", new Property(() => Tweaks.settings.Mode.Equals(Mode.Zen), value =>
        {
            if ((bool)value) Tweaks.settings.Mode = Mode.Zen;
            //If statement here in case Time Mode was already set
            else if (Tweaks.settings.Mode != Mode.Time) Tweaks.settings.Mode = Mode.Normal;
        }));
		AddProperty("TimeModeStartingTime", new Property(() => Modes.settings.TimeModeStartingTime, value =>
		{
			Modes.settings.TimeModeStartingTime = (float) value;
			Modes.modConfig.Settings = Modes.settings;
		}));
        AddProperty("ZenModeTimePenalty", new Property(() => Modes.settings.ZenModeTimePenalty, value =>
        {
            Modes.settings.ZenModeTimePenalty = (float) value;
            Modes.modConfig.Settings = Modes.settings;
        }));
	}
}