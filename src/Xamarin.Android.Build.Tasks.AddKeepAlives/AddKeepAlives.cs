// Based on https://github.com/xamarin/xamarin-android/blob/885b57bdcf32e559961b183e1537844c5aa8143e/src/Xamarin.Android.Build.Tasks/Tasks/LinkAssembliesNoShrink.cs

using System;
using System.Diagnostics;
using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using MonoDroid.Tuner;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Tasks
{
	public class AddKeepAlives : Task
	{
		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem [] SourceFiles { get; set; }

		[Required]
		public ITaskItem [] DestinationFiles { get; set; }

		public bool Deterministic { get; set; }

		public override bool Execute ()
		{
			var rp = new ReaderParameters {
				InMemory = true,
			};
			var writerParameters = new WriterParameters {
				DeterministicMvid = Deterministic,
			};

			using (var resolver = new DirectoryAssemblyResolver (
				(Action<TraceLevel, string>) ((level, value) => { Log.LogMessage (MessageImportance.Low, "{0}", value); }),
				loadDebugSymbols: false,
				loadReaderParameters: rp)) {

				// Add SearchDirectories with ResolvedAssemblies
				foreach (var assembly in ResolvedAssemblies) {
					var path = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
					if (!resolver.SearchDirectories.Contains(path))
						resolver.SearchDirectories.Add(path);
				}

				var step = new AddKeepAlivesStep (resolver, new TypeDefinitionCache ());
				for (int i = 0; i < SourceFiles.Length; i++) {
					var source = SourceFiles [i];
					var destination = DestinationFiles [i];
					AssemblyDefinition assemblyDefinition = resolver.GetAssembly (source.ItemSpec);

					if (step.AddKeepAlives (assemblyDefinition)) {
						Log.LogMessage (MessageImportance.Low, $"Saving modified assembly: {destination.ItemSpec}");
						writerParameters.WriteSymbols = assemblyDefinition.MainModule.HasSymbols;
						assemblyDefinition.Write (destination.ItemSpec, writerParameters);
						continue;
					}

					if (MonoAndroidHelper.CopyAssemblyAndSymbols (source.ItemSpec, destination.ItemSpec)) {
						Log.LogMessage (MessageImportance.Low, $"Copied: {destination.ItemSpec}");
					} else {
						Log.LogMessage (MessageImportance.Low, $"Skipped unchanged file: {destination.ItemSpec}");

						// NOTE: We still need to update the timestamp on this file, or this target would run again
						File.SetLastWriteTimeUtc (destination.ItemSpec, DateTime.UtcNow);
					}
				}
			}

			return true;
		}
	}
}
