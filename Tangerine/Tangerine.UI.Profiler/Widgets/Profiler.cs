using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lime;
using Lime.Profilers;
using GpuHistory = Lime.Graphics.Platform.Profiling.GpuHistory;
using Tangerine.UI.Timeline;
using System;

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
		private ThemedCheckBox cpuTraceCheckBox;

		private FixedCapacityQueue<long> indexesStorage;
		private ChartsPanel chartsPanel;
		private CpuTrace cpuTrace;
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
		private float[] selectedCpuUsageTime;
		private long outdatedSamplesCount;

		public Profiler(Widget contentWidget)
		{
#if LIME_PROFILER
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
			InitializeCpuTracePanel();
			selectedRenderTime = new float[ChartsPanel.HistorySize];
			selectedCpuUsageTime = new float[ChartsPanel.HistorySize];
			outdatedSamplesCount = ChartsPanel.HistorySize;
			Tasks.Add(StateUpdateTask);
			lastProcessedUpdateIndex = 0;
#endif
		}

		private void InitializeMainControlPanel(Widget settingsWidget)
		{
			mainControlPanel = new MainControlPanel(settingsWidget);
			mainControlPanel.SceneFilteringChanged += (value) => {
				gpuTrace.Timeline.IsSceneOnly = value;
				cpuTrace.Timeline.IsSceneOnly = value;
				isNodeFilteringChanged = true;
				outdatedSamplesCount = 0;
			};
			mainControlPanel.NodeFilteringChanged += (value) => {
				gpuTrace.Timeline.RegexNodeFilter = value;
				cpuTrace.Timeline.RegexNodeFilter = value;
				isNodeFilteringChanged = true;
				outdatedSamplesCount = 0;
			};
			AddNode(mainControlPanel);
		}

		private void InitializeChartsPanel()
		{
			indexesStorage = new FixedCapacityQueue<long>(ChartsPanel.HistorySize);
			for (int i = 0; i < indexesStorage.Capacity; i++) {
				indexesStorage[i] = -1;
			}
			chartsPanel = new ChartsPanel();
			chartsPanel.SliceSelected += OnChartSliceSelected;
			AddNode(chartsPanel);
		}

		private void InitializeGpuTracePanel()
		{
			AddNode(gpuTrace = new GpuTrace());
			gpuTraceMessageLabel = new SimpleText {
				Color = ColorTheme.Current.Profiler.TimelineRulerAndText,
				Anchors = Anchors.LeftRight,
				Text = "No deep profiling data available for this frame.",
				FontHeight = 48,
				Padding = new Thickness(8)
			};
			AddNode(gpuTraceMessagePanel = new Widget {
				Visible = false,
				Height = 64,
				MinMaxHeight = 64,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineHeaderBackground),
				Nodes = { gpuTraceMessageLabel }
			});
		}

		private void InitializeCpuTracePanel()
		{
			AddNode(cpuTrace = new CpuTrace() { MaxHeight = 400 });
			cpuTrace.Timeline.MaxHeight = 300;
		}

		private void OnContextChanged()
		{
			isNodeFilteringChanged = false;
			chartsPanel.Reset();
			outdatedSamplesCount = ChartsPanel.HistorySize;
			lastProcessedUpdateIndex = LimeProfiler.CpuHistory.ProfiledUpdatesCount;
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
			cpuTraceCheckBox = new ThemedCheckBox { Checked = true };
			cpuTraceCheckBox.Changed += (args) => {
				cpuTrace.Visible = args.Value;
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
				Layout = new HBoxLayout(),
				Padding = new Thickness(8),
				Visible = false,
				Nodes = {
					new Widget {
						Layout = new VBoxLayout(),
						Padding = new Thickness(8),
						Nodes = {
							HGroup(gpuDeepProfilingCheckBox, CreateLabel("Deep profiling GPU")),
							HGroup(gpuSceneOnlyDeepProfilingCheckBox, CreateLabel("Scene only GPU deep profiling"))
						}
					},
					new Widget {
						Layout = new VBoxLayout(),
						Padding = new Thickness(8),
						Nodes = {
							HGroup(gpuTraceCheckBox, CreateLabel("GPU trace timeline")),
							HGroup(cpuTraceCheckBox, CreateLabel("CPU trace timeline")),

						}
					},
					new Widget {
						Layout = new VBoxLayout(),
						Padding = new Thickness(8),
						Nodes = {
							HGroup(cpuInfoCheckBox, CreateLabel("CPU Charts")),
							HGroup(gpuInfoCheckBox, CreateLabel("GPU Charts")),
						}
					},
					new Widget {
						Layout = new VBoxLayout(),
						Padding = new Thickness(8),
						Nodes = {
							HGroup(geometryInfoCheckBox, CreateLabel("Geometry Charts")),
						}
					},
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
					outdatedSamplesCount += 1;
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
				chartsPanel.UpdateGpuChartsSelectedArea(selectedRenderTime);
				SelectUpdateTime(
					cpuTrace.Timeline.IsSceneOnly,
					cpuTrace.Timeline.RegexNodeFilter,
					selectedCpuUsageTime);
				chartsPanel.UpdateCpuChartsSelectedArea(selectedCpuUsageTime);
			}
		}

		private void OnChartSliceSelected(int sliceIndex)
		{
			mainControlPanel.ResetFilters();
			long updateIndex = indexesStorage.GetItem(sliceIndex);
			if (LimeProfiler.CpuHistory.TryLockUpdate(updateIndex)) {
				var update = LimeProfiler.CpuHistory.GetUpdate(updateIndex);
				GpuHistory.Item frame = null;
				if (LimeProfiler.GpuHistory.TryLockFrame(update.FrameIndex)) {
					frame = LimeProfiler.GpuHistory.GetFrame(update.FrameIndex);
					gpuTrace.Visible = frame.IsDeepProfilingEnabled;
					gpuTraceMessagePanel.Visible = !frame.IsDeepProfilingEnabled;
					if (frame.IsDeepProfilingEnabled) {
						gpuTrace.Timeline.Rebuild(frame);
					}
					cpuTrace.Timeline.Rebuild(update);
				}
				long srtIndex = sliceIndex + outdatedSamplesCount;
				float srt = srtIndex >= selectedRenderTime.Length ? 0 : selectedRenderTime[srtIndex];
				float sut = srtIndex >= selectedCpuUsageTime.Length ? 0 : selectedCpuUsageTime[srtIndex];
				chartsPanel.UpdateChartsLegends(frame, update, srt, sut);
			}
		}

		private void SelectRenderTime(bool isSceneOnly, Regex regexNodeFilter, float[] resultsBuffer)
		{
			int chartSliceIndex = 0;
			foreach (var index in indexesStorage) {
				resultsBuffer[chartSliceIndex] = 0;
				if (LimeProfiler.CpuHistory.IsUpdateIndexValid(index)) {
					long frameIndex = LimeProfiler.CpuHistory.GetUpdate(index).FrameIndex;
					if (LimeProfiler.GpuHistory.IsFrameIndexValid(frameIndex)) {
						var frame = LimeProfiler.GpuHistory.GetFrame(frameIndex);
						float renderTimeOfSelected = 0f;
						uint endOfInterval = 0;
						foreach (var dc in frame.DrawCalls) {
							var pi = dc.GpuCallInfo;
							bool isContainsTargetNode = GpuUsageTimeline.CheckTargetNode(regexNodeFilter, dc);
							bool isSceneFilterPassed = !isSceneOnly || pi.IsPartOfScene;
							bool isFilteringPassed =
								!(!isSceneOnly && regexNodeFilter == null) &&
								isContainsTargetNode && isSceneFilterPassed;
							if (isFilteringPassed) {
								if (endOfInterval < dc.Start) {
									endOfInterval = dc.Finish;
									renderTimeOfSelected += (dc.Finish - dc.Start) / 1000f;
								} else if (endOfInterval < dc.Finish) {
									renderTimeOfSelected += (dc.Finish - endOfInterval) / 1000f;
									endOfInterval = dc.Finish;
								}
							}
						}
						resultsBuffer[chartSliceIndex] = renderTimeOfSelected / (float)frame.FullGpuRenderTime;
					}
				}
				chartSliceIndex += 1;
			}
		}

		private void SelectUpdateTime(bool isSceneOnly, Regex regexNodeFilter, float[] resultsBuffer)
		{
			int chartSliceIndex = 0;
			foreach (var index in indexesStorage) {
				resultsBuffer[chartSliceIndex] = 0;
				if (LimeProfiler.CpuHistory.IsUpdateIndexValid(index)) {
					var update = LimeProfiler.CpuHistory.GetUpdate(index);
					float cpuUsageTimeOfSelected = 0f;
					uint endOfInterval = 0;
					foreach (var usage in update.NodesResults) {
						bool isContainsTargetNode = CpuUsageTimeline.CheckTargetNode(regexNodeFilter, usage);
						bool isSceneFilterPassed = !isSceneOnly || usage.IsPartOfScene;
						bool isFilteringPassed =
							!(!isSceneOnly && regexNodeFilter == null) &&
							isContainsTargetNode && isSceneFilterPassed;
						if (isFilteringPassed) {
							if (endOfInterval < usage.Start) {
								endOfInterval = usage.Finish;
								cpuUsageTimeOfSelected += (usage.Finish - usage.Start) / 1000f;
							} else if (endOfInterval < usage.Finish) {
								cpuUsageTimeOfSelected += (usage.Finish - endOfInterval) / 1000f;
								endOfInterval = usage.Finish;
							}
						}
					}
					resultsBuffer[chartSliceIndex] = cpuUsageTimeOfSelected / update.DeltaTime;
				}
				chartSliceIndex += 1;
			}
		}
	}
}