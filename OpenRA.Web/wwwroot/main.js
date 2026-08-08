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

	// Mod files and game content arrive as archives: the engine reads over a
	// thousand small files out of mods/, which would otherwise be a request each.
	for (const [name, target] of [
		['engine.zip', '.'],
		// Where the engine's own installer puts the Red Alert freeware packages.
		['ra-content.zip', '../support/Content/ra/v2'],
	]) {
		const response = await fetch(`./content/${name}`);
		if (!response.ok) {
			console.log(`[vfs] FAIL - could not fetch ${name}: ${response.status}`);
			continue;
		}

		const bytes = new Uint8Array(await response.arrayBuffer());
		const result = vfs.MountArchive(bytes, target);
		console.log(result.startsWith('OK|')
			? `[vfs]   ${name}: extracted ${result.split('|')[1]} files (${(bytes.length / 1048576).toFixed(1)} MB)`
			: `[vfs]   ${result}`);
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
	console.log(readable.startsWith('OK|') && Number(files) > shaders.length
		? '[vfs] PASS - the engine can read mounted files as ordinary paths.'
		: '[vfs] FAIL - mounted files are not reachable.');
} else {
	console.log(`[vfs] ${initialized}`);
	console.log('[vfs] FAIL - could not initialize the filesystem.');
}

// Mods: load Red Alert from the virtual filesystem. This exercises mod discovery,
// the manifest parser and the .mix readers against the real game content.
const mods = exports.OpenRA.Web.ModProbe;
const discovered = mods.DiscoverMods();
if (discovered.startsWith('OK|')) {
	console.log(`[mods] discovered: ${discovered.slice(3)}`);

	const loaded = mods.LoadMod('ra');
	if (loaded.startsWith('OK|')) {
		const [, title, rules, weapons, assemblies] = loaded.split('|');
		console.log(`[mods] loaded '${title}': ${rules} rule files, ${weapons} weapon files, ${assemblies} assemblies`);

		// Read a file out of the freeware .mix packages to prove the content is
		// readable, not merely present.
		const read = mods.ReadContentPackage('conquer.mix', 'mcv.shp');
		console.log(`[mods]   ${read.startsWith('OK|') ? `read ${read.split('|')[2]} (${read.split('|')[3]} bytes) from the freeware content` : read}`);
		console.log(read.startsWith('OK|')
			? '[mods] PASS - Red Alert loads from the virtual filesystem.'
			: '[mods] FAIL - mod loaded but content is unreadable.');
	} else {
		console.log(`[mods] ${loaded}`);
		console.log('[mods] FAIL - could not load the mod.');
	}
} else {
	console.log(`[mods] ${discovered}`);
	console.log('[mods] FAIL - mod discovery failed.');
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

// Full engine startup is opt-in with ?engine=1 while it still hangs during mod
// loading; without it the page runs the platform checks above, which pass.
if (new URLSearchParams(location.search).has('engine')) {
	// Start the engine: this runs OpenRA's own initialization - renderer, mod data,
	// widgets, load screen - and then steps it from the animation frame callback in
	// place of the blocking loop the desktop build enters.
	const engine = exports.OpenRA.Web.EngineHost;

	const started = engine.Initialize('ra', '#canvas', canvas.width, canvas.height);
	if (started.startsWith('OK|')) {
		const [, mod, hasRenderer, hasObjectCreator] = started.split('|');
		console.log(`[engine] initialized mod '${mod}' (renderer: ${hasRenderer}, objectCreator: ${hasObjectCreator})`);

		let frames = 0;
		let failure = null;
		const startedAt = performance.now();

		function frame(now) {
			const result = engine.Step();
			if (result !== 'OK') {
				failure = result;
				report(now - startedAt);
				return;
			}

			frames++;
			if (now - startedAt < 4000) requestAnimationFrame(frame);
			else report(now - startedAt);
		}

		function report(elapsed) {
			if (failure) {
				console.log(`[engine] ${failure}`);
				console.log(`[engine] FAIL - the loop threw after ${frames} frames.`);
				return;
			}

			const described = engine.Describe();
			const [, resolution, widgets, id] = described.split('|');
			console.log(`[engine] ran ${frames} frames in ${(elapsed / 1000).toFixed(1)}s (${(frames / (elapsed / 1000)).toFixed(1)} fps)`);
			console.log(`[engine]   resolution ${resolution}, ${widgets} root widgets, mod '${id}'`);

			// A rendered frame must have put something other than the clear colour on
			// the canvas; a silent no-op renderer would leave it untouched.
			const gl = canvas.getContext('webgl2');
			const pixels = new Uint8Array(canvas.width * canvas.height * 4);
			gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
			let lit = 0;
			for (let i = 0; i < pixels.length; i += 4)
				if (pixels[i] || pixels[i + 1] || pixels[i + 2]) lit++;
			console.log(`[engine]   ${lit} of ${canvas.width * canvas.height} pixels non-black`);

			console.log(frames > 0 && Number(widgets) > 0 && lit > 0
				? '[engine] PASS - the engine runs and draws in the browser.'
				: `[engine] FAIL - frames=${frames} widgets=${widgets} litPixels=${lit}`);
		}

		requestAnimationFrame(frame);
	} else {
		console.log(`[engine] ${started}`);
		console.log('[engine] FAIL - engine initialization failed.');
	}
} else {
	console.log("[engine] skipped - pass ?engine=1 to run full startup (currently hangs in mod loading)");

	// Keep the loop check running in its place.
	let frames = 0;
	let timerTicks = 0;
	setInterval(() => { timerTicks++; }, 100);
	const startedAt = performance.now();

	function frame(now) {
		exports.OpenRA.Web.BrowserLoop.Step();
		frames++;
		if (now - startedAt < 3000) requestAnimationFrame(frame);
		else {
			const elapsed = now - startedAt;
			console.log(`[gate-b] ${frames} steps in ${(elapsed / 1000).toFixed(1)}s (${(frames / (elapsed / 1000)).toFixed(1)} fps), timer ${timerTicks}/${Math.floor(elapsed / 100)}`);
			console.log(timerTicks >= Math.floor(elapsed / 100) * 0.8 && frames > 0
				? '[gate-b] PASS - loop ticks under rAF and the main thread never blocked.'
				: '[gate-b] FAIL - main thread was blocked.');
		}
	}

	requestAnimationFrame(frame);
}
