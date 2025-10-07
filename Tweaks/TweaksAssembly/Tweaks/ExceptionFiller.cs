using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using System.Runtime.InteropServices;
using TweaksAssembly.Patching;
using TweaksAssembly.Patching.NativeMethods;
using Debug = UnityEngine.Debug;

public class ExceptionFiller : Tweak
{
	internal class OffsetMap
	{
		internal int NativeOffset;
		internal int ILOffset;
		internal int DocumentID;
		internal int StartLine;
		internal int StartCol;
		
		public override string ToString() => $"{DebugLine}{ILOffset}_{DocumentID}_{StartLine}_{StartCol}";

		internal bool IsEqual(OffsetMap other) => ILOffset == other.ILOffset && DocumentID == other.DocumentID &&
		                                          StartLine == other.StartLine && StartCol == other.StartCol;

		internal OffsetMap(string line)
		{
			var numbers = line.Substring(DebugLine.Length).Split('_').Select(int.Parse).ToArray();
			ILOffset = numbers[0];
			DocumentID = numbers[1];
			StartLine = numbers[2];
			StartCol = numbers[3];
		}
	}

	internal class AssemblyMap
	{
		internal Type DebugLinesAttributeType;
		internal FieldInfo DebugLinesAttributeField;
		internal string[] SourceFiles;
	}
	
	private const string UnknownFile = "[0x00000] in <filename unknown>:0";
	private const string DebugLine = "_ktane_debugline_";

	private static readonly int StringHeaderSize;
	private static readonly byte AbsoluteAddressInstruction1;
	private static readonly byte AbsoluteAddressInstruction2;
	private static readonly byte MetadataTokenInstruction;

	internal static bool _ShouldEnable => Tweaks.settings.FillExceptionLines && Tweaks.nativeMethods != null &&
	                                      (IntPtr.Size == 8 || IntPtr.Size == 4);
	public override bool ShouldEnable => _ShouldEnable;
	
	private static Dictionary<Assembly, AssemblyMap> assemblyMap = new  Dictionary<Assembly, AssemblyMap>();
	private static Dictionary<MethodBase, OffsetMap[]> methodOffsetMap = new Dictionary<MethodBase, OffsetMap[]>();
	private static Dictionary<long, string> absoluteDebugLines = new Dictionary<long, string>();
	
	private static bool ProcessAssembly(Assembly assembly)
	{
		if (assemblyMap.ContainsKey(assembly))
			return true;
		var debugType = assembly.GetType("KModkit.Internal.DebugInfo");
		if (debugType == null)
			return false;
		var debugLinesAttribute = assembly.GetType("KModkit.Internal.DebugLinesAttribute");
		if (debugLinesAttribute == null)
			return false;
		var debugLinesAttributeField = debugLinesAttribute.GetField("debugLines", BindingFlags.NonPublic | BindingFlags.Instance);
		if (debugLinesAttributeField == null)
			return false;
		var debugLinesMethod = debugType.GetMethod("DebugLines", BindingFlags.NonPublic | BindingFlags.Static);
		if (debugLinesMethod == null)
			return false;
		try
		{
			foreach (var line in (string[])debugLinesMethod.Invoke(null, new object[0]))
			{
				var interned = string.Intern(line);
				var handle = GCHandle.Alloc(interned, GCHandleType.Pinned);
				var ptr = handle.AddrOfPinnedObject();
				handle.Free();
				var address = ptr.ToInt64() - StringHeaderSize;
				if(!absoluteDebugLines.ContainsKey(address))
					absoluteDebugLines.Add(address, interned);
			}
		}
		catch
		{
			return false;
		}
		var offsetMethod = debugType.GetMethod("SourceDocuments", BindingFlags.NonPublic | BindingFlags.Static);
		if (offsetMethod == null)
			return false;
		try
		{
			assemblyMap.Add(assembly, new AssemblyMap
			{
				DebugLinesAttributeType = debugLinesAttribute,
				DebugLinesAttributeField = debugLinesAttributeField,
				SourceFiles = (string[])offsetMethod.Invoke(null, new object[0])
			});
			return true;
		}
		catch
		{
			return false;
		}
	}
	
	private static void ProcessMethod(MethodBase method)
	{
		if (!ProcessAssembly(method.Module.Assembly))
			return;
		if (methodOffsetMap.ContainsKey(method))
			return;

		var asmMap = assemblyMap[method.Module.Assembly];
		var debugLinesAttribute = Attribute.GetCustomAttribute(method, asmMap.DebugLinesAttributeType);
		if (debugLinesAttribute is null)
			return;
		var debugLines = (string[])asmMap.DebugLinesAttributeField.GetValue(debugLinesAttribute);
		var debugMap = debugLines.Select(line => new OffsetMap(line)).ToArray();
		var jitInfo = (NativeTypes.MonoJitInfo) Marshal.PtrToStructure(Tweaks.nativeMethods.mono_jit_info_table_find(
			Tweaks.nativeMethods.mono_domain_get(),
			Tweaks.nativeMethods.mono_compile_method(method.MethodHandle.Value)), typeof(NativeTypes.MonoJitInfo));
		var methodBytes = new byte[jitInfo.code_size];
		Marshal.Copy(jitInfo.code_start, methodBytes, 0, methodBytes.Length);
		var windowSize = IntPtr.Size;
		var tokenOffset = BitConverter.IsLittleEndian ? 3 : 0;
		var foundLines = new List<OffsetMap>();
		for (int i = 3; i < methodBytes.Length - 3; i++)
		{
			string offsetString = null;
			var num = BitConverter.ToInt32(methodBytes, i);
			
			//Absolute addresses
			if (i <= methodBytes.Length - windowSize && methodBytes[i-1] == AbsoluteAddressInstruction2 && (windowSize == 4 || methodBytes[i-2] == AbsoluteAddressInstruction1))
			{
				var address = windowSize == 8
					? BitConverter.ToInt64(methodBytes, i)
					: (long) num;
				if (absoluteDebugLines.TryGetValue(address, out offsetString))
					goto ProcessOffset;
			}
			
			//Metadata tokens
			if (methodBytes[i-1] != MetadataTokenInstruction || methodBytes[i + tokenOffset] != 0x00)
				continue;
			var token = num | 0x70000000;	//String metadata tokens start with a 0x70 byte, but it's omitted in the machine code
			try
			{
				offsetString = method.Module.ResolveString(token);
			}
			catch (Exception e)
			{
				if (!(e is ArgumentException || e is ArgumentOutOfRangeException))
					throw;
			}
			
			ProcessOffset:
			if (string.IsNullOrEmpty(offsetString) || !offsetString.StartsWith(DebugLine) || !debugLines.Contains(offsetString))
				continue;
			try
			{
				var map = new OffsetMap(offsetString);
				map.NativeOffset = i;
				foundLines.Add(map);
			}
			catch
			{
			}
		}
		
		var offsets = LCS(debugMap, foundLines.OrderBy(offset => offset.ILOffset).ToArray());
		methodOffsetMap.Add(method, offsets);
	}

