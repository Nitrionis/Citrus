using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Profiler")]
	public class ProfilerGameUI : Widget
	{
		private SimpleText statusLabel;
		private EditBox ipPortInput;
		private SimpleButton connectButton;
		private SimpleButton disconnectButton;

		private Color4 backgroundColor = new Color4(45, 45, 45);

		[YuzuMember]
		public Color4 BackgroundColor
		{
			get { return backgroundColor; }
			set {
				backgroundColor = value;
				((WidgetFlatFillPresenter)Presenter).Color = value;
			}
		}

		private Color4 componentsBackgroundColor = new Color4(45, 45, 45).Lighten(0.1f);

		[YuzuMember]
		public Color4 ComponentsBackgroundColor
		{
			get { return componentsBackgroundColor; }
			set {
				componentsBackgroundColor = value;
				connectButton.BackgroundColor = value;
				disconnectButton.BackgroundColor = value;
			}
		}

		private int fontHeight = 32;

		[YuzuMember]
		public int FontHeight
		{
			get { return fontHeight; }
			set {
				fontHeight = value;
				UpdateWidgetsFontHeight();
			}
		}

		private int leftMargin = 32;

		[YuzuMember]
		public int LeftMargin
		{
			get { return leftMargin; }
			set {
				leftMargin = value;
				UpdateComponentsLocation();
			}
		}

		private Vector2 componentsSize = new Vector2(256, 32);

		[YuzuMember]
		public Vector2 ComponentsSize
		{
			get { return componentsSize; }
			set {
				componentsSize = value;
				UpdateComponentsLocation();
			}
		}

		private int keyboardButtonHeight = 32;

		[YuzuMember]
		public int KeyboardButtonHeight
		{
			get { return keyboardButtonHeight; }
			set {
				keyboardButtonHeight = value;
				UpdateComponentsLocation();
			}
		}

		public override bool IsNotDecorated() => false;

		public ProfilerGameUI()
		{
			Presenter = new WidgetFlatFillPresenter(backgroundColor);

			statusLabel = new SimpleText {
				Text = "Not connected",
				FontHeight = FontHeight,
				Color = Color4.White,
				Anchors = Anchors.LeftRight,
				HAlignment = HAlignment.Center,
				VAlignment = VAlignment.Center,
			};
			AddNode(statusLabel);

			ipPortInput = new EditBox {
				Text = "ip:port",
				Color = Color4.White,
				Anchors = Anchors.LeftRight
			};
			ipPortInput.TextWidget.FontHeight = FontHeight;
			ipPortInput.TextWidget.HAlignment = HAlignment.Center;
			ipPortInput.TextWidget.VAlignment = VAlignment.Center;
			AddNode(ipPortInput);

			connectButton = new SimpleButton {
				Text = "Connect",
				Clicked = () => {
					connectButton.BackgroundColor = Color4.Green;
				}
			};
			connectButton.Caption.FontHeight = fontHeight;
			AddNode(connectButton);

			disconnectButton = new SimpleButton {
				Text = "Disconnect",
				Visible = false
			};
			disconnectButton.Caption.FontHeight = fontHeight;
			AddNode(disconnectButton);

			UpdateComponentsLocation();
		}

		private void UpdateComponentsLocation()
		{
			const int spacing = 8;
			int topOffset = 32;

			statusLabel.Size = componentsSize;
			statusLabel.Position = new Vector2(leftMargin, topOffset);
			topOffset += spacing + (int)componentsSize.Y;

			ipPortInput.Size = componentsSize;
			ipPortInput.Position = new Vector2(leftMargin, topOffset);
			topOffset += spacing + (int)componentsSize.Y;

			connectButton.Size = componentsSize;
			connectButton.Position = new Vector2(leftMargin, topOffset);
			disconnectButton.Size = componentsSize;
			disconnectButton.Position = new Vector2(leftMargin, topOffset);
		}

		private void UpdateWidgetsFontHeight()
		{
			statusLabel.FontHeight = fontHeight;
			ipPortInput.TextWidget.FontHeight = fontHeight;
			connectButton.Caption.FontHeight = fontHeight;
			disconnectButton.Caption.FontHeight = fontHeight;
		}

		[YuzuDontGenerateDeserializer]
		private class SimpleButton : Button
		{
			private const int DefaultFontHeight = 16;

			public readonly SimpleText Caption;

			private Color4 backgroundColor;

			public Color4 BackgroundColor
			{
				get { return backgroundColor; }
				set {
					backgroundColor = value;
					((ButtonPresenter)Presenter).BackgroundColor = value;
				}
			}

			public SimpleButton()
			{
				Presenter = new ButtonPresenter(Color4.Blue);
				HitTestTarget = true;
				HitTestMethod = HitTestMethod.BoundingRect;
				Caption = new SimpleText {
					Id = "TextPresenter",
					TextColor = Color4.White,
					FontHeight = DefaultFontHeight,
					HAlignment = HAlignment.Center,
					VAlignment = VAlignment.Center,
					OverflowMode = TextOverflowMode.Ellipsis,
					HitTestTarget = false
				};
				AddNode(Caption);
				Caption.ExpandToContainerWithAnchors();
			}

			private class ButtonPresenter : IPresenter
			{
				public Color4 BackgroundColor;

				public ButtonPresenter(Color4 backgroundColor) => BackgroundColor = backgroundColor;

				public bool PartialHitTest(Node node, ref HitTestArgs args) => node.PartialHitTest(ref args);

				public virtual Lime.RenderObject GetRenderObject(Node node)
				{
					var widget = (Widget)node;
					var ro = RenderObjectPool<RenderObject>.Acquire();
					ro.CaptureRenderState(widget);
					ro.Size = widget.Size;
					ro.BackgroundColor = BackgroundColor;
					return ro;
				}

				public IPresenter Clone() => (IPresenter)MemberwiseClone();

				private class RenderObject : WidgetRenderObject
				{
					public Vector2 Size;
					public Color4 BackgroundColor;

					public override void Render()
					{
						PrepareRenderState();
						Renderer.DrawRect(Vector2.Zero, Size, BackgroundColor);
					}
				}
			}
		}
	}
}
