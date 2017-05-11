using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.RegularExpressions;
using Lime;
using Widget = Lime.Widget;

namespace Orange
{
	static class AnalyzeResources
	{
		internal struct PathRequestRecord
		{
			public string path;
			public string bundle;
		}

		internal static List<PathRequestRecord> requestedPaths;
		public class BundleSwitcher : AssetBundle
		{
			AssetBundle mainBundle;
			AssetBundle secondaryBundle;

			public BundleSwitcher(AssetBundle mainBundle, AssetBundle secondaryBundle)
			{
				this.mainBundle = mainBundle;
				this.secondaryBundle = secondaryBundle;
			}

			public override Stream OpenFile(string path)
			{
				if (mainBundle.FileExists(path)) {
					requestedPaths.Add(new PathRequestRecord() {
						path = path,
						bundle = (mainBundle as PackedAssetBundle).Path,
					});
					return mainBundle.OpenFile(path);
				} else {
					return secondaryBundle.OpenFile(path);
				}
			}

			public override DateTime GetFileLastWriteTime(string path)
			{
				if (mainBundle.FileExists(path)) {
					return mainBundle.GetFileLastWriteTime(path);
				} else {
					return secondaryBundle.GetFileLastWriteTime(path);
				}
			}

			public override void DeleteFile(string path)
			{
				throw new Lime.Exception("DeleteFile is not supported for BundleSwitcher (file '{0}')", path);
			}

			public override bool FileExists(string path)
			{
				return mainBundle.FileExists(path) || secondaryBundle.FileExists(path);
			}

			public override void ImportFile(string path, Stream stream, int reserve, string sourceExtension, AssetAttributes attributes)
			{
				throw new Lime.Exception("ImportFile is not supported for BundleSwitcher (file '{0}')", path);
			}

