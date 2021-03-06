using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Orange
{
	class DeleteOrphanedMasks : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield break; } }
		public IEnumerable<string> BundleExtensions { get { yield return maskExtension; } }

		private readonly string maskExtension = ".mask";

		public DeleteOrphanedMasks(AssetCooker assetCooker) : base(assetCooker)	{ }

		public int GetOperationsCount() => The.Workspace.AssetFiles.Enumerate(maskExtension).Count();

		public void Action()
		{
			foreach (var maskPath in AssetCooker.AssetBundle.EnumerateFiles().ToList()) {
				if (maskPath.EndsWith(maskExtension, StringComparison.OrdinalIgnoreCase)) {
					var origImageFile = Path.ChangeExtension(maskPath, AssetCooker.GetPlatformTextureExtension());
					if (!AssetCooker.AssetBundle.FileExists(origImageFile)) {
						AssetCooker.DeleteFileFromBundle(maskPath);
					}
					UserInterface.Instance.IncreaseProgressBar();
				}
			}
		}
	}
}
