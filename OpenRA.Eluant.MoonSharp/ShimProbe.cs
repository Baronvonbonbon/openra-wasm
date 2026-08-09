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
	/// <summary>
	/// Placeholder standing in for the real runtime while the MoonSharp-backed
	/// implementation is written. Its only job right now is to prove that this
	/// assembly is substituted for the real Eluant in the browser bundle: reaching
	/// this constructor means the engine bound to the shim rather than to the
	/// native-backed original.
	/// </summary>
	public class MemoryConstrainedLuaRuntime : IDisposable
	{
		public MemoryConstrainedLuaRuntime()
		{
			throw new NotImplementedException("MoonSharp shim reached: assembly substitution works.");
		}

		public void Dispose() => GC.SuppressFinalize(this);
	}
}
