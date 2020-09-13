using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class ModuleTweak
{
	protected static Dictionary<string, Type> componentTypes = new Dictionary<string, Type>();
	protected readonly Type componentType;
	protected readonly Component component;
	protected readonly BombComponent bombComponent;

	protected ModuleTweak(BombComponent bombComponent, string componentString)
	{
		if (!componentTypes.ContainsKey(componentString))
			componentTypes[componentString] = ReflectionHelper.FindType(componentString);

		componentType = componentTypes[componentString];
		component = bombComponent.GetComponent(componentType);

		this.bombComponent = bombComponent;
	}
}