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
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenRA.FileSystem
{
	public class ZipFileLoader : IPackageLoader
	{
		const uint ZipSignature = 0x04034b50;

		public class ReadOnlyZipFile : IReadOnlyPackage
		{
			public string Name { get; protected set; }
			protected ZipArchive pkg;

			// Dummy constructor for use with ReadWriteZipFile
			protected ReadOnlyZipFile() { }

			public ReadOnlyZipFile(Stream s, string filename)
			{
				Name = filename;
				pkg = new ZipArchive(s, ZipArchiveMode.Read);
			}

			public Stream GetStream(string filename)
			{
				var entry = pkg.GetEntry(filename);
				if (entry == null)
					return null;

				// Entries are decompressed on demand and the returned stream is not
				// seekable, so the contents are copied out for the caller.
				using (var z = entry.Open())
				{
					var ms = new MemoryStream((int)entry.Length);
					z.CopyTo(ms);
					ms.Seek(0, SeekOrigin.Begin);
					return ms;
				}
			}

			public IEnumerable<string> Contents
			{
				get
				{
					foreach (var entry in pkg.Entries)
						if (!IsDirectory(entry))
							yield return entry.FullName;
				}
			}

			// Directories are stored with a trailing "/" in the index, and have no content.
			protected static bool IsDirectory(ZipArchiveEntry entry) => entry.FullName.EndsWith('/');

			public bool Contains(string filename)
			{
				return pkg.GetEntry(filename) != null;
			}

			public void Dispose()
			{
				pkg?.Dispose();
				GC.SuppressFinalize(this);
			}

			public IReadOnlyPackage OpenPackage(string filename, FileSystem context)
			{
				// Directories are stored with a trailing "/" in the index, but the entry
				// is optional: an archive may contain "folder/file" without ever naming
				// "folder/" itself, so a prefix match is the fallback.
				var entry = pkg.GetEntry(filename) ?? pkg.GetEntry(filename + "/");
				if (entry == null)
				{
					var prefix = filename + '/';
					if (pkg.Entries.Any(e => e.FullName.StartsWith(prefix, StringComparison.Ordinal)))
						return new ZipFolder(this, filename);

					return null;
				}

				if (IsDirectory(entry))
					return new ZipFolder(this, filename);

				// Other package types can be loaded normally
				var s = GetStream(filename);
				if (s == null)
					return null;

				if (context.TryParsePackage(s, filename, out var package))
					return package;

				s.Dispose();
				return null;
			}
		}

		public sealed class ReadWriteZipFile : ReadOnlyZipFile, IReadWritePackage
		{
			readonly MemoryStream pkgStream = new();

			public ReadWriteZipFile(string filename = null, bool create = false)
			{
				// Updating an archive in place requires writing back through the stream it
				// was opened from, so the contents are copied into memory and that copy
				// becomes the source of truth, cutting all outside references.
				if (!string.IsNullOrEmpty(filename) && !create)
				{
					var contents = File.ReadAllBytes(filename);
					pkgStream.Capacity = contents.Length;
					pkgStream.Write(contents);
				}
				else
				{
					// An empty stream is not a valid archive; writing one out produces the
					// end-of-central-directory record that makes it readable.
					using (new ZipArchive(pkgStream, ZipArchiveMode.Create, true)) { }
				}

				Name = filename;
				Reopen();
			}

			public ReadWriteZipFile(byte[] data)
			{
				pkgStream.Capacity = data.Length;
				pkgStream.Write(data);
				Name = null;
				Reopen();
			}

			/// <summary>
			/// Reopens the archive for reading over the in-memory copy. An archive opened
			/// for update only writes its index out when it is disposed, so mutations are
			/// applied by a short-lived update archive and the read view rebuilt after.
			/// </summary>
			void Reopen()
			{
				pkg?.Dispose();
				pkgStream.Position = 0;
				pkg = new ZipArchive(pkgStream, ZipArchiveMode.Read, true);
			}

			void Mutate(Action<ZipArchive> mutation)
			{
				// The read view holds the stream open, so it has to be dropped first.
				pkg?.Dispose();
				pkg = null;

				pkgStream.Position = 0;
				using (var update = new ZipArchive(pkgStream, ZipArchiveMode.Update, true))
					mutation(update);

				Reopen();
				Commit();
			}

			void Commit()
			{
				if (!string.IsNullOrEmpty(Name))
					File.WriteAllBytes(Name, pkgStream.ToArray());
			}

			public void Update(string filename, byte[] contents)
			{
				Mutate(archive =>
				{
					// Adding an entry that already exists would leave both in the index.
					archive.GetEntry(filename)?.Delete();

					var entry = archive.CreateEntry(filename);
					using (var s = entry.Open())
						s.Write(contents);
				});
			}

			public void Delete(string filename)
			{
				Mutate(archive => archive.GetEntry(filename)?.Delete());
			}
		}

		sealed class ZipFolder : IReadOnlyPackage
		{
			public string Name { get; }
			public ReadOnlyZipFile Parent { get; }

			public ZipFolder(ReadOnlyZipFile parent, string path)
			{
				if (path.EndsWith('/'))
					path = path[..^1];

				Name = path;
				Parent = parent;
			}

			public Stream GetStream(string filename)
			{
				// Zip files use '/' as a path separator
				return Parent.GetStream(Name + '/' + filename);
			}

			public IEnumerable<string> Contents
			{
				get
				{
					foreach (var entry in Parent.Contents)
					{
						if (entry.StartsWith(Name, StringComparison.Ordinal) && entry != Name)
						{
							var filename = entry[(Name.Length + 1)..];
							var dirLevels = filename.Split('/').Count(c => !string.IsNullOrEmpty(c));
							if (dirLevels == 1)
								yield return filename;
						}
					}
				}
			}

			public bool Contains(string filename)
			{
				return Parent.Contains(Name + '/' + filename);
			}

			public IReadOnlyPackage OpenPackage(string filename, FileSystem context)
			{
				return Parent.OpenPackage(Name + '/' + filename, context);
			}

			public void Dispose() { /* nothing to do */ }
		}

		public bool TryParsePackage(Stream s, string filename, FileSystem context, out IReadOnlyPackage package)
		{
			var readSignature = s.ReadUInt32();
			s.Position -= 4;

			if (readSignature != ZipSignature)
			{
				package = null;
				return false;
			}

			package = new ReadOnlyZipFile(s, filename);
			return true;
		}

		public static bool TryParseReadWritePackage(string filename, out IReadWritePackage package)
		{
			using (var s = File.OpenRead(filename))
			{
				if (s.ReadUInt32() != ZipSignature)
				{
					package = null;
					return false;
				}
			}

			package = new ReadWriteZipFile(filename);
			return true;
		}

		public static IReadWritePackage Create(string filename)
		{
			return new ReadWriteZipFile(filename, true);
		}
	}
}
