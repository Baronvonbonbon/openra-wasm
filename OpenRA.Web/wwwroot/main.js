import { dotnet } from './_framework/dotnet.js';
import { attachInput } from './input.js';

const { getAssemblyExports, getConfig, runMain } = await dotnet
	.withDiagnosticTracing(false)
	.withApplicationArgumentsFromQuery()
	.create();

const exports = await getAssemblyExports(getConfig().mainAssemblyName);
globalThis.openra = exports.OpenRA.Web;

await runMain();

// Populate the virtual filesystem before anything reads Platform.EngineDir,
// which latches on first access. The engine then opens these as ordinary paths.
const vfs = exports.OpenRA.Web.BrowserFileSystem;

const initialized = vfs.Initialize();
if (initialized.startsWith('OK|')) {
	const [, platform, engineDir, supportDir] = initialized.split('|');
	console.log('[vfs] filesystem ready');
	console.log(`[vfs]   Platform.CurrentPlatform: ${platform}`);
	console.log(`[vfs]   EngineDir: ${engineDir}`);
	console.log(`[vfs]   SupportDir: ${supportDir}`);

	const shaders = [
		'combined.vert', 'combined.frag', 'model.vert', 'model.frag',
		'postprocess.vert', 'postprocess_textured.vert', 'postprocess_tint.frag',
		'postprocess_flash.frag', 'postprocess_menufade.frag',
		'postprocess_chronoshift.frag', 'postprocess_textured_sonic.frag',
		'postprocess_textured_vortex.frag',
	];

	for (const name of shaders) {
		const response = await fetch(`./engine/glsl/${name}`);
		if (!response.ok) {
			console.log(`[vfs] FAIL - could not fetch ${name}: ${response.status}`);
			break;
		}

		const bytes = new Uint8Array(await response.arrayBuffer());
		const result = vfs.Mount(`glsl/${name}`, bytes);
		if (result !== 'OK') {
			console.log(`[vfs] ${result}`);
			break;
		}
	}

	// The font is needed by the rasterization check below.
	const fontResponse = await fetch('./engine/mods/common/FreeSans.ttf');
	if (fontResponse.ok)
		vfs.Mount('mods/common/FreeSans.ttf', new Uint8Array(await fontResponse.arrayBuffer()));
	else
		console.log(`[vfs] FAIL - could not fetch FreeSans.ttf: ${fontResponse.status}`);

	const [files, bytes] = vfs.Describe().split('|');
	const readable = vfs.VerifyReadable('glsl/combined.frag');
	console.log(`[vfs]   mounted ${files} files, ${bytes} bytes`);
	console.log(`[vfs]   combined.frag readable through System.IO: ${readable}`);
	console.log(readable.startsWith('OK|') && Number(files) === shaders.length + 1
		? '[vfs] PASS - the engine can read mounted files as ordinary paths.'
		: '[vfs] FAIL - mounted files are not reachable.');
} else {
	console.log(`[vfs] ${initialized}`);
	console.log('[vfs] FAIL - could not initialize the filesystem.');
}

// Fonts: rasterize a glyph through FreeType, built by packaging/web/build-freetype.sh.
const glyph = exports.OpenRA.Web.FontProbe.RasterizeGlyph('mods/common/FreeSans.ttf', 'A', 16);
if (glyph.startsWith('OK|')) {
	const [, dimensions, advance, offset, ink] = glyph.split('|');
	console.log(`[fonts] rasterized 'A' at 16px: ${dimensions}, advance ${advance}, offset ${offset}`);
	console.log(`[fonts]   ${ink} non-zero pixels in the bitmap`);
	const [w, h] = dimensions.split('x').map(Number);
	console.log(w > 0 && h > 0 && Number(ink) > 0
		? '[fonts] PASS - FreeType rasterizes glyphs in the browser.'
		: '[fonts] FAIL - glyph bitmap is empty.');
} else {
	console.log(`[fonts] ${glyph}`);
	console.log('[fonts] FAIL - could not rasterize a glyph.');
}

// Input: DOM listeners queue events; the engine drains them in PumpInput.
const canvas = document.getElementById('canvas');
attachInput(canvas, exports.OpenRA.Web.InputBridge);
console.log('[input] listeners attached to the canvas');

