class TweaksProperties : PropertiesBehaviour
{
	public TweaksProperties()
	{
		AddProperty("Mode", new Property(() => Tweaks.userSettings.Mode, value =>
		{
			Tweaks.userSettings.Mode = (Mode) value;
			Tweaks.UpdateSettings(false);
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
        AddProperty("TimeMode", new Property(() => Tweaks.userSettings.Mode.Equals(Mode.Time), value => {
			Tweaks.userSettings.Mode = (bool) value ? Mode.Time : Mode.Normal;
			Tweaks.UpdateSettings(false);
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
        AddProperty("ZenMode", new Property(() => Tweaks.userSettings.Mode.Equals(Mode.Zen), value => {
			Tweaks.userSettings.Mode = (bool) value ? Mode.Zen : Mode.Normal;
			Tweaks.UpdateSettings(false);
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
		AddProperty("SteadyMode", new Property(() => Tweaks.userSettings.Mode.Equals(Mode.Steady), value => {
			Tweaks.userSettings.Mode = (bool) value ? Mode.Steady : Mode.Normal;
			Tweaks.UpdateSettings(false);
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
		AddProperty("TimeModeStartingTime", new Property(() => Modes.settings.TimeModeStartingTime, value =>
		{
			Modes.settings.TimeModeStartingTime = (float) value;
			Modes.modConfig.Write(Modes.settings);
			StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
		}));
        AddProperty("ZenModeTimePenalty", new Property(() => Modes.settings.ZenModeTimePenalty, value =>
        {
            Modes.settings.ZenModeTimePenalty = (float) value;
            Modes.modConfig.Write(Modes.settings);
        }));
	}
}