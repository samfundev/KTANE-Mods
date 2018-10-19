class TweaksProperties : PropertiesBehaviour
{
	public TweaksProperties()
	{
		AddProperty("Mode", new Property(() => Tweaks.settings.Mode, value =>
		{
			Tweaks.settings.Mode = (Mode) value;
			Tweaks.modConfig.Settings = Tweaks.settings;
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
        AddProperty("TimeMode", new Property(() => Tweaks.settings.Mode.Equals(Mode.Time), value => {
			Tweaks.settings.Mode = (bool) value ? Mode.Time : Mode.Normal;
			Tweaks.modConfig.Settings = Tweaks.settings;
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
        AddProperty("ZenMode", new Property(() => Tweaks.settings.Mode.Equals(Mode.Zen), value => {
			Tweaks.settings.Mode = (bool) value ? Mode.Zen : Mode.Normal;
			Tweaks.modConfig.Settings = Tweaks.settings;
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
		AddProperty("TimeModeStartingTime", new Property(() => Modes.settings.TimeModeStartingTime, value =>
		{
			Modes.settings.TimeModeStartingTime = (float) value;
			Modes.modConfig.Settings = Modes.settings;
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
        AddProperty("ZenModeTimePenalty", new Property(() => Modes.settings.ZenModeTimePenalty, value =>
        {
            Modes.settings.ZenModeTimePenalty = (float) value;
            Modes.modConfig.Settings = Modes.settings;
        }));
	}
}