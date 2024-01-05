using Assets.Scripts.Missions;

class TTKSTweak : ModuleTweak
{
	public TTKSTweak(BombComponent bombComponent) : base(bombComponent, "TurnKeyAdvancedModule")
	{
		componentType.SetValue("LeftAfterA", new string[]
		{
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Password),
			"Passwords Translated",
			"Crazy Talk",
			"Crazy Talk Translated",
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.WhosOnFirst),
			"Who's on First Translated",
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Keypad),
			"Listening",
			"Orientation Cube"
		});
		componentType.SetValue("LeftBeforeA", new string[]
		{
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Maze),
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Memory),
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Venn),
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.WireSequence),
			"Cryptography"
		});
		componentType.SetValue("RightAfterA", new string[]
		{
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Morse),
			"Morse Code Translated",
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Wires),
			"Two Bits",
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.BigButton),
			"Big Button Translated",
			"Colour Flash",
			"Colour Flash ES",
			"Colour Flash PL",
			"Round Keypad"
		});
		componentType.SetValue("RightBeforeA", new string[]
		{
			"Semaphore",
			"Combination Lock",
			MissionUtil.GetLocalizedModuleName(ComponentTypeEnum.Simon),
			"Astrology",
			"Switches",
			"Plumbing"
		});
	}
}