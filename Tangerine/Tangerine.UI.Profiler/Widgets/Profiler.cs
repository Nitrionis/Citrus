using System;
using System.Collections.Generic;
using Lime;
using Lime.Widgets.Charts;
using Lime.Graphics.Platform;
using LimeProfiler = Lime.Profilers.Profiler;

namespace Tangerine.UI
{
	public class Profiler : Widget
	{
		private ThemedDropDownList profilingMode;
		private ThemedCheckBox baseInfoCheckBox;
		private ThemedCheckBox geometryInfoCheckBox;
		private ThemedCheckBox drawCallsInfoCheckBox;

		private Legend areaLegend;
		private AreaCharts areaCharts;

		private Legend lineLegend;
		private LineCharts lineCharts;

		private Charts.Slice areaLastSlice;
		private Charts.Slice lineLastSlice;

		private Queue<ProfilerHistory.Item> unfinished;
		private ProfilerHistory.Item lastInQueue;

		private Widget contentWidget;
		private Widget settingsWidget;

		private bool isPause = false;
		private ThemedButton pauseСontinueButton;

		public Profiler(Widget panel)
		{
			new LimeProfiler();

			Layout = new VBoxLayout();

			contentWidget = new Widget {
				Anchors = Anchors.LeftRightTopBottom,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.Background),
				Nodes = { this }
			};
			panel.AddNode(contentWidget);

			unfinished = new Queue<ProfilerHistory.Item>();
			
			AddNode(CreateMainUi());
			AddNode(CreateSettingsBlock());
			AddNode(CreateChartsSegment());

			LimeProfiler.OnFrameCompleted += OnFrameRenderCompleted;
		}

		private Widget CreateMainUi()
		{
			profilingMode = new ThemedDropDownList() {
				Padding = new Thickness(0, 4),
				MinMaxWidth = 128
			};
			profilingMode.Items.Add(new ThemedDropDownList.Item("Local device mode"));
			profilingMode.Items.Add(new ThemedDropDownList.Item("Remote device mode"));
			profilingMode.Index = 0;
			var settingsButton = new ThemedButton("Settings") {
				Clicked = () => { settingsWidget.Visible = !settingsWidget.Visible; }
			};
			pauseСontinueButton = new ThemedButton("Pause") {
				Clicked = () => {
					isPause = !isPause;
					pauseСontinueButton.Text = isPause ? "Сontinue" : "Pause";
				}
			};
			return new Widget {
				Layout = new HBoxLayout(),
				Padding = new Thickness(8, 0, 2, 0),
				Nodes = {
					settingsButton,
					profilingMode,
					pauseСontinueButton
				}
			};
		}

		private Widget CreateSettingsBlock()
		{
			baseInfoCheckBox = new ThemedCheckBox() {
				Checked = true,
				Clicked = () => {
					areaLegend.Visible = !areaLegend.Visible;
					areaCharts.Visible = !areaCharts.Visible;
				}
			};
			geometryInfoCheckBox = new ThemedCheckBox() {
				Checked = true,
				Clicked = () => {
					lineLegend.Visible = !lineLegend.Visible;
					lineCharts.Visible = !lineCharts.Visible;
				}
			};
			drawCallsInfoCheckBox = new ThemedCheckBox() {

			};
			SimpleText CreateLabel(string text) => new SimpleText {
				Text = text,
				FontHeight = 18,
				Padding = new Thickness(4, 0),
				Color = ColorTheme.Current.Profiler.LegendText,
			};
			Node HGroup(Node n1, Node n2) => new Widget {
				Layout = new HBoxLayout(),
				Padding = new Thickness(0, 2),
				Nodes = { n1, n2 }
			};
			settingsWidget = new Widget {
				Layout = new VBoxLayout(),
				Padding = new Thickness(8),
				Nodes = {
					HGroup(baseInfoCheckBox, CreateLabel("Basic rendering information.")),
					HGroup(geometryInfoCheckBox,CreateLabel("Rendering geometry information.")),
					HGroup(drawCallsInfoCheckBox, CreateLabel("Draw time information for each draw call.")),
				}
			};
			return settingsWidget;
		}

		private Widget CreateChartsSegment()
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
			var parameters = new AreaCharts.Parameters(ProfilerHistory.HistoryFramesCount, colors) {
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
				MinMaxHeight = parameters.ChartHeight,
				Height = parameters.ChartHeight
			};
			// Create line chars.
			parameters = new LineCharts.Parameters(ProfilerHistory.HistoryFramesCount, colors) {
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

			return new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new Widget {
						Layout = new VBoxLayout { Spacing = 6 },
						Nodes = { areaLegend, lineLegend },
						Padding = new Thickness(6)
					},
					new Widget {
						Layout = new VBoxLayout { Spacing = 6 },
						Nodes = { areaCharts, lineCharts },
						Padding = new Thickness(6)
					}
				}
			};
		}

		public void OnFrameRenderCompleted()
		{
			if (lastInQueue != LimeProfiler.GpuHistory.LastFrame) { // todo
				lastInQueue = LimeProfiler.GpuHistory.LastFrame;
				unfinished.Enqueue(lastInQueue);
				if (unfinished.Peek().IsDeepProfilingCompleted) {
					var frame = unfinished.Dequeue();
					var points = new float[] {
						(float)frame.FullGpuRenderTime,
						0f, // CPU
						0f, // Selected
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
			}
		}

		private void SliceSelected(Charts.Slice slice)
		{
			areaLastSlice = areaCharts.GetSlice(slice.Index);
			lineLastSlice = lineCharts.GetSlice(slice.Index);
			areaLegend.SetValues(areaLastSlice.Points);
			lineLegend.SetValues(lineLastSlice.Points);

			//long frameIndex = PlatformProfiler.Instance.ProfiledFramesCount -
			//	(PlatformProfiler.HistoryFramesCount - slice.Index);
			//var frameInfo = PlatformProfiler.Instance.TryLockFrame(frameIndex);
		}

		private void UpdateActiveSliceIndicator()
		{
			if (areaLastSlice != null && areaLastSlice.Index >= 0) {
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
				areaLastSlice.Index = areaLastSlice.Index - 1;
				lineLastSlice.Index = lineLastSlice.Index - 1;
			}
		}
	}
}
