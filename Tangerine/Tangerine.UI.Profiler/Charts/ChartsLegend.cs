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

		public ChartsLegend(IChartsGroup[] groups, IEnumerable<ItemDescription> items)
		{
			var itemsDescriptions = items.ToArray();
			this.items = new Item[itemsDescriptions.Length];
			void DecorateContainer(Widget container) {
				container.Layout = new VBoxLayout { Spacing = 2 };
				foreach (var node in container.Nodes) {
					var w = node.AsWidget;
					w.Height = w.MinMaxHeight = Theme.Metrics.TextHeight;
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
			DecorateContainer(checkboxesContainer);
			DecorateContainer(labelsContainer);
			DecorateContainer(valuesContainer);
			AddNode(checkboxesContainer);
			AddNode(labelsContainer);
			AddNode(valuesContainer);
		}

		public void SetValues(float[] values)
		{
			if (values.Length != items.Length) {
				throw new InvalidOperationException("ChartsLegend: wrong number of parameters!");
			}
			for (int i = 0; i < items.Length; i++) {
				items[i].ValueWidget.Text = string.Format(items[i].ValueFormat, values[i]);
			}
		}

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