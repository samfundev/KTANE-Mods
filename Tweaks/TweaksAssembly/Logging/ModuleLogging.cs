using System;
using UnityEngine;

public abstract class ModuleLogging
{
	public static string moduleType;
	protected static Type componentType;
	protected Component component;

	protected readonly BombComponent bombComponent = null;

	public ModuleLogging(BombComponent BombComponent)
	{
		bombComponent = BombComponent;
	}
}