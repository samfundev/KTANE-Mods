static class TweaksAPI
{
	private static object[] TwitchProperties;

	public static void Setup()
	{
		// These properties are shared with TP and will get disabled so that TP can handle them when it's active.
		TwitchProperties = new[]
		{
			ModdedAPI.AddProperty("Mode", () => Tweaks.userSettings.Mode, value =>
			{
				Tweaks.userSettings.Mode = (Mode) value;
				UpdateSettingsAndFreeplay();
			}),
			ModdedAPI.AddProperty("TimeMode", () => Tweaks.userSettings.Mode.Equals(Mode.Time), value => {
				Tweaks.userSettings.Mode = (bool) value ? Mode.Time : Mode.Normal;
				UpdateSettingsAndFreeplay();
			}),
			ModdedAPI.AddProperty("ZenMode", () => Tweaks.userSettings.Mode.Equals(Mode.Zen), value => {
				Tweaks.userSettings.Mode = (bool) value ? Mode.Zen : Mode.Normal;
				UpdateSettingsAndFreeplay();
			}),
			ModdedAPI.AddProperty("TimeModeStartingTime", () => Modes.settings.TimeModeStartingTime, value =>
			{
				Modes.settings.TimeModeStartingTime = (float) value;
				UpdateSettingsAndFreeplay();
			}),
			ModdedAPI.AddProperty("ExplodeBomb", null, value =>
			{
				if (value is string stringValue)
					Tweaks.bombWrappers[0].CauseStrikesToExplosion(stringValue);
			}),
		};

		ModdedAPI.AddProperty("SteadyMode", () => Tweaks.userSettings.Mode.Equals(Mode.Steady), value =>
		{
			Tweaks.userSettings.Mode = (bool) value ? Mode.Steady : Mode.Normal;
			UpdateSettingsAndFreeplay();
		});
		ModdedAPI.AddProperty("ZenModeTimePenalty", () => Modes.settings.ZenModeTimePenalty, value =>
		{
			Modes.settings.ZenModeTimePenalty = (float) value;
			Modes.modConfig.Write(Modes.settings);
		});
	}

	public static void SetTPProperties(bool enabled)
	{
		foreach (object property in TwitchProperties)
		{
			ModdedAPI.SetEnabled(property, enabled);
		}
	}

	private static void UpdateSettingsAndFreeplay()
	{
		Tweaks.UpdateSettings(false);
		Tweaks.Instance.StartCoroutine(Tweaks.ModifyFreeplayDevice(false));
	}
}