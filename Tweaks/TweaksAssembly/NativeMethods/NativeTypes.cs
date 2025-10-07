using System;
using System.Runtime.InteropServices;

namespace TweaksAssembly.Patching.NativeMethods
{
	public static class NativeTypes
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct MonoJitInfo
		{
			public IntPtr method;
			public IntPtr next_jit_code_hash;
			public IntPtr code_start;
			public uint used_regs;
			public int code_size;
			public uint bitfield;
			public IntPtr clauses;
		}
	}
}