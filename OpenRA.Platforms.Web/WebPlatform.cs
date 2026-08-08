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

		public IPlatformWindow CreateWindow(
			Size size, WindowMode windowMode, float scaleModifier, int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile)
		{
			// The page controls the canvas size, so window mode and display index
			// are ignored and the requested size is taken as the surface size.
			return new WebPlatformWindow(CanvasSelector, size, scaleModifier);
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
