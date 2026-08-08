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
using System.Linq;
using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.Web
{
	/// <summary>
	/// Loads the Red Alert mod from the virtual filesystem, which exercises mod
	/// discovery, the manifest parser, the .mix package readers and the rule
	/// loader against the real game content.
	/// </summary>
	internal static partial class ModProbe
	{
		static ModData modData;

		[JSExport]
		public static string DiscoverMods()
		{
			try
			{
				var mods = new InstalledMods([BrowserFileSystem.EngineDir + "mods"], []);
				return $"OK|{string.Join(",", mods.Keys.OrderBy(k => k))}";
			}
			catch (Exception e)
			{
				return $"FAIL|{e.GetType().Name}: {e.Message}";
			}
		}

		[JSExport]
		public static string LoadMod(string mod)
		{
			try
			{
				// Traits read Game.Settings while being constructed, which
				// Game.InitializeAndRun would normally have set up by this point.
				Game.InitializeSettings(new Arguments());

				var mods = new InstalledMods([BrowserFileSystem.EngineDir + "mods"], []);
				if (!mods.TryGetValue(mod, out var manifest))
					return $"FAIL|mod '{mod}' was not found";

				modData = new ModData(manifest, mods);

				return $"OK|{manifest.Metadata.Title}|{manifest.Rules.Length}|" +
					$"{manifest.Weapons.Length}|{manifest.Assemblies.Length}";
			}
			catch (Exception e)
			{
				return $"FAIL|{Flatten(e)}";
			}
		}

		/// <summary>
		/// Opens a package from the freeware content, proving the mounted .mix files
		/// are readable and not merely present.
		/// </summary>
		[JSExport]
		public static string ReadContentPackage(string package, string file)
		{
			try
			{
				if (modData == null)
					return "FAIL|no mod loaded";

				if (!modData.DefaultFileSystem.TryOpen(file, out var stream))
					return $"FAIL|could not open '{file}' from the mounted content";

				using (stream)
					return $"OK|{package}|{file}|{stream.Length}";
			}
			catch (Exception e)
			{
				return $"FAIL|{Flatten(e)}";
			}
		}

		static string Flatten(Exception e)
		{
			var text = "";
			for (var current = e; current != null; current = current.InnerException)
				text += $"{(text.Length > 0 ? " <- " : "")}{current.GetType().Name}: {current.Message}";

			var innermost = e;
			while (innermost.InnerException != null)
				innermost = innermost.InnerException;

			var frames = (innermost.StackTrace ?? "").Split('\n');
			return text + " @@ " + string.Join(" | ", frames.Take(4).Select(f => f.Trim()));
		}
	}
}
