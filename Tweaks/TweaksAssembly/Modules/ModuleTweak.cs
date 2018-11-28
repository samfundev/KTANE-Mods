using UnityEngine;

public abstract class ModuleTweak
{
	protected Component component;

	protected readonly BombComponent bombComponent = null;

	public ModuleTweak(BombComponent BombComponent)
	{
		bombComponent = BombComponent;
	}
}