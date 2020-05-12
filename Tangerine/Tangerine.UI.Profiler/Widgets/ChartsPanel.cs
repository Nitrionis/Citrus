using System;
using Lime;
using Lime.Profilers;
using GpuHistory = Lime.Graphics.Platform.GpuHistory;
using Tangerine.UI.Charts;

namespace Tangerine.UI
{
	internal class ChartsPanel : Widget
	{
		public const int HistorySize = GpuHistory.HistoryFramesCount - 1;
		private const float ChartsHeight = 64;

		public readonly Legend CpuLegend;
		public readonly AreaReplaceableCharts CpuCharts;

		public readonly Legend GpuLegend;
		public readonly AreaReplaceableCharts GpuCharts;

		public readonly Legend LineLegend;
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
				ChartsCount = 3,
				UserLinesCount = 4,
				SliceSelected = OnSliceSelected
			};
			CpuCharts = new AreaReplaceableCharts(parameters) {
				Id = "CPU Charts",
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground,
				Height = ChartsHeight,
				MinMaxHeight = ChartsHeight
			};
			var targetWidth = (parameters.ControlPointsCount - 1) * parameters.ControlPointsSpacing;
			// Horizontal line for lowest fps
			float fpsHeight = Logarithm(1000.0f / 1.0f);
			CpuCharts.SetLine(0, new ChartsContainer.Line(
				new Vector2(0, fpsHeight), new Vector2(targetWidth, fpsHeight), 10, null));
			// Horizontal line for 30 fps
			fpsHeight = Logarithm(1000.0f / 30.0f);
			CpuCharts.SetLine(1, new ChartsContainer.Line(
				new Vector2(0, fpsHeight), new Vector2(targetWidth, fpsHeight), 3, null));
			// Horizontal line for 60 fps
			fpsHeight = Logarithm(1000.0f / 60.0f);
			CpuCharts.SetLine(2, new ChartsContainer.Line(
				new Vector2(0, fpsHeight), new Vector2(targetWidth, fpsHeight), 9, null));
			// Create legend for CPU charts.
			var cpuLegendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "CPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Selected", Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[2], Name = "GC",       Format = "{0,6:0.00}" },
			};
			CpuLegend = new Legend(cpuLegendItems, CpuCharts.SetActive) {
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
			GpuCharts = new AreaReplaceableCharts(parameters) {
				Id = "GPU Charts",
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground,
				Height = ChartsHeight,
				MinMaxHeight = ChartsHeight
			};
			GpuCharts.SetLine(0, new ChartsContainer.Line(
				new Vector2(0, 1), new Vector2(targetWidth, 1), 10, "10 ms"));
			// Create legend for GPU charts.
			var gpuLegendItems = new Legend.ItemDescription[] {
				new Legend.ItemDescription { Color = colors[0], Name = "GPU",      Format = "{0,6:0.00}" },
				new Legend.ItemDescription { Color = colors[1], Name = "Selected", Format = "{0,6:0.00}" },
			};
			GpuLegend = new Legend(gpuLegendItems, GpuCharts.SetActive) {
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
			LineCharts = new LineCharts((LineCharts.Parameters)parameters) {
				Id = "GPU LineCharts",
				BackgroundColor = ColorTheme.Current.Profiler.ChartsBackground,
				MinMaxHeight = 100,
				Height = 100,
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
			LineLegend = new Legend(legendItems, LineCharts.SetActive) {
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
						Nodes = { CpuLegend, GpuLegend, LineLegend },
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

		public void Enqueue(GpuHistory.Item frame, CpuHistory.Item update)
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
			CpuLegend.Visible = value;
			CpuCharts.Visible = value;
		}

		public void SetGpuChartsPanelVisible(bool value)
		{
			GpuLegend.Visible = value;
			GpuCharts.Visible = value;
		}

		public void SetLineChartsPanelVisible(bool value)
		{
			LineLegend.Visible = value;
			LineCharts.Visible = value;
		}

		private void PushChartsSlice(GpuHistory.Item frame, CpuHistory.Item update)
		{
			// Push CPU charts data
			float scaledDeltaTime = Logarithm(update.DeltaTime);
			float gcTime = scaledDeltaTime * (update.GcTimePercent / 100.0f);
			CpuCharts.PushSlice(new float[] { scaledDeltaTime - gcTime, 0f, gcTime });
			// Update CPU charts max value line
			float cpuMaxValue = CpuCharts.ChartsMaxValue;
			float originalValue = cpuMaxValue;//AntiLogarithm(cpuMaxValue);
			CpuCharts.SetLine(lineIndex: 0, new ChartsContainer.Line(
				start:       new Vector2(0, cpuMaxValue),
				end:         new Vector2(CpuCharts.Width, cpuMaxValue),
				colorIndex:  10,
				caption:     string.Format("{0:0.00} fps {1:0.00} ms", 1000f / originalValue, originalValue)));
			// Push GPU charts data
			GpuCharts.PushSlice(frame == null ? new float[2] : new float[] { (float)frame.FullGpuRenderTime, 0f });
			// Update GPU charts max value line
			float gpuMaxValue = GpuCharts.ChartsMaxValue;
			GpuCharts.SetLine(lineIndex: 0, new ChartsContainer.Line(
				start:       new Vector2(0, gpuMaxValue),
				end:         new Vector2(GpuCharts.Width, gpuMaxValue),
				colorIndex:  10,
				caption:     string.Format("{0:0.##} ms", gpuMaxValue)));
			// Push LineCharts data
			var points = frame == null ?
				new float[4] :
				new float[] {
					frame.SceneSavedByBatching,
					frame.SceneDrawCallCount,
					frame.SceneVerticesCount,
					frame.SceneTrianglesCount
				};
			LineCharts.PushSlice(points);
			UpdateChartsLegends(frame, update, selectedRenderTime: 0, selectedUpdateTime: 0);
		}

		public void UpdateChartsLegends(GpuHistory.Item frame, CpuHistory.Item update, float selectedRenderTime, float selectedUpdateTime)
		{
			CpuLegend.SetValues(new float[] {
				update.DeltaTime,
				selectedUpdateTime,
				update.DeltaTime * update.GcTimePercent / 100f
			});
			GpuLegend.SetValues(frame == null ? new float[2] : new float[] {
				(float)frame.FullGpuRenderTime,
				selectedRenderTime
			});
			var points = frame == null ?
				new float[4] :
				new float[] {
					frame.SceneSavedByBatching,
					frame.SceneDrawCallCount,
					frame.SceneVerticesCount,
					frame.SceneTrianglesCount
				};
			LineLegend.SetValues(points);
		}

		private void OnSliceSelected(ChartsContainer.Slice slice)
		{
			cpuLastSlice = CpuCharts.GetSlice(slice.Index);
			gpuLastSlice = GpuCharts.GetSlice(slice.Index);
			lineLastSlice = LineCharts.GetSlice(slice.Index);

			SetSlicePosition();
			SliceSelected?.Invoke(slice.Index);
		}

		private void SetSlicePosition()
		{
			float x = cpuLastSlice.Index * CpuCharts.ControlPointsSpacing;
			CpuCharts.SetLine(lineIndex: 3, new ChartsContainer.Line(
				start: new Vector2(x, 0),
				end: new Vector2(x, CpuCharts.Height * (1f / CpuCharts.ScaleCoefficient)),
				colorIndex: 10,
				caption: null));
			GpuCharts.SetLine(lineIndex: 1, new ChartsContainer.Line(
				start: new Vector2(x, 0),
				end: new Vector2(x, GpuCharts.Height * (1f / GpuCharts.ScaleCoefficient)),
				colorIndex: 10,
				caption: null));
			LineCharts.SetLine(lineIndex: 0, new ChartsContainer.Line(
				start: new Vector2(x, 0),
				end: new Vector2(x, LineCharts.Height), // because IsIndependentMode
				colorIndex: 10,
				caption: null));
		}

		private void UpdateActiveSliceIndicator()
		{
			if (cpuLastSlice != null) {
				if (cpuLastSlice.Index < 0) {
					CpuCharts.SetLine(3, new ChartsContainer.Line(
						Vector2.Zero, Vector2.Zero, colorIndex: 10, caption: null));
					GpuCharts.SetLine(1, new ChartsContainer.Line(
						Vector2.Zero, Vector2.Zero, colorIndex: 10, caption: null));
					LineCharts.SetLine(0, new ChartsContainer.Line(
						Vector2.Zero, Vector2.Zero, colorIndex: 10, caption: null));
				} else {
					SetSlicePosition();
					cpuLastSlice.Index -= 1;
					gpuLastSlice.Index -= 1;
					lineLastSlice.Index -= 1;
				}
			}
		}

		private static float Logarithm(float value) => value <= 33.3f ? value :
			33.3f + (float)Math.Log((value - 33.3) / 16.0 + 1.0, 2);

		private static float AntiLogarithm(float value) => value <= 33.3f ? value :
			33.3f + ((float)Math.Pow(2, value - 33.3) - 1f) * 16.0f;
	}
}
