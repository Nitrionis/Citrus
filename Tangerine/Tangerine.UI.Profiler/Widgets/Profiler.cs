using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lime;
using Lime.Profilers;
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

		private long lastProcessedUpdateIndex;
		private IndexesStorage indexesStorage;
		private ChartsPanel chartsPanel;
		private GpuTrace gpuTrace;

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
			LimeProfiler.ContextChanged += Reset;
			LimeProfiler.LocalDeviceUpdateStarted = OnLocalDeviceUpdateStarted;
			LimeProfiler.LocalDeviceFrameRenderCompleted = OnLocalDeviceFrameRenderCompleted;
			contentWidget.AddNode(this);
			Layout = new VBoxLayout();
			settingsWidget = CreateSettingsBlock();
			mainControlPanel = new MainControlPanel(settingsWidget);
			AddNode(mainControlPanel);
			AddNode(settingsWidget);
			indexesStorage = new IndexesStorage(GpuHistory.HistoryFramesCount);
			chartsPanel = new ChartsPanel();
			chartsPanel.SliceSelected += OnChartSliceSelected;
			AddNode(chartsPanel);
			gpuTrace = new GpuTrace {
				NodeFilteringChanged = () => { isNodeFilteringChanged = true; }
			};
			AddNode(gpuTrace);
			selectedRenderTime = new float[GpuHistory.HistoryFramesCount];
			Tasks.Add(StateUpdateTask);
			lastProcessedUpdateIndex = 0;
		}

		private void Reset()
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
			while (lastProcessedUpdateIndex < LimeProfiler.CpuHistory.ProfiledUpdatesCount) {
				var update = LimeProfiler.CpuHistory.GetUpdate(lastProcessedUpdateIndex);
				var frame = LimeProfiler.GpuHistory.GetFrame(update.FrameIndex);
				if (frame.IsCompleted) {
					chartsPanel.FrameCompleted(frame, update);
					indexesStorage.Enqueue(new IndexesStorage.Item {
						FrameIndex = frame.FrameIndex,
						UpdateIndex = update.UpdateIndex
					});
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
				SelectRenderTime(gpuTrace.Timeline.IsSceneOnly, gpuTrace.Timeline.RegexNodeFilter, selectedRenderTime);
				chartsPanel.GpuCharts.Subtract(0, selectedRenderTime);
				chartsPanel.GpuCharts.Add(1, selectedRenderTime);
			}
		}

		private void OnChartSliceSelected(int sliceIndex)
		{
			var indices = indexesStorage.GetItem(sliceIndex);
			if (
				LimeProfiler.GpuHistory.IsFrameIndexValid(indices.FrameIndex) &&
				LimeProfiler.GpuHistory.TryLockFrame(indices.FrameIndex)
				)
			{
				var frame = LimeProfiler.GpuHistory.GetFrame(indices.FrameIndex);
				if (frame.IsDeepProfilingEnabled) {
					gpuTrace.Timeline.Rebuild(frame);
				}
			}
		}

		private void SelectRenderTime(bool isSceneOnly, Regex regexNodeFilter, float[] resultsBuffer)
		{
			for (long i = 0; i < GpuHistory.HistoryFramesCount; i++) {
				foreach (var indices in indexesStorage) {
					resultsBuffer[i] = 0;
					if (LimeProfiler.GpuHistory.IsFrameIndexValid(indices.FrameIndex)) {
						var frame = LimeProfiler.GpuHistory.GetFrame(indices.FrameIndex);
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
