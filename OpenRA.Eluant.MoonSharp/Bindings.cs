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

namespace Eluant.ObjectBinding
{
	/// <summary>
	/// Metamethod interfaces implemented by engine types that are handed to Lua.
	/// A CLR object wrapped in a <see cref="LuaCustomClrObject"/> gets the Lua
	/// metamethod corresponding to each of these it implements.
	/// </summary>
	public interface ILuaTableBinding
	{
		LuaValue this[LuaRuntime runtime, LuaValue key] { get; set; }
	}

	public interface ILuaToStringBinding
	{
		LuaValue ToString(LuaRuntime runtime);
	}

	public interface ILuaEqualityBinding
	{
		LuaValue Equals(LuaRuntime runtime, LuaValue left, LuaValue right);
	}

	public interface ILuaAdditionBinding
	{
		LuaValue Add(LuaRuntime runtime, LuaValue left, LuaValue right);
	}

	public interface ILuaSubtractionBinding
	{
		LuaValue Subtract(LuaRuntime runtime, LuaValue left, LuaValue right);
	}

	public interface ILuaMultiplicationBinding
	{
		LuaValue Multiply(LuaRuntime runtime, LuaValue left, LuaValue right);
	}

	public interface ILuaDivisionBinding
	{
		LuaValue Divide(LuaRuntime runtime, LuaValue left, LuaValue right);
	}

	public interface ILuaUnaryMinusBinding
	{
		// The operand is the implementing object itself, so only the runtime is passed.
		LuaValue Minus(LuaRuntime runtime);
	}

	public interface ILuaLessThanBinding
	{
		LuaValue LessThan(LuaRuntime runtime, LuaValue left, LuaValue right);
	}

	public interface ILuaLessThanOrEqualToBinding
	{
		LuaValue LessThanOrEqualTo(LuaRuntime runtime, LuaValue left, LuaValue right);
	}
}
