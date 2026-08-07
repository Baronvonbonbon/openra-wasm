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

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// Entry points into Emscripten's WebGL emulation.
	/// .NET resolves wasm P/Invokes through a table generated at build time rather
	/// than by loading a shared library at runtime, so these are reached through a
	/// C shim linked via NativeFileReference instead of by importing libhtml5/libGL.
	/// </summary>
	static class Emscripten
	{
		const string Shim = "webgl_shim";

		[DllImport(Shim)]
		public static extern int openra_gl_create_context([MarshalAs(UnmanagedType.LPUTF8Str)] string selector);

		[DllImport(Shim)]
		public static extern IntPtr openra_gl_get_proc_address([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

		[DllImport(Shim)]
		public static extern int openra_gl_is_extension_supported([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

		[DllImport(Shim)]
		public static extern void openra_gl_destroy_context(int context);

		[DllImport(Shim)]
		public static extern IntPtr openra_gl_extensions();

		[DllImport(Shim)]
		public static extern void openra_canvas_size(
			[MarshalAs(UnmanagedType.LPUTF8Str)] string selector, out int width, out int height);
	}
}
