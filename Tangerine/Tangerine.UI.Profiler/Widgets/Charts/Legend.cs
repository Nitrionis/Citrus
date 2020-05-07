using System;
using Lime;

namespace Tangerine.UI.Charts
{
	internal class Legend : Widget
	{
		public struct ItemDescription
		{
			public Color4 Color;
			public string Name;
			public string Format;
		}

		protected class Item
		{
			public struct LabelInfo
			{
				public ColorCheckBox CheckBox;
				public ThemedSimpleText TextWidget;
			}

			public struct ValueInfo
			{
				public string Format;
				public ThemedSimpleText TextWidget;
			}

			public LabelInfo Label;
			public ValueInfo Value;
		}

		protected readonly Item[] items;
		protected readonly Widget labels;
		protected readonly Widget values;

		public Color4 TextColor
		{
			set {
				foreach (var item in items) {
					item.Label.TextWidget.Color = value;
					item.Value.TextWidget.Color = value;
				}
			}
		}

		public Legend(ItemDescription[] items, Action<int, bool> OnChanged)
		{
			this.items = new Item[items.Length];
			Layout = new HBoxLayout();
			Widget CreateSegmentWidget() => new Widget {
				Layout = new VBoxLayout { Spacing = 6 },
				Padding = new Thickness(4, 8)
			};
			// Create first segment with checkboxes and charts names
			ColorCheckBox InitializeCheckBox(int itemIndex) {
				var checkBox = new ColorCheckBox(items[itemIndex].Color);
				checkBox.Changed += args => OnChanged(itemIndex, args.Value);
				return checkBox;
			};
			labels = CreateSegmentWidget();
			for (int i = 0; i < items.Length; i++) {
				this.items[i] = new Item {
					Label = new Item.LabelInfo {
						CheckBox = InitializeCheckBox(i),
						TextWidget = new ThemedSimpleText { Text = items[i].Name }
					}
				};
				var widget = new Widget {
					Layout = new HBoxLayout { Spacing = 6 }
				};
				widget.AddNode(this.items[i].Label.CheckBox);
				widget.AddNode(this.items[i].Label.TextWidget);
				labels.AddNode(widget);
				
			}
			AddNode(labels);
			// Create second segment with slice data
			values = CreateSegmentWidget();
			for (int i = 0; i < items.Length; i++) {
				this.items[i].Value = new Item.ValueInfo {
					Format = items[i].Format,
					TextWidget = new ThemedSimpleText { MinHeight = 16 }
				};
				values.AddNode(this.items[i].Value.TextWidget);
			}
			AddNode(values);
		}

		public void SetValues(float[] values)
		{
			for (int i = 0; i < values.Length; i++) {
				items[i].Value.TextWidget.Text = string.Format(items[i].Value.Format, values[i]);
			}
		}
	}
}
