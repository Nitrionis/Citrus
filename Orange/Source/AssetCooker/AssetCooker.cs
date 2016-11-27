using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Lime;

namespace Orange
{
	public static class AssetCooker
	{
		private static readonly List<Action> cookStages = new List<Action>();
		public static IEnumerable<Action> CookStages { get { return cookStages; } }

		private delegate bool Converter(string srcPath, string dstPath);

		private static AssetsBundle assetsBundle { get { return AssetsBundle.Instance; } }
		private static TargetPlatform platform;
		private static Dictionary<string, CookingRules> cookingRulesMap;

		private static string atlasesPostfix = string.Empty;

		const int MaxAtlasChainLength = 1000;

		public static void CookForActivePlatform()
		{
			Cook(The.Workspace.ActivePlatform);
		}

		public static void AddStage(Action action)
		{
			cookStages.Add(action);
		}

		public static void RemoveStage(Action action)
		{
			cookStages.Remove(action);
		}

		static string GetOriginalAssetExtension(string path)
		{
			switch (Path.GetExtension(path)) {
			case ".dds":
			case ".pvr":
			case ".atlasPart":
			case ".mask":
			case ".jpg":
				return ".png";
			case ".sound":
				return ".ogg";
			case ".daeModel":
				return ".dae";
			case ".fbxModel":
				return ".fbx";
			default:
				return Path.GetExtension(path);
			}
		}

		static string GetPlatformTextureExtension()
		{
			switch (platform) {
				case TargetPlatform.iOS:
					return ".pvr";
				case TargetPlatform.Android:
					return ".pvr";
				case TargetPlatform.Unity:
					return ".png";
				default:
					return ".dds";
			}
		}

		public static void Cook(TargetPlatform platform)
		{
			AssetCooker.platform = platform;
			cookingRulesMap = CookingRulesBuilder.Build(The.Workspace.AssetFiles, platform, The.Workspace.Target);
			var extraBundles = new HashSet<string>();
			foreach (var dictionaryItem in cookingRulesMap) {
				foreach (var bundle in dictionaryItem.Value.Bundles) {
					if (bundle != CookingRules.MainBundleName) {
						extraBundles.Add(bundle);
					}
				}
			}
			CookBundle(CookingRules.MainBundleName);
			foreach (var extraBundle in extraBundles) {
				CookBundle(extraBundle);
			}
		}

		private static void CookBundle(string bundleName)
		{
			using (AssetsBundle.Instance = CreateBundle(bundleName)) {
				CookBundleHelper(bundleName);
			}
			// Open the bundle again in order to make some plugin post-processing (e.g. generate code from scene assets)
			using (AssetsBundle.Instance = CreateBundle(bundleName)) {
				using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
					PluginLoader.AfterAssetsCooked(bundleName);
				}
			}
			if (platform != TargetPlatform.Unity) {
				var bundlePath = The.Workspace.GetBundlePath(bundleName);
				PackedAssetsBundle.RefreshBundleCheckSum(bundlePath);
			}
		}

		private static AssetsBundle CreateBundle(string bundleName)
		{
			if (platform == TargetPlatform.Unity) {
				var path = The.Workspace.GetUnityProjectDirectory();
				path = bundleName == CookingRules.MainBundleName ?
					Path.Combine(path, "Assets", "Resources") :
					Path.Combine(path, "Assets", "Bundles", bundleName);
				Directory.CreateDirectory(path);
				return new UnityAssetBundle(path);
			}
			var bundlePath = The.Workspace.GetBundlePath(bundleName);

			// Create directory for bundle if it placed in subdirectory
			Directory.CreateDirectory(Path.GetDirectoryName(bundlePath));

			return new PackedAssetsBundle(bundlePath, AssetBundleFlags.Writable);
		}

