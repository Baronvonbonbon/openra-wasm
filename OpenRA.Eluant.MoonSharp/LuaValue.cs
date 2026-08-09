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
using MoonSharp.Interpreter;

namespace Eluant
{
	/// <summary>
	/// A Lua value.
	/// <para>
	/// The engine tests the concrete type of these with `is` to decide how to convert
	/// them, so the hierarchy has to stay exactly as it is: LuaNil, LuaBoolean,
	/// LuaNumber, LuaString, LuaTable, LuaFunction and LuaCustomClrObject.
	/// </para>
	/// <para>
	/// Values are immutable wrappers over MoonSharp's DynValue, which is garbage
	/// collected, so disposal is a no-op and CopyReference can hand back the same
	/// value. The engine disposes values it receives and copies references it intends
	/// to keep; both are safe here because nothing owns unmanaged state.
	/// </para>
	/// </summary>
	public abstract class LuaValue : IDisposable
	{
		internal abstract DynValue Value { get; }

		/// <summary>
		/// Returns a value that can be disposed independently of this one. Nothing here
		/// owns unmanaged state, so the same instance is safe to share.
		/// </summary>
		public LuaValue CopyReference() => this;

		public virtual void Dispose() => GC.SuppressFinalize(this);

		/// <summary>Lua truthiness: everything except nil and false is true.</summary>
		public bool ToBoolean() => Value.CastToBool();

		/// <summary>The numeric value, or null where it is not coercible to a number.</summary>
		public double? ToNumber() => Value.CastToNumber();


		internal static LuaValue FromDynValue(DynValue value)
		{
			if (value == null)
				return LuaNil.Instance;

			switch (value.Type)
			{
				case DataType.Nil:
				case DataType.Void:
					return LuaNil.Instance;
				case DataType.Boolean:
					return (LuaBoolean)value.Boolean;
				case DataType.Number:
					return new LuaNumber(value.Number);
				case DataType.String:
					return new LuaString(value.String);
				case DataType.Table:
					return new LuaTable(value);
				case DataType.Function:
				case DataType.ClrFunction:
					return new LuaFunction(value);
				case DataType.UserData:
					return value.UserData?.Object is { } clr
						? new LuaCustomClrObject(clr)
						: LuaNil.Instance;
				default:
					throw new LuaException($"Cannot represent a Lua {value.Type} value.");
			}
		}

		public static implicit operator LuaValue(bool value) => (LuaBoolean)value;
		public static implicit operator LuaValue(double value) => new LuaNumber(value);
		public static implicit operator LuaValue(int value) => new LuaNumber(value);
		public static implicit operator LuaValue(long value) => new LuaNumber(value);
		public static implicit operator LuaValue(float value) => new LuaNumber(value);
		public static implicit operator LuaValue(string value) =>
			value == null ? LuaNil.Instance : new LuaString(value);

		// The shipped engine assemblies were compiled against Eluant's nullable
		// conversions, so these must exist even where the engine could have used the
		// non-nullable ones. A null converts to nil.
		public static implicit operator LuaValue(bool? value) =>
			value.HasValue ? (LuaBoolean)value.Value : LuaNil.Instance;
		public static implicit operator LuaValue(double? value) =>
			value.HasValue ? new LuaNumber(value.Value) : LuaNil.Instance;
		public static implicit operator LuaValue(int? value) =>
			value.HasValue ? new LuaNumber(value.Value) : LuaNil.Instance;
		public static implicit operator LuaValue(long? value) =>
			value.HasValue ? new LuaNumber(value.Value) : LuaNil.Instance;
		public static implicit operator LuaValue(float? value) =>
			value.HasValue ? new LuaNumber(value.Value) : LuaNil.Instance;

		public override bool Equals(object obj) =>
			obj is LuaValue other && Equals(Value, other.Value);

		public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	}

	/// <summary>Base of the by-value types: nil, booleans, numbers and strings.</summary>
	public abstract class LuaValueType : LuaValue
	{
	}

	/// <summary>Base of the by-reference types: tables, functions and userdata.</summary>
	public abstract class LuaReference : LuaValue
	{
	}

