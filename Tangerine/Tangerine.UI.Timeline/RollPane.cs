﻿using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class RollPane
	{
		public readonly Widget ContentWidget;
		public readonly Widget RootWidget;
		public readonly Widget OverlayWidget;
		public event Action<Widget> OnRenderOverlay;

		public RollPane()
		{
			RootWidget = new Frame {
				Id = nameof(RollPane),
				MinWidth = TimelineMetrics.ToolbarMinWidth,
				ClipChildren = ClipMethod.ScissorTest,
				HitTestTarget = true,
			};
			ContentWidget = new Widget {
				Padding = new Thickness { Top = 1, Bottom = 1 },
				Width = RootWidget.Width,
				Height = 0,
				Anchors = Anchors.LeftRight,
				Layout = new VBoxLayout { Spacing = TimelineMetrics.RowSpacing },
				Presenter = new DelegatePresenter<Node>(RenderBackground)
			};
			OverlayWidget = new Widget { Presenter = new DelegatePresenter<Widget>(w => OnRenderOverlay?.Invoke(w)) };
			RootWidget.AddNode(OverlayWidget);
			RootWidget.AddNode(ContentWidget);
			ContentWidget.Updating += delta => ContentWidget.Y = -Timeline.Instance.ScrollPos.Y;
		}
				
		private void RenderBackground(Node node)
		{
			RootWidget.PrepareRendererState();
			Renderer.DrawRect(Vector2.Zero, RootWidget.Size, ColorTheme.Current.TimelineRoll.Lines);
		}
	}
}
