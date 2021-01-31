using System;
using System.IO;

namespace Deli.VFS.Disk
{
	public sealed class FileHandle : IFileHandle, IDiskChildHandle
	{
		public string Path { get; }

		public string Name { get; }

		public DirectoryHandle Directory { get; }
		IDirectoryHandle IChildHandle.Directory => Directory;

		public string PathOnDisk { get; }

		public event Action? Updated;

		internal FileHandle(string name, string pathOnDisk, DirectoryHandle directory)
		{
			Path = directory.Path + name;
			Name = name;
			Directory = directory;
			PathOnDisk = pathOnDisk;
		}

		public Stream OpenRead()
		{
			return new FileStream(PathOnDisk, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
		}

		public override string ToString()
		{
			return PathOnDisk;
		}
	}
}
