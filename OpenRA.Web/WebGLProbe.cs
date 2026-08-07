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
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.Web
{
	/// <summary>
	/// Probes whether Emscripten's WebGL emulation is reachable through P/Invoke.
	/// If it is, the existing OpenGL.cs / Shader.cs / Texture.cs / VertexBuffer.cs
	/// stack can be reused against WebGL2 and only the SDL2 window layer needs
	/// replacing. If it is not, the graphics context has to be rebuilt against
	/// [JSImport] WebGL calls instead.
	/// </summary>
	internal static partial class WebGLProbe
	{
		// Any name works: .NET resolves wasm P/Invokes through a generated table
		// keyed on the entry point, not by loading a shared library at runtime.
		const string Shim = "webgl_shim";

		[DllImport(Shim)]
		static extern int openra_gl_create_context([MarshalAs(UnmanagedType.LPUTF8Str)] string selector);

		[DllImport(Shim)]
		static extern IntPtr openra_gl_get_string(uint name);

		[DllImport(Shim)]
		static extern void openra_gl_clear_color(float r, float g, float b, float a);

		[DllImport(Shim)]
		static extern void openra_gl_clear(uint mask);

		[DllImport(Shim)]
		static extern void openra_gl_read_pixel(int x, int y, byte[] rgba);

		[DllImport(Shim)]
		static extern IntPtr openra_gl_get_proc_address([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void ClearColor(float r, float g, float b, float a);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate void Clear(uint mask);

		const uint GL_COLOR_BUFFER_BIT = 0x00004000;
		const uint GL_VERSION = 0x1F02;
		const uint GL_SHADING_LANGUAGE_VERSION = 0x8B8C;
		const uint GL_RENDERER = 0x1F01;

		[JSExport]
		public static string Probe(string canvasSelector)
		{
			var context = openra_gl_create_context(canvasSelector);
			if (context == 0)
				return "FAIL: could not create a WebGL2 context";

			var version = Marshal.PtrToStringUTF8(openra_gl_get_string(GL_VERSION));
			var glsl = Marshal.PtrToStringUTF8(openra_gl_get_string(GL_SHADING_LANGUAGE_VERSION));
			var renderer = Marshal.PtrToStringUTF8(openra_gl_get_string(GL_RENDERER));

			// Clear to a known colour and read it back, so the result proves pixels
			// actually reached the drawing buffer rather than just that the calls returned.
			openra_gl_clear_color(0.15f, 0.35f, 0.6f, 1f);
			openra_gl_clear(GL_COLOR_BUFFER_BIT);

			var pixel = new byte[4];
			openra_gl_read_pixel(0, 0, pixel);

			// The decisive test for reuse: OpenRA binds every GL entry point through a
			// single Bind<T>(name) helper over SDL_GL_GetProcAddress. If the same
			// delegate binding works here, OpenGL.cs can be reused as-is.
			var procAddress = openra_gl_get_proc_address("glClearColor");
			var boundVia = "unavailable";
			if (procAddress != IntPtr.Zero)
			{
				var clearColor = Marshal.GetDelegateForFunctionPointer<ClearColor>(procAddress);
				var clear = Marshal.GetDelegateForFunctionPointer<Clear>(openra_gl_get_proc_address("glClear"));

				// Clear to a different colour through the delegates and read it back.
				clearColor(0.8f, 0.2f, 0.4f, 1f);
				clear(GL_COLOR_BUFFER_BIT);

				var viaDelegate = new byte[4];
				openra_gl_read_pixel(0, 0, viaDelegate);
				boundVia = $"{viaDelegate[0]},{viaDelegate[1]},{viaDelegate[2]},{viaDelegate[3]}";
			}

			return $"OK|{version}|{glsl}|{renderer}|{pixel[0]},{pixel[1]},{pixel[2]},{pixel[3]}|{boundVia}";
		}
	}
}
