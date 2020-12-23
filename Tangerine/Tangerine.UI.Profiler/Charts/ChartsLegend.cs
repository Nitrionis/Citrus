#if PROFILER

using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.UI.Charts
{
	internal class ChartsLegend : Widget
	{
		private readonly Item[] items;

		public readonly Widget ValuesContainer;
		
		public ChartsLegend(IChartsGroup[] groups, IEnumerable<ItemDescription> items)
		{
			var itemsDescriptions = items.ToArray();
			this.items = new Item[itemsDescriptions.Length];
			void DecorateContainer(Widget container, Thickness padding, float spacing) {
				container.Layout = new VBoxLayout { Spacing = spacing };
				foreach (var node in container.Nodes) {
					var w = node.AsWidget;
					w.Height = w.MinMaxHeight = 12;
					w.Padding = padding;
				}
			}
			var checkboxesContainer = new ChartsVisibilityController(groups);
			if (checkboxesContainer.Checkboxes.Count != itemsDescriptions.Length) {
				throw new InvalidOperationException("ChartsLegend: wrong number of items!");
			}
			var labelsContainer = new Widget();
			var valuesContainer = new Widget();
			for (int i = 0; i < this.items.Length; i++) {
				var description = itemsDescriptions[i];
				var labelWidget = new ThemedSimpleText(description.Label);
				var valueWidget = new ThemedSimpleText();
				this.items[i] = new Item {
					Checkbox = checkboxesContainer.Checkboxes[i],
					LabelWidget = labelWidget,
					ValueWidget = valueWidget,
					ValueFormat = description.ValueFormat
				};
				labelsContainer.AddNode(labelWidget);
				valuesContainer.AddNode(valueWidget);
			}
			Layout = new HBoxLayout { Spacing = 4 };
			DecorateContainer(checkboxesContainer, new Thickness(0), 2);
			DecorateContainer(labelsContainer, new Thickness(0, -2), 2);
			DecorateContainer(valuesContainer, new Thickness(0, -2), 2);
			AddNode(checkboxesContainer);
			AddNode(labelsContainer);
			ValuesContainer = valuesContainer;
		}

		public void SetValue(float value, int chartIndex) => 
			items[chartIndex].ValueWidget.Text = string.Format(items[chartIndex].ValueFormat, value);

		public struct ItemDescription
		{
			public string Label;
			public string ValueFormat;
		}

		private struct Item
		{
			public ThemedColorCheckBox Checkbox;
			public ThemedSimpleText LabelWidget;
			public ThemedSimpleText ValueWidget;
			public string ValueFormat;
		}
	}
}

#endif // PROFILER