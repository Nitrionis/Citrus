using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lime;
using Lime.Profilers;
using Lime.Profilers.Contexts;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
using Tangerine.UI.Timeline;

namespace Tangerine.UI
{
	public class Profiler : Widget
	{
		private MainControlPanel mainControlPanel;

		private Widget settingsWidget;
		private ThemedCheckBox cpuInfoCheckBox;
		private ThemedCheckBox gpuInfoCheckBox;
		private ThemedCheckBox geometryInfoCheckBox;
		private ThemedCheckBox gpuDeepProfilingCheckBox;
		private ThemedCheckBox gpuSceneOnlyDeepProfilingCheckBox;
		private ThemedCheckBox gpuTraceCheckBox;

		private IndexesStorage indexesStorage;
		private ChartsPanel chartsPanel;
		private GpuTrace gpuTrace;
		private Widget gpuTraceMessagePanel;
		private SimpleText gpuTraceMessageLabel;

		private long lastProcessedUpdateIndex;
		private bool isDrawCallsRenderTimeEnabled;
		private bool isSceneOnlyDrawCallsRenderTime;
		private bool isNodeFilteringChanged;

		/// <summary>
		/// Rendering time for each frame for selected draw calls.
		/// </summary>
		private float[] selectedRenderTime;

		public Profiler(Widget contentWidget)
		{
			LimeProfiler.Initialize();
			LimeProfiler.ContextChanged += OnContextChanged;
			LimeProfiler.LocalDeviceUpdateStarted = OnLocalDeviceUpdateStarted;
			LimeProfiler.LocalDeviceFrameRenderCompleted = OnLocalDeviceFrameRenderCompleted;
			contentWidget.AddNode(this);
			Layout = new VBoxLayout();
			settingsWidget = CreateSettingsBlock();
			InitializeMainControlPanel(settingsWidget);
			AddNode(settingsWidget);
			InitializeChartsPanel();
			InitializeGpuTracePanel();
			selectedRenderTime = new float[GpuHistory.HistoryFramesCount];
			Tasks.Add(StateUpdateTask);
			lastProcessedUpdateIndex = 0;
		}

		private void InitializeMainControlPanel(Widget settingsWidget)
		{
			mainControlPanel = new MainControlPanel(settingsWidget);
			mainControlPanel.SceneFilteringChanged += (value) => {
				gpuTrace.Timeline.IsSceneOnly = value;
			};
			mainControlPanel.NodeFilteringChanged += (value) => {
				gpuTrace.Timeline.RegexNodeFilter = value;
			};
			AddNode(mainControlPanel);
		}

		private void InitializeChartsPanel()
		{
			indexesStorage = new IndexesStorage(GpuHistory.HistoryFramesCount);
			chartsPanel = new ChartsPanel();
			chartsPanel.SliceSelected += OnChartSliceSelected;
			AddNode(chartsPanel);
		}

		private void InitializeGpuTracePanel()
		{
			AddNode(gpuTrace = new GpuTrace());
			AddNode(gpuTraceMessagePanel = new Widget {
				Visible = false,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineHeaderBackground),
				Nodes = {
					(gpuTraceMessageLabel = new SimpleText {
						Color = ColorTheme.Current.Profiler.TimelineRulerAndText,
						Text = "No data for frame.",
						FontHeight = 64
					})
				}
			});
		}

		private void OnContextChanged()
		{
			isNodeFilteringChanged = false;
			chartsPanel.Reset();
			lastProcessedUpdateIndex = 0;
		}

		private IEnumerator<object> StateUpdateTask()
		{
			while (true) {
				if (isDrawCallsRenderTimeEnabled != LimeProfiler.IsDrawCallsRenderTimeEnabled) {
					isDrawCallsRenderTimeEnabled = LimeProfiler.IsDrawCallsRenderTimeEnabled;
					gpuDeepProfilingCheckBox.Checked = isDrawCallsRenderTimeEnabled;
				}
				if (isSceneOnlyDrawCallsRenderTime != LimeProfiler.IsSceneOnlyDrawCallsRenderTime) {
					isSceneOnlyDrawCallsRenderTime = LimeProfiler.IsSceneOnlyDrawCallsRenderTime;
					gpuSceneOnlyDeepProfilingCheckBox.Checked = isSceneOnlyDrawCallsRenderTime;
				}
				yield return null;
			}
		}

