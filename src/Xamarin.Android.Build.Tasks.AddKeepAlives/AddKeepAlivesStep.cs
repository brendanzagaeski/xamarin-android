using System.Collections.Generic;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MonoDroid.Tuner
{
	public class AddKeepAlivesStep
	{
		readonly DirectoryAssemblyResolver resolver;
		readonly TypeDefinitionCache cache;

		public AddKeepAlivesStep (DirectoryAssemblyResolver resolver, TypeDefinitionCache cache)
		{
			this.resolver = resolver;
			this.cache = cache;
		}

		// Adapted from https://github.com/xamarin/xamarin-android/blob/885b57bdcf32e559961b183e1537844c5aa8143e/src/Xamarin.Android.Build.Tasks/Linker/MonoDroid.Tuner/FixAbstractMethodsStep.cs#L86-L97
		internal bool AddKeepAlives (AssemblyDefinition assembly)
		{
			// Java.Lang.Object is defined in Mono.Android.dll, so that assembly needs to be special-cased.
			if (!(assembly.MainModule.HasTypeReference ("Java.Lang.Object") || assembly.MainModule.Name == "Mono.Android.dll"))
				return false;
			bool changed = false;
			foreach (TypeDefinition type in assembly.MainModule.Types) {
				if (MightNeedFix (type))
					changed |= AddKeepAlives (type);
			}
			return changed;
		}

		// Copied from https://github.com/xamarin/xamarin-android/blob/885b57bdcf32e559961b183e1537844c5aa8143e/src/Xamarin.Android.Build.Tasks/Linker/MonoDroid.Tuner/FixAbstractMethodsStep.cs#L104-L107
		bool MightNeedFix (TypeDefinition type)
		{
			return !type.IsAbstract && type.IsSubclassOf ("Java.Lang.Object", cache);
		}

		// New code written for this task specifically
		bool AddKeepAlives (TypeDefinition type)
		{
			bool changed = false;
			foreach (MethodDefinition method in type.Methods) {
				if (!method.CustomAttributes.Any (a => a.AttributeType.FullName == "Android.Runtime.RegisterAttribute"))
					continue;
				ILProcessor processor = method.Body.GetILProcessor ();
				ModuleDefinition module = method.DeclaringType.Module;
				MethodDefinition methodKeepAlive = GetMethod ("mscorlib", "System.GC", "KeepAlive", new string [] { "System.Object" });
				Instruction end = method.Body.Instructions.Last ();
				if (end.Previous.OpCode == OpCodes.Endfinally)
					end = end.Previous;
				for (int i = 0; i < method.Parameters.Count; i++) {
					if (method.Parameters [i].ParameterType.IsValueType)
						continue;
					changed = true;
					processor.InsertBefore (end, GetLoadArgumentInstruction (method.IsStatic ? i : i + 1, method.Parameters [i]));
					processor.InsertBefore (end, Instruction.Create (OpCodes.Call, module.ImportReference (methodKeepAlive)));
				}
			}
			return changed;
		}

		// Adapted from https://github.com/xamarin/xamarin-android/blob/885b57bdcf32e559961b183e1537844c5aa8143e/src/Mono.Android.Export/Mono.CodeGeneration/CodeArgumentReference.cs#L44-L55
		static Instruction GetLoadArgumentInstruction (int argNum, ParameterDefinition parameter)
		{
			switch (argNum) {
				case 0: return Instruction.Create (OpCodes.Ldarg_0);
				case 1: return Instruction.Create (OpCodes.Ldarg_1);
				case 2: return Instruction.Create (OpCodes.Ldarg_2);
				case 3: return Instruction.Create (OpCodes.Ldarg_3);
				default: return Instruction.Create (OpCodes.Ldarg, parameter);
			}
		}

		// `Get` methods copied from https://github.com/xamarin/xamarin-android/blob/885b57bdcf32e559961b183e1537844c5aa8143e/src/Xamarin.Android.Build.Tasks/Linker/MonoDroid.Tuner/MonoDroidMarkStep.cs

		MethodDefinition GetMethod (string ns, string typeName, string name, string [] parameters)
		{
			var type = GetType (ns, typeName);
			if (type == null)
				return null;

			return GetMethod (type, name, parameters);
		}

		MethodDefinition GetMethod (TypeDefinition type, string name, string [] parameters)
		{
			MethodDefinition method = null;
			foreach (var md in type.Methods) {
				if (md.Name != name)
					continue;

				if (md.Parameters.Count != parameters.Length)
					continue;

				var equal = true;
				for (int i = 0; i < parameters.Length; i++) {
					if (md.Parameters [i].ParameterType.FullName != parameters [i]) {
						equal = false;
						break;
					}
				}

				if (!equal)
					continue;

				method = md;
				break;
			}

			return method;
		}

		AssemblyDefinition GetAssembly (string assemblyName)
		{
			return resolver.GetAssembly (assemblyName);
		}

		TypeDefinition GetType (string assemblyName, string typeName)
		{
			AssemblyDefinition ad = GetAssembly (assemblyName);
			return ad == null ? null : GetType (ad, typeName);
		}

		TypeDefinition GetType (AssemblyDefinition assembly, string typeName)
		{
			return assembly.MainModule.GetType (typeName);
		}
	}
}
