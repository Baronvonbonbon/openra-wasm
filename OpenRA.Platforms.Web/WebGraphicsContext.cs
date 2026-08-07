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

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// A GL context backed by WebGL2. All of the drawing logic is inherited from
	/// <see cref="GraphicsContext"/>; only creating, binding and destroying the
	/// context differ from the desktop platform.
	/// </summary>
	sealed class WebGraphicsContext : GraphicsContext
	{
		readonly WebPlatformWindow window;
		int context;

		public WebGraphicsContext(WebPlatformWindow window)
		{
			this.window = window;

			context = Emscripten.openra_gl_create_context(window.CanvasSelector);
			if (context == 0)
				throw new InvalidOperationException(
					$"Can not create a WebGL2 context on '{window.CanvasSelector}'.");
		}

		protected override IPlatformWindow Window => window;

		protected override void MakeContextCurrent()
		{
			OpenGL.GetProcAddress = Emscripten.openra_gl_get_proc_address;
			OpenGL.IsExtensionSupported = name => Emscripten.openra_gl_is_extension_supported(name) != 0;
		}

		public override void Present()
		{
			VerifyThreadAffinity();

			// The browser presents the drawing buffer automatically when the frame
			// callback returns, so there is no swap to perform here.
		}

		public override void SetVSyncEnabled(bool enabled)
		{
			VerifyThreadAffinity();

			// requestAnimationFrame is already vsynced and the rate cannot be changed.
		}

		protected override void DisposeContext()
		{
			if (context != 0)
			{
				Emscripten.openra_gl_destroy_context(context);
				context = 0;
			}
		}
	}
}
