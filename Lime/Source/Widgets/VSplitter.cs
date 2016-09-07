﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	public class VSplitter : Splitter
	{
		public VSplitter()
		{
			Tasks.Add(MainTask());
			PostPresenter = new DelegatePresenter<Widget>(RenderSeparator);
			Theme.Current.Apply(this);
			Layout = new VBoxLayout { Spacing = SeparatorWidth };
		}

		void RenderSeparator(Widget widget)
		{
			widget.PrepareRendererState();
			for (int i = 0; i < Nodes.Count - 1; i++) {
				var w = Nodes[i + 1].AsWidget;
				var y = w.Y - SeparatorWidth * 0.5f;
				Renderer.DrawLine(w.X, y, w.Width + w.X, y, SeparatorColor, thickness: SeparatorWidth);
			}
		}

		private IEnumerator<object> MainTask()
		{
			var p = new SeparatorsHitTestPresenter();
			CompoundPostPresenter.Add(p);
			while (true) {
				if (IsMouseOver() && p.SeparatorUnderMouse >= 0) {
					WidgetContext.Current.MouseCursor = MouseCursor.SizeNS;
					if (Input.WasMousePressed()) {
						yield return DragSeparatorTask(p.SeparatorUnderMouse);
					}
				}
				yield return null;
			}
		}

		private IEnumerator<object> DragSeparatorTask(int index)
		{
			RaiseDragStarted();
			var initialMousePosition = Input.MousePosition;
			var initialHeights = Nodes.Select(i => i.AsWidget.Height).ToList();
			Input.CaptureMouse();
			while (Input.IsMousePressed()) {
				WidgetContext.Current.MouseCursor = MouseCursor.SizeNS;
				var dragDelta = Input.MousePosition.Y - initialMousePosition.Y;
				AdjustStretchDelta(initialHeights[index], Nodes[index].AsWidget, ref dragDelta);
				dragDelta = -dragDelta;
				AdjustStretchDelta(initialHeights[index + 1], Nodes[index + 1].AsWidget, ref dragDelta);
				dragDelta = -dragDelta;
				for (int i = 0; i < Nodes.Count; i++) {
					var d = (i == index) ? dragDelta : ((i == index + 1) ? -dragDelta : 0);
					Nodes[i].AsWidget.LayoutCell = new LayoutCell { StretchY = initialHeights[i] + d };
				}
				Layout.InvalidateConstraintsAndArrangement(this);
				yield return null;
			}
			Input.ReleaseMouse();
			RaiseDragEnded();
		}

		private void AdjustStretchDelta(float initialHeight, Widget widget, ref float delta)
		{
			if (initialHeight + delta <= widget.MinHeight) {
				delta = widget.MinHeight - initialHeight;
			}
			if (initialHeight + delta >= widget.MaxHeight) {
				delta = widget.MaxHeight - initialHeight;
			}
		}

		class SeparatorsHitTestPresenter : CustomPresenter
		{
			public int SeparatorUnderMouse { get; private set; }

			public override bool PartialHitTest(Node node, ref HitTestArgs args)
			{
				var splitter = (Splitter)node;
				for (int i = 0; i < splitter.Nodes.Count - 1; i++) {
					var widget = splitter.Nodes[i + 1].AsWidget;
					var widgetPos = widget.GlobalPosition;
					var mousePos = Window.Current.Input.MousePosition;
					if (Mathf.Abs(mousePos.Y - (widgetPos.Y - splitter.SeparatorWidth * 0.5f)) < splitter.SeparatorActiveAreaWidth * 0.5f) {
						if (mousePos.X > widgetPos.X && mousePos.X < widgetPos.X + widget.Width) {
							SeparatorUnderMouse = i;
							args.Node = splitter;
							return true;
						}
					}
				}
				SeparatorUnderMouse = -1;
				return false;
			}
		}
	}
}