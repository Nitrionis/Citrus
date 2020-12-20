#if PROFILER

using System;
using System.Collections.ObjectModel;
using Lime;

namespace Tangerine.UI.Charts
{
	/// <summary>
	/// Base class for charts with a fixed horizontal interval.
	/// </summary>
	internal abstract class FixedHorizontalSpacingCharts : Widget, IChartsGroup
	{
		private readonly ReadOnlyCollection<IChart> baseInterfaceCharts;

		protected readonly FixedSpacingChart[] charts;
		protected readonly Color4[] colors;

		/// <summary>
		/// Number of control points horizontally.
		/// </summary>
		public readonly int ControlPointsCount;

		/// <summary>
		/// The horizontal distance between the control points in pixels.
		/// </summary>
		public readonly int ControlPointsSpacing;

		/// <inheritdoc/>
		public Widget Container => this;

		/// <summary>
		/// Provides extended access to charts.
		/// </summary>
		public ReadOnlyCollection<IFixedSpacingChart> Charts { get; }

		/// <inheritdoc/>
		ReadOnlyCollection<IChart> IChartsGroup.Charts => baseInterfaceCharts;

		/// <inheritdoc/>
		public ReadOnlyCollection<Color4> Colors { get; }

		/// <inheritdoc/>
		public event Action<IChart> ChartVisibleChanged;

		/// <summary>
		/// Called when you click on the charts.
		/// </summary>
		public event Action<VerticalSlice> SliceSelected;

		protected FixedHorizontalSpacingCharts(Parameters parameters)
		{
			ControlPointsCount = parameters.ControlPointsCount;
			ControlPointsSpacing = parameters.ControlPointsSpacing;
			charts = new FixedSpacingChart[parameters.ChartsCount];
			Charts = new ReadOnlyCollection<IFixedSpacingChart>(charts);
			baseInterfaceCharts = new ReadOnlyCollection<IChart>(charts);
			for (int i = 0; i < charts.Length; i++) {
				charts[i] = new FixedSpacingChart(ControlPointsCount) {
					ColorIndex = i,
					SlotIndex = i
				};
			}
			if (parameters.Colors.Length != ChartsMaterial.ColorsCount) {
				throw new InvalidOperationException("Charts: wrong colors count!");
			}
			this.colors = (Color4[])parameters.Colors.Clone();
			Colors = new ReadOnlyCollection<Color4>(this.colors);
			AddNode(new Widget {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect,
				Clicked = SelectSliceUnderCursor
			});
		}

		/// <summary>
		/// Invoke <see cref="SliceSelected"/> for slice under cursor.
		/// </summary>
		public void SelectSliceUnderCursor()
		{
			int index = ((int)LocalMousePosition().X + ControlPointsSpacing / 2) / ControlPointsSpacing;
			SliceSelected?.Invoke(GetSlice(Math.Min(ControlPointsCount - 1, index)));
		}
		
		/// <inheritdoc/>
		public abstract void Invalidate();

		/// <inheritdoc/>
		public void SetVisibleFor(IChart chart, bool visible)
		{
			var typedChart = (ChartBase)chart;
			if (typedChart.Visible != visible) {
				typedChart.Visible = visible;
				Invalidate();
				ChartVisibleChanged?.Invoke(chart);
			}
		}

		/// <inheritdoc/>
		public void SetColorFor(IChart chart, int colorIndex)
		{
			var typedChart = (ChartBase)chart;
			if (typedChart.ColorIndex != colorIndex) {
				typedChart.ColorIndex = colorIndex;
				Invalidate();
			}
		}

		/// <param name="index">See <see cref="VerticalSlice.Index"/>.</param>
		public VerticalSlice GetSlice(int index)
		{
			var values = new float[Charts.Count];
			for (int i = 0; i < Charts.Count; i++) {
				values[i] = ((FixedSpacingChart)Charts[i]).Heights[index];
			}
			return new VerticalSlice { Heights = values, Index = index };
		}

		public interface IFixedSpacingChart : IChart
		{
			float[] Heights { get; }
		}

		protected class FixedSpacingChart : ChartBase, IFixedSpacingChart
		{
			public float[] Heights { get; }

			public FixedSpacingChart(int capacity) => Heights = new float[capacity];
		}

		/// <summary>
		/// Charts constructor parameters.
		/// </summary>
		public class Parameters
		{
			/// <summary>
			/// See <see cref="IChartsGroup.Colors"/>.
			/// </summary>
			public Color4[] Colors;

			/// <summary>
			/// See <see cref="FixedHorizontalSpacingCharts.ControlPointsCount"/>.
			/// </summary>
			public int ControlPointsCount = 100;

			/// <summary>
			/// See <see cref="FixedHorizontalSpacingCharts.ControlPointsSpacing"/>.
			/// </summary>
			public int ControlPointsSpacing = 5;

			/// <summary>
			/// It is count of <see cref="IChartsGroup.Charts"/>.
			/// </summary>
			public int ChartsCount = 1;

			public Parameters()
			{
				Colors = new Color4[ChartsMaterial.ColorsCount] {
					ColorTheme.Current.Profiler.ChartOne,
					ColorTheme.Current.Profiler.ChartTwo,
					ColorTheme.Current.Profiler.ChartThree,
					ColorTheme.Current.Profiler.ChartFour,
					ColorTheme.Current.Profiler.ChartFive,
					ColorTheme.Current.Profiler.ChartSix,
					ColorTheme.Current.Profiler.ChartSeven,
					ColorTheme.Current.Profiler.ChartEight,
					ColorTheme.Current.Profiler.ChartNine,
					Color4.Black,
					Color4.Black,
					Color4.White,
					Color4.Green,
					Color4.Yellow,
					Color4.Red,
					Color4.DarkGray
				};
			}
		}

		/// <summary>
		/// Represents vertical slice of charts.
		/// </summary>
		public struct VerticalSlice
		{
			/// <summary>
			/// Index in <see cref="FixedSpacingChart.Heights"/>.
			/// </summary>
			public int Index;

			/// <summary>
			/// Value by Index for each chart.
			/// </summary>
			public float[] Heights;
		}
	}
}

#endif // PROFILER