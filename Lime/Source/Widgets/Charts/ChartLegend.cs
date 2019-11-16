namespace Lime
{
	public class ChartLegend : Widget
	{
		public delegate void SetActiveChart(int index, bool value);

		public struct Item
		{
			public string Name;
			public string Format;
		}

		private SimpleText[] simpleTexts;
		private Item[] items;

		public ChartLegend(SetActiveChart toggle, Vector4[] colors, Item[] items)
		{
			this.items = items;
			Layout = new HBoxLayout();
			// Create first segment with checkboxes and charts names
			{
				var segmentWidget = CreateSegmentWidget;
				for (int i = 0; i < items.Length; i++) {
					var widget = new Widget {
						Layout = new HBoxLayout { Spacing = 6 }
					};
					var color = colors[i + 1];
					var checkBox = new ColorCheckBox(Color4.FromFloats(color.X, color.Y, color.Z));
					int iterationIndex = i;
					checkBox.Changed += args => {
						toggle(iterationIndex, args.Value);
					};
					widget.AddNode(checkBox);
					widget.AddNode(
						new SimpleText {
							Text = items[i].Name,
							Color = Color4.White
						}
					);
					segmentWidget.AddNode(widget);
				}
				AddNode(segmentWidget);
			}
			// Create second segment with frame data
			{
				simpleTexts = new SimpleText[items.Length];
				var segmentWidget = CreateSegmentWidget;
				for (int i = 0; i < items.Length; i++) {
					simpleTexts[i] = new SimpleText {
						Color = Color4.White,
						MinHeight = 16
					};
					segmentWidget.AddNode(simpleTexts[i]);
				}
				AddNode(segmentWidget);
			}
		}

		public void SliceSelected(ChartsGroup.Slice slice)
		{
			for (int i = 0; i < simpleTexts.Length; i++) {
				simpleTexts[i].Text = string.Format(items[i].Format, slice.Values[i]);
			}
		}

		private Widget CreateSegmentWidget => new Widget {
			Layout = new VBoxLayout { Spacing = 6 },
			Padding = new Thickness(4, 8)
		};
	}
}