// Dispatch real DOM events and confirm they arrive at an IInputHandler through
// PumpInput, rather than only reaching the queue.
{
	const rect = canvas.getBoundingClientRect();
	const at = (type, init) => canvas.dispatchEvent(new PointerEvent(type, {
		clientX: rect.left + 32, clientY: rect.top + 24, bubbles: true, ...init,
	}));

	at('pointerdown', { button: 0, buttons: 1 });
	at('pointermove', { buttons: 1 });
	at('pointerup', { button: 0, buttons: 0 });
	canvas.dispatchEvent(new WheelEvent('wheel', {
		clientX: rect.left + 32, clientY: rect.top + 24, deltaY: -120, bubbles: true, cancelable: true,
	}));
	canvas.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyA', key: 'a', shiftKey: true, bubbles: true }));
	canvas.dispatchEvent(new KeyboardEvent('keyup', { code: 'KeyA', key: 'a', bubbles: true }));
	canvas.dispatchEvent(new KeyboardEvent('keydown', { code: 'F5', key: 'F5', bubbles: true }));
	canvas.dispatchEvent(new KeyboardEvent('keydown', { code: 'ArrowLeft', key: 'ArrowLeft', bubbles: true }));

	const queued = exports.OpenRA.Web.InputBridge.PendingCount();
	console.log(`[input] ${queued} events queued from ${8} dispatched DOM events`);
}

// Gate C: bring up the engine's real graphics stack - WebPlatform creates the
// window and context, which runs OpenGL.Initialize and binds every GL entry point.
const renderer = exports.OpenRA.Web.RendererProbe;

const created = renderer.CreateWindow('#canvas', 640, 360);
if (created.startsWith('OK|')) {
	const [, version, profile, features] = created.split('|');
	console.log('[gate-c] engine graphics context created');
	console.log(`[gate-c]   OpenGL.Version: ${version}`);
	console.log(`[gate-c]   OpenGL.Profile: ${profile}`);
	console.log(`[gate-c]   OpenGL.Features: ${features}`);

	// Clear through IGraphicsContext.Clear() and read the pixel back, so the result
	// proves the engine's own code path reached the drawing buffer.
	const cleared = renderer.ClearAndReadPixel(0.2, 0.6, 0.3);
	if (cleared.startsWith('OK|')) {
		const [, pixel] = cleared.split('|');
		const [r, g, b] = pixel.split(',').map(Number);
		const expected = [51, 153, 77];
		const close = expected.every((e, i) => Math.abs([r, g, b][i] - e) <= 2);
		console.log(`[gate-c]   cleared through the engine GL bindings -> rgba(${pixel}), expected ~${expected}`);
		console.log(close && profile === 'Embedded'
			? '[gate-c] PASS - the engine renders through its own GL stack in the browser.'
			: `[gate-c] FAIL - profile=${profile} pixelMatch=${close}`);
	} else {
		console.log(`[gate-c] ${cleared}`);
		console.log('[gate-c] FAIL - clear/read failed.');
	}

	// Now that a window exists, drain the queued input through it.
	const pumped = renderer.PumpInput();
	if (pumped.startsWith('OK|')) {
		const received = pumped.slice(3).split(';').filter(Boolean);
		console.log(`[input] IInputHandler received ${received.length} events:`);
		for (const r of received) console.log(`[input]   ${r}`);

		const has = p => received.some(r => r.startsWith(p));
		const keycodes = ['KeyA', 'F5', 'ArrowLeft', 'Digit7', 'Escape']
			.map(c => `${c}->${exports.OpenRA.Web.InputBridge.ResolveKeycode(c)}`);
		console.log(`[input]   keycode mapping: ${keycodes.join(' ')}`);

		console.log(has('mouse:Down') && has('mouse:Move') && has('mouse:Up')
				&& has('mouse:Scroll') && has('key:Down') && has('key:Up') && has('text:a')
			? '[input] PASS - DOM events reach the engine through PumpInput.'
			: '[input] FAIL - some event kinds did not arrive.');
	} else {
		console.log(`[input] ${pumped}`);
		console.log('[input] FAIL - could not pump input.');
	}

	// Compile the engine's real shader: reads from the virtual filesystem,
	// substitutes {VERSION} and {DEFINES}, and compiles as GLSL ES 3.00.
	const compiled = renderer.CompileCombinedShader();
	if (compiled.startsWith('OK|')) {
		const [, name, attributes, swapped] = compiled.split('|');
		console.log(`[gate-c] compiled shader '${name}' with ${attributes} vertex attributes`);
		console.log(`[gate-c]   channel swap active in source: ${swapped}`);
		console.log('[gate-c] PASS - the engine compiles its real shaders in the browser.');
	} else {
		console.log(`[gate-c] ${compiled}`);
		console.log('[gate-c] FAIL - shader compilation failed.');
	}
} else {
	console.log(`[gate-c] ${created}`);
	console.log('[gate-c] FAIL - could not create the engine graphics context.');
}

