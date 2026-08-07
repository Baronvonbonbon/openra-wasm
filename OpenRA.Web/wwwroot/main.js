import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig, runMain } = await dotnet
	.withDiagnosticTracing(false)
	.withApplicationArgumentsFromQuery()
	.create();

const exports = await getAssemblyExports(getConfig().mainAssemblyName);
globalThis.openra = exports.OpenRA.Web;

await runMain();

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
		console.log(`[gate-c]   IGraphicsContext.Clear() -> rgba(${pixel}), expected ~${expected}`);
		console.log(close && profile === 'Embedded'
			? '[gate-c] PASS - the engine renders through its own GL stack in the browser.'
			: `[gate-c] FAIL - profile=${profile} pixelMatch=${close}`);
	} else {
		console.log(`[gate-c] ${cleared}`);
		console.log('[gate-c] FAIL - clear/read failed.');
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
