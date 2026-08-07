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
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// A window backed by an HTML canvas. The browser owns the surface, so most of
	/// the desktop window's responsibilities - display enumeration, window modes,
	/// mouse grabbing - either do not apply or are handled by the page.
	/// </summary>
	sealed class WebPlatformWindow : IPlatformWindow
	{
		readonly WebGraphicsContext context;
		float scaleModifier;

		public string CanvasSelector { get; }

		public IGraphicsContext Context => context;

		public Size NativeWindowSize { get; private set; }
		public Size SurfaceSize { get; private set; }

		// The browser reports CSS pixels and handles device pixel ratio itself.
		public float NativeWindowScale => 1f;
		public float EffectiveWindowScale => scaleModifier;
		public Size EffectiveWindowSize =>
			new((int)(NativeWindowSize.Width / scaleModifier), (int)(NativeWindowSize.Height / scaleModifier));

		public int DisplayCount => 1;
		public int CurrentDisplay => 0;

		// A canvas is only rendered while the tab is visible, and the rAF loop stops
		// when it is not, so the window is never in a suspended-but-ticking state.
		public bool HasInputFocus => true;
		public bool IsSuspended => false;

		public GLProfile GLProfile => GLProfile.Embedded;
		public GLProfile[] SupportedGLProfiles => [GLProfile.Embedded];

		public event Action<float, float, float, float> OnWindowScaleChanged;

		public WebPlatformWindow(string canvasSelector, Size size, float scaleModifier)
		{
			CanvasSelector = canvasSelector;
			this.scaleModifier = scaleModifier;

			NativeWindowSize = size;
			SurfaceSize = size;

			context = new WebGraphicsContext(this);
			context.InitializeOpenGL();
		}

		public void PumpInput(IInputHandler inputHandler)
		{
			// Input arrives from DOM event listeners rather than from a polled queue.
		}

		public string GetClipboardText() => string.Empty;
		public bool SetClipboardText(string text) => false;
		public bool TryOpenUrl(string url) => false;

		public void GrabWindowMouseFocus() { }
		public void ReleaseWindowMouseFocus() { }

		public IHardwareCursor CreateHardwareCursor(string name, Size size, byte[] data, int2 hotspot, bool pixelDouble) => null;
		public void SetHardwareCursor(IHardwareCursor cursor) { }
		public void SetWindowTitle(string title) { }
		public void SetRelativeMouseMode(bool mode) { }

		public void SetScaleModifier(float scale)
		{
			var oldScale = scaleModifier;
			scaleModifier = scale;
			OnWindowScaleChanged?.Invoke(NativeWindowScale, oldScale, NativeWindowScale, scaleModifier);
		}

		public void Dispose()
		{
			context.Dispose();
		}
	}
}
