using System;
using System.Runtime.InteropServices;

namespace TweaksAssembly.Patching.NativeMethods
{
	public class OSXMethodProvider : NativeMethodProvider
	{
		[DllImport("libmono.0.dylib", EntryPoint = "mono_domain_get", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr _mono_domain_get();
		
		[DllImport("libmono.0.dylib", EntryPoint = "mono_compile_method", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr _mono_compile_method(IntPtr method);
	
		[DllImport("libmono.0.dylib", EntryPoint = "mono_jit_info_table_find", CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr _mono_jit_info_table_find(IntPtr domain, IntPtr method);

		public override IntPtr mono_domain_get() => _mono_domain_get();
		public override IntPtr mono_compile_method(IntPtr method) => _mono_compile_method(method);
		public override IntPtr mono_jit_info_table_find(IntPtr domain, IntPtr method) => _mono_jit_info_table_find(domain, method);
	}
}