		private static void CookBundleHelper(string bundleName)
		{
			Console.WriteLine("------------- Cooking Assets ({0}) -------------", bundleName);
			The.Workspace.AssetFiles.EnumerationFilter = (info) => {
				CookingRules rules;
				if (cookingRulesMap.TryGetValue(info.Path, out rules)) {
					if (rules.Ignore)
						return false;
					return Array.IndexOf(rules.Bundles, bundleName) != -1;
				} else {
					// There are no cooking rules for text files, consider them as part of the main bundle.
					return bundleName == CookingRules.MainBundleName;
				}
			};
			// Every asset bundle must have its own atlases folder, so they aren't conflict with each other
			atlasesPostfix = bundleName != CookingRules.MainBundleName ? bundleName : "";
			try {
				using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
					foreach (var stage in CookStages) {
						stage();
					}
				}
			} finally {
				The.Workspace.AssetFiles.EnumerationFilter = null;
				atlasesPostfix = "";
			}
		}

		static AssetCooker()
		{
			AddStage(SyncAtlases);
			AddStage(SyncDeleted);
			AddStage(() => SyncRawAssets(".json", AssetAttributes.ZippedDeflate));
			AddStage(() => SyncRawAssets(".txt", AssetAttributes.ZippedDeflate));
			AddStage(() => SyncRawAssets(".csv", AssetAttributes.ZippedDeflate));
			string rawAssetExtensions = The.Workspace.ProjectJson["RawAssetExtensions"] as string;
			if (rawAssetExtensions != null) {
				foreach (var extension in rawAssetExtensions.Split(' ')) {
					AddStage(() => SyncRawAssets(extension, AssetAttributes.ZippedDeflate));
				}
			}
			AddStage(SyncTextures);
			AddStage(DeleteOrphanedMasks);
			AddStage(DeleteOrphanedAlphaTextures);
			AddStage(SyncFonts);
			AddStage(SyncHotFonts);
			AddStage(() => SyncRawAssets(".ttf"));
			AddStage(() => SyncRawAssets(".otf"));
			AddStage(() => SyncRawAssets(".ogv"));
			AddStage(SyncModels);
			AddStage(SyncScenes);
			AddStage(SyncHotScenes);
			AddStage(SyncSounds);
			AddStage(() => SyncRawAssets(".shader"));
			AddStage(() => SyncRawAssets(".xml"));
			AddStage(() => SyncRawAssets(".raw"));
			AddStage(WarnAboutNPOTTextures);
			AddStage(() => SyncRawAssets(".bin"));
		}

		private static void WarnAboutNPOTTextures()
		{
			foreach (var path in assetsBundle.EnumerateFiles()) {
				if ((assetsBundle.GetAttributes(path) & AssetAttributes.NonPowerOf2Texture) != 0) {
					Console.WriteLine("Warning: non-power of two texture: {0}", path);
				}
			}
		}

		private static void DeleteOrphanedAlphaTextures()
		{
			var alphaExt = ".alpha" + GetPlatformTextureExtension();
			foreach (var path in assetsBundle.EnumerateFiles()) {
				if (path.EndsWith(alphaExt)) {
					var origImageFile =
						path.Substring(0, path.Length - alphaExt.Length) + GetPlatformTextureExtension();
					if (!assetsBundle.FileExists(origImageFile)) {
						DeleteFileFromBundle(path);
					}
				}
			}
		}

		private static void DeleteFileFromBundle(string path)
		{
			Console.WriteLine("- " + path);
			assetsBundle.DeleteFile(path);
		}

		private static void DeleteOrphanedMasks()
		{
			foreach (var maskPath in assetsBundle.EnumerateFiles()) {
				if (Path.GetExtension(maskPath) == ".mask") {
					var origImageFile = Path.ChangeExtension(maskPath, GetPlatformTextureExtension());
					if (!assetsBundle.FileExists(origImageFile)) {
						DeleteFileFromBundle(maskPath);
					}
				}
			}
		}

		private static void SyncRawAssets(string extension, AssetAttributes attributes = AssetAttributes.None)
		{
			SyncUpdated(extension, extension, (srcPath, dstPath) => {
				assetsBundle.ImportFile(srcPath, dstPath, 0, extension, attributes);
				return true;
			});
		}

		private static void SyncSounds()
		{
			const string sourceExtension = ".ogg";
			if (platform == TargetPlatform.Unity) {
				SyncRawAssets(sourceExtension);
				return;
			}
			SyncUpdated(sourceExtension, ".sound", (srcPath, dstPath) => {
				using (var stream = new FileStream(srcPath, FileMode.Open)) {
					// All sounds below 100kb size (can be changed with cooking rules) are converted
					// from OGG to Wav/Adpcm
					var rules = cookingRulesMap[srcPath];
					if (stream.Length > rules.ADPCMLimit * 1024) {
						assetsBundle.ImportFile(dstPath, stream, 0, sourceExtension);
					} else {
						Console.WriteLine("Converting sound to ADPCM/IMA4 format...");
						using (var input = new OggDecoder(stream)) {
							using (var output = new MemoryStream()) {
								WaveIMA4Converter.Encode(input, output);
								output.Seek(0, SeekOrigin.Begin);
								assetsBundle.ImportFile(dstPath, output, 0, sourceExtension);
							}
						}
					}
					return true;
				}
			});
		}

		private static void SyncScenes()
		{
			SyncUpdated(".tan", ".tan", (srcPath, dstPath) => {
				var node = Serialization.ReadObjectFromFile<Node>(srcPath);
				Serialization.WriteObjectToBundle(assetsBundle, dstPath, node, Serialization.Format.Binary, ".tan");
				return true;
			});
		}

		private static void SyncFonts()
		{
			SyncUpdated(".tft", ".tft", (srcPath, dstPath) => {
				var font = Serialization.ReadObjectFromFile<Font>(srcPath);
				Serialization.WriteObjectToBundle(assetsBundle, dstPath, font, Serialization.Format.Binary, ".tft");
				return true;
			});
		}

		private static void SyncHotScenes()
		{
			SyncUpdated(".scene", ".scene", (srcPath, dstPath) => {
				var importer = HotSceneImporterFactory.CreateImporter(srcPath);
				var node = importer.ParseNode();
				Serialization.WriteObjectToBundle(assetsBundle, dstPath, node, Serialization.Format.Binary, ".scene");
				return true;
			});
		}

		private static void SyncHotFonts()
		{
			SyncUpdated(".fnt", ".fnt", (srcPath, dstPath) => {
				var importer = new HotFontImporter();
				var font = importer.ParseFont(srcPath, dstPath);
				Serialization.WriteObjectToBundle(assetsBundle, dstPath, font, Serialization.Format.Binary, ".fnt");
				return true;
			});
		}

		private static void SyncTextures()
		{
			SyncUpdated(".png", GetPlatformTextureExtension(), (srcPath, dstPath) => {
				var rules = cookingRulesMap[Path.ChangeExtension(dstPath, ".png")];
				if (rules.TextureAtlas != null) {
					// No need to cache this texture since it is a part of texture atlas.
					return false;
				}
				if (platform == TargetPlatform.Unity) {
					assetsBundle.ImportFile(srcPath, dstPath, reserve: 0, sourceExtension: ".png");
				} else {
					Gdk.Pixbuf pixbuf = null;
					try {
						pixbuf = new Gdk.Pixbuf(srcPath);
						DownscaleTextureIfNeeded(ref pixbuf, srcPath, rules);
						ImportTexture(dstPath, pixbuf, rules);
					} finally {
						pixbuf.Dispose();
					}
				}
				return true;
			});
		}

		static void SyncDeleted()
		{
			var assetsFiles = new HashSet<string>();
			foreach (var fileInfo in The.Workspace.AssetFiles.Enumerate()) {
				assetsFiles.Add(fileInfo.Path);
			}
			foreach (var path in assetsBundle.EnumerateFiles()) {
				// Ignoring texture atlases
				if (path.StartsWith("Atlases")) {
					continue;
				}
				// Ignore atlas parts and masks
				var ext = Path.GetExtension(path);
				if (ext == ".atlasPart" || ext == ".mask") {
					continue;
				}
				var pathWithoutExt = Path.GetFileNameWithoutExtension(path);
				if (!string.IsNullOrEmpty(pathWithoutExt) && Path.GetExtension(pathWithoutExt) == ".alpha") {
					// Alpha mask
					continue;
				}
				var assetPath = Path.ChangeExtension(path, GetOriginalAssetExtension(path));
				if (!assetsFiles.Contains(assetPath)) {
					DeleteFileFromBundle(path);
				}
			}
		}

		static void SyncUpdated(string fileExtension, string bundleAssetExtension, Converter converter)
		{
			foreach (var srcFileInfo in The.Workspace.AssetFiles.Enumerate(fileExtension)) {
				var srcPath = srcFileInfo.Path;
				var dstPath = Path.ChangeExtension(srcPath, bundleAssetExtension);
				var bundled = assetsBundle.FileExists(dstPath);
				var needUpdate =  !bundled || srcFileInfo.LastWriteTime > assetsBundle.GetFileLastWriteTime(dstPath);
				if (needUpdate) {
					if (converter != null) {
						try {
							if (converter(srcPath, dstPath)) {
								Console.WriteLine((bundled ? "* " : "+ ") + dstPath);
							}
						} catch (System.Exception e) {
							Console.WriteLine(
								"An exception was caught while processing '{0}': {1}\n", srcPath, e.Message);
							throw;
						}
					} else {
						Console.WriteLine((bundled ? "* " : "+ ") + dstPath);
						using (Stream stream = new FileStream(srcPath, FileMode.Open, FileAccess.Read)) {
							assetsBundle.ImportFile(dstPath, stream, 0, fileExtension);
						}
					}
				}
			}
		}

		class AtlasItem
		{
			public string Path;
			public Gdk.Pixbuf Pixbuf;
			public IntRectangle AtlasRect;
			public bool Allocated;
			public bool MipMapped;
			public PVRFormat PVRFormat;
			public DDSFormat DDSFormat;
			public string SourceExtension;
		}

		static string GetAtlasPath(string atlasChain, int index)
		{
			var path = AssetPath.Combine(
				"Atlases" + atlasesPostfix, atlasChain + '.' + index.ToString("000") + GetPlatformTextureExtension());
			return path;
		}

		static void BuildAtlasChain(string atlasChain)
		{
			for (var i = 0; i < MaxAtlasChainLength; i++) {
				var atlasPath = GetAtlasPath(atlasChain, i);
				if (assetsBundle.FileExists(atlasPath)) {
					DeleteFileFromBundle(atlasPath);
					var alphaPath = GetAlphaTexturePath(atlasPath);
					if (assetsBundle.FileExists(alphaPath)) {
						DeleteFileFromBundle(alphaPath);
					}
				} else {
					break;
				}
			}
			var items = new Dictionary<AtlasOptimization, List<AtlasItem>>();
			items[AtlasOptimization.Memory] = new List<AtlasItem>();
			items[AtlasOptimization.DrawCalls] = new List<AtlasItem>();
			foreach (var fileInfo in The.Workspace.AssetFiles.Enumerate(".png")) {
				var cookingRules = cookingRulesMap[fileInfo.Path];
				if (cookingRules.TextureAtlas == atlasChain) {
					var srcTexturePath = AssetPath.Combine(The.Workspace.AssetsDirectory, fileInfo.Path);
					var pixbuf = new Gdk.Pixbuf(srcTexturePath);
					DownscaleTextureIfNeeded(ref pixbuf, srcTexturePath, cookingRules);
					// Ensure that no image exceeded maxAtlasSize limit
					DownscaleTextureToFitAtlas(ref pixbuf, srcTexturePath);
					var item = new AtlasItem {
						Path = Path.ChangeExtension(fileInfo.Path, ".atlasPart"),
						Pixbuf = pixbuf,
						MipMapped = cookingRules.MipMaps,
						PVRFormat = cookingRules.PVRFormat,
						DDSFormat = cookingRules.DDSFormat,
						SourceExtension = Path.GetExtension(fileInfo.Path)
					};
					items[cookingRules.AtlasOptimization].Add(item);
				}
			}
			var initialAtlasId = 0;
			foreach (var kv in items) {
				initialAtlasId = PackItemsToAtlasWithBestSize(atlasChain, kv.Value, kv.Key, initialAtlasId);
			}
		}

		private static int PackItemsToAtlasWithBestSize(string atlasChain, List<AtlasItem> items,
			AtlasOptimization atlasOptimization, int initialAtlasId)
		{
			// Sort images in descending size order
			items.Sort((x, y) => {
				var a = Math.Max(x.Pixbuf.Width, x.Pixbuf.Height);
				var b = Math.Max(y.Pixbuf.Width, y.Pixbuf.Height);
				return b - a;
			});

			// PVRTC2/4 textures must be square
			var squareAtlas = (platform == TargetPlatform.iOS) && items.Any(
				i => i.PVRFormat == PVRFormat.PVRTC4 ||
					i.PVRFormat == PVRFormat.PVRTC4_Forced ||
					i.PVRFormat == PVRFormat.PVRTC2);
			var atlasId = initialAtlasId;
			while (items.Count > 0) {
				if (atlasId >= MaxAtlasChainLength) {
					throw new Lime.Exception("Too many textures in the atlas chain {0}", atlasChain);
				}
				var bestSize = new Size(0, 0);
				double bestPackRate = 0;
				int minItemsLeft = Int32.MaxValue;
				foreach (var size in EnumerateAtlasSizes(squareAtlas: squareAtlas)) {
					double packRate;
					PackItemsToAtlas(items, size, out packRate);
					switch (atlasOptimization) {
						case AtlasOptimization.Memory:
							if (packRate * 0.95f > bestPackRate) {
								bestPackRate = packRate;
								bestSize = size;
							}
							break;
						case AtlasOptimization.DrawCalls: {
							var notAllocatedCount = items.Count(item => !item.Allocated);
							if (notAllocatedCount < minItemsLeft) {
								minItemsLeft = notAllocatedCount;
								bestSize = size;
							}
							break;
						}
					}
				}
				if (atlasOptimization == AtlasOptimization.Memory && bestPackRate == 0) {
					throw new Lime.Exception("Failed to create atlas '{0}'", atlasChain);
				}
				PackItemsToAtlas(items, bestSize, out bestPackRate);
				CopyAllocatedItemsToAtlas(items, atlasChain, atlasId, bestSize);
				items.RemoveAll(x => x.Allocated);
				atlasId++;
			}
			return atlasId;
		}

		private static string GetAlphaTexturePath(string path)
		{
			return Path.ChangeExtension(path, ".alpha" + GetPlatformTextureExtension());
		}

		private static IEnumerable<Size> EnumerateAtlasSizes(bool squareAtlas)
		{
			if (squareAtlas) {
				for (var i = 64; i <= GetMaxAtlasSize().Width; i *= 2) {
					yield return new Size(i, i);
				}
			} else {
				for (var i = 64; i <= GetMaxAtlasSize().Width / 2; i *= 2) {
					yield return new Size(i, i);
					yield return new Size(i * 2, i);
					yield return new Size(i, i * 2);
				}
				yield return GetMaxAtlasSize();
			}
		}

		private static Size GetMaxAtlasSize()
		{
			return new Size(1024, 1024);
		}

		private static void PackItemsToAtlas(List<AtlasItem> items, Size size, out double packRate)
		{
			items.ForEach(i => i.Allocated = false);
			// Take in account 1 pixel border for each side.
			var a = new RectAllocator(new Size(size.Width + 2, size.Height + 2));
			AtlasItem firstAllocatedItem = null;
			foreach (var item in items) {
				var sz = new Size(item.Pixbuf.Width + 2, item.Pixbuf.Height + 2);
				if (firstAllocatedItem == null || AreAtlasItemsCompatible(firstAllocatedItem, item)) {
					if (a.Allocate(sz, out item.AtlasRect)) {
						item.Allocated = true;
						firstAllocatedItem = firstAllocatedItem ?? item;
					}
				}
			}
			packRate = a.GetPackRate();
		}

		/// <summary>
		/// Checks whether two items can be packed to the same texture
		/// </summary>
		private static bool AreAtlasItemsCompatible(AtlasItem item1, AtlasItem item2)
		{
			if (item1.MipMapped != item2.MipMapped) {
				return false;
			}
			switch (platform) {
				case TargetPlatform.Android:
				case TargetPlatform.iOS:
					return item1.PVRFormat == item2.PVRFormat && item1.Pixbuf.HasAlpha == item2.Pixbuf.HasAlpha;
				case TargetPlatform.Desktop:
					return item1.DDSFormat == item2.DDSFormat;
				case TargetPlatform.Unity:
					return true;
				default:
					throw new ArgumentException();
			}
		}

		private static void CopyAllocatedItemsToAtlas(List<AtlasItem> items, string atlasChain, int atlasId, Size size)
		{
			var atlasPath = GetAtlasPath(atlasChain, atlasId);
			var hasAlpha = items.Where(i => i.Allocated).Any(i => i.Pixbuf.HasAlpha);
			var atlas = new Gdk.Pixbuf(Gdk.Colorspace.Rgb, hasAlpha, 8, size.Width, size.Height);
			atlas.Fill(0);
			foreach (var item in items.Where(i => i.Allocated)) {
				var p = item.Pixbuf;
				p.CopyArea(0, 0, p.Width, p.Height, atlas, item.AtlasRect.A.X, item.AtlasRect.A.Y);
				var atlasPart = new TextureAtlasElement.Params();
				atlasPart.AtlasRect = item.AtlasRect;
				atlasPart.AtlasRect.B -= new IntVector2(2, 2);
				atlasPart.AtlasPath = Path.ChangeExtension(atlasPath, null);
				Serialization.WriteObjectToBundle(assetsBundle, item.Path, atlasPart, Serialization.Format.Binary, item.SourceExtension);
				// Delete non-atlased texture since now its useless
				var texturePath = Path.ChangeExtension(item.Path, GetPlatformTextureExtension());
				if (assetsBundle.FileExists(texturePath)) {
					DeleteFileFromBundle(texturePath);
				}
			}
			Console.WriteLine("+ " + atlasPath);
			var firstItem = items.First(i => i.Allocated);
			var rules = new CookingRules {
				MipMaps = firstItem.MipMapped,
				PVRFormat = firstItem.PVRFormat,
				DDSFormat = firstItem.DDSFormat
			};
			ImportTexture(atlasPath, atlas, rules);
		}

		private static bool ShouldGenerateOpacityMasks()
		{
			return !The.Workspace.ProjectJson.GetValue("DontGenerateOpacityMasks", false);
		}

		private static void ImportTexture(string path, Gdk.Pixbuf texture, CookingRules rules)
		{
			if (ShouldGenerateOpacityMasks()) {
				var maskPath = Path.ChangeExtension(path, ".mask");
				OpacityMaskCreator.CreateMask(assetsBundle, texture, maskPath);
			}
			var attributes = AssetAttributes.ZippedDeflate;
			if (!TextureConverterUtils.IsPowerOf2(texture.Width) || !TextureConverterUtils.IsPowerOf2(texture.Height)) {
				attributes |= AssetAttributes.NonPowerOf2Texture;
			}
			var alphaPath = GetAlphaTexturePath(path);
			switch (platform) {
				case TargetPlatform.Unity:
					ConvertTexture(path, attributes, file => texture.Save(file, "png"));
					break;
				case TargetPlatform.Android:
					ConvertTexture(path, attributes, file => TextureConverter.ToPVR(texture, file, rules.MipMaps, rules.HighQualityCompression, rules.PVRFormat));
					// ETC1 textures on Android use separate alpha channel
					if (texture.HasAlpha && rules.PVRFormat == PVRFormat.ETC1) {
						using (var alphaTexture = new Gdk.Pixbuf(texture, 0, 0, texture.Width, texture.Height)) {
							TextureConverterUtils.ConvertBitmapToAlphaMask(alphaTexture);
							ConvertTexture(alphaPath, AssetAttributes.ZippedDeflate, file => TextureConverter.ToPVR(alphaTexture, file, rules.MipMaps, rules.HighQualityCompression, PVRFormat.ETC1));
						}
					}
					break;
				case TargetPlatform.iOS:
					ConvertTexture(path, attributes, file => TextureConverter.ToPVR(texture, file, rules.MipMaps, rules.HighQualityCompression, rules.PVRFormat));
					break;
				case TargetPlatform.Desktop:
					ConvertTexture(path, attributes, file => TextureConverter.ToDDS(texture, file, rules.DDSFormat, rules.MipMaps));
					break;
				default:
					throw new Lime.Exception();
			}
		}

		private static void ConvertTexture(string path, AssetAttributes attributes, Action<string> converter)
		{
			string sourceExtension = Path.GetExtension(path);
			var tmpFile = Toolbox.GetTempFilePathWithExtension(sourceExtension);
			try {
				converter(tmpFile);
				assetsBundle.ImportFile(tmpFile, path, 0, "", attributes);
			} finally {
				File.Delete(tmpFile);
			}
		}

		private static void DownscaleTextureToFitAtlas (ref Gdk.Pixbuf texture, string path)
		{
			var maxWidth = GetMaxAtlasSize ().Width;
			var maxHeight = GetMaxAtlasSize ().Height;
			if (texture.Width > maxWidth || texture.Height > maxHeight) {
				float ratio = Mathf.Min (maxWidth / (float)texture.Width, maxHeight / (float)texture.Height);
				int w = Math.Min ((texture.Width * ratio).Round (), maxWidth);
				int h = Math.Min ((texture.Height * ratio).Round (), maxHeight);
				Console.WriteLine ("{0} downscaled to {1}x{2}", path, w, h);
				texture = texture.ScaleSimple (w, h, Gdk.InterpType.Bilinear);
			}
		}

		private static void DownscaleTextureToFitAtlas(ref Gdk.Pixbuf texture, string path, CookingRules rules)
		{
			var maxWidth = GetMaxAtlasSize().Width;
			var maxHeight = GetMaxAtlasSize().Height;
			if (texture.Width > maxWidth || texture.Height > maxHeight) {
				float ratio = Mathf.Min(maxWidth / (float)texture.Width, maxHeight / (float)texture.Height);
				int w = Math.Min((texture.Width * ratio).Round(), maxWidth);
				int h = Math.Min((texture.Height * ratio).Round(), maxHeight);
				Console.WriteLine("{0} downscaled to {1}x{2}", path, w, h);
				texture = texture.ScaleSimple(w, h, Gdk.InterpType.Bilinear);
			}
		}

		private static void DownscaleTextureIfNeeded(ref Gdk.Pixbuf texture, string path, CookingRules rules)
		{
			if (rules.TextureScaleFactor != 1.0f) {
				const int maxSize = 1024;
				const float scaleRatio = 0.75f;
				int scaleLargerThan = (platform == TargetPlatform.Android) ? 32 : 256;
				if (texture.Width > scaleLargerThan || texture.Height > scaleLargerThan) {
					var ratio = scaleRatio;
					if (texture.Width > maxSize || texture.Height > maxSize) {
						var max = Math.Max(texture.Width, texture.Height);
						ratio *= maxSize / (float)max;
					}
					int w = texture.Width;
					int h = texture.Height;
					if (texture.Width > scaleLargerThan) {
						w = Math.Min((texture.Width * ratio).Round(), maxSize);
					}
					if (texture.Height > scaleLargerThan) {
						h = Math.Min((texture.Height * ratio).Round(), maxSize);
					}
					Console.WriteLine("{0} downscaled to {1}x{2}", path, w, h);
					texture = texture.ScaleSimple(w, h, Gdk.InterpType.Bilinear);
				}
			}
		}

		static void SyncAtlases()
		{
			var textures = new Dictionary<string, DateTime>();
			foreach (var fileInfo in The.Workspace.AssetFiles.Enumerate(".png")) {
				textures[fileInfo.Path] = fileInfo.LastWriteTime;
			}
			var atlasChainsToRebuild = new HashSet<string>();
			// Figure out atlas chains to rebuild
			foreach (var atlasPartPath in assetsBundle.EnumerateFiles()) {
				if (Path.GetExtension(atlasPartPath) != ".atlasPart")
					continue;

				// If atlas part has been outdated we should rebuild full atlas chain
				var srcTexturePath = Path.ChangeExtension(atlasPartPath, ".png");
				if (!textures.ContainsKey(srcTexturePath) || assetsBundle.GetFileLastWriteTime(atlasPartPath) < textures[srcTexturePath]) {
					srcTexturePath = AssetPath.Combine(The.Workspace.AssetsDirectory, srcTexturePath);
					var part = TextureAtlasElement.Params.ReadFromBundle(atlasPartPath);
					var atlasChain = Path.GetFileNameWithoutExtension(part.AtlasPath);
					atlasChainsToRebuild.Add(atlasChain);
					if (!textures.ContainsKey(srcTexturePath)) {
						DeleteFileFromBundle(atlasPartPath);
					} else {
						srcTexturePath = Path.ChangeExtension(atlasPartPath, ".png");
						if (cookingRulesMap[srcTexturePath].TextureAtlas != null) {
							var rules = cookingRulesMap[srcTexturePath];
							atlasChainsToRebuild.Add(rules.TextureAtlas);
						} else {
							DeleteFileFromBundle(atlasPartPath);
						}
					}
				}
			}
			// Find which new textures must be added to the atlas chain
			foreach (var t in textures) {
				var atlasPartPath = Path.ChangeExtension(t.Key, ".atlasPart");
				var cookingRules = cookingRulesMap[t.Key];
				var atlasNeedRebuld = cookingRules.TextureAtlas != null && !assetsBundle.FileExists(atlasPartPath);
				if (atlasNeedRebuld) {
					atlasChainsToRebuild.Add(cookingRules.TextureAtlas);
				}
			}
			foreach (var atlasChain in atlasChainsToRebuild) {
				BuildAtlasChain(atlasChain);
			}
		}

		private static void SyncModels()
		{
			SyncUpdated(".fbx", ".fbxModel", ConvertModel);
			SyncUpdated(".dae", ".daeModel", ConvertModel);
		}

		private static bool ConvertModel(string srcPath, string dstPath)
		{
			var rootNode = new Lime.Frame();
			rootNode.AddNode(new ModelImporter(srcPath, The.Workspace.ActivePlatform).Model);

			var rules = cookingRulesMap[srcPath];
			Serialization.WriteObjectToBundle(assetsBundle, dstPath, rootNode, Serialization.Format.Binary, Path.GetExtension(srcPath), rules.ModelCompressing);
			return true;
		}
	}
}
