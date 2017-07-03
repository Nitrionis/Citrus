#if !ANDROID && !iOS
using System;

namespace Lime
{
	public class ThemedButton : Button
	{
		protected override bool IsNotDecorated() => false;

		public ThemedButton(string caption) : this()
		{
			Text = caption;
		}

		public ThemedButton()
		{
			var presenter = new ButtonPresenter();
			Nodes.Clear();
			MinMaxSize = Theme.Metrics.DefaultButtonSize;
			Size = MinSize;
			Padding = Theme.Metrics.ControlsPadding;
			Presenter = presenter;
			PostPresenter = new Theme.KeyboardFocusBorderPresenter();
			DefaultAnimation.AnimationEngine = new AnimationEngineDelegate {
				OnRunAnimation = (animation, markerId) => {
					presenter.SetState(markerId);
					return true;
				}
			};
			var caption = new SimpleText {
				Id = "TextPresenter",
				TextColor = Theme.Colors.BlackText,
				FontHeight = Theme.Metrics.TextHeight,
				HAlignment = HAlignment.Center,
				VAlignment = VAlignment.Center,
				OverflowMode = TextOverflowMode.Ellipsis
			};
			AddNode(caption);
			TabTravesable = new TabTraversable();
			caption.ExpandToContainerWithAnchors();
		}

		public interface IButtonPresenter
		{
			void SetState(string state);
		}

		class ButtonPresenter : CustomPresenter, IButtonPresenter
		{
			private ColorGradient innerGradient;

			public void SetState(string state)
			{
				CommonWindow.Current.Invalidate();
				switch (state) {
				case "Press":
					innerGradient = Theme.Colors.ButtonPress;
					break;
				case "Focus":
					innerGradient = Theme.Colors.ButtonHover;
					break;
				case "Disable":
					innerGradient = Theme.Colors.ButtonDisable;
					break;
				default:
					innerGradient = Theme.Colors.ButtonDefault;
					break;
				}
			}

			public override void Render(Node node)
			{
				var widget = node.AsWidget;
				widget.PrepareRendererState();
				Renderer.DrawVerticalGradientRect(Vector2.Zero, widget.Size, innerGradient);
				Renderer.DrawRectOutline(Vector2.Zero, widget.Size, Theme.Colors.ControlBorder);
			}

			public override bool PartialHitTest(Node node, ref HitTestArgs args)
			{
				return node.PartialHitTest(ref args);
			}
		}
	}
}
#endif