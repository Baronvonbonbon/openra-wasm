#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.Web
{
	/// <summary>
	/// Populates the engine's virtual filesystem. The runtime provides an in-memory
	/// filesystem that System.IO works against normally, but nothing is in it at
	/// startup: the page fetches the engine files and game content over the network
	/// and hands them here, and the engine then reads them as ordinary paths.
	/// </summary>
	internal static partial class BrowserFileSystem
	{
		/// <summary>Root of the virtual filesystem holding the engine files.</summary>
		public const string EngineDir = "/openra/engine/";

		/// <summary>Mirrors <see cref="Platform.GetSupportDir"/> for the browser.</summary>
		public const string SupportDir = "/openra/support/";

		static int fileCount;
		static long byteCount;

		/// <summary>
		/// Creates the filesystem roots and points the engine at them. Must be called
		/// before anything reads Platform.EngineDir or Platform.SupportDir, since
		/// those latch on first access.
		/// </summary>
		[JSExport]
		public static string Initialize()
		{
			try
			{
				Directory.CreateDirectory(EngineDir);
				Directory.CreateDirectory(SupportDir);

				Platform.OverrideEngineDir(EngineDir);
				Platform.OverrideSupportDir(SupportDir);

				return $"OK|{Platform.CurrentPlatform}|{Platform.EngineDir}|{Platform.SupportDir}";
			}
			catch (Exception e)
			{
				return $"FAIL|{e.GetType().Name}: {e.Message}";
			}
		}

		/// <summary>Writes a file into the virtual filesystem, creating parent directories.</summary>
		[JSExport]
		public static string Mount(string path, byte[] data)
		{
			try
			{
				var full = Path.Combine(EngineDir, path);
				var directory = Path.GetDirectoryName(full);
				if (!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);

				File.WriteAllBytes(full, data);

				fileCount++;
				byteCount += data.Length;
				return "OK";
			}
			catch (Exception e)
			{
				return $"FAIL|{path}: {e.GetType().Name}: {e.Message}";
			}
		}

		/// <summary>Reports what has been mounted, for verification.</summary>
		[JSExport]
		public static string Describe() => $"{fileCount}|{byteCount}";

		/// <summary>
		/// Reads a mounted file back through ordinary System.IO, confirming the engine
		/// can reach it the same way it does on desktop.
		/// </summary>
		[JSExport]
		public static string VerifyReadable(string path)
		{
			try
			{
				var full = Path.Combine(EngineDir, path);
				if (!File.Exists(full))
					return $"FAIL|{full} does not exist";

				return $"OK|{new FileInfo(full).Length}";
			}
			catch (Exception e)
			{
				return $"FAIL|{e.GetType().Name}: {e.Message}";
			}
		}
	}
}
