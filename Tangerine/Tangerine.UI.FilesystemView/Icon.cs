using System.IO;
using Lime;

namespace Tangerine.UI.FilesystemView
{
	public class Icon : Widget
	{
		public string FilesystemPath;
		public Icon(string path)
		{
			SimpleText text = null;
			FilesystemPath = path;
			MinMaxSize = new Vector2(200, 16);
			Layout = new HBoxLayout();
			//PostPresenter = new LayoutDebugPresenter(Color4.Red.Transparentify(0.5f));
			Padding = new Thickness(2);
			HitTestTarget = true;
			Nodes.AddRange(
				new Image {
					LayoutCell = new LayoutCell
					{
						Stretch = Vector2.Zero,
						Alignment = new Alignment { X = HAlignment.Right, Y = VAlignment.Center }
					},
					MinMaxSize = new Vector2(16, 16),
					Texture = SystemIconTextureProvider.Instance.GetTexture(path),
				},
				(text = new SimpleText {
					ForceUncutText = false,
					OverflowMode = TextOverflowMode.Ellipsis,
					Text = Path.GetFileName(path),
					LayoutCell = new LayoutCell {
						Alignment = new Alignment { X = HAlignment.Right, Y = VAlignment.Bottom }
					}
				}),
				new Widget {
					LayoutCell = new LayoutCell
					{
						StretchX = float.MaxValue
					}
				}
			);
			text.Width = text.MinMaxWidth = Mathf.Min(200 - 16, text.MeasureUncutText().X);
		}
	}
}