// Gate B: drive the engine from requestAnimationFrame. The desktop build blocks in a
// while loop and calls Thread.Sleep between iterations; doing that on the browser's
// single thread would freeze the tab, so each frame takes exactly one engine step
// and yields back to the browser.
const loop = exports.OpenRA.Web.BrowserLoop;
const blocking = new URLSearchParams(location.search).has('blocking');
if (blocking) console.log('[gate-b] CONTROL RUN - deliberately blocking the main thread');

// A plain interval timer is the responsiveness witness: if the engine were blocking
// the main thread the way the desktop loop does, this could not keep firing.
const TimerIntervalMs = 100;
let timerTicks = 0;
setInterval(() => { timerTicks++; }, TimerIntervalMs);

const WarmupMs = 1500;
const MeasureMs = 3000;

let phase = 'warmup';
let frames = 0;
let worstGap = 0;
let measureStart = 0;
let timerAtMeasureStart = 0;
let stepsAtMeasureStart = 0;
let last = performance.now();
const bootedAt = last;

function frame(now) {
	const gap = now - last;
	last = now;

	loop.Step();

	// Control mode: deliberately block the main thread the way the desktop loop
	// does, to prove the interval-timer witness below actually detects blocking.
	if (blocking && phase === 'measure') loop.BlockFor(300);

	if (phase === 'warmup') {
		// Startup pays for runtime boot, tier-0 interpretation and WebGL context
		// creation. That is a real cost, but it is not the loop blocking, so it is
		// reported separately rather than folded into the steady-state numbers.
		if (now - bootedAt >= WarmupMs) {
			phase = 'measure';
			measureStart = now;
			timerAtMeasureStart = timerTicks;
			stepsAtMeasureStart = loop.GetStepCount();
			frames = 0;
			worstGap = 0;
		}
		requestAnimationFrame(frame);
		return;
	}

	frames++;
	worstGap = Math.max(worstGap, gap);

	if (now - measureStart < MeasureMs) {
		requestAnimationFrame(frame);
	} else {
		report(now - measureStart);
	}
}

function report(elapsed) {
	const steps = loop.GetStepCount() - stepsAtMeasureStart;
	const ticks = timerTicks - timerAtMeasureStart;
	const expectedTicks = Math.floor(elapsed / TimerIntervalMs);
	const fps = frames / (elapsed / 1000);

	console.log(`[gate-b] steady state over ${(elapsed / 1000).toFixed(1)}s after ${WarmupMs}ms warmup`);
	console.log(`[gate-b] ${steps} engine steps across ${frames} animation frames (${fps.toFixed(1)} fps)`);
	console.log(`[gate-b] worst frame gap ${worstGap.toFixed(1)} ms`);
	console.log(`[gate-b] interval timer fired ${ticks}/${expectedTicks} expected times`);

	// The timer is the real proof: a blocked main thread starves it. Frame pacing is
	// reported for information, since this runs on software rendering.
	const timerHealthy = ticks >= expectedTicks * 0.8;
	const loopRunning = steps > 0 && frames > 0;
	const noStalls = worstGap < 250;

	console.log(timerHealthy && loopRunning && noStalls
		? '[gate-b] PASS - loop ticks under rAF and the main thread never blocked.'
		: `[gate-b] FAIL - timerHealthy=${timerHealthy} loopRunning=${loopRunning} noStalls=${noStalls}`);
}

requestAnimationFrame(frame);
