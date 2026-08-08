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
using System.Runtime.InteropServices.JavaScript;
using OpenRA.Platforms.Default;
using OpenRA.Widgets;

namespace OpenRA.Web
{
	/// <summary>
	/// Runs the engine's own startup path and then steps it from the page's
	/// animation frame callback, in place of the blocking loop the desktop build
	/// enters at the end of Game.InitializeAndRun.
	/// </summary>
	internal static partial class EngineHost
	{
		static bool initialized;

		[JSExport]
		public static string Initialize(string mod, string canvasSelector, int width, int height)
		{
			try
			{
				WebPlatform.CanvasSelector = canvasSelector;
				WebPlatform.CanvasSize = new Primitives.Size(width, height);

				// The engine and support directories are already set by
				// BrowserFileSystem.Initialize and latch on first access, so passing
				// them again here would be rejected as a second override.
				Game.InitializeWithoutRunning([
					$"Game.Mod={mod}",

					// Selects OpenRA.Platforms.Web; the desktop default would look for
					// a platform assembly that is not published here.
					"Game.Platform=Web",
				]);

				initialized = true;
				return $"OK|{Game.ModData.Manifest.Id}|{Game.Renderer != null}|{Game.ModData.ObjectCreator != null}";
			}
			catch (Exception e)
			{
				return $"FAIL|{Flatten(e)}";
			}
		}

		/// <summary>Runs one iteration of the game loop.</summary>
		[JSExport]
		public static string Step()
		{
			if (!initialized)
				return "FAIL|not initialized";

			try
			{
				Game.LoopStep();
				return "OK";
			}
			catch (Exception e)
			{
				initialized = false;
				return $"FAIL|{Flatten(e)}";
			}
		}

		/// <summary>Reports what the engine has brought up, for verification.</summary>
		[JSExport]
		public static string Describe()
		{
			if (!initialized)
				return "FAIL|not initialized";

			var renderer = Game.Renderer;
			return $"OK|{renderer.Resolution.Width}x{renderer.Resolution.Height}|" +
				$"{Ui.Root?.Children.Count ?? -1}|{Game.ModData.Manifest.Id}";
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
			return text + " @@ " + string.Join(" | ", frames[..Math.Min(3, frames.Length)]).Replace("  ", " ");
		}
	}
}
