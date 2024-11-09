using System;

class ParallelMazesTweak : ModuleTweak
{
	public ParallelMazesTweak(BombComponent bombComponent) : base(bombComponent, "ParallelMazesModule")
	{
		// Attempt to change the dead server URL to a valid one
		Type t = ReflectionHelper.FindType("WSClient");
		component.GetValue<object>("_client").SetValue("WS", t.GetConstructor(new[] { typeof(string) }).Invoke(new[] { "ws://parallelmazes.eltrick.uk:3000" }));
		component.CallMethod("Start");
	}
}