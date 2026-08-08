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
using System.Text;

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// Nothing here is ever called.
	/// <para>
	/// Calling native code from the interpreter needs an interp-to-native trampoline
	/// for each method signature, and the build only generates trampolines for
	/// signatures it finds in DllImport declarations. OpenGL binds every entry point
	/// through Marshal.GetDelegateForFunctionPointer, and those indirect calls need
	/// matching trampolines too - without one the call fails at runtime with
	/// "aot-runtime-wasm.c: &lt;disabled&gt;".
	/// </para>
	/// <para>
	/// Declaring one import per distinct signature used by those delegates makes the
	/// generator emit the trampolines they need. There are far fewer distinct
	/// signatures than entry points, so this list is short. Keep it in step with the
	/// delegate declarations in OpenGL.cs: adding a GL function with a new signature
	/// shape means adding it here too.
	/// </para>
	/// </summary>
	static class TrampolineAnchors
	{
		const string Shim = "webgl_shim";

		// 1 GL entry point, e.g. glDebugProc
		[DllImport(Shim)]
		static extern void openra_trampoline_00(int a0, int a1, uint a2, int a3, int a4, StringBuilder a5, IntPtr a6);

		// 1 GL entry point, e.g. glDebugMessageCallback
		[DllImport(Shim)]
		static extern void openra_trampoline_01(OpenGL.DebugProc a0, IntPtr a1);

		// 1 GL entry point, e.g. glDebugMessageInsert
		[DllImport(Shim)]
		static extern void openra_trampoline_02(int a0, int a1, uint a2, int a3, int a4, string a5);

		// 2 GL entry points, e.g. glFlush
		[DllImport(Shim)]
		static extern void openra_trampoline_03();

		// 3 GL entry points, e.g. glViewport
		[DllImport(Shim)]
		static extern void openra_trampoline_04(int a0, int a1, int a2, int a3);

		// 8 GL entry points, e.g. glClear
		[DllImport(Shim)]
		static extern void openra_trampoline_05(int a0);

		// 1 GL entry point, e.g. glClearColor
		[DllImport(Shim)]
		static extern void openra_trampoline_06(float a0, float a1, float a2, float a3);

		// 1 GL entry point, e.g. glGetError
		[DllImport(Shim)]
		static extern int openra_trampoline_07();

		// 1 GL entry point, e.g. glGetString
		[DllImport(Shim)]
		static extern IntPtr openra_trampoline_08(int a0);

		// 1 GL entry point, e.g. glGetStringi
		[DllImport(Shim)]
		static extern IntPtr openra_trampoline_09(int a0, uint a1);

		// 1 GL entry point, e.g. glGetIntegerv
		[DllImport(Shim)]
		static extern void openra_trampoline_10(int a0, out int a1);

		// 1 GL entry point, e.g. glCreateProgram
		[DllImport(Shim)]
		static extern uint openra_trampoline_11();

		// 4 GL entry points, e.g. glUseProgram
		[DllImport(Shim)]
		static extern void openra_trampoline_12(uint a0);

		// 2 GL entry points, e.g. glGetProgramiv
		[DllImport(Shim)]
		static extern void openra_trampoline_13(uint a0, int a1, out int a2);

		// 1 GL entry point, e.g. glCreateShader
		[DllImport(Shim)]
		static extern uint openra_trampoline_14(int a0);

		// 1 GL entry point, e.g. glShaderSource
		[DllImport(Shim)]
		static extern void openra_trampoline_15(uint a0, int a1, string[] a2, IntPtr a3);

		// 1 GL entry point, e.g. glAttachShader
		[DllImport(Shim)]
		static extern void openra_trampoline_16(uint a0, uint a1);

		// 2 GL entry points, e.g. glGetShaderInfoLog
		[DllImport(Shim)]
		static extern void openra_trampoline_17(uint a0, int a1, out int a2, StringBuilder a3);

		// 1 GL entry point, e.g. glGetUniformLocation
		[DllImport(Shim)]
		static extern int openra_trampoline_18(uint a0, string a1);

		// 1 GL entry point, e.g. glGetActiveUniform
		[DllImport(Shim)]
		static extern void openra_trampoline_19(uint a0, int a1, int a2, out int a3, out int a4, out int a5, StringBuilder a6);

		// 3 GL entry points, e.g. glUniform1i
		[DllImport(Shim)]
		static extern void openra_trampoline_20(int a0, int a1);

		// 1 GL entry point, e.g. glUniform1f
		[DllImport(Shim)]
		static extern void openra_trampoline_21(int a0, float a1);

		// 1 GL entry point, e.g. glUniform2f
		[DllImport(Shim)]
		static extern void openra_trampoline_22(int a0, float a1, float a2);

		// 1 GL entry point, e.g. glUniform3f
		[DllImport(Shim)]
		static extern void openra_trampoline_23(int a0, float a1, float a2, float a3);

		// 4 GL entry points, e.g. glUniform1fv
		[DllImport(Shim)]
		static extern void openra_trampoline_24(int a0, int a1, IntPtr a2);

		// 1 GL entry point, e.g. glUniformMatrix4fv
		[DllImport(Shim)]
		static extern void openra_trampoline_25(int a0, int a1, bool a2, IntPtr a3);

		// 5 GL entry points, e.g. glGenBuffers
		[DllImport(Shim)]
		static extern void openra_trampoline_26(int a0, out uint a1);

		// 4 GL entry points, e.g. glBindBuffer
		[DllImport(Shim)]
		static extern void openra_trampoline_27(int a0, uint a1);

		// 1 GL entry point, e.g. glBufferData
		[DllImport(Shim)]
		static extern void openra_trampoline_28(int a0, IntPtr a1, IntPtr a2, int a3);

		// 1 GL entry point, e.g. glBufferSubData
		[DllImport(Shim)]
		static extern void openra_trampoline_29(int a0, IntPtr a1, IntPtr a2, IntPtr a3);

		// 4 GL entry points, e.g. glDeleteBuffers
		[DllImport(Shim)]
		static extern void openra_trampoline_30(int a0, ref uint a1);

		// 2 GL entry points, e.g. glBindAttribLocation
		[DllImport(Shim)]
		static extern void openra_trampoline_31(uint a0, int a1, string a2);

		// 1 GL entry point, e.g. glVertexAttribPointer
		[DllImport(Shim)]
		static extern void openra_trampoline_32(int a0, int a1, int a2, bool a3, int a4, IntPtr a5);

		// 2 GL entry points, e.g. glVertexAttribIPointer
		[DllImport(Shim)]
		static extern void openra_trampoline_33(int a0, int a1, int a2, int a3, IntPtr a4);

		// 2 GL entry points, e.g. glDrawArrays
		[DllImport(Shim)]
		static extern void openra_trampoline_34(int a0, int a1, int a2);

		// 1 GL entry point, e.g. glDrawElements
		[DllImport(Shim)]
		static extern void openra_trampoline_35(int a0, int a1, int a2, IntPtr a3);

		// 1 GL entry point, e.g. glReadPixels
		[DllImport(Shim)]
		static extern void openra_trampoline_36(int a0, int a1, int a2, int a3, int a4, int a5, IntPtr a6);

		// 1 GL entry point, e.g. glIsTexture
		[DllImport(Shim)]
		static extern bool openra_trampoline_37(uint a0);

		// 1 GL entry point, e.g. glTexImage2D
		[DllImport(Shim)]
		static extern void openra_trampoline_38(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7, IntPtr a8);

		// 1 GL entry point, e.g. glCopyTexImage2D
		[DllImport(Shim)]
		static extern void openra_trampoline_39(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7);

		// 1 GL entry point, e.g. glTexParameterf
		[DllImport(Shim)]
		static extern void openra_trampoline_40(int a0, int a1, float a2);

		// 1 GL entry point, e.g. glFramebufferTexture2D
		[DllImport(Shim)]
		static extern void openra_trampoline_41(int a0, int a1, int a2, uint a3, int a4);

		// 1 GL entry point, e.g. glFramebufferRenderbuffer
		[DllImport(Shim)]
		static extern void openra_trampoline_42(int a0, int a1, int a2, uint a3);

		// 1 GL entry point, e.g. glCheckFramebufferStatus
		[DllImport(Shim)]
		static extern int openra_trampoline_43(int a0);
	}
}
