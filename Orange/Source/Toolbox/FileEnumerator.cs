﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Orange
{
	public struct FileInfo
	{
		public string Path;
		public DateTime LastWriteTime;
	}

	public class FileEnumerator : IFileEnumerator
	{
		public string Directory { get; }
		public Predicate<FileInfo> EnumerationFilter;
		readonly List<FileInfo> files = new List<FileInfo>();

		public FileEnumerator(string directory)
		{
			Directory = directory;
			Rescan();
		}

		public void Rescan()
		{
			files.Clear();
			var dirInfo = new DirectoryInfo(Directory);

			foreach (var fileInfo in dirInfo.GetFiles("*.*", SearchOption.AllDirectories)) {
				var file = fileInfo.FullName;
				if (file.Contains(".svn"))
					continue;
				file = file.Remove(0, dirInfo.FullName.Length + 1);
				file = CsprojSynchronization.ToUnixSlashes(file);
				files.Add(new FileInfo { Path = file, LastWriteTime = fileInfo.LastWriteTime });
			}
		}

		public List<FileInfo> Enumerate(string extension = null)
		{
			if (extension == null && EnumerationFilter == null) {
				return files;
			}
			return files.Where(file => extension == null || Path.GetExtension(file.Path) == extension)
				.Where(file => EnumerationFilter == null || EnumerationFilter(file))
				.ToList();
		}
	}
}
