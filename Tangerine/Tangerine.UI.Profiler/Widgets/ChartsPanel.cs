using System;
using Lime;
using Lime.Profilers;
using GpuHistory = Lime.Graphics.Platform.Profiling.GpuHistory;
using Tangerine.UI.Charts;

namespace Tangerine.UI
{
	internal class ChartsPanel : Widget
	{
		public const int HistorySize = GpuHistory.HistoryFramesCount - 1;
		private const float ChartsHeight = 64;

		private readonly Legend cpuLegend;
		private readonly AreaReplaceableCharts cpuCharts;

		private readonly Legend gpuLegend;
		private readonly AreaReplaceableCharts gpuCharts;

		private readonly Legend lineLegend;
		private readonly LineCharts lineCharts;

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
			Id = "ChartsPanel";
			
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
			var parameters = new AreaCharts.Parameters(HistorySize, colors) {
				ChartsCount = 2,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			cpuCharts = new AreaReplaceableCharts(parameters) {
				Id = "CPU Charts",
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground,
				Height = ChartsHeight,
				MinMaxHeight = ChartsHeight
			};
			var targetWidth = (parameters.ControlPointsCount - 1) * parameters.ControlPointsSpacing;
			// Horizontal line for lowest fps
			float fpsHeight = Logarithm(1000.0f / 1.0f);
			cpuCharts.SetLine(0, new ChartsContainer.Line(
				new Vector2(0, fpsHeight), new Vector2(targetWidth, fpsHeight), 10, null));
			// Horizontal line for 30 fps
			fpsHeight = Logarithm(1000.0f / 30.0f);
			cpuCharts.SetLine(1, new ChartsContainer.Line(
				new Vector2(0, fpsHeight), new Vector2(targetWidth, fpsHeight), 3, null));
			// Horizontal line for 60 fps
			fpsHeight = Logarithm(1000.0f / 60.0f);
			cpuCharts.SetLine(2, new ChartsContainer.Line(
				new Vector2(0, fpsHeight), new Vector2(targetWidth, fpsHeight), 9, null));
			// Create legend for CPU charts.
			var cpuLegendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "CPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Selected", Format = "{0,6:0.00}" },
			};
			cpuLegend = new Legend(cpuLegendItems, cpuCharts.SetActive) {
				MinMaxHeight = ChartsHeight,
				Height = ChartsHeight,
				TextColor = ColorTheme.Current.Profiler.LegendText
			};
			// Create GPU charts.
			parameters = new AreaCharts.Parameters(HistorySize, colors) {
				ChartsCount = 2,
				UserLinesCount = 3,
				SliceSelected = OnSliceSelected
			};
			gpuCharts = new AreaReplaceableCharts(parameters) {
				Id = "GPU Charts",
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground,
				Height = ChartsHeight,
				MinMaxHeight = ChartsHeight
			};
			gpuCharts.SetLine(0, new ChartsContainer.Line(
				new Vector2(0, 1), new Vector2(targetWidth, 1), 10, "10 ms"));
			// Create legend for GPU charts.
			var gpuLegendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "GPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Selected", Format = "{0,6:0.00}" },
			};
			gpuLegend = new Legend(gpuLegendItems, gpuCharts.SetActive) {
				MinMaxHeight = ChartsHeight,
				Height = ChartsHeight,
				TextColor = ColorTheme.Current.Profiler.LegendText
			};
			// Create line chars.
			parameters = new LineCharts.Parameters(HistorySize, colors) {
				IsIndependentMode = true,
				ChartsCount = 4,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			lineCharts = new LineCharts((LineCharts.Parameters)parameters) {
				Id = "GPU LineCharts",
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground,
				MinMaxHeight = 100,
				Height = 100,
			};
			lineCharts.CustomChartScales[0] = 1.0f;
			lineCharts.CustomChartScales[1] = 0.9f;
			lineCharts.CustomChartScales[2] = 0.8f;
			lineCharts.CustomChartScales[3] = 0.7f;
			// Create legend for line charts.
			var legendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "Saved by batching", Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Draw Calls",        Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[2], Name = "Vertices",          Format = "{0,6}" },
				new Legend.ItemDescription { Color = colors[3], Name = "Triangles",         Format = "{0,6}" },
			};
			lineLegend = new Legend(legendItems, lineCharts.SetActive) {
				TextColor = ColorTheme.Current.Profiler.LegendText,
				MinMaxHeight = 100,
				Height = 100,
			};
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
						Nodes = { cpuCharts, gpuCharts, lineCharts },
						Padding = new Thickness(6)
					}
				}
			});
		}

		public void Enqueue(GpuHistory.Item frame, CpuHistory.Item update)
		{
			PushChartsSlice(frame, update);
			UpdateActiveSliceIndicator();
		}

		public void Reset()
		{
			cpuCharts.Reset();
			gpuCharts.Reset();
			lineCharts.Reset();
		}

		public void SetCpuChartsPanelVisible(bool value)
		{
			cpuLegend.Visible = value;
			cpuCharts.Visible = value;
		}

		public void SetGpuChartsPanelVisible(bool value)
		{
			gpuLegend.Visible = value;
			gpuCharts.Visible = value;
		}

		public void SetLineChartsPanelVisible(bool value)
		{
			lineLegend.Visible = value;
			lineCharts.Visible = value;
		}

		private void PushChartsSlice(GpuHistory.Item frame, CpuHistory.Item update)
		{
			// Push CPU charts data
			cpuCharts.EnqueueSlice(new float[] {
				Logarithm(update.DeltaTime), 0f
			});
			// Update CPU charts max value line
			float cpuMaxValue = cpuCharts.ChartsMaxValue;
			float cpuUnscaledValue = AntiLogarithm(cpuMaxValue);
			cpuCharts.SetLine(lineIndex: 0, new ChartsContainer.Line(
				start: new Vector2(0, cpuMaxValue),
				end: new Vector2(cpuCharts.Width, cpuMaxValue),
				colorIndex: 10,
				caption: string.Format("{0:0.00} fps {1:0.00} ms", 1000f / cpuUnscaledValue, cpuUnscaledValue)));
			// Push GPU charts data
			gpuCharts.EnqueueSlice(frame == null ? new float[2] : new float[] {
				Logarithm((float)frame.FullGpuRenderTime), 0f
			});
			// Update GPU charts max value line
			float gpuMaxValue = gpuCharts.ChartsMaxValue;
			float gpuUnscaledValue = AntiLogarithm(gpuMaxValue);
			gpuCharts.SetLine(lineIndex: 0, new ChartsContainer.Line(
				start: new Vector2(0, gpuMaxValue),
				end: new Vector2(gpuCharts.Width, gpuMaxValue),
				colorIndex: 10,
				caption: string.Format("{0:0.##} ms", gpuUnscaledValue)));
			// Push LineCharts data
			var points = frame == null ?
				new float[4] :
				new float[] {
					frame.SceneSavedByBatching,
					frame.SceneDrawCallCount,
					frame.SceneVerticesCount,
					frame.SceneTrianglesCount
				};
			lineCharts.EnqueueSlice(points);
			UpdateChartsLegends(frame, update, selectedRenderTime: 0, selectedUpdateTime: 0);
		}

		public void UpdateChartsLegends(GpuHistory.Item frame, CpuHistory.Item update, float selectedRenderTime, float selectedUpdateTime)
		{
			cpuLegend.SetValues(new float[] {
				update.DeltaTime, update.DeltaTime * selectedUpdateTime
			});
			gpuLegend.SetValues(frame == null ? new float[2] : new float[] {
				(float)frame.FullGpuRenderTime, (float)frame.FullGpuRenderTime * selectedRenderTime
			});
			var points = frame == null ?
				new float[4] :
				new float[] {
					frame.SceneSavedByBatching,
					frame.SceneDrawCallCount,
					frame.SceneVerticesCount,
					frame.SceneTrianglesCount
				};
			lineLegend.SetValues(points);
		}

		private void OnSliceSelected(ChartsContainer.Slice slice)
		{
			cpuLastSlice = cpuCharts.GetSlice(slice.Index);
			gpuLastSlice = gpuCharts.GetSlice(slice.Index);
			lineLastSlice = lineCharts.GetSlice(slice.Index);

			SetSlicePosition();
			SliceSelected?.Invoke(slice.Index);
		}

		private void SetSlicePosition()
		{
			float x = cpuLastSlice.Index * cpuCharts.ControlPointsSpacing;
			cpuCharts.SetLine(lineIndex: 3, new ChartsContainer.Line(
				new Vector2(x, 0), new Vector2(x, cpuCharts.Height), colorIndex: 10) { IsScalable = false });
			gpuCharts.SetLine(lineIndex: 1, new ChartsContainer.Line(
				new Vector2(x, 0), new Vector2(x, gpuCharts.Height), colorIndex: 10) { IsScalable = false });
			lineCharts.SetLine(lineIndex: 0, new ChartsContainer.Line(
				new Vector2(x, 0), new Vector2(x, lineCharts.Height), colorIndex: 10) { IsScalable = false });
		}

		private void UpdateActiveSliceIndicator()
		{
			if (cpuLastSlice != null) {
				if (cpuLastSlice.Index < 0) {
					cpuCharts.SetLine(3, new ChartsContainer.Line(
						Vector2.Zero, Vector2.Zero, colorIndex: 10) { IsScalable = false });
					gpuCharts.SetLine(1, new ChartsContainer.Line(
						Vector2.Zero, Vector2.Zero, colorIndex: 10) { IsScalable = false });
					lineCharts.SetLine(0, new ChartsContainer.Line(
						Vector2.Zero, Vector2.Zero, colorIndex: 10) { IsScalable = false });
				} else {
					SetSlicePosition();
					cpuLastSlice.Index -= 1;
					gpuLastSlice.Index -= 1;
					lineLastSlice.Index -= 1;
				}
			}
		}

		private static float Logarithm(float value) =>
			value <= 16.6f ? value / 16.6f : (float)Math.Log(value / 16.6f) + 1;

		private static float AntiLogarithm(float value) =>
			value <= 1 ? value * 16.6f : 16.6f * (float)Math.Exp(value - 1);

		public void UpdateCpuChartsSelectedArea(float[] points)
		{
			var data = new float[points.Length];
			for (int i = 0; i < data.Length; i++) {
				data[i] = points[i] * cpuCharts.GetOriginalPoint(0, i);
			}
			cpuCharts.Subtract(0, data, restoreOriginalValues: true);
			cpuCharts.Add(1, data, restoreOriginalValues: true);
		}

		public void UpdateGpuChartsSelectedArea(float[] points)
		{
			var data = new float[points.Length];
			for (int i = 0; i < data.Length; i++) {
				data[i] = points[i] * gpuCharts.GetOriginalPoint(0, i);
			}
			gpuCharts.Subtract(0, data, restoreOriginalValues: true);
			gpuCharts.Add(1, data, restoreOriginalValues: true);
		}
	}
}
