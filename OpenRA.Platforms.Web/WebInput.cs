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
using System.Collections.Generic;

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// Buffers browser input events until the engine polls for them.
	/// <para>
	/// The desktop platform pumps an SDL event queue from inside the game loop, but
	/// the browser delivers input through callbacks that fire whenever they like.
	/// Events are queued as they arrive and drained in PumpInput, which keeps
	/// dispatch on the loop's own schedule rather than reentering the engine from an
	/// arbitrary callback.
	/// </para>
	/// </summary>
	public static class WebInput
	{
		enum EventKind { Mouse, Key, Text }

		readonly record struct Event(
			EventKind Kind, int Type, int Button, int X, int Y, int DeltaX, int DeltaY,
			Modifiers Modifiers, string Text, bool IsRepeat);

		static readonly Queue<Event> Pending = new();

		/// <summary>The most recent modifier state, reported separately by the engine.</summary>
		public static Modifiers CurrentModifiers { get; private set; }

		public static void QueueMouse(int type, int button, int x, int y, int deltaX, int deltaY, int modifiers)
		{
			CurrentModifiers = (Modifiers)modifiers;
			Pending.Enqueue(new Event(EventKind.Mouse, type, button, x, y, deltaX, deltaY, CurrentModifiers, null, false));
		}

		public static void QueueKey(int type, string code, int modifiers, bool isRepeat)
		{
			CurrentModifiers = (Modifiers)modifiers;
			Pending.Enqueue(new Event(EventKind.Key, type, (int)MapKeycode(code), 0, 0, 0, 0, CurrentModifiers, code, isRepeat));
		}

		public static void QueueText(string text)
		{
			Pending.Enqueue(new Event(EventKind.Text, 0, 0, 0, 0, 0, 0, CurrentModifiers, text, false));
		}

		/// <summary>Number of events waiting, for diagnostics.</summary>
		public static int PendingCount => Pending.Count;

		/// <summary>Dispatches everything queued since the last call.</summary>
		public static void Dispatch(IInputHandler handler)
		{
			handler.ModifierKeys(CurrentModifiers);

			while (Pending.Count > 0)
			{
				var e = Pending.Dequeue();
				switch (e.Kind)
				{
					case EventKind.Mouse:
					{
						var button = (MouseButton)e.Button;
						var location = new int2(e.X, e.Y);
						var mouseEvent = (MouseInputEvent)e.Type;

						// Scroll amounts travel in the same field as movement deltas,
						// which is what the desktop platform does too.
						var delta = new int2(e.DeltaX, e.DeltaY);

						var taps = mouseEvent == MouseInputEvent.Down
							? MultiTapDetection.DetectFromMouse((byte)e.Button, location)
							: MultiTapDetection.InfoFromMouse((byte)e.Button);

						handler.OnMouseInput(new MouseInput(mouseEvent, button, location, delta, e.Modifiers, taps));
						break;
					}

					case EventKind.Key:
					{
						var key = (Keycode)e.Button;
						var keyEvent = (KeyInputEvent)e.Type;
						var taps = keyEvent == KeyInputEvent.Down
							? MultiTapDetection.DetectFromKeyboard(key, e.Modifiers)
							: MultiTapDetection.InfoFromKeyboard(key, e.Modifiers);

						handler.OnKeyInput(new KeyInput
						{
							Event = keyEvent,
							Key = key,
							Modifiers = e.Modifiers,
							MultiTapCount = taps,
							UnicodeChar = e.Text != null && e.Text.Length == 1 ? e.Text[0] : '\0',
							IsRepeat = e.IsRepeat,
						});

						break;
					}

					case EventKind.Text:
						handler.OnTextInput(e.Text);
						break;
				}
			}
		}

		/// <summary>
		/// Maps a KeyboardEvent.code to a Keycode. Physical codes are used rather than
		/// the produced character so that bindings do not move with the layout, which
		/// matches how the desktop platform reads scancodes.
		/// </summary>
		public static Keycode MapKeycode(string code)
		{
			if (string.IsNullOrEmpty(code))
				return Keycode.UNKNOWN;

			// Letters and digits are defined as their lowercase character values.
			if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal))
				return (Keycode)char.ToLowerInvariant(code[3]);

			if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal))
				return (Keycode)code[5];

			return code switch
			{
				"Escape" => Keycode.ESCAPE,
				"Enter" or "NumpadEnter" => Keycode.RETURN,
				"Space" => Keycode.SPACE,
				"Backspace" => Keycode.BACKSPACE,
				"Tab" => Keycode.TAB,
				"Delete" => Keycode.DELETE,
				"Insert" => Keycode.INSERT,
				"Home" => Keycode.HOME,
				"End" => Keycode.END,
				"PageUp" => Keycode.PAGEUP,
				"PageDown" => Keycode.PAGEDOWN,
				"ArrowLeft" => Keycode.LEFT,
				"ArrowRight" => Keycode.RIGHT,
				"ArrowUp" => Keycode.UP,
				"ArrowDown" => Keycode.DOWN,
				"ShiftLeft" => Keycode.LSHIFT,
				"ShiftRight" => Keycode.RSHIFT,
				"ControlLeft" => Keycode.LCTRL,
				"ControlRight" => Keycode.RCTRL,
				"AltLeft" => Keycode.LALT,
				"AltRight" => Keycode.RALT,
				"MetaLeft" => Keycode.LGUI,
				"MetaRight" => Keycode.RGUI,
				"Minus" => Keycode.MINUS,
				"Equal" => Keycode.EQUALS,
				"BracketLeft" => Keycode.LEFTBRACKET,
				"BracketRight" => Keycode.RIGHTBRACKET,
				"Backslash" => Keycode.BACKSLASH,
				"Semicolon" => Keycode.SEMICOLON,
				"Quote" => Keycode.QUOTE,
				"Comma" => Keycode.COMMA,
				"Period" => Keycode.PERIOD,
				"Slash" => Keycode.SLASH,
				"Backquote" => Keycode.BACKQUOTE,
				_ => code.Length > 1 && code[0] == 'F' && int.TryParse(code[1..], out var f) && f >= 1 && f <= 24
					? Keycode.F1 + (f - 1)
					: Keycode.UNKNOWN,
			};
		}
	}
}
