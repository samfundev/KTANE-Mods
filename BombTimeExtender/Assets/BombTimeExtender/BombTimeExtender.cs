using UnityEngine;
using System;
using System.Reflection;

[RequireComponent(typeof(KMService))]
public class BombTimeExtender : MonoBehaviour
{
	public static Type FindType(string qualifiedTypeName)
	{
		Type t = Type.GetType(qualifiedTypeName);

		if (t != null)
		{
			return t;
		}
		else
		{
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				t = asm.GetType(qualifiedTypeName);
				if (t != null)
					return t;
			}
			return null;
		}
	}

	void Start()
	{
		Type freeplayDeviceType = FindType("FreeplayDevice");
		freeplayDeviceType.GetField("MAX_SECONDS_TO_SOLVE", BindingFlags.Static | BindingFlags.Public).SetValue(GameObject.FindObjectOfType(freeplayDeviceType), float.MaxValue);
	}
}
