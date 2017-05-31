﻿#if WIN
using System;
using System.IO;

namespace Lime
{
	public class FileSystemWatcher : IFileSystemWatcher
	{
		private System.IO.FileSystemWatcher fsWatcher;

		public event Action<string> Changed;
		public event Action<string> Created;
		public event Action<string> Deleted;
		public event Action<string> Renamed;
		public string Path { get { return fsWatcher.Path; } set { fsWatcher.Path = value; } }
		public bool IncludeSubdirectories { get { return fsWatcher.IncludeSubdirectories; } set { fsWatcher.IncludeSubdirectories = value; } }

		public FileSystemWatcher(string path)
		{
			fsWatcher = new System.IO.FileSystemWatcher(path);
			fsWatcher.IncludeSubdirectories = true;
			// Watch for changes in LastAccess and LastWrite times, and the renaming of files or directories.
			fsWatcher.NotifyFilter =
				NotifyFilters.LastAccess | NotifyFilters.LastWrite |
				NotifyFilters.FileName | NotifyFilters.DirectoryName;
			fsWatcher.Changed += (sender, e) => Application.InvokeOnMainThread(() => Changed?.Invoke(e.FullPath));
			fsWatcher.Created += (sender, e) => Application.InvokeOnMainThread(() => Created?.Invoke(e.FullPath));
			fsWatcher.Deleted += (sender, e) => Application.InvokeOnMainThread(() => Deleted?.Invoke(e.FullPath));
			fsWatcher.Renamed += (sender, e) => Application.InvokeOnMainThread(() => Renamed?.Invoke(e.FullPath));
			fsWatcher.EnableRaisingEvents = true;
		}

		public void Dispose()
		{
			if (fsWatcher != null) {
				fsWatcher.Dispose();
				fsWatcher = null;
			}
		}
	}
}
#endif