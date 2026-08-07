#!/usr/bin/env node
// Minimal Chrome DevTools Protocol harness for the browser-wasm build.
//
// Headless Chrome's --dump-dom snapshots the page at load and --virtual-time-budget
// makes performance.now() jump, so neither can observe a requestAnimationFrame loop
// running in real time. This drives a real browser over CDP instead and streams
// console output back, which is what the gate harnesses assert against.
//
// Usage: node browser-test.mjs <url> [seconds] [--headless=old|new] [--keep]

import { spawn } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const url = process.argv[2];
const seconds = Number(process.argv[3] ?? 6);
const headlessArg = process.argv.find(a => a.startsWith('--headless=')) ?? '--headless=old';
if (!url) {
	console.error('usage: browser-test.mjs <url> [seconds] [--headless=old|new]');
	process.exit(2);
}

const port = 9222 + Math.floor(Math.random() * 500);
const profile = mkdtempSync(join(tmpdir(), 'openra-cdp-'));

const chrome = spawn('google-chrome', [
	headlessArg,
	'--no-sandbox',
	'--no-first-run',
	'--disable-dev-shm-usage',
	// SwiftShader gives us a real WebGL implementation without a GPU, which the
	// renderer gate depends on.
	'--use-gl=angle',
	'--use-angle=swiftshader',
	'--enable-unsafe-swiftshader',
	`--remote-debugging-port=${port}`,
	`--user-data-dir=${profile}`,
	'--window-size=1280,720',
	url,
], { stdio: ['ignore', 'ignore', 'pipe'] });

let chromeStderr = '';
chrome.stderr.on('data', d => { chromeStderr += d; });

const cleanup = () => {
	try { chrome.kill('SIGKILL'); } catch {}
	if (!process.argv.includes('--keep')) { try { rmSync(profile, { recursive: true, force: true }); } catch {} }
};
process.on('exit', cleanup);

async function endpoint() {
	for (let i = 0; i < 100; i++) {
		try {
			const res = await fetch(`http://127.0.0.1:${port}/json/list`);
			const targets = await res.json();
			const page = targets.find(t => t.type === 'page' && t.webSocketDebuggerUrl);
			if (page) return page.webSocketDebuggerUrl;
		} catch {}
		await new Promise(r => setTimeout(r, 100));
	}
	throw new Error(`Chrome devtools never came up on ${port}.\n${chromeStderr}`);
}

const ws = new WebSocket(await endpoint());
await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });

let id = 0;
const send = (method, params = {}) => ws.send(JSON.stringify({ id: ++id, method, params }));

const lines = [];
const render = arg =>
	arg.value !== undefined ? String(arg.value)
	: arg.description !== undefined ? arg.description
	: arg.type;

ws.onmessage = ev => {
	const msg = JSON.parse(ev.data);
	if (msg.method === 'Runtime.consoleAPICalled') {
		const text = msg.params.args.map(render).join(' ');
		lines.push(text);
		console.log(text);
	} else if (msg.method === 'Runtime.exceptionThrown') {
		const d = msg.params.exceptionDetails;
		const text = `PAGE EXCEPTION: ${d.exception?.description ?? d.text}`;
		lines.push(text);
		console.log(text);
	}
};

send('Runtime.enable');
send('Page.enable');
// The page may already have loaded before we attached; reload so no output is missed.
send('Page.reload', { ignoreCache: true });

await new Promise(r => setTimeout(r, seconds * 1000));
cleanup();

// Surface a non-zero exit if the page reported a failure, so callers can gate on it.
const failed = lines.some(l => /\bFAIL\b|PAGE EXCEPTION/.test(l));
process.exit(failed ? 1 : 0);
