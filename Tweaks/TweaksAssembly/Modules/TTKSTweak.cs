class TTKSTweak : ModuleTweak
{
	public TTKSTweak(BombComponent bombComponent) : base(bombComponent, "TurnKeyAdvancedModule")
	{
		componentType.SetValue("LeftAfterA", new string[]
		{
			"Password",
			"Crazy Talk",
			"Who's on First",
			"Keypad",
			"Listening",
			"Orientation Cube"
		});
	}
}