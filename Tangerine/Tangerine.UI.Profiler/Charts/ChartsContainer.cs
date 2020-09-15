using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lime;

namespace Tangerine.UI.Charts
{
	/// <summary>
	/// Common interface for all charts.
	/// </summary>
	internal interface IChart
	{
		/// <summary>
		/// Slot index in a charts group.
		/// </summary>
		int SlotIndex { get; }

		/// <summary>
		/// Indicates the visibility of the chart.
		/// </summary>
		bool Visible { get; }

		/// <summary>
		/// Each charts group supports up to 16 colors.
		/// </summary>
		int ColorIndex { get; }
	}

	/// <summary>
	/// Common code for some charts.
	/// </summary>
	internal class ChartBase : IChart
	{
		private int slotIndex = -1;

		public int SlotIndex
		{
			get { return slotIndex; }
			set {
				if (slotIndex != -1) {
					throw new NotImplementedException();
				}
				slotIndex = value;
			}
		}

		public bool Visible { get; set; }

		public int ColorIndex { get; set; }
	}

	/// <summary>
	/// Contains several charts.
	/// </summary>
	internal interface IChartsGroup
	{
		/// <summary>
		/// The widget that is responsible for drawing this group of charts.
		/// </summary>
		Widget Container { get; }

		/// <summary>
		/// Provides access to the charts.
		/// </summary>
		ReadOnlyCollection<IChart> Charts { get; }

		/// <summary>
		/// Invoked when chart visibility changed.
		/// </summary>
		event Action<IChart> ChartVisibleChanged;

		/// <summary>
		/// Available colors for charts.
		/// </summary>
		ReadOnlyCollection<Color4> Colors { get; }

		/// <summary>
		/// Allows you to turn the display of a chart on and off.
		/// </summary>
		void SetVisibleFor(IChart chart, bool visible);

		/// <summary>
		/// Allows you to change color of a chart.
		/// </summary>
		/// <param name="colorIndex">Index of an item at <see cref="Colors"/>.</param>
		void SetColorFor(IChart chart, int colorIndex);
	}

	/// <summary>
	/// Stores several groups of сharts.
	/// </summary>
	internal class ChartsContainer : Widget
	{
		private readonly WidgetFlatFillPresenter presenter;

		/// <summary>
		/// Background color for charts.
		/// </summary>
		public Color4 BackgroundColor
		{
			get => presenter.Color;
			set => presenter.Color = value;
		}

		public ChartsContainer(IEnumerable<IChartsGroup> groups)
		{
			presenter = new WidgetFlatFillPresenter(Color4.Black);
			CompoundPresenter.Add(presenter);
			Layout = new StackLayout();
			foreach (var group in groups) {
				AddNode(group.Container);
			}
		}
	}
}
