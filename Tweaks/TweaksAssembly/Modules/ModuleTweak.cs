using System;
using UnityEngine;

public abstract class ModuleTweak
{
	public static string moduleType;
	protected static Type componentType;
	protected Component component;

	protected readonly BombComponent bombComponent = null;

	public ModuleTweak(BombComponent BombComponent)
	{
		bombComponent = BombComponent;
	}
}