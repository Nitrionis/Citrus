using System;
using Lime;
using Lime.Widgets.Charts;
using Lime.Profilers;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;

namespace Tangerine.UI
{
	internal class ChartsPanel : Widget
	{
		private readonly Legend areaLegend;
		private readonly AreaCharts areaCharts;
		private readonly float[] originalCpuPoints;

		private readonly Legend lineLegend;
		private readonly LineCharts lineCharts;

		private readonly Widget areaChartsPanel;
		private readonly Widget lineChartsPanel;

		public struct Indices
		{
			public long Frame;
			public long Update;
		}

		public Indices[] FrameUpdateIndices;

		private ChartsContainer.Slice areaLastSlice;
		private ChartsContainer.Slice lineLastSlice;
		private int previousSliceIndex;

		/// <summary>
		/// Invoked when you click on the charts.
		/// The first parameter is a frame index.
		/// The second parameter is a update index.
		/// </summary>
		public Action<long, long> OnFrameSelected;

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
			FrameUpdateIndices = new Indices[GpuHistory.HistoryFramesCount];
			// Create area charts.
			var parameters = new AreaCharts.Parameters(GpuHistory.HistoryFramesCount, colors) {
				ChartsCount = 3,
				UserLinesCount = 4,
				OnSliceSelected = SliceSelected
			};
			areaCharts = new AreaCharts(parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			var targetWidth = (parameters.ControlPointsCount - 1) * parameters.ControlPointsSpacing;
			// Horizontal line for 15 fps
			areaCharts.SetLinePos(0, new Vector2(0, 1000.0f / 15.0f), new Vector2(targetWidth, 1000.0f / 15.0f), 11);
			// Horizontal line for 30 fps
			areaCharts.SetLinePos(1, new Vector2(0, 1000.0f / 30.0f), new Vector2(targetWidth, 1000.0f / 30.0f), 10);
			// Horizontal line for 60 fps
			areaCharts.SetLinePos(2, new Vector2(0, 1000.0f / 60.0f), new Vector2(targetWidth, 1000.0f / 60.0f), 9);
			// Create legend for area charts.
			var items = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "GPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "CPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[2], Name = "Selected", Format = "{0,6:0.00}" },
			};
			areaLegend = new Legend(items, areaCharts.SetActive) {
				MinMaxHeight = parameters.Height,
				Height = parameters.Height
			};
			originalCpuPoints = new float[GpuHistory.HistoryFramesCount];
			// Create line chars.
			parameters = new LineCharts.Parameters(GpuHistory.HistoryFramesCount, colors) {
				IsIndependentMode = true,
				ChartsCount = 4,
				UserLinesCount = 4,
				OnSliceSelected = SliceSelected
			};
			lineCharts = new LineCharts((LineCharts.Parameters)parameters) {
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground
			};
			lineCharts.CustomChartScales[0] = 1.0f;
			lineCharts.CustomChartScales[1] = 0.9f;
			lineCharts.CustomChartScales[2] = 0.8f;
			lineCharts.CustomChartScales[3] = 0.7f;
			// Create legend for line charts.
			items = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "Saved by batching", Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Draw Calls",        Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[2], Name = "Vertices",          Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[3], Name = "Triangles",         Format = "{0,6}" },
			};
			lineLegend = new Legend(items, lineCharts.SetActive);
			areaChartsPanel = new Widget {
				Layout = new VBoxLayout { Spacing = 6 },
				Nodes = { areaLegend, lineLegend },
				Padding = new Thickness(6)
			};
			lineChartsPanel = new Widget {
				Layout = new VBoxLayout { Spacing = 6 },
				Nodes = { areaCharts, lineCharts },
				Padding = new Thickness(6)
			};
			AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					areaChartsPanel,
					lineChartsPanel
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
			for (int i = 0; i < FrameUpdateIndices.Length; i++) {
				FrameUpdateIndices[i] = new Indices { Frame = -1, Update = -1 };
			}
			areaCharts.Reset();
			lineCharts.Reset();
		}

		public void SetAreaChartsPanelVisible(bool value) => areaChartsPanel.Visible = value;
		public void SetLineChartsPanelVisible(bool value) => lineChartsPanel.Visible = value;

		private void PushChartsSlice(GpuHistory.Item frame, CpuHistory.Item update)
		{
			Array.Copy(originalCpuPoints, 1, originalCpuPoints, 0, originalCpuPoints.Length - 1);
			originalCpuPoints[originalCpuPoints.Length - 1] = update.DeltaTime;
			var points = new float[] {
				(float)frame.FullGpuRenderTime,
				update.DeltaTime,
				0f,
			};
			areaCharts.PushSlice(points);
			areaLegend.SetValues(points);
			points = new float[] {
				frame.SceneSavedByBatching,
				frame.SceneDrawCallCount,
				frame.SceneVerticesCount,
				frame.SceneTrianglesCount
			};
			lineCharts.PushSlice(points);
			lineLegend.SetValues(points);
			UpdateActiveSliceIndicator();
		}

		private void SliceSelected(ChartsContainer.Slice slice)
		{
			areaLastSlice = areaCharts.GetSlice(slice.Index);
			lineLastSlice = lineCharts.GetSlice(slice.Index);
			areaLegend.SetValues(areaLastSlice.Points);
			lineLegend.SetValues(lineLastSlice.Points);

			long frameIndex = FrameUpdateIndices[slice.Index].Frame;
			long updateIndex = FrameUpdateIndices[slice.Index].Update;
			if (
				LimeProfiler.GpuHistory.IsFrameIndexValid(frameIndex) &&
				LimeProfiler.CpuHistory.IsUpdateIndexValid(updateIndex)
				)
			{
				OnFrameSelected?.Invoke(frameIndex, updateIndex);
			}
		}

		private void UpdateActiveSliceIndicator()
		{
			if (areaLastSlice != null) {
				if (areaLastSlice.Index == 0 && previousSliceIndex != 0) {
					areaCharts.SetLinePos(3, Vector2.Zero, Vector2.Zero, colorIndex: 10);
					lineCharts.SetLinePos(3, Vector2.Zero, Vector2.Zero, colorIndex: 10);
					previousSliceIndex = areaLastSlice.Index;
				}
				else {
					float x = areaLastSlice.Index * areaCharts.ControlPointsSpacing;
					areaCharts.SetLinePos(
						lineIndex: 3,
						start: new Vector2(x, 0),
						end: new Vector2(x, areaCharts.Height / areaCharts.ScaleCoefficient),
						colorIndex: 10);
					lineCharts.SetLinePos(
						lineIndex: 0,
						start: new Vector2(x, 0),
						end: new Vector2(x, lineCharts.Height), // because IsIndependentMode
						colorIndex: 10);
					previousSliceIndex = areaLastSlice.Index;
					areaLastSlice.Index = areaLastSlice.Index - 1;
					lineLastSlice.Index = lineLastSlice.Index - 1;
				}
			}
		}

		/// <summary>
		/// todo
		/// </summary>
		public void UpdateSelectedObjectsRenderTime(float[] selectedPoints)
		{
			var cpuTimeChart = areaCharts.Charts[1];
			for (int i = 0; i < cpuTimeChart.Points.Length; i++) {
				cpuTimeChart.Points[i] = originalCpuPoints[i] - selectedPoints[i];
			}
			var selectedTimeChart = areaCharts.Charts[2];
			for (int i = 0; i < cpuTimeChart.Points.Length; i++) {
				selectedTimeChart.Points[i] = selectedPoints[i];
			}
		}
	}
}
