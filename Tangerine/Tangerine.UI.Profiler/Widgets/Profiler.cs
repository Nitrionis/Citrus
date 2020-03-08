using System;
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
			//LimeProfiler.ContextChanged += Reset;
			//LimeProfiler.LocalDeviceFrameRenderCompleted = OnLocalDeviceFrameRenderCompleted;
			//contentWidget.AddNode(this);
			//settingsWidget = CreateSettingsBlock();
			//mainControlPanel = new MainControlPanel(settingsWidget);
			//AddNode(mainControlPanel);
			//AddNode(settingsWidget);
			//chartsPanel = new ChartsPanel();
			//AddNode(chartsPanel);
			//gpuTrace = new GpuTrace {
			//	NodeFilteringChanged = () => { isNodeFilteringChanged = true; }
			//};
			//AddNode(gpuTrace);
			//selectedRenderTime = new float[GpuHistory.HistoryFramesCount];
			//Tasks.Add(StateUpdateTask);
		}

		private void Reset()
		{
			isNodeFilteringChanged = false;
			lastProfiledFrameIndex = LimeProfiler.GpuHistory.ProfiledFramesCount;
			lastProfiledUpdateIndex = LimeProfiler.CpuHistory.ProfiledUpdatesCount;
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
			while (lastProfiledFrameIndex < LimeProfiler.GpuHistory.ProfiledFramesCount - 1) {
				chartsPanel.FrameCompleted(
					frame: LimeProfiler.GpuHistory.GetFrame(++lastProfiledFrameIndex),
					update: LimeProfiler.CpuHistory.GetUpdate(++lastProfiledUpdateIndex));
			}
			if (isNodeFilteringChanged) {
				isNodeFilteringChanged = false;
				SelectRenderTime(gpuTrace.Timeline.IsSceneOnly, gpuTrace.Timeline.RegexNodeFilter, selectedRenderTime);
				chartsPanel.UpdateSelectedObjectsRenderTime(selectedRenderTime);
			}
		}

		private void SelectRenderTime(bool isSceneOnly, Regex regexNodeFilter, float[] resultsBuffer)
		{
			for (long i = 0; i < GpuHistory.HistoryFramesCount; i++) {
				long frameIndex = chartsPanel.FrameUpdateIndices[i].Frame;
				if (LimeProfiler.GpuHistory.IsFrameIndexValid(frameIndex)) {
					var frame = LimeProfiler.GpuHistory.GetFrame(frameIndex);
					float renderTimeOfSelected = 0f;
					foreach (var dc in frame.DrawCalls) {
						var pi = dc.ProfilingInfo;
						bool isContainsTargetNode = DrawCallsTimeline.CheckTargetNode(regexNodeFilter, dc);
						bool isSceneFilterPassed = !isSceneOnly || pi.Owners == null || pi.IsPartOfScene;
						bool isFilteringPassed = isContainsTargetNode && isSceneFilterPassed;
						renderTimeOfSelected += isFilteringPassed ? (dc.Finish - dc.Start) / 1000f : 0f;
					}
					resultsBuffer[i] = renderTimeOfSelected;
				}
			}
		}
	}
}
