using System;
using Lime;
using Lime.Widgets.Charts;
using Lime.Profilers;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;

namespace Tangerine.UI
{
	internal class ChartsPanel : Widget
	{
		public const int GpuChartIndex = 0;
		public const int CpuChartIndex = 0;
		public const int SelectedChartIndex = 0;

		private readonly Legend areaLegend;
		public readonly AreaReplaceableCharts AreaCharts;

		private readonly Legend lineLegend;
		public readonly LineCharts LineCharts;

		private ChartsContainer.Slice areaLastSlice;
		private ChartsContainer.Slice lineLastSlice;

		/// <summary>
		/// Invoked when you click on the charts.
		/// The first parameter is a frame index.
		/// The second parameter is a update index.
		/// </summary>
		public Action<int> SliceSelected;

		public ChartsPanel()
		{
			var colors = new Color4[] {
				ColorTheme.Current.Profiler.ChartOne,
				ColorTheme.Current.Profiler.ChartTwo,
				ColorTheme.Current.Profiler.ChartThree,
				ColorTheme.Current.Profiler.ChartFour,
				ColorTheme.Current.Profiler.ChartFive,
				ColorTheme.Current.Profiler.ChartSix,
				ColorTheme.Current.Profiler.ChartSeven,
				ColorTheme.Current.Profiler.ChartEight,
				ColorTheme.Current.Profiler.ChartNine,
				Color4.Green,
				Color4.White,
				Color4.Red
			};
			// Create area charts.
			var parameters = new AreaCharts.Parameters(GpuHistory.HistoryFramesCount, colors) {
				ChartsCount = 3,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			AreaCharts = new AreaReplaceableCharts(parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			var targetWidth = (parameters.ControlPointsCount - 1) * parameters.ControlPointsSpacing;
			// Horizontal line for 15 fps
			AreaCharts.SetLinePos(0, new Vector2(0, 1000.0f / 15.0f), new Vector2(targetWidth, 1000.0f / 15.0f), 11);
			// Horizontal line for 30 fps
			AreaCharts.SetLinePos(1, new Vector2(0, 1000.0f / 30.0f), new Vector2(targetWidth, 1000.0f / 30.0f), 10);
			// Horizontal line for 60 fps
			AreaCharts.SetLinePos(2, new Vector2(0, 1000.0f / 60.0f), new Vector2(targetWidth, 1000.0f / 60.0f), 9);
			// Create legend for area charts.
			var items = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "GPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "CPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[2], Name = "Selected", Format = "{0,6:0.00}" },
			};
			areaLegend = new Legend(items, AreaCharts.SetActive) {
				MinMaxHeight = parameters.Height,
				Height = parameters.Height
			};
			// Create line chars.
			parameters = new LineCharts.Parameters(GpuHistory.HistoryFramesCount, colors) {
				IsIndependentMode = true,
				ChartsCount = 4,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			LineCharts = new LineCharts((LineCharts.Parameters)parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			LineCharts.CustomChartScales[0] = 1.0f;
			LineCharts.CustomChartScales[1] = 0.9f;
			LineCharts.CustomChartScales[2] = 0.8f;
			LineCharts.CustomChartScales[3] = 0.7f;
			// Create legend for line charts.
			items = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "Saved by batching", Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Draw Calls",        Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[2], Name = "Vertices",          Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[3], Name = "Triangles",         Format = "{0,6}" },
			};
			lineLegend = new Legend(items, LineCharts.SetActive);
			Layout = new VBoxLayout();
			AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 6 },
						Nodes = { areaLegend, lineLegend },
						Padding = new Thickness(6)
					},
					new Widget {
						Layout = new VBoxLayout { Spacing = 6 },
						Nodes = { AreaCharts, LineCharts },
						Padding = new Thickness(6)
					}
				}
			});
		}

		public void FrameCompleted(GpuHistory.Item frame, CpuHistory.Item update)
		{
			PushChartsSlice(frame, update);
			UpdateActiveSliceIndicator();
		}

		public void Reset()
		{
			AreaCharts.Reset();
			LineCharts.Reset();
		}

		public void SetAreaChartsPanelVisible(bool value)
		{
			areaLegend.Visible = value;
			AreaCharts.Visible = value;
		}

		public void SetLineChartsPanelVisible(bool value)
		{
			lineLegend.Visible = value;
			LineCharts.Visible = value;
		}

		private void PushChartsSlice(GpuHistory.Item frame, CpuHistory.Item update)
		{
			float gpuRenderTime = (float)frame.FullGpuRenderTime;
			var points = new float[] {
				gpuRenderTime,
				update.DeltaTime - gpuRenderTime,
				0f,
			};
			AreaCharts.PushSlice(points);
			areaLegend.SetValues(points);
			points = new float[] {
				frame.SceneSavedByBatching,
				frame.SceneDrawCallCount,
				frame.SceneVerticesCount,
				frame.SceneTrianglesCount
			};
			LineCharts.PushSlice(points);
			lineLegend.SetValues(points);
		}

		private void OnSliceSelected(ChartsContainer.Slice slice)
		{
			areaLastSlice = AreaCharts.GetSlice(slice.Index);
			lineLastSlice = LineCharts.GetSlice(slice.Index);
			areaLegend.SetValues(areaLastSlice.Points);
			lineLegend.SetValues(lineLastSlice.Points);
			SliceSelected?.Invoke(slice.Index);
		}

		private void UpdateActiveSliceIndicator()
		{
			if (areaLastSlice != null) {
				if (areaLastSlice.Index < 0) {
					AreaCharts.SetLinePos(3, Vector2.Zero, Vector2.Zero, colorIndex: 10);
					LineCharts.SetLinePos(3, Vector2.Zero, Vector2.Zero, colorIndex: 10);
				} else {
					float x = areaLastSlice.Index * AreaCharts.ControlPointsSpacing;
					AreaCharts.SetLinePos(
						lineIndex: 3,
						start: new Vector2(x, 0),
						end: new Vector2(x, AreaCharts.Height / AreaCharts.ScaleCoefficient),
						colorIndex: 10);
					LineCharts.SetLinePos(
						lineIndex: 0,
						start: new Vector2(x, 0),
						end: new Vector2(x, LineCharts.Height), // because IsIndependentMode
						colorIndex: 10);
					areaLastSlice.Index -= 1;
					lineLastSlice.Index -= 1;
				}
			}
		}
	}
}
