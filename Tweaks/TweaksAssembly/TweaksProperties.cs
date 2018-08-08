class TweaksProperties : PropertiesBehaviour
{
	public TweaksProperties()
	{
		AddProperty("TimeMode", new Property(() => Tweaks.settings.TimeMode, value =>
		{
			Tweaks.settings.TimeMode = (bool) value;
			Tweaks.modConfig.Settings = Tweaks.settings;
		}));
		AddProperty("TimeModeStartingTime", new Property(() => TimeMode.settings.TimeModeStartingTime, value =>
		{
			TimeMode.settings.TimeModeStartingTime = (float) value;
			TimeMode.modConfig.Settings = TimeMode.settings;
		}));
	}
}