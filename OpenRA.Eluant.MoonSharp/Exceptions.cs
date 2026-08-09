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

namespace Eluant
{
	public class LuaException : Exception
	{
		public LuaException() { }
		public LuaException(string message) : base(message) { }
		public LuaException(string message, Exception innerException) : base(message, innerException) { }
	}

	public class LuaRuntimeException : LuaException
	{
		public LuaRuntimeException() { }
		public LuaRuntimeException(string message) : base(message) { }
		public LuaRuntimeException(string message, Exception innerException) : base(message, innerException) { }

		public string TracebackFragment { get; set; }
	}
}
