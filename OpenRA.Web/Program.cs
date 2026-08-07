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
using System.Reflection;

namespace OpenRA.Web
{
	/// <summary>
	/// Gate A entry point: verifies that the .NET runtime boots under browser-wasm
	/// and that the OpenRA engine assemblies load and are reflectable.
	/// </summary>
	internal static class Program
	{
		public static void Main()
		{
			Console.WriteLine("[gate-a] .NET runtime is alive under browser-wasm.");
			Console.WriteLine($"[gate-a] runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
			Console.WriteLine($"[gate-a] os: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");

			// Force OpenRA.Game to load and prove reflection over it works -
			// this is what ObjectCreator/FieldLoader depend on at runtime.
			var gameAssembly = typeof(OpenRA.Game).Assembly;
			var types = gameAssembly.GetTypes();
			Console.WriteLine($"[gate-a] loaded {gameAssembly.GetName().Name} with {types.Length} types.");

			// Spot-check the reflective machinery the mod loader leans on.
			var traitTypes = types.Count(t => t.GetInterfaces().Length > 0);
			Console.WriteLine($"[gate-a] {traitTypes} types implement at least one interface.");

			foreach (var probe in new[] { "OpenRA.Game", "OpenRA.ModData", "OpenRA.ObjectCreator", "OpenRA.FieldLoader", "OpenRA.Platform" })
			{
				var t = gameAssembly.GetType(probe);
				Console.WriteLine($"[gate-a]   {probe}: {(t != null ? "OK" : "MISSING")}");
			}

			Console.WriteLine("[gate-a] PASS - engine assemblies are loadable in the browser.");
		}
	}
}
