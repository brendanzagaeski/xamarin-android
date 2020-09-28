// Adapted from src/Xamarin.Android.Build.Tasks/Utilities/Files.cs

using System;
using System.IO;
using System.Security.Cryptography;
using Java.Interop.Tools.JavaCallableWrappers;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Tools
{
	static class Files {
		public static bool CopyIfChanged (string source, string destination)
		{
			if (HasFileChanged (source, destination)) {
				var directory = Path.GetDirectoryName (destination);
				if (!string.IsNullOrEmpty (directory))
					Directory.CreateDirectory (directory);

				if (!Directory.Exists (source)) {
					MonoAndroidHelper.SetWriteable (destination);
					File.Delete (destination);
					File.Copy (source, destination);
					MonoAndroidHelper.SetWriteable (destination);
					File.SetLastWriteTimeUtc (destination, DateTime.UtcNow);
					return true;
				}
			}

			return false;
		}

		// This is for if the file contents have changed.  Often we have to
		// regenerate a file, but we don't want to update it if hasn't changed
		// so that incremental build is as efficient as possible
		public static bool HasFileChanged (string source, string destination)
		{
			// If either are missing, that's definitely a change
			if (!File.Exists (source) || !File.Exists (destination))
				return true;

			var src_hash = HashFile (source);
			var dst_hash = HashFile (destination);

			// If the hashes don't match, then the file has changed
			if (src_hash != dst_hash)
				return true;

			return false;
		}

		public static string HashFile (string filename)
		{
			using (HashAlgorithm hashAlg = new Crc64 ()) {
				return HashFile (filename, hashAlg);
			}
		}

		public static string HashFile (string filename, HashAlgorithm hashAlg)
		{
			using (Stream file = new FileStream (filename, FileMode.Open, FileAccess.Read)) {
				byte [] hash = hashAlg.ComputeHash (file);
				return ToHexString (hash);
			}
		}

		public static string ToHexString (byte [] hash)
		{
			char [] array = new char [hash.Length * 2];
			for (int i = 0, j = 0; i < hash.Length; i += 1, j += 2) {
				byte b = hash [i];
				array [j] = GetHexValue (b / 16);
				array [j + 1] = GetHexValue (b % 16);
			}
			return new string (array);
		}

		static char GetHexValue (int i) => (char) (i < 10 ? i + 48 : i - 10 + 65);

		const uint ppdb_signature = 0x424a5342;

		public static bool IsPortablePdb (string filename)
		{
			try {
				using (var fs = new FileStream (filename, FileMode.Open, FileAccess.Read)) {
					using (var br = new BinaryReader (fs)) {
						return br.ReadUInt32 () == ppdb_signature;
					}
				}
			}
			catch {
				return false;
			}
		}
	}
}

