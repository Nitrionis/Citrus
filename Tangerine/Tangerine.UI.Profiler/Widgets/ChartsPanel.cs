using System;
using Lime;
using Lime.Widgets.Charts;
using Lime.Profilers;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;

namespace Tangerine.UI
{
	internal class ChartsPanel : Widget
	{
		private readonly Legend cpuLegend;
		public readonly AreaReplaceableCharts CpuCharts;

		private readonly Legend gpuLegend;
		public readonly AreaReplaceableCharts GpuCharts;

		private readonly Legend lineLegend;
		public readonly LineCharts LineCharts;

		private ChartsContainer.Slice cpuLastSlice;
		private ChartsContainer.Slice gpuLastSlice;
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
			// Create CPU charts.
			var parameters = new AreaCharts.Parameters(GpuHistory.HistoryFramesCount, colors) {
				Height = 64,
				ChartsCount = 1,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			CpuCharts = new AreaReplaceableCharts(parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			var targetWidth = (parameters.ControlPointsCount - 1) * parameters.ControlPointsSpacing;
			// Horizontal line for 15 fps
			CpuCharts.SetLinePos(0, new Vector2(0, 1000.0f / 15.0f), new Vector2(targetWidth, 1000.0f / 15.0f), 11);
			// Horizontal line for 30 fps
			CpuCharts.SetLinePos(1, new Vector2(0, 1000.0f / 30.0f), new Vector2(targetWidth, 1000.0f / 30.0f), 10);
			// Horizontal line for 60 fps
			CpuCharts.SetLinePos(2, new Vector2(0, 1000.0f / 60.0f), new Vector2(targetWidth, 1000.0f / 60.0f), 9);
			// Create legend for CPU charts.
			var cpuLegendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[1], Name = "CPU", Format = "{0,6:0.00}" },
			};
			cpuLegend = new Legend(cpuLegendItems, CpuCharts.SetActive) {
				MinMaxHeight = parameters.Height,
				Height = parameters.Height
			};
			// Create GPU charts.
			parameters = new AreaCharts.Parameters(GpuHistory.HistoryFramesCount, colors) {
				Height = 64,
				ChartsCount = 2,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			GpuCharts = new AreaReplaceableCharts(parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			// Horizontal line for 15 fps
			GpuCharts.SetLinePos(0, new Vector2(0, 1000.0f / 15.0f), new Vector2(targetWidth, 1000.0f / 15.0f), 11);
			// Horizontal line for 30 fps
			GpuCharts.SetLinePos(1, new Vector2(0, 1000.0f / 30.0f), new Vector2(targetWidth, 1000.0f / 30.0f), 10);
			// Horizontal line for 60 fps
			GpuCharts.SetLinePos(2, new Vector2(0, 1000.0f / 60.0f), new Vector2(targetWidth, 1000.0f / 60.0f), 9);
			// Create legend for GPU charts.
			var gpuLegendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "GPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Selected", Format = "{0,6:0.00}" },
			};
			gpuLegend = new Legend(gpuLegendItems, GpuCharts.SetActive) {
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
			var legendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "Saved by batching", Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Draw Calls",        Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[2], Name = "Vertices",          Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[3], Name = "Triangles",         Format = "{0,6}" },
			};
			lineLegend = new Legend(legendItems, LineCharts.SetActive);
			Layout = new VBoxLayout();
			AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 6 },
						Nodes = { cpuLegend, gpuLegend, lineLegend },
						Padding = new Thickness(6)
					},
					new Widget {
						Layout = new VBoxLayout { Spacing = 6 },
						Nodes = { CpuCharts, GpuCharts, LineCharts },
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
			CpuCharts.Reset();
			GpuCharts.Reset();
			LineCharts.Reset();
		}

		public void SetCpuChartsPanelVisible(bool value)
		{
			cpuLegend.Visible = value;
			CpuCharts.Visible = value;
		}

		public void SetGpuChartsPanelVisible(bool value)
		{
			gpuLegend.Visible = value;
			GpuCharts.Visible = value;
		}

		public void SetLineChartsPanelVisible(bool value)
		{
			lineLegend.Visible = value;
			LineCharts.Visible = value;
		}

		private void PushChartsSlice(GpuHistory.Item frame, CpuHistory.Item update)
		{
			var points = new float[] {
				update.DeltaTime,
			};
			CpuCharts.PushSlice(points);
			cpuLegend.SetValues(points);
			points = new float[] {
				(float)frame.FullGpuRenderTime,
				0f,
			};
			GpuCharts.PushSlice(points);
			gpuLegend.SetValues(points);
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
			cpuLastSlice = CpuCharts.GetSlice(slice.Index);
			gpuLastSlice = GpuCharts.GetSlice(slice.Index);
			lineLastSlice = LineCharts.GetSlice(slice.Index);
			cpuLegend.SetValues(cpuLastSlice.Points);
			gpuLastSlice.Points[0] += gpuLastSlice.Points[1];
			gpuLegend.SetValues(gpuLastSlice.Points);
			lineLegend.SetValues(lineLastSlice.Points);
			SetSlicePosition();
			SliceSelected?.Invoke(slice.Index);
		}

		private void SetSlicePosition()
		{
			float x = cpuLastSlice.Index * CpuCharts.ControlPointsSpacing;
			CpuCharts.SetLinePos(
				lineIndex: 3,
				start: new Vector2(x, 0),
				end: new Vector2(x, CpuCharts.Height * CpuCharts.ScaleCoefficient),
				colorIndex: 10);
			GpuCharts.SetLinePos(
				lineIndex: 3,
				start: new Vector2(x, 0),
				end: new Vector2(x, GpuCharts.Height * GpuCharts.ScaleCoefficient),
				colorIndex: 10);
			LineCharts.SetLinePos(
				lineIndex: 0,
				start: new Vector2(x, 0),
				end: new Vector2(x, LineCharts.Height), // because IsIndependentMode
				colorIndex: 10);
		}

		private void UpdateActiveSliceIndicator()
		{
			if (cpuLastSlice != null) {
				if (cpuLastSlice.Index < 0) {
					CpuCharts.SetLinePos(3, Vector2.Zero, Vector2.Zero, colorIndex: 10);
					GpuCharts.SetLinePos(3, Vector2.Zero, Vector2.Zero, colorIndex: 10);
					LineCharts.SetLinePos(0, Vector2.Zero, Vector2.Zero, colorIndex: 10);
				} else {
					SetSlicePosition();
					cpuLastSlice.Index -= 1;
					gpuLastSlice.Index -= 1;
					lineLastSlice.Index -= 1;
				}
			}
		}
	}
}
