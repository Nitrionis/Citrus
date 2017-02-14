using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lime
{
	public class AggregateAssetBundle : AssetBundle
	{
		private readonly List<AssetBundle> bundles = new List<AssetBundle>();

		public AggregateAssetBundle(params AssetBundle[] bundles)
		{
			this.bundles.AddRange(bundles);
		}

		public void Attach(AssetBundle bundle)
		{
			bundles.Add(bundle);
		}

		public override Stream OpenFile(string path)
		{
			foreach (var bundle in bundles) {
				if (bundle.FileExists(path)) {
					return bundle.OpenFile(path);
				}
			}
			throw new FileNotFoundException($"File {path} not found in aggregate asset bundle.");
		}

		public override void Dispose()
		{
			foreach (var bundle in bundles) {
				bundle.Dispose();
			}
			bundles.Clear();
		}

		public override DateTime GetFileLastWriteTime(string path)
		{
			foreach (var bundle in bundles) {
				if (bundle.FileExists(path)) {
					return bundle.GetFileLastWriteTime(path);
				}
			}
			throw new InvalidOperationException($"Path {path} not found in aggregate asset bundle.");
		}

		public override void DeleteFile(string path)
		{
			throw new InvalidOperationException("Not supported by aggregate asset bundle.");
		}

		public override bool FileExists(string path)
		{
			foreach (var bundle in bundles) {
				if (bundle.FileExists(path)) {
					return true;
				}
			}
			return false;
		}

		public override void ImportFile(string path, Stream stream, int reserve, string sourceExtension,
			AssetAttributes attributes = AssetAttributes.None)
		{
			throw new InvalidOperationException("Not supported by aggregate asset bundle.");
		}

		public override IEnumerable<string> EnumerateFiles(string path = null)
		{
			return bundles.SelectMany(bundle => bundle.EnumerateFiles(path));
		}
	}
}