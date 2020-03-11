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
		private ThemedCheckBox baseInfoCheckBox;
		private ThemedCheckBox geometryInfoCheckBox;
		private ThemedCheckBox gpuDeepProfilingCheckBox;
		private ThemedCheckBox gpuSceneOnlyDeepProfilingCheckBox;
		private ThemedCheckBox gpuTraceCheckBox;

		private Queue<CpuHistory.Item> unpushedUpdates;
		private CpuHistory.Item lastPushed;
		private IndexesStorage indexesStorage;
		private ChartsPanel chartsPanel;
		private GpuTrace gpuTrace;

		private bool isDrawCallsRenderTimeEnabled;
		private bool isSceneOnlyDrawCallsRenderTime;

		private bool isNodeFilteringChanged;
		private long lastProfiledFrameIndex;
		private long lastProfiledUpdateIndex;

		/// <summary>
		/// Rendering time for each frame for selected draw calls.
		/// </summary>
		private float[] selectedRenderTime;

		public Profiler(Widget contentWidget)
		{
			LimeProfiler.Initialize();
			LimeProfiler.ContextChanged += Reset;
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
			unpushedUpdates = new Queue<CpuHistory.Item>();
			Tasks.Add(ChartsUpdateTask);
		}

		private void Reset()
		{
			isNodeFilteringChanged = false;
			lastProfiledFrameIndex = LimeProfiler.GpuHistory.ProfiledFramesCount + 1;
			lastProfiledUpdateIndex = LimeProfiler.CpuHistory.ProfiledUpdatesCount + 1;
			chartsPanel.Reset();
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

		private IEnumerator<object> ChartsUpdateTask()
		{
			while (true) {
				if (
					lastPushed != LimeProfiler.CpuHistory.LastUpdate &&
					LimeProfiler.CpuHistory.LastUpdate.FrameIndex != CpuHistory.Item.FrameIndexUnset
					)
				{
					lastPushed = LimeProfiler.CpuHistory.LastUpdate;
					unpushedUpdates.Enqueue(LimeProfiler.CpuHistory.LastUpdate);
				}
				var update = unpushedUpdates.Count == 0 ? null : unpushedUpdates.Peek();
				if (update != null && update.FrameIndex != LimeProfiler.GpuHistory.ProfiledFramesCount) {
					var frame = LimeProfiler.GpuHistory.GetFrame(update.FrameIndex);
					if (frame.IsCompleted) {
						chartsPanel.FrameCompleted(frame, update);
						indexesStorage.Enqueue(new IndexesStorage.Item {
							FrameIndex = frame.FrameIndex,
							UpdateIndex = update.UpdateIndex
						});
						unpushedUpdates.Dequeue();
					}
				}
				yield return null;
			}
		}

		private Widget CreateSettingsBlock()
		{
			baseInfoCheckBox = new ThemedCheckBox { Checked = true };
			baseInfoCheckBox.Changed += (args) => {
				chartsPanel.SetAreaChartsPanelVisible(args.Value);
			};
			geometryInfoCheckBox = new ThemedCheckBox { Checked = true };
			geometryInfoCheckBox.Changed += (args) => {
				chartsPanel.SetLineChartsPanelVisible(args.Value);
			};
			gpuTraceCheckBox = new ThemedCheckBox { Checked = false };
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
				Nodes = {
					HGroup(baseInfoCheckBox, CreateLabel("Basic rendering information")),
					HGroup(geometryInfoCheckBox, CreateLabel("Rendering geometry information")),
					HGroup(gpuTraceCheckBox, CreateLabel("GPU trace timeline")),
					HGroup(gpuDeepProfilingCheckBox, CreateLabel("Deep profiling GPU")),
					HGroup(gpuSceneOnlyDeepProfilingCheckBox, CreateLabel("Scene only GPU deep profiling"))
				}
			};
			return settingsWidget;
		}

		private void OnLocalDeviceFrameRenderCompleted()
		{

			//while (lastProfiledFrameIndex < LimeProfiler.GpuHistory.LastFrame.FrameIndex) {
			//	var frame = LimeProfiler.GpuHistory.GetFrame(lastProfiledFrameIndex + 1);
			//	var update = LimeProfiler.CpuHistory.GetUpdate(lastProfiledUpdateIndex + 1);
			//	if (update.FrameIndex == CpuHistory.Item.FrameIndexUnset) {
			//		lastProfiledFrameIndex++;
			//		lastProfiledUpdateIndex++;
			//	} else if (!frame.IsCompleted || update.FrameIndex == CpuHistory.Item.FrameIndexPendingConfirmation) {
			//		break;
			//	} else {
			//		chartsPanel.FrameCompleted(frame, update);
			//		indexesStorage.Enqueue(new IndexesStorage.Item {
			//			FrameIndex = frame.FrameIndex,
			//			UpdateIndex = update.UpdateIndex
			//		});
			//		//lastProfiledFrameIndex++;
			//		//lastProfiledUpdateIndex++;
			//	}
			//}
			// move to separate thread
			if (isNodeFilteringChanged) {
				isNodeFilteringChanged = false;
				SelectRenderTime(gpuTrace.Timeline.IsSceneOnly, gpuTrace.Timeline.RegexNodeFilter, selectedRenderTime);
				chartsPanel.AreaCharts.Subtract(ChartsPanel.GpuChartIndex, selectedRenderTime);
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
