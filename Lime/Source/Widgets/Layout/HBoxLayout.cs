﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	public class HBoxLayout : CommonLayout, ILayout
	{
		public float Spacing { get; set; }
		public LayoutCell CellDefaults { get; set; }

		public HBoxLayout()
		{
			CellDefaults = new LayoutCell();
			DebugRectangles = new List<Rectangle>();
		}

		public override void ArrangeChildren(Widget widget)
		{
			ArrangementValid = true;
			var widgets = GetChildren(widget);
			if (widgets.Count == 0) {
				return;
			}
			var constraints = new LinearAllocator.Constraints[widgets.Count];
			int i = 0;
			foreach (var child in widgets) {
				constraints[i++] = new LinearAllocator.Constraints {
					MinSize = child.EffectiveMinSize.X,
					MaxSize = child.EffectiveMaxSize.X,
					Stretch = (child.LayoutCell ?? CellDefaults).StretchX
				};
			}
			var availableWidth = Math.Max(0, widget.ContentWidth - (widgets.Count - 1) * Spacing);
			var sizes = LinearAllocator.Allocate(availableWidth, constraints, roundSizes: true);
			i = 0;
			DebugRectangles.Clear();
			var position = new Vector2(widget.Padding.Left, widget.Padding.Top);
			foreach (var child in widgets) {
				var size = new Vector2(sizes[i], widget.ContentHeight);
				var align = (child.LayoutCell ?? CellDefaults).Alignment;
				LayoutWidgetWithinCell(child, position, size, align, DebugRectangles);
				position.X += size.X + Spacing;
				i++;
			}
		}

		public override void MeasureSizeConstraints(Widget widget)
		{
			ConstraintsValid = true;
			var widgets = GetChildren(widget);
			if (widgets.Count == 0) {
				widget.MeasuredMinSize = Vector2.Zero;
				widget.MeasuredMaxSize = Vector2.PositiveInfinity;
				return;
			}
			var minSize = new Vector2(
				widgets.Sum(i => i.EffectiveMinSize.X),
				widgets.Max(i => i.EffectiveMinSize.Y));
			var maxSize = new Vector2(
				widgets.Sum(i => i.EffectiveMaxSize.X),
				widgets.Max(i => i.EffectiveMaxSize.Y));
			var extraSpace = new Vector2((widgets.Count - 1) * Spacing, 0) + widget.Padding;
			widget.MeasuredMinSize = minSize + extraSpace;
			widget.MeasuredMaxSize = maxSize + extraSpace;
		}
	}
}