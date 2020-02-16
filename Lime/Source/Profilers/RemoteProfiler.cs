using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Profiler")]
	public class RemoteProfiler : Widget
	{
		private SimpleText statusLabel;
		private SimpleText ipPortInput;
		private SimpleButton connectButton;
		private SimpleButton disconnectButton;
		private SimpleButton clearAllButton;
		private SimpleButton clearOneButton;
		private SimpleButton[] keyboard;

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
				clearAllButton.BackgroundColor = value;
				clearOneButton.BackgroundColor = value;
				foreach (var button in keyboard) {
					button.BackgroundColor = value;
				}
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

		public RemoteProfiler()
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

			ipPortInput = new SimpleText {
				Text = "ip:port",
				FontHeight = FontHeight,
				Color = Color4.White,
				Anchors = Anchors.LeftRight,
				HAlignment = HAlignment.Center,
				VAlignment = VAlignment.Center,
			};
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

			clearAllButton = new SimpleButton {
				Text = "Clear",
				Clicked = () => {
					ipPortInput.Text = "";
				}
			};
			clearAllButton.Caption.FontHeight = fontHeight;
			AddNode(clearAllButton);

			clearOneButton = new SimpleButton {
				Text = "<X",
				Clicked = () => {
					ipPortInput.Text = ipPortInput.Text.Substring(0, ipPortInput.Text.Length - 1);
				}
			};
			clearOneButton.Caption.FontHeight = fontHeight;
			AddNode(clearOneButton);

			keyboard = new SimpleButton[12];
			var buttonsValues = new string[] {
				"1", "2", "3",
				"4", "5", "6",
				"7", "8", "9",
				".", "0", ":"
			};
			for (int i = 0; i < keyboard.Length; i++) {
				var button = new SimpleButton {
					Text = buttonsValues[i]
				};
				button.Caption.FontHeight = fontHeight;
				button.Clicked = () => {
					ipPortInput.Text += button.Caption.Text;
				};
				keyboard[i] = button;
				AddNode(button);
			}

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
			topOffset += spacing + (int)componentsSize.Y;

			clearAllButton.Position = new Vector2(leftMargin, topOffset);
			clearAllButton.Size = new Vector2(componentsSize.X / 2, componentsSize.Y);
			clearOneButton.Position = new Vector2(leftMargin + componentsSize.X / 2, topOffset);
			clearOneButton.Size = new Vector2(componentsSize.X / 2, componentsSize.Y);
			topOffset += spacing + (int)componentsSize.Y;

			UpdateKeyboardButtonsLocation(topOffset);
		}

		private void UpdateKeyboardButtonsLocation(int topMargin)
		{
			const int spacing = 3;
			int keyboardButtonWidth = ((int)componentsSize.X - 2 * spacing) / 3;
			for (int i = 0; i < keyboard.Length; i++) {
				keyboard[i].Position = new Vector2(
					leftMargin + (i % 3) * (keyboardButtonWidth + spacing),
					topMargin + (i / 3) * (keyboardButtonHeight + spacing)
				);
				keyboard[i].Size = new Vector2(keyboardButtonWidth, keyboardButtonHeight);
			}
		}

		private void UpdateWidgetsFontHeight()
		{
			statusLabel.FontHeight = fontHeight;
			ipPortInput.FontHeight = fontHeight;
			connectButton.Caption.FontHeight = fontHeight;
			disconnectButton.Caption.FontHeight = fontHeight;
			clearAllButton.Caption.FontHeight = fontHeight;
			clearOneButton.Caption.FontHeight = fontHeight;
			for (int i = 0; i < keyboard.Length; i++) {
				keyboard[i].Caption.FontHeight = fontHeight;
			}
		}

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
