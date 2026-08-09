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
using Eluant.ObjectBinding;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Eluant
{
	/// <summary>
	/// Exposes a CLR object to Lua by routing metamethods to the
	/// Eluant.ObjectBinding interfaces the object implements. Engine types such as
	/// WPos, CPos and Actor rely on this: indexing, comparison and arithmetic on them
	/// from a mission script all arrive here.
	/// </summary>
	sealed class ClrObjectDescriptor : IUserDataDescriptor
	{
		public static readonly ClrObjectDescriptor Instance = new();

		public string Name => "clr";
		public Type Type => typeof(object);
		public string AsString(object obj) => obj?.ToString();

		// Any CLR object the engine hands to Lua goes through this descriptor.
		public bool IsTypeCompatible(Type type, object obj) => type.IsInstanceOfType(obj);

		public DynValue Index(Script script, object obj, DynValue index, bool isDirectIndexing)
		{
			if (obj is not ILuaTableBinding binding)
				return null;

			var runtime = RuntimeFor(script);
			var result = binding[runtime, LuaValue.FromDynValue(index)];
			return result?.Value ?? DynValue.Nil;
		}

		public bool SetIndex(Script script, object obj, DynValue index, DynValue value, bool isDirectIndexing)
		{
			if (obj is not ILuaTableBinding binding)
				return false;

			binding[RuntimeFor(script), LuaValue.FromDynValue(index)] = LuaValue.FromDynValue(value);
			return true;
		}

		public DynValue MetaIndex(Script script, object obj, string metaname)
		{
			var runtime = RuntimeFor(script);

			// Each metamethod is offered only if the object implements the matching
			// interface, so Lua falls back to its default behaviour otherwise.
			switch (metaname)
			{
				case "__tostring" when obj is ILuaToStringBinding toString:
					return Unary(script, _ => toString.ToString(runtime));
				case "__eq" when obj is ILuaEqualityBinding equality:
					return Binary(script, (l, r) => equality.Equals(runtime, l, r));
				case "__add" when obj is ILuaAdditionBinding addition:
					return Binary(script, (l, r) => addition.Add(runtime, l, r));
				case "__sub" when obj is ILuaSubtractionBinding subtraction:
					return Binary(script, (l, r) => subtraction.Subtract(runtime, l, r));
				case "__mul" when obj is ILuaMultiplicationBinding multiplication:
					return Binary(script, (l, r) => multiplication.Multiply(runtime, l, r));
				case "__div" when obj is ILuaDivisionBinding division:
					return Binary(script, (l, r) => division.Divide(runtime, l, r));
				case "__unm" when obj is ILuaUnaryMinusBinding unaryMinus:
					return Unary(script, _ => unaryMinus.Minus(runtime));
				case "__lt" when obj is ILuaLessThanBinding lessThan:
					return Binary(script, (l, r) => lessThan.LessThan(runtime, l, r));
				case "__le" when obj is ILuaLessThanOrEqualToBinding lessThanOrEqual:
					return Binary(script, (l, r) => lessThanOrEqual.LessThanOrEqualTo(runtime, l, r));
				default:
					return null;
			}
		}

		static DynValue Binary(Script script, Func<LuaValue, LuaValue, LuaValue> op) =>
			DynValue.NewCallback((_, args) =>
				op(LuaValue.FromDynValue(args.RawGet(0, false)),
					LuaValue.FromDynValue(args.RawGet(1, false)))?.Value ?? DynValue.Nil);

		static DynValue Unary(Script script, Func<LuaValue, LuaValue> op) =>
			DynValue.NewCallback((_, args) =>
				op(LuaValue.FromDynValue(args.RawGet(0, false)))?.Value ?? DynValue.Nil);

		// The bindings take a LuaRuntime purely to construct return values, and every
		// script in a world shares one runtime, so the association is kept here rather
		// than threaded through MoonSharp.
		static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Script, LuaRuntime> Runtimes = new();

		internal static void Register(Script script, LuaRuntime runtime) => Runtimes.AddOrUpdate(script, runtime);

		static LuaRuntime RuntimeFor(Script script) =>
			Runtimes.TryGetValue(script, out var runtime) ? runtime : null;
	}
}