		private Widget CreateSettingsBlock()
		{
			cpuInfoCheckBox = new ThemedCheckBox { Checked = true };
			cpuInfoCheckBox.Changed += (args) => {
				chartsPanel.SetCpuChartsPanelVisible(args.Value);
			};
			gpuInfoCheckBox = new ThemedCheckBox { Checked = true };
			gpuInfoCheckBox.Changed += (args) => {
				chartsPanel.SetGpuChartsPanelVisible(args.Value);
			};
			geometryInfoCheckBox = new ThemedCheckBox { Checked = true };
			geometryInfoCheckBox.Changed += (args) => {
				chartsPanel.SetLineChartsPanelVisible(args.Value);
			};
			gpuTraceCheckBox = new ThemedCheckBox { Checked = true };
			gpuTraceCheckBox.Changed += (args) => {
				gpuTrace.Visible = args.Value;
			};
			gpuDeepProfilingCheckBox = new ThemedCheckBox { Checked = false };
			gpuDeepProfilingCheckBox.Changed += (args) => {
				if (args.ChangedByUser) {
					LimeProfiler.IsDrawCallsRenderTimeEnabled = args.Value;
				}
			};
			gpuSceneOnlyDeepProfilingCheckBox = new ThemedCheckBox { Checked = false };
			gpuSceneOnlyDeepProfilingCheckBox.Changed += (args) => {
				if (args.ChangedByUser) {
					LimeProfiler.IsSceneOnlyDrawCallsRenderTime = args.Value;
				}
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
				Visible = false,
				Nodes = {
					HGroup(cpuInfoCheckBox, CreateLabel("CPU information")),
					HGroup(gpuInfoCheckBox, CreateLabel("GPU information")),
					HGroup(geometryInfoCheckBox, CreateLabel("Geometry information")),
					HGroup(gpuTraceCheckBox, CreateLabel("GPU trace timeline")),
					HGroup(gpuDeepProfilingCheckBox, CreateLabel("Deep profiling GPU")),
					HGroup(gpuSceneOnlyDeepProfilingCheckBox, CreateLabel("Scene only GPU deep profiling"))
				}
			};
			return settingsWidget;
		}

		private void OnLocalDeviceUpdateStarted()
		{
			var cpuHistory = LimeProfiler.CpuHistory;
			var gpuHistory = LimeProfiler.GpuHistory;
			while (lastProcessedUpdateIndex < cpuHistory.ProfiledUpdatesCount) {
				var update = cpuHistory.GetUpdate(lastProcessedUpdateIndex);
				bool hasFrame = gpuHistory.IsFrameIndexValid(update.FrameIndex);
				var frame = !hasFrame ? null : gpuHistory.GetFrame(update.FrameIndex);
				if (!hasFrame || frame.IsCompleted) {
					chartsPanel.Enqueue(frame, update);
					indexesStorage.Enqueue(update.UpdateIndex);
					lastProcessedUpdateIndex += 1;
				} else {
					break;
				}
			}
		}

		private void OnLocalDeviceFrameRenderCompleted()
		{
			if (isNodeFilteringChanged) {
				isNodeFilteringChanged = false;
				SelectRenderTime(
					gpuTrace.Timeline.IsSceneOnly,
					gpuTrace.Timeline.RegexNodeFilter,
					selectedRenderTime);
				chartsPanel.GpuCharts.Subtract(0, selectedRenderTime);
				chartsPanel.GpuCharts.Add(1, selectedRenderTime);
			}
		}

		private void OnChartSliceSelected(int sliceIndex)
		{
			mainControlPanel.ResetFilters();
			var update = LimeProfiler.CpuHistory.GetUpdate(indexesStorage.GetItem(sliceIndex));
			var frame = !LimeProfiler.GpuHistory.IsFrameIndexValid(update.FrameIndex) ?
				null : LimeProfiler.GpuHistory.GetFrame(update.FrameIndex);
			UpdateChartsLegends(sliceIndex, frame, update);
			if (frame != null && LimeProfiler.GpuHistory.TryLockFrame(update.FrameIndex)) {
				if (frame.IsDeepProfilingEnabled) {
					// todo create select request
					gpuTrace.Timeline.Rebuild(frame);
				}
			}
		}

		private void UpdateChartsLegends(int sliceIndex, GpuHistory.Item frame, CpuHistory.Item update)
		{
			var cpuValues = new float[] {
				update.DeltaTime,
				0f
			};
			chartsPanel.CpuLegend.SetValues(cpuValues);
			var gpuValues = new float[] {
				(float)frame.FullGpuRenderTime,
				0f
			};
			chartsPanel.GpuLegend.SetValues(gpuValues);
			var lineValues = new float[] {
				frame.SceneSavedByBatching,
				frame.SceneDrawCallCount,
				frame.SceneVerticesCount,
				frame.SceneTrianglesCount
			};
			chartsPanel.LineLegend.SetValues(lineValues);
		}

		private void SelectRenderTime(bool isSceneOnly, Regex regexNodeFilter, float[] resultsBuffer)
		{
			for (long i = 0; i < GpuHistory.HistoryFramesCount; i++) {
				foreach (var index in indexesStorage) {
					resultsBuffer[i] = 0;
					long frameIndex = LimeProfiler.CpuHistory.GetUpdate(index).FrameIndex;
					if (LimeProfiler.GpuHistory.IsFrameIndexValid(frameIndex)) {
						var frame = LimeProfiler.GpuHistory.GetFrame(frameIndex);
						float renderTimeOfSelected = 0f;
						foreach (var dc in frame.DrawCalls) {
							var pi = dc.ProfilingInfo;
							bool isContainsTargetNode = DrawCallsTimeline.CheckTargetNode(regexNodeFilter, dc);
							bool isSceneFilterPassed = !isSceneOnly || pi.IsPartOfScene;
							bool isFilteringPassed = isContainsTargetNode && isSceneFilterPassed;
							renderTimeOfSelected += isFilteringPassed ? (dc.Finish - dc.Start) / 1000f : 0f;
						}
						resultsBuffer[i] = renderTimeOfSelected;
					}
				}
			}
		}
	}
}
