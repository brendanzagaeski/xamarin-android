// Adapted from https://github.com/xamarin/xamarin-android/blob/885b57bdcf32e559961b183e1537844c5aa8143e/src/Xamarin.Android.Build.Tasks/Utilities/MonoAndroidHelper.cs

using System;
using System.IO;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public partial class MonoAndroidHelper
	{
		public static bool IsMonoAndroidAssembly (ITaskItem assembly)
		{
			var tfi = assembly.GetMetadata ("TargetFrameworkIdentifier");
			if (string.Compare (tfi, "MonoAndroid", StringComparison.OrdinalIgnoreCase) == 0)
				return true;

			var hasReference = assembly.GetMetadata ("HasMonoAndroidReference");
			return bool.TryParse (hasReference, out bool value) && value;
		}

		public static bool IsForceRetainedAssembly (string assembly)
		{
			switch (assembly) {
				case "Mono.Android.Export.dll": // this is totally referenced by reflection.
					return true;
			}
			return false;
		}

		public static void SetWriteable (string source)
		{
			if (!File.Exists (source))
				return;

			var fileInfo = new FileInfo (source);
			if (fileInfo.IsReadOnly)
				fileInfo.IsReadOnly = false;
		}

		public static bool CopyAssemblyAndSymbols (string source, string destination)
		{
			bool changed = CopyIfChanged (source, destination);
			var mdb = source + ".mdb";
			if (File.Exists (mdb)) {
				var mdbDestination = destination + ".mdb";
				CopyIfChanged (mdb, mdbDestination);
			}
			var pdb = Path.ChangeExtension (source, "pdb");
			if (File.Exists (pdb) && Files.IsPortablePdb (pdb)) {
				var pdbDestination = Path.ChangeExtension (destination, "pdb");
				CopyIfChanged (pdb, pdbDestination);
			}
			return changed;
		}

		public static bool CopyIfChanged (string source, string destination)
		{
			return Files.CopyIfChanged (source, destination);
		}
	}
}
