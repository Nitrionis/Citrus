using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI.FilesDropHandler
{
	public class ImagesDropHandler : IFilesDropHandler
	{
		private readonly List<Type> imageTypes = new List<Type> {
			typeof(Image), typeof(DistortionMesh), typeof(NineGrid),
			typeof(TiledImage), typeof(ParticleModifier),
		};

		public FilesDropManager Manager { get; set; }
		public List<string> Extensions { get; } = new List<string> { ".png" };

		public bool TryHandle(IEnumerable<string> files)
		{
			CreateContextMenu(Utils.GetAssetPaths(files));
			return true;
		}

		private void CreateContextMenu(IEnumerable<string> assetPaths)
		{
			var menu = new Menu();
			foreach (var imageType in imageTypes) {
				if (NodeCompositionValidator.Validate(Document.Current.Container.GetType(), imageType)) {
					menu.Add(new Command($"Create {imageType.Name}",
						() => CreateImageTypeInstance(imageType, assetPaths)));
				}
			}
			menu.Popup();
		}


		private void CreateImageTypeInstance(Type type, IEnumerable<string> assetPaths)
		{
			using (Document.Current.History.BeginTransaction()) {
				foreach (var assetPath in assetPaths) {
					var args = new FilesDropManager.NodeCreatingEventArgs(assetPath, ".png");
					Manager.OnNodeCreating(args);
					if (args.Cancel) {
						continue;
					}
					var node = CreateNode.Perform(type);
					var texture = new SerializableTexture(assetPath);
					var nodeSize = (Vector2)texture.ImageSize;
					var nodeId = Path.GetFileNameWithoutExtension(assetPath);
					if (node is Widget) {
						SetProperty.Perform(node, nameof(Widget.Texture), texture);
						SetProperty.Perform(node, nameof(Widget.Pivot), Vector2.Half);
						SetProperty.Perform(node, nameof(Widget.Size), nodeSize);
						SetProperty.Perform(node, nameof(Widget.Id), nodeId);
					} else if (node is ParticleModifier) {
						SetProperty.Perform(node, nameof(ParticleModifier.Texture), texture);
						SetProperty.Perform(node, nameof(ParticleModifier.Size), nodeSize);
						SetProperty.Perform(node, nameof(ParticleModifier.Id), nodeId);
					}
					Manager.OnNodeCreated(node);
				}
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
