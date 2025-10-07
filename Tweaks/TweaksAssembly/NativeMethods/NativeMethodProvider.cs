using System;
using UnityEngine;

namespace TweaksAssembly.Patching.NativeMethods
{
	public abstract class NativeMethodProvider
	{
		public abstract IntPtr mono_domain_get();
		public abstract IntPtr mono_compile_method(IntPtr method);
		public abstract IntPtr mono_jit_info_table_find(IntPtr domain, IntPtr method);

		public bool Test()
		{
			try
			{
				mono_domain_get();
				return true;
			}
			catch(Exception e)
			{
				Debug.LogError("[Tweaks] Native method call failed");
				Debug.LogException(e);
				return false;
			}
		}
	}
}