	private static OffsetMap[] LCS(OffsetMap[] A, OffsetMap[] B)
	{
		//Use longest common subsequence as heuristic to filter out potential fake debug lines
		var n = A.Length;
		var m = B.Length;
		int[,] dp = new int[n + 1, m + 1];
		for (int i = 1; i <= n; i++)
		{
			for (int j = 1; j <= m; j++)
			{
				if (A[i - 1].IsEqual(B[j - 1]))
					dp[i, j] = dp[i - 1, j - 1] + 1;
				else dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
			}
		}

		var index = dp[n, m];
		var final = new OffsetMap[index];
		var k = n;
		var l = m;
		while (k > 0 && l > 0)
		{
			if (A[k - 1].IsEqual(B[l - 1]))
			{
				final[index - 1] = B[l - 1];
				k--;
				l--;
				index--;
			}
			else if (dp[k - 1, l] > dp[k, l - 1])
				k--;
			else l--;
		}
		return final.OrderBy(offset => offset.NativeOffset).ToArray();
	}

	private static OffsetMap GetOffsetData(StackFrame frame)
	{
		var method = frame.GetMethod();
		var nativeOffset = frame.GetNativeOffset();
		if (!method.HasMethodBody())
			return null;
		ProcessMethod(method);
		if (!methodOffsetMap.TryGetValue(method, out var offsets))
			return null;
		if (offsets.Length == 0)
			return null;
		for (int i = 0; i < offsets.Length; i++)
		{
			var offset = offsets[i];
			if (offset.NativeOffset > nativeOffset)
			{
				if (i == 0)
					return offsets[0];
				return offsets[i - 1];
			}
		}
		return offsets[offsets.Length - 1];
	}

	private static string FillException(Exception exc, string stackTrace)
	{
		var lines = stackTrace.Split('\n');
		var trace = new StackTrace(exc);
		var frames = Math.Min(lines.Length, trace.FrameCount);
		
		for (int i = 0; i < frames; i++)
		{
			if (!lines[i].Contains(UnknownFile))
				continue;
			var frame = trace.GetFrame(i);
			var offsetData = GetOffsetData(frame);
			if (offsetData is null)
				continue;
			var sourceFile = assemblyMap[frame.GetMethod().Module.Assembly].SourceFiles;
			if (sourceFile.Length <= offsetData.DocumentID)
				continue;
			lines[i] = lines[i].Replace(UnknownFile,
				$"(at {sourceFile[offsetData.DocumentID]}:{offsetData.StartLine}:{offsetData.StartCol})");
		}
		return string.Join("\n", lines);
	}

	public override void Setup()
	{
		Patching.EnsurePatch("ExceptionFiller", typeof(StackTracePatch));
	}

	public override void Teardown()
	{
		assemblyMap.Clear();
		methodOffsetMap.Clear();
		absoluteDebugLines.Clear();
	}

	static ExceptionFiller()
	{
		if (IntPtr.Size == 8)	//x86/64
		{
			StringHeaderSize = 20;
			
			//String reference by address: movabs rdi, <address>
			AbsoluteAddressInstruction1 = 0x48;
			AbsoluteAddressInstruction2 = 0xBF;
			
			//String reference by metadata token: mov esi, <token>
			MetadataTokenInstruction = 0xBE;
		}
		else	//x86/32
		{
			StringHeaderSize = 12;
			
			//String reference by address: push <address>
			AbsoluteAddressInstruction1 = 0x00;		//Unused
			AbsoluteAddressInstruction2 = 0x68;
			
			//String reference by metadata token: push <token>
			MetadataTokenInstruction = 0x68;
		}
	}
	
	[HarmonyPatch(typeof(Exception), nameof(Exception.StackTrace), MethodType.Getter)]
	static class StackTracePatch
	{
		private static bool enable = true;
		
		static void Postfix(Exception __instance, ref string __result)
		{
			if (!_ShouldEnable || !enable)
				return;
			var original = __result;
			try
			{
				__result = FillException(__instance, __result);
			}
			catch (Exception e)
			{
				Debug.LogError($"[Tweaks] [ExceptionFiller] An exception occurred while processing the stack trace");
				enable = false;
				Debug.LogException(e);
				enable = true;
				__result = original;
			}
		}
	}
}