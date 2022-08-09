﻿using System.Collections.Generic;
using UnityEngine;

public abstract class ModuleLogging : ModuleTweak
{
	private static readonly Dictionary<string, int> idCounters = new Dictionary<string, int>();
	public readonly int moduleID;
	private readonly string logName;

	protected ModuleLogging(BombComponent bombComponent, string componentString, string logName) : base(bombComponent, componentString)
	{
		if (!idCounters.ContainsKey(logName)) idCounters[logName] = 1;
		moduleID = idCounters[logName]++;
		this.logName = logName;
	}

	internal void Log(string message)
	{
		Debug.Log($"[{logName} #{moduleID}] {message}");
	}
}