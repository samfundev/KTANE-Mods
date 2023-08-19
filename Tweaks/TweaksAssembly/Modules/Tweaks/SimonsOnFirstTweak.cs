class SimonsOnFirstTweak : ModuleTweak
{
	public SimonsOnFirstTweak(BombComponent bombComponent) : base(bombComponent, "SimonsOnFirstScript")
	{
		bombComponent.OnPass += (_) => {
			// Make sure that the flashing coroutine is stopped when the module is solved by setting this variable to true
			component.SetValue("interacting", true);
			return false;
		};
	}
}