			public override IEnumerable<string> EnumerateFiles(string path = null)
			{
				foreach (var s in mainBundle.EnumerateFiles(path)) {
					yield return s;
				}
				foreach (var s in secondaryBundle.EnumerateFiles(path)) {
					yield return s;
				}
			}
		}

		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Analyze Resources")]
		public static void AnalyzeResourcesAction()
		{
			requestedPaths = new List<PathRequestRecord>();
			var crossRefReport = new List<Tuple<string, List<string>>>();
			var missingResourcesReport = new List<string>();
			var suspiciousTexturesReport = new List<string>();
			var bundles = new HashSet<string>();
			var cookingRulesMap = CookingRulesBuilder.Build(The.Workspace.AssetFiles,
				The.Workspace.ActivePlatform, The.Workspace.ActiveTarget);
			AssetBundle.Instance = new PackedAssetBundle(
				The.Workspace.GetBundlePath(CookingRulesBuilder.MainBundleName));
			foreach (var i in cookingRulesMap) {
				if (i.Key.EndsWith(".png")) {
					if (i.Value.TextureAtlas == null && i.Value.PVRFormat != PVRFormat.PVRTC4 && i.Value.PVRFormat != PVRFormat.PVRTC4_Forced) {
						suspiciousTexturesReport.Add(string.Format("{0}: {1}, atlas: none",
							i.Key, i.Value.PVRFormat));
					}
					if (i.Value.PVRFormat != PVRFormat.PVRTC4 && i.Value.PVRFormat != PVRFormat.PVRTC4_Forced && i.Value.PVRFormat != PVRFormat.PVRTC2) {
						int w;
						int h;
						bool hasAlpha;
						TextureConverterUtils.GetPngFileInfo(
							Path.Combine(The.Workspace.AssetsDirectory, i.Key), out w, out h,
							out hasAlpha);
						if (w >= 1024 || h >= 1024) {
							suspiciousTexturesReport.Add(string.Format("{3}: {0}, {1}, {2}, {4}, atlas: {5}",
								w, h, hasAlpha, i.Key, i.Value.PVRFormat, i.Value.TextureAtlas));
						}
					}
				}
				foreach (var bundle in i.Value.Bundle) {
					if (bundle != CookingRulesBuilder.MainBundleName) {
						bundles.Add(bundle);
					}
				}
			}
			foreach (var i in bundles) {
				AssetBundle.Instance = new BundleSwitcher(new PackedAssetBundle(The.Workspace.GetBundlePath(i)),
					AssetBundle.Instance);
			}
			The.Workspace.AssetFiles.EnumerationFilter = (info) => {
				CookingRules rules;
				if (cookingRulesMap.TryGetValue(info.Path, out rules)) {
					if (rules.Ignore)
						return false;
				}
				return true;
			};
			foreach (var srcFileInfo in The.Workspace.AssetFiles.Enumerate(".scene")) {
				var srcPath = srcFileInfo.Path;
				using (Lime.Frame scene = new Lime.Frame(srcPath)) {
					foreach (var j in scene.Descendants) {
						var checkTexture = new Action<ITexture>((Lime.ITexture t) => {
							if (t == null) {
								return;
							}
							string texPath;
							try {
								texPath = t.SerializationPath;
							} catch {
								return;
							}
							if (string.IsNullOrEmpty(texPath)) {
								return;
							}
							if (texPath.Length == 2 && texPath[0] == '#') {
								switch (texPath[1]) {
									case 'a': case'b': case 'c': case 'd': case 'e': case 'f': case 'g':
										return;
									default:
										suspiciousTexturesReport.Add(string.Format("wrong render target: {0}, {1}", texPath, j.ToString()));
										return;
								}
							}
							string[] possiblePaths = new string[]
							{
								texPath + ".atlasPart",
								texPath + ".pvr",
								texPath + ".jpg",
								texPath + ".png",
								texPath + ".dds",
								texPath + ".jpg",
								texPath + ".png",
							};
							foreach (var tpp in possiblePaths) {
								if (Lime.AssetBundle.Instance.FileExists(tpp)) {
									Lime.AssetBundle.Instance.OpenFile(tpp);
									return;
								}
							}
							missingResourcesReport.Add(string.Format("texture missing:\n\ttexture path: {0}\n\tscene path: {1}\n",
								t.SerializationPath, j.ToString()));
						});
						var checkAnimators = new Action<Node>((Node n) => {
							Lime.Animator<Lime.ITexture> ta;
							if (n.Animators.TryFind<ITexture>("Texture", out ta)) {
								foreach (var key in ta.ReadonlyKeys) {
									checkTexture(key.Value);
								}
							}
						});
						if (j is Widget) {
							var w = j as Lime.Widget;
							checkTexture(w.Texture);
							checkAnimators(w);
						} else if (j is ParticleModifier) {
							var pm = j as Lime.ParticleModifier;
							checkTexture(pm.Texture);
							checkAnimators(pm);
						} else if (j is Lime.Audio) {
							var au = j as Lime.Audio;
							var path = au.Sample.SerializationPath + ".sound";
							if (!Lime.AssetBundle.Instance.FileExists(path)) {
								missingResourcesReport.Add(string.Format("audio missing:\n\taudio path: {0}\n\tscene path: {1}\n",
									path, j.ToString()));
							} else {
								using (var tempStream = Lime.AssetBundle.Instance.OpenFile(path)) {

								}
							}
							// FIXME: should we check for audio:Sample animators too?
						}
					}
				}
				var reportList = new List<string>();
				foreach (var rpr in requestedPaths) {
					string pattern = String.Format(@".*[/\\](.*)\.{0}",
						Orange.Toolbox.GetTargetPlatformString(The.Workspace.ActivePlatform));
					string bundle = "";
					foreach (Match m in Regex.Matches(rpr.bundle, pattern, RegexOptions.IgnoreCase)) {
						bundle = m.Groups[1].Value;
					}
					int index = Array.IndexOf(cookingRulesMap[srcPath].Bundle, bundle);
					if (index == -1) {
						reportList.Add(string.Format("\t[{0}]=>[{2}]: {1}",
							string.Join(", ", cookingRulesMap[srcPath].Bundle), rpr.path, bundle));
					}
				}
				requestedPaths.Clear();
				if (reportList.Count > 0) {
					crossRefReport.Add(new Tuple<string, List<string>>(srcPath, reportList));
				}
				Lime.Application.FreeScheduledActions();
			}
			Action<string> writeHeader = (s) => {
				int n0 = (80 - s.Length) / 2;
				int n1 = (80 - s.Length)%2 == 0 ? n0 : n0 - 1;
				Console.WriteLine("\n" + new String('=', n0) + " " + s + " " + new String('=', n1));
			};
			writeHeader("Cross Bundle Dependencies");
			foreach (var scenePath in crossRefReport) {
				Console.WriteLine("\n" + scenePath.Item1);
				foreach (var refStr in scenePath.Item2) {
					Console.WriteLine(refStr);
				}
			}
			writeHeader("Missing Resources");
			foreach (var s in missingResourcesReport) {
				Console.WriteLine(s);
			}
			writeHeader("Suspicious Textures");
			foreach (var s in suspiciousTexturesReport) {
				Console.WriteLine(s);
			}
			The.Workspace.AssetFiles.EnumerationFilter = null;
		}
	}
}
