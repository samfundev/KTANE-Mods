using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MicrocontrollerLogging : ModuleLogging
{
	public MicrocontrollerLogging(BombComponent bombComponent) : base(bombComponent, "Microcontroller", "Microcontroller")
	{

		bombComponent.GetComponent<KMBombModule>().OnActivate += () =>
		{
			Log("Microcontroller log initialization");
		};

		bombComponent.GetComponent<KMBombModule>().OnPass += () =>
		{
			Log("Module Solved");
			return false;
		};

		bombComponent.GetComponent<KMBombModule>().OnStrike += () =>
		{
			Log("Module Stiked");
			return false;
		};
	}
}