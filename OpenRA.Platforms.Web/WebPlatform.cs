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

using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	public class WebPlatform : IPlatform
	{
		/// <summary>The canvas the engine renders into.</summary>
		public static string CanvasSelector { get; set; } = "#canvas";

		/// <summary>
		/// The canvas backing size. The page owns the canvas, so this is used in place
		/// of the resolution the engine would otherwise request.
		/// </summary>
		public static Size CanvasSize { get; set; } = new(1280, 720);

		public IPlatformWindow CreateWindow(
			Size size, WindowMode windowMode, float scaleModifier, int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile)
		{
			// The page controls the canvas, so window mode, display index and the
			// requested resolution are all ignored in favour of the canvas size.
			return new WebPlatformWindow(CanvasSelector, CanvasSize, scaleModifier);
		}

		public ISoundEngine CreateSound(string device)
		{
			// WebAudio is not wired up yet; silence keeps the engine running.
			return new DummySoundEngine();
		}

		public IFont CreateFont(byte[] data)
		{
			return new FreeTypeFont(data);
		}
	}
}
