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

using System.Runtime.InteropServices.JavaScript;
using OpenRA.Platforms.Default;

namespace OpenRA.Web
{
	/// <summary>
	/// Entry points for the page's DOM event listeners. These only queue; the engine
	/// drains the queue from its own loop through IPlatformWindow.PumpInput.
	/// </summary>
	internal static partial class InputBridge
	{
		[JSExport]
		public static void Mouse(int type, int button, int x, int y, int deltaX, int deltaY, int modifiers) =>
			WebInput.QueueMouse(type, button, x, y, deltaX, deltaY, modifiers);

		[JSExport]
		public static void Key(int type, string code, int modifiers, bool isRepeat) =>
			WebInput.QueueKey(type, code, modifiers, isRepeat);

		[JSExport]
		public static void Text(string text) => WebInput.QueueText(text);

		/// <summary>Events waiting to be dispatched, for diagnostics.</summary>
		[JSExport]
		public static int PendingCount() => WebInput.PendingCount;

		/// <summary>Resolves a KeyboardEvent.code to a Keycode name, for diagnostics.</summary>
		[JSExport]
		public static string ResolveKeycode(string code) => WebInput.MapKeycode(code).ToString();
	}
}
