// Attaches DOM listeners that queue input for the engine. Nothing is dispatched
// here: the engine drains the queue from its own loop via PumpInput, so input is
// never delivered from inside an arbitrary browser callback.

const MouseEventType = { Down: 0, Move: 1, Up: 2, Scroll: 3 };
const KeyEventType = { Down: 0, Up: 1 };

// Matches OpenRA's MouseButton flags.
const MouseButton = { None: 0, Left: 1, Right: 2, Middle: 4 };
const BUTTON_FLAGS = [MouseButton.Left, MouseButton.Middle, MouseButton.Right];

// Matches OpenRA's Modifiers flags.
function modifiersOf(e) {
	return (e.shiftKey ? 1 : 0) | (e.altKey ? 2 : 0) | (e.ctrlKey ? 4 : 0) | (e.metaKey ? 8 : 0);
}

export function attachInput(canvas, bridge) {
	const positionOf = e => {
		// The canvas is laid out in CSS pixels but drawn at its backing size.
		const rect = canvas.getBoundingClientRect();
		return [
			Math.round((e.clientX - rect.left) * (canvas.width / rect.width)),
			Math.round((e.clientY - rect.top) * (canvas.height / rect.height)),
		];
	};

	canvas.addEventListener('pointerdown', e => {
		const [x, y] = positionOf(e);
		bridge.Mouse(MouseEventType.Down, BUTTON_FLAGS[e.button] ?? 0, x, y, 0, 0, modifiersOf(e));

		// Capture keeps drag gestures alive when the pointer leaves the canvas, but
		// it is not essential and throws for a pointer id that is no longer active,
		// so it must not come before the event is queued.
		try { canvas.setPointerCapture(e.pointerId); } catch { /* not capturable */ }

		canvas.focus();
		e.preventDefault();
	});

	canvas.addEventListener('pointerup', e => {
		const [x, y] = positionOf(e);
		bridge.Mouse(MouseEventType.Up, BUTTON_FLAGS[e.button] ?? 0, x, y, 0, 0, modifiersOf(e));
		e.preventDefault();
	});

	canvas.addEventListener('pointermove', e => {
		const [x, y] = positionOf(e);
		// Buttons held during a move are a bitmask, not the single button that changed.
		let held = 0;
		if (e.buttons & 1) held |= MouseButton.Left;
		if (e.buttons & 2) held |= MouseButton.Right;
		if (e.buttons & 4) held |= MouseButton.Middle;
		bridge.Mouse(MouseEventType.Move, held, x, y, e.movementX | 0, e.movementY | 0, modifiersOf(e));
	});

	canvas.addEventListener('wheel', e => {
		const [x, y] = positionOf(e);
		// Engine scroll steps are discrete; the sign is what matters.
		bridge.Mouse(MouseEventType.Scroll, 0, x, y, Math.sign(-e.deltaX), Math.sign(-e.deltaY), modifiersOf(e));
		e.preventDefault();
	}, { passive: false });

	// The canvas needs focus to receive key events, and a context menu would
	// swallow right-click, which the game uses.
	canvas.tabIndex = 0;
	canvas.addEventListener('contextmenu', e => e.preventDefault());

	canvas.addEventListener('keydown', e => {
		bridge.Key(KeyEventType.Down, e.code, modifiersOf(e), e.repeat);

		// Printable characters are reported separately, as text input.
		if (e.key.length === 1 && !e.ctrlKey && !e.metaKey)
			bridge.Text(e.key);

		// Browser shortcuts on keys the game binds would otherwise steal the input.
		if (e.code === 'Tab' || e.code.startsWith('F') || e.ctrlKey)
			e.preventDefault();
	});

	canvas.addEventListener('keyup', e => {
		bridge.Key(KeyEventType.Up, e.code, modifiersOf(e), false);
	});
}
