using System.Net;
using Lime.Profilers;
using Lime.Profilers.Contexts;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Profiler")]
	public class ProfilerGameUI : Widget
	{
		private static ClientContext context;

		private SimpleText statusLabel;
		private EditBox ipPortInput;
		private Widget editBoxBackground;
		private SimpleButton connectButton;
		private SimpleButton disconnectButton;
		private WidgetFlatFillPresenter backgroundPresenter;

		private Color4 backgroundColor = new Color4(45, 45, 45);

		[YuzuMember]
		public Color4 BackgroundColor
		{
			get { return backgroundColor; }
			set {
				backgroundColor = value;
				backgroundPresenter.Color = value;
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
				((WidgetFlatFillPresenter)editBoxBackground.Presenter).Color = value;
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

		public override bool IsNotDecorated() => false;

		public ProfilerGameUI()
		{
			LimeProfiler.Initialize();

			backgroundPresenter = new WidgetFlatFillPresenter(backgroundColor);
			Presenter = backgroundPresenter;

			statusLabel = new SimpleText {
				Text = "Not connected",
				FontHeight = FontHeight,
				Color = Color4.White,
				Anchors = Anchors.LeftRightTopBottom,
				HAlignment = HAlignment.Center,
				VAlignment = VAlignment.Center,
			};

			ipPortInput = new EditBox {
				Text = "ip:port",
				Color = Color4.White,
				Anchors = Anchors.LeftRightTopBottom,
				Presenter = new CustomFlatFillPresenter(componentsBackgroundColor)
			};
			ipPortInput.SetFocus();
			ipPortInput.TextWidget.FontHeight = FontHeight;
			ipPortInput.TextWidget.HAlignment = HAlignment.Center;
			ipPortInput.TextWidget.VAlignment = VAlignment.Center;
			ipPortInput.TextWidget.TrimWhitespaces = false;
			var editorParams = new EditorParams {
				MaxLines = 1,
				Scroll = ipPortInput.ScrollView,
				OffsetContextMenu = p => p + new Vector2(1f, ipPortInput.TextWidget.FontHeight + 1f),
				SelectAllOnFocus = true
			};
			ipPortInput.Editor = new Editor(
				displayWidget: ipPortInput.TextWidget,
				editorParams: editorParams,
				focusableWidget: ipPortInput,
				clickableWidget: ipPortInput.ScrollWidget);
			editBoxBackground = new Widget {
				HitTestTarget = true,
				Nodes = { ipPortInput }
			};

			connectButton = new SimpleButton {
				Text = "Connect",
				Visible = true,
				Anchors = Anchors.LeftRight,
				BackgroundColor = componentsBackgroundColor,
				Clicked = () => TryConnect()
			};
			connectButton.Caption.FontHeight = fontHeight;

			disconnectButton = new SimpleButton {
				Text = "Disconnect",
				Visible = false,
				BackgroundColor = componentsBackgroundColor,
				Clicked = () => {
					LimeProfiler.SetContext(new LocalContext());
					backgroundPresenter.Color = BackgroundColor;
					statusLabel.Text = "Not connected";
				}
			};
			disconnectButton.Caption.FontHeight = fontHeight;

			AddNode(new Widget {
				Layout = new VBoxLayout { Spacing = 8 },
				Padding = new Thickness(16),
				HitTestTarget = false,
				Anchors = Anchors.LeftRightTopBottom,
				Nodes = {
					statusLabel,
					editBoxBackground,
					connectButton,
					disconnectButton,
				}
			});

			UpdateWidgetsFontHeight();
		}

		[YuzuAfterDeserialization]
		private void Deserialized() => ((Widget)Nodes[0]).Size = Size;

		private void TryConnect()
		{
			var values = string.IsNullOrEmpty(ipPortInput.Text) ?
				new string[] { "" } : ipPortInput.Text.Split(':');
			IPAddress ip;
			int port = 0;
			bool isIpValid = IPAddress.TryParse(values[0], out ip);
			bool isPortValid = values.Length < 2 ? false : int.TryParse(values[1], out port);
			if (isIpValid && isPortValid) {
				context = new ClientContext();
				LimeProfiler.SetContext(context);
				if (!context.TryLaunch(new IPEndPoint(ip, port))) {
					LimeProfiler.SetContext(new LocalContext());
					statusLabel.Text = "Launch failed";
					backgroundPresenter.Color = Color4.Red;
				} else {
					connectButton.Visible = false;
					disconnectButton.Visible = true;
					statusLabel.Text = "Launch completed";
					backgroundPresenter.Color = Color4.Green.Darken(0.2f);
				}
			} else {
				connectButton.Visible = true;
				disconnectButton.Visible = false;
				statusLabel.Text =
					"wrong " +
					(!isIpValid ?                  "ip " : "") +
					(!isIpValid && !isPortValid ? "and " : "") +
					(!isPortValid ?               "port" : "");
				backgroundPresenter.Color = Color4.Red;
			}
		}

		private void UpdateWidgetsFontHeight()
		{
			statusLabel.FontHeight = fontHeight;
			ipPortInput.TextWidget.FontHeight = fontHeight;
			connectButton.Caption.FontHeight = fontHeight;
			disconnectButton.Caption.FontHeight = fontHeight;
		}

		private class CustomFlatFillPresenter : IPresenter
		{
			public Color4 BackgroundColor;

			public CustomFlatFillPresenter(Color4 backgroundColor) => BackgroundColor = backgroundColor;

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

		[YuzuDontGenerateDeserializer]
		public class SimpleButton : Button
		{
			private const int DefaultFontHeight = 16;

			public readonly SimpleText Caption;

			private Color4 backgroundColor;

			public Color4 BackgroundColor
			{
				get { return backgroundColor; }
				set {
					backgroundColor = value;
					((CustomFlatFillPresenter)Presenter).BackgroundColor = value;
				}
			}

			public SimpleButton()
			{
				Presenter = new CustomFlatFillPresenter(Color4.Blue);
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
		}
	}
}
