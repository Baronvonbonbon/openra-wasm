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

namespace Eluant
{
	/// <summary>
	/// Extensions for reaching the CLR object behind a Lua value.
	/// <para>
	/// This has to be a static extension class rather than a method on LuaValue: the
	/// engine's compiled assemblies call Eluant.ClrObject.TryGetClrObject, so putting
	/// it on the value type satisfies a source build but not the shipped IL.
	/// </para>
	/// </summary>
	public static class ClrObject
	{
		public static bool TryGetClrObject(this LuaValue value, out object clrObject)
		{
			clrObject = (value as LuaClrObjectValue)?.ClrObject;
			return clrObject != null;
		}
	}
}
