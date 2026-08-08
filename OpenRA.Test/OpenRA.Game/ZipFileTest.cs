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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.FileSystem;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class ZipFileTest
	{
		static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

		static string Text(Stream s)
		{
			using (var reader = new StreamReader(s))
				return reader.ReadToEnd();
		}

		[TestCase(TestName = "Zip packages round-trip writes, updates and deletes")]
		public void ReadWriteRoundTrip()
		{
			var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
			try
			{
				using (var package = ZipFileLoader.Create(path))
				{
					package.Update("first.txt", Bytes("one"));
					package.Update("nested/second.txt", Bytes("two"));

					Assert.That(package.Contains("first.txt"), Is.True);
					Assert.That(Text(package.GetStream("nested/second.txt")), Is.EqualTo("two"));

					// Updating an existing entry must replace it, not add a duplicate.
					package.Update("first.txt", Bytes("one modified"));
					Assert.That(Text(package.GetStream("first.txt")), Is.EqualTo("one modified"));
					Assert.That(package.Contents.Count(c => c == "first.txt"), Is.EqualTo(1));

					package.Delete("nested/second.txt");
					Assert.That(package.Contains("nested/second.txt"), Is.False);
					Assert.That(package.Contains("first.txt"), Is.True);
				}

				// Reopening from disk proves the changes were committed, not just held
				// in memory by the writing package.
				Assert.That(ZipFileLoader.TryParseReadWritePackage(path, out var reopened), Is.True);
				using (reopened)
				{
					Assert.That(Text(reopened.GetStream("first.txt")), Is.EqualTo("one modified"));
					Assert.That(reopened.Contains("nested/second.txt"), Is.False);
				}
			}
			finally
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[TestCase(TestName = "Zip packages expose directories as sub-packages")]
		public void DirectoriesOpenAsPackages()
		{
			var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
			try
			{
				using (var package = ZipFileLoader.Create(path))
				{
					package.Update("folder/inside.txt", Bytes("nested"));

					// Entry names are full paths, so a directory is addressed by prefix.
					Assert.That(package.Contents, Does.Contain("folder/inside.txt"));

					var context = new FileSystem.FileSystem("test", new Dictionary<string, Manifest>(), []);
					using (var folder = package.OpenPackage("folder", context))
					{
						Assert.That(folder, Is.Not.Null);
						Assert.That(folder.Contents, Does.Contain("inside.txt"));
						Assert.That(Text(folder.GetStream("inside.txt")), Is.EqualTo("nested"));
					}
				}
			}
			finally
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}
	}
}
