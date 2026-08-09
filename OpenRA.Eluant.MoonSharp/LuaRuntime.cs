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
using System.Linq;
using MoonSharp.Interpreter;

namespace Eluant
{
	/// <summary>
	/// A Lua interpreter, backed by MoonSharp.
	/// </summary>
	public class LuaRuntime : IDisposable
	{
		internal Script Script { get; }

		public LuaGlobalsTable Globals { get; }

		public LuaRuntime()
		{
			// The engine also clears globals it does not want, but starting from a
			// restricted module set means the sandbox does not depend on that alone.
			// Coroutines are included because mission scripts use them.
			Script = new Script(CoreModules.Basic | CoreModules.String | CoreModules.Math
				| CoreModules.Table | CoreModules.TableIterators | CoreModules.Coroutine
				| CoreModules.ErrorHandling | CoreModules.OS_Time | CoreModules.Metatables);

			Globals = new LuaGlobalsTable(DynValue.NewTable(Script.Globals));

			// The metamethod bindings are handed a runtime to build return values with,
			// and reach it from the script they were invoked on.
			ClrObjectDescriptor.Register(Script, this);
		}

		public LuaTable CreateTable() => new(DynValue.NewTable(Script));

		/// <summary>Wraps a CLR delegate so Lua can call it.</summary>
		public LuaFunction CreateFunctionFromDelegate(Delegate function)
		{
			var parameters = function.Method.GetParameters();

			// A delegate that takes a single LuaVararg receives every argument of the
			// call rather than just the first, which is how the engine's script member
			// wrappers are bound.
			var takesVararg = parameters.Length == 1 && parameters[0].ParameterType == typeof(LuaVararg);

			DynValue Invoke(ScriptExecutionContext context, CallbackArguments args)
			{
				object[] arguments;
				if (takesVararg)
				{
					var values = new LuaValue[args.Count];
					for (var i = 0; i < args.Count; i++)
						values[i] = LuaValue.FromDynValue(args.RawGet(i, false));

					arguments = [new LuaVararg(values)];
				}
				else
				{
					arguments = new object[parameters.Length];
					for (var i = 0; i < parameters.Length; i++)
						arguments[i] = ToClr(args.RawGet(i, false), parameters[i].ParameterType);
				}

				try
				{
					var result = function.DynamicInvoke(arguments);
					return result is LuaValue value ? value.Value : DynValue.FromObject(Script, result);
				}
				catch (System.Reflection.TargetInvocationException e) when (e.InnerException != null)
				{
					// Surface the script-facing error rather than the reflection wrapper.
					throw e.InnerException;
				}
			}

			return new LuaFunction(DynValue.NewCallback(Invoke));
		}

		static object ToClr(DynValue value, Type type)
		{
			if (value == null || value.IsNil())
				return type.IsValueType ? Activator.CreateInstance(type) : null;

			if (type == typeof(string))
				return value.CastToString();

			if (type == typeof(bool))
				return value.CastToBool();

			if (type.IsAssignableFrom(typeof(LuaValue)) || type.IsSubclassOf(typeof(LuaValue)))
				return LuaValue.FromDynValue(value);

			if (value.Type == DataType.UserData)
				return value.UserData?.Object;

			var number = value.CastToNumber();
			return number == null ? null : Convert.ChangeType(number.Value, type);
		}

		/// <summary>Compiles and runs a chunk of Lua source.</summary>
		public LuaVararg DoBuffer(string code, string chunkName)
		{
			try
			{
				return new LuaVararg(Script.DoString(code, null, chunkName));
			}
			catch (InterpreterException e)
			{
				throw new LuaException(e.DecoratedMessage ?? e.Message, e);
			}
		}

		public virtual void Dispose() => GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Eluant capped the memory a script could allocate. MoonSharp has no equivalent,
	/// so this adds nothing but the name the engine constructs. Scripts in the browser
	/// are therefore not memory limited.
	/// </summary>
	public class MemoryConstrainedLuaRuntime : LuaRuntime
	{
		public long MaxMemoryUse { get; set; }
		public long MemoryUse => 0;
	}
}
