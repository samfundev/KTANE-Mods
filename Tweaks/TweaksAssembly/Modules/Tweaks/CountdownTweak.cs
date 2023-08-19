using UnityEngine;

class CountdownTweak : ModuleTweak
{
	public CountdownTweak(BombComponent bombComponent, string scriptName) : base(bombComponent, scriptName)
	{
		// Resets the most recently made answer variable to prevent extremely rare solve on strike bug
		bombComponent.OnStrike += (_) => {
			component.SetValue("mostRecentSolve", 0);
			return false;
		};
	}
}