	/// <summary>
	/// A CLR object handed to Lua. Eluant exposes the wrapped object through
	/// <see cref="ClrObject"/>, and the engine's assemblies bind to that, so the
	/// property lives on this base rather than on the concrete classes.
	/// </summary>
	public abstract class LuaClrObjectValue : LuaValue
	{
		public object ClrObject { get; }

		protected LuaClrObjectValue(object clrObject) { ClrObject = clrObject; }
	}

	public sealed class LuaNil : LuaValueType
	{
		public static LuaNil Instance { get; } = new();

		LuaNil() { }

		internal override DynValue Value => DynValue.Nil;

		public override string ToString() => "nil";
		public override bool Equals(object obj) => obj is LuaNil;
		public override int GetHashCode() => 0;
	}

	public sealed class LuaBoolean : LuaValueType
	{
		public static LuaBoolean True { get; } = new(true);
		public static LuaBoolean False { get; } = new(false);

		readonly bool value;

		LuaBoolean(bool value) { this.value = value; }

		internal override DynValue Value => DynValue.NewBoolean(value);

		public static implicit operator LuaBoolean(bool value) => value ? True : False;
		public static implicit operator bool(LuaBoolean value) => value.value;

		public override string ToString() => value ? "true" : "false";
		public override bool Equals(object obj) => obj is LuaBoolean b && b.value == value;
		public override int GetHashCode() => value.GetHashCode();
	}

	public sealed class LuaNumber : LuaValueType
	{
		readonly double value;

		public LuaNumber(double value) { this.value = value; }

		internal override DynValue Value => DynValue.NewNumber(value);

		public static implicit operator double(LuaNumber value) => value.value;

		public override string ToString() => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
		public override bool Equals(object obj) => obj is LuaNumber n && n.value.Equals(value);
		public override int GetHashCode() => value.GetHashCode();
	}

	public sealed class LuaString : LuaValueType
	{
		readonly string value;

		public LuaString(string value) { this.value = value; }

		internal override DynValue Value => DynValue.NewString(value);

		public static implicit operator string(LuaString value) => value?.value;

		public override string ToString() => value;
		public override bool Equals(object obj) => obj is LuaString s && s.value == value;
		public override int GetHashCode() => value?.GetHashCode(StringComparison.Ordinal) ?? 0;
	}

	/// <summary>
	/// Wraps a CLR object for Lua. The engine passes its own types through this, and
	/// their metamethods come from whichever Eluant.ObjectBinding interfaces they
	/// implement - see ClrObjectDescriptor.
	/// </summary>
	public sealed class LuaCustomClrObject : LuaClrObjectValue
	{
		public LuaCustomClrObject(object clrObject) : base(clrObject) { }

		internal override DynValue Value => UserData.Create(ClrObject, ClrObjectDescriptor.Instance);

		public override string ToString() => ClrObject?.ToString();
		public override bool Equals(object obj) => obj is LuaCustomClrObject o && Equals(o.ClrObject, ClrObject);
		public override int GetHashCode() => ClrObject?.GetHashCode() ?? 0;
	}
}

namespace Eluant
{
	/// <summary>
	/// A CLR object exposed to Lua without member access - Lua can hold it and hand
	/// it back, but not inspect it.
	/// </summary>
	public sealed class LuaOpaqueClrObject : LuaClrObjectValue
	{
		public LuaOpaqueClrObject(object clrObject) : base(clrObject) { }

		internal override MoonSharp.Interpreter.DynValue Value =>
			MoonSharp.Interpreter.DynValue.NewNil();

		public override string ToString() => ClrObject?.ToString();
	}

	/// <summary>
	/// A CLR object whose members Lua may reach directly.
	/// </summary>
	public sealed class LuaTransparentClrObject : LuaClrObjectValue
	{
		public LuaTransparentClrObject(object clrObject) : base(clrObject) { }

		internal override MoonSharp.Interpreter.DynValue Value =>
			MoonSharp.Interpreter.UserData.Create(ClrObject, ClrObjectDescriptor.Instance);

		public override string ToString() => ClrObject?.ToString();
	}
}
