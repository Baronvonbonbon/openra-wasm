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
	/// Reports what the GL bindings resolved to, and reads pixels back from the
	/// drawing buffer. Used to verify the browser platform actually renders rather
	/// than merely returning success codes. OpenGL itself stays internal.
	/// </summary>
	public static class GLDiagnostics
	{
		/// <summary>The detected profile, feature flags and driver version string.</summary>
		public static string Describe() => $"{OpenGL.Version}|{OpenGL.Profile}|{OpenGL.Features}";

		/// <summary>The driver's extension string, for diagnosing feature detection.</summary>
		public static string Extensions() =>
			System.Runtime.InteropServices.Marshal.PtrToStringUTF8(Emscripten.openra_gl_extensions()) ?? "";

		/// <summary>Reads a single RGBA pixel out of the current drawing buffer.</summary>
		public static byte[] ReadPixel(int x, int y)
		{
			var pixel = new byte[4];
			unsafe
			{
				fixed (byte* p = pixel)
					OpenGL.glReadPixels(x, y, 1, 1, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, new IntPtr(p));
			}

			OpenGL.CheckGLError();
			return pixel;
		}

		/// <summary>
		/// Clears the drawing buffer to a colour. IGraphicsContext.Clear always clears
		/// to black, so this is used to verify that rendering actually reaches the
		/// buffer rather than merely that the call succeeded.
		/// </summary>
		public static void ClearTo(float r, float g, float b)
		{
			OpenGL.glClearColor(r, g, b, 1f);
			OpenGL.CheckGLError();
			OpenGL.glClear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
		}
	}
}
