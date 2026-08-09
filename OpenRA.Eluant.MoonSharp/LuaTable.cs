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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;

namespace Eluant
{
	public class LuaTable : LuaValue, IEnumerable<KeyValuePair<LuaValue, LuaValue>>
	{
		readonly DynValue value;

		internal LuaTable(DynValue value) { this.value = value; }

		internal override DynValue Value => value;

		internal Table Table => value.Table;

		public LuaValue this[LuaValue key]
		{
			get => FromDynValue(Table.Get(key.Value));

			// Assigning nil removes the entry, which is how the engine clears globals
			// it does not want scripts to reach.
			set => Table.Set(key.Value, value == null ? DynValue.Nil : value.Value);
		}

		public ICollection<LuaValue> Keys =>
			Table.Keys.Select(FromDynValue).ToList();

		public ICollection<LuaValue> Values =>
			Table.Values.Select(FromDynValue).ToList();

		public int Count => Table.Length;

		public void Add(LuaValue key, LuaValue value) => this[key] = value;

		public bool ContainsKey(LuaValue key) => !Table.Get(key.Value).IsNil();

		public bool Remove(LuaValue key)
		{
			if (!ContainsKey(key))
				return false;

			Table.Remove(key.Value);
			return true;
		}

		public bool TryGetValue(LuaValue key, out LuaValue result)
		{
			var found = Table.Get(key.Value);
			result = FromDynValue(found);
			return !found.IsNil();
		}

		public IEnumerator<KeyValuePair<LuaValue, LuaValue>> GetEnumerator() =>
			Table.Pairs
				.Select(p => new KeyValuePair<LuaValue, LuaValue>(FromDynValue(p.Key), FromDynValue(p.Value)))
				.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override string ToString() => "table";
	}
	/// <summary>
	/// The globals table. The engine's shipped assemblies name this type directly, so
	/// it must exist even though it adds nothing over a plain table.
	/// </summary>
	public sealed class LuaGlobalsTable : LuaTable
	{
		internal LuaGlobalsTable(DynValue value) : base(value) { }
	}
}
