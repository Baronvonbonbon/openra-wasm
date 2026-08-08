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
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using OpenRA.Platforms.Default;

namespace OpenRA.Web
{
	/// <summary>
	/// Rasterizes a glyph through the platform's font implementation, to verify
	/// that the FreeType archive built by packaging/web/build-freetype.sh works
	/// and not merely that it links.
	/// </summary>
	internal static partial class FontProbe
	{
		[JSExport]
		public static string RasterizeGlyph(string fontPath, string character, int size)
		{
			try
			{
				var data = File.ReadAllBytes(Path.Combine(BrowserFileSystem.EngineDir, fontPath));

				using (var font = new WebPlatform().CreateFont(data))
				{
					var glyph = font.CreateGlyph(character[0], size, 1f);

					// A rasterized glyph must have a non-empty bitmap with some ink in
					// it; a linked-but-broken FreeType returns zeroed metrics instead.
					var ink = 0;
					if (glyph.Data != null)
						foreach (var b in glyph.Data)
							if (b != 0)
								ink++;

					return $"OK|{glyph.Size.Width}x{glyph.Size.Height}|{glyph.Advance:0.##}|" +
						$"{glyph.Offset.X},{glyph.Offset.Y}|{ink}";
				}
			}
			catch (Exception e)
			{
				return $"FAIL|{e.GetType().Name}: {e.Message}";
			}
		}
	}
}
