using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncTextures: ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return originalTextureExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return PlatformTextureExtension; } }

		private readonly string originalTextureExtension = ".png";
		private string PlatformTextureExtension => AssetCooker.GetPlatformTextureExtension();

		public void Action()
		{
			SyncUpdated.Sync(originalTextureExtension, PlatformTextureExtension, AssetBundle.Current, Converter);
		}

		private bool Converter(string srcPath, string dstPath)
		{
			var rules = AssetCooker.CookingRulesMap[Path.ChangeExtension(dstPath, originalTextureExtension)];
			if (rules.TextureAtlas != null) {
				// Reverse double counting
				UserInterface.Instance.IncreaseProgressBar(-1);
				// No need to cache this texture since it is a part of texture atlas.
				return false;
			}
			using (var stream = File.OpenRead(srcPath)) {
				var bitmap = new Bitmap(stream);
				if (TextureTools.ShouldDownscale(bitmap, rules)) {
					var scaledBitmap = TextureTools.DownscaleTexture(bitmap, srcPath, rules);
					bitmap.Dispose();
					bitmap = scaledBitmap;
				}
				AssetCooker.ImportTexture(dstPath, bitmap, rules, File.GetLastWriteTime(srcPath), rules.SHA1);
				bitmap.Dispose();
			}
			return true;
		}
	}
}