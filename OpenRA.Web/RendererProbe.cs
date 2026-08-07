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
using OpenRA.Primitives;

namespace OpenRA.Web
{
	/// <summary>
	/// Brings up the engine's real graphics stack in the browser: WebPlatform creates
	/// the window and context, which runs OpenGL.Initialize and DetectGLFeatures and
	/// binds every GL entry point through the shared OpenGL.cs.
	/// </summary>
	internal static partial class RendererProbe
	{
		static IPlatformWindow window;

		[JSExport]
		public static string CreateWindow(string canvasSelector, int width, int height)
		{
			try
			{
				// Game.InitializeAndRun normally registers the log channels; this probe
				// brings up the graphics stack on its own, so it registers what GL needs.
				Log.AddChannel("graphics", null);

				WebPlatform.CanvasSelector = canvasSelector;

				var platform = new WebPlatform();
				window = platform.CreateWindow(
					new Size(width, height), WindowMode.Windowed, 1f, 0, 0, 0, GLProfile.Embedded);

				return $"OK|{GLDiagnostics.Describe()}";
			}
			catch (Exception e)
			{
				return $"FAIL|{Describe(e)}|{GLDiagnostics.Extensions()}";
			}
		}

		static string Describe(Exception e)
		{
			var description = "";
			for (var current = e; current != null; current = current.InnerException)
				description += $"{(description.Length > 0 ? " <- " : "")}{current.GetType().Name}: {current.Message}";

			return description;
		}

		[JSExport]
		public static string ClearAndReadPixel(float r, float g, float b)
		{
			try
			{
				GLDiagnostics.ClearTo(r, g, b);

				var pixel = GLDiagnostics.ReadPixel(0, 0);
				return $"OK|{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}";
			}
			catch (Exception e)
			{
				return $"FAIL|{Describe(e)}";
			}
		}
	}
}
