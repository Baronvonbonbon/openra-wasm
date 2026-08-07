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

using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace OpenRA.Web
{
	/// <summary>
	/// Drives the engine from the browser's requestAnimationFrame instead of a
	/// blocking while loop. The desktop build spins in Game.Loop() and calls
	/// Thread.Sleep between steps; that would deadlock the browser's single thread,
	/// so here JS calls Step() once per animation frame and Game.LoopStep()
	/// performs exactly one iteration and returns.
	/// </summary>
	internal static partial class BrowserLoop
	{
		static readonly Stopwatch Clock = Stopwatch.StartNew();
		static long steps;
		static long busyMilliseconds;

		/// <summary>
		/// Performs one iteration of the game loop.
		/// Returns the milliseconds the caller may idle for before stepping again;
		/// requestAnimationFrame paces itself, so JS is free to ignore it.
		/// </summary>
		[JSExport]
		public static int Step()
		{
			var start = Clock.ElapsedMilliseconds;

			// Gate B runs the pump before the renderer exists (that is Gate C), so the
			// engine step is stubbed out here. Once Game.InitializeAndRun has set up
			// ModData and the Renderer this becomes: return Game.LoopStep();
			var sleep = StepEngine();

			steps++;
			busyMilliseconds += Clock.ElapsedMilliseconds - start;
			return sleep;
		}

		static int StepEngine()
		{
			// Stand-in for Game.LoopStep(): does a small amount of managed work so the
			// managed/JS boundary is exercised with a realistic per-frame cost.
			var acc = 0d;
			for (var i = 0; i < 20000; i++)
				acc += System.Math.Sqrt(i);

			return acc > 0 ? 0 : 1;
		}

		/// <summary>
		/// Blocks the calling thread for the given duration, emulating the desktop
		/// loop's Thread.Sleep. Used as a control in the Gate B harness to prove the
		/// responsiveness witness actually detects a blocked main thread.
		/// </summary>
		[JSExport]
		public static void BlockFor(int milliseconds)
		{
			var until = Clock.ElapsedMilliseconds + milliseconds;
			while (Clock.ElapsedMilliseconds < until)
			{
			}
		}

		/// <summary>Steps performed since startup, for the Gate B report.</summary>
		[JSExport]
		public static int GetStepCount() => (int)steps;

		/// <summary>Milliseconds spent inside managed code, for the Gate B report.</summary>
		[JSExport]
		public static int GetBusyMilliseconds() => (int)busyMilliseconds;
	}
}
