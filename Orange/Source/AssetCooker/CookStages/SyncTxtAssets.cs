using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncTxtAssets: AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return txtExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return txtExtension; } }

		private readonly string txtExtension = ".txt";
		private readonly string t3dExtension = ".t3d";

		public SyncTxtAssets(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationsCount() => SyncUpdated.GetOperationsCount(txtExtension);

		public void Action() => SyncUpdated.Sync(txtExtension, txtExtension, AssetBundle.Current, Converter);

		private bool Converter(string srcPath, string dstPath)
		{
			var modelAttachmentExtIndex = dstPath.LastIndexOf(Model3DAttachment.FileExtension);
			if (modelAttachmentExtIndex >= 0) {
				AssetCooker.ModelsToRebuild.Add(dstPath.Remove(modelAttachmentExtIndex) + t3dExtension);
			}
			AssetCooker.AssetBundle.ImportFile(srcPath, dstPath, 0, txtExtension, AssetAttributes.Zipped, File.GetLastWriteTime(srcPath), AssetCooker.CookingRulesMap[srcPath].SHA1);
			return true;
		}
	}
}
