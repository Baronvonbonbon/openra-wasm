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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;

namespace Eluant
{
	public sealed class LuaFunction : LuaValue
	{
		readonly DynValue value;

		internal LuaFunction(DynValue value) { this.value = value; }

		internal override DynValue Value => value;

		internal Script Script { get; set; }

		public LuaVararg Call(params LuaValue[] args)
		{
			var arguments = args.Select(a => a?.Value ?? DynValue.Nil).ToArray();

			try
			{
				var script = Script ?? value.Function?.OwnerScript;
				var result = script != null
					? script.Call(value, arguments)
					: value.Callback.ClrCallback(null, new CallbackArguments(arguments, false));

				return new LuaVararg(result);
			}
			catch (InterpreterException e)
			{
				// Script errors surface to the engine as LuaException; it logs them and
				// keeps the game running rather than tearing down the world.
				throw new LuaException(e.DecoratedMessage ?? e.Message, e);
			}
		}

		public override string ToString() => "function";
	}

	/// <summary>
	/// The values returned from a call. Lua functions can return several, and the
	/// engine indexes into the result, so this is a list rather than a single value.
	/// </summary>
	public sealed class LuaVararg : IList<LuaValue>, IDisposable
	{
		readonly List<LuaValue> values;

		internal LuaVararg(DynValue result)
		{
			values = result == null || result.IsNil()
				? []
				: result.Type == DataType.Tuple
					? result.Tuple.Select(LuaValue.FromDynValue).ToList()
					: [LuaValue.FromDynValue(result)];
		}

		public LuaVararg(IEnumerable<LuaValue> values, bool ownsValues = true)
		{
			this.values = values.ToList();
		}

		public LuaValue this[int index]
		{
			get => index >= 0 && index < values.Count ? values[index] : LuaNil.Instance;
			set => values[index] = value;
		}

		public int Count => values.Count;
		public bool IsReadOnly => false;

		public void Add(LuaValue item) => values.Add(item);
		public void Clear() => values.Clear();
		public bool Contains(LuaValue item) => values.Contains(item);
		public void CopyTo(LuaValue[] array, int arrayIndex) => values.CopyTo(array, arrayIndex);
		public int IndexOf(LuaValue item) => values.IndexOf(item);
		public void Insert(int index, LuaValue item) => values.Insert(index, item);
		public bool Remove(LuaValue item) => values.Remove(item);
		public void RemoveAt(int index) => values.RemoveAt(index);

		public IEnumerator<LuaValue> GetEnumerator() => values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public void Dispose() => GC.SuppressFinalize(this);
	}
}
