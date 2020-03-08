using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lime;
using Lime.Profilers;
using DrawCallInfo = Lime.Graphics.Platform.ProfilingResult;
using Frame = Lime.Graphics.Platform.ProfilerHistory.Item;

namespace Tangerine.UI.Timeline
{
	/// <summary>
	/// Shows the application’s GPU-side activity.
	/// </summary>
	internal class DrawCallsTimeline : TimelineContainer
	{
		private Frame lastFrame;

		private bool isSceneOnly;

		public bool IsSceneOnly
		{
			get { return isSceneOnly; }
			set {
				isSceneOnly = value;
				foreach (var node in container.Nodes) {
					((DrawCallWidget)node).SceneFilterChanged(value);
				}
			}
		}

		private Regex regexNodeFilter;

		public Regex RegexNodeFilter
		{
			get { return regexNodeFilter; }
			set {
				regexNodeFilter = value;
				foreach (var node in container.Nodes) {
					((DrawCallWidget)node).TargetNodeChanged(value);
				}
			}
		}

		public Action<DrawCallInfo> DrawCallSelected;

		public DrawCallsTimeline() { }

		public void Rebuild(Frame frame)
		{
			lastFrame = frame;
			ResetContainer();
			frame.DrawCalls.Sort(0, frame.FullDrawCallCount, new TimePeriodComparer<DrawCallInfo>());
			for (int i = 0; i < frame.FullDrawCallCount; i++) {
				Widget widget = new DrawCallWidget(frame.DrawCalls[i], DrawCallSelected);
				widget.Visible = IsItemVisible(widget);
				container.AddNode(widget);
			}
			float historyHeight = freeSpaceOfLines.Count * (ItemHeight + ItemTopMargin) + ItemTopMargin;
			UpdateHistorySize(new Vector2(CalculateHistoryWidth(), historyHeight));
		}

		protected override float CalculateHistoryWidth() =>
			(float)lastFrame.FullGpuRenderTime * 1000 / MicrosecondsInPixel;

		protected override void UpdateItemTransform(Node widget)
		{
			var drawCallWidget = (DrawCallWidget)widget;
			var dc = drawCallWidget.DrawCallInfo;

			uint firstLength = dc.AllPreviousFinishTime - dc.StartTime;
			uint secondLength = dc.FinishTime - dc.AllPreviousFinishTime;

			if (firstLength < MicrosecondsInPixel && firstLength != 0) {
				firstLength = (uint)MicrosecondsInPixel;
			}
			if (secondLength < MicrosecondsInPixel && secondLength != 0) {
				secondLength = (uint)MicrosecondsInPixel;
			}
			uint finalLength = firstLength + secondLength;
			var period = finalLength != (dc.Finish - dc.Start) ?
				new TimePeriod(dc.Start, dc.Start + finalLength) : (ITimePeriod)dc;
			var pos = AcquirePosition(period);

			drawCallWidget.Position = pos;
			drawCallWidget.Width = finalLength / MicrosecondsInPixel;

			var p1 = widget.Nodes[0] as Widget;
			p1.Position = Vector2.Zero;
			p1.Width = firstLength / MicrosecondsInPixel;

			var p2 = widget.Nodes[1] as Widget;
			p2.Position = new Vector2(firstLength / MicrosecondsInPixel, 0);
			p2.Width = secondLength / MicrosecondsInPixel;
		}

		public static bool CheckTargetNode(Regex regex, DrawCallInfo drawCallInfo)
		{
			if (regex == null) {
				return true;
			}
			var pi = drawCallInfo.ProfilingInfo;
			if (pi.Owners is List<object> list) {
				foreach (var item in list) {
					if (item != null) {
						if (item is Node node && node.Id != null && regex.IsMatch(node.Id)) {
							return true;
						} else {
							if (regex.IsMatch((string)item)) {
								return true;
							}
						}
					}
				}
			} else if (pi.Owners != null) {
				if (pi.Owners is Node node && node.Id != null && regex.IsMatch(node.Id)) {
					return true;
				} else {
					if (regex.IsMatch((string)pi.Owners)) {
						return true;
					}
				}
			}
			return false;
		}

		private class DrawCallWidget : Widget
		{
			public readonly DrawCallInfo DrawCallInfo;

			private enum ColorPair
			{
				Unknown = 0,
				Scene = 1,
				UI = 2,
				Unselected = 3
			}

			private class PresenterPair
			{
				public IPresenter First;
				public IPresenter Second;
			}

			private PresenterPair originalPresenterPair;

			private static PresenterPair[] presenters = new PresenterPair[] {
				new PresenterPair { // Unknown
					First = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallUnknownOne),
					Second = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallUnknownTwo)
				},
				new PresenterPair { // Scene
					First = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallSceneOne),
					Second = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallSceneTwo)
				},
				new PresenterPair { // UI
					First = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallUiOne),
					Second = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallUiTwo)
				},
				new PresenterPair { // Unselected
					First = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallUnselectedOne),
					Second = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.DrawCallUnselectedTwo)
				}
			};

			private bool isSceneFilterPassed;
			private bool isContainsTargetNode;

			public DrawCallWidget(DrawCallInfo drawCall, Action<DrawCallInfo> drawCallSelected)
			{
				DrawCallInfo = drawCall;
				Height = TimelineContainer.ItemHeight;
				Visible = false;
				HitTestTarget = true;
				Id = "DrawCallWidget main";
				Clicked += () => drawCallSelected?.Invoke(DrawCallInfo);
				AddNode(new Widget {
					Height = TimelineContainer.ItemHeight,
					HitTestTarget = false,
					Id = "DrawCallWidget first"
				});
				AddNode(new Widget {
					Height = TimelineContainer.ItemHeight,
					HitTestTarget = false,
					Id = "DrawCallWidget second"
				});
				originalPresenterPair = GetColorTheme(drawCall);
				TargetNodeChanged(regex: null);
			}

			public void TargetNodeChanged(Regex regex)
			{
				isContainsTargetNode = CheckTargetNode(regex, DrawCallInfo);
				DecorateWidget();
			}

			public void SceneFilterChanged(bool value)
			{
				var pi = DrawCallInfo.ProfilingInfo;
				isSceneFilterPassed = !value || pi.Owners == null || pi.IsPartOfScene;
				DecorateWidget();
			}

			private void DecorateWidget()
			{
				var presenterPair = isSceneFilterPassed && isContainsTargetNode ?
					originalPresenterPair : presenters[(int)ColorPair.Unselected];
				Nodes[0].Presenter = presenterPair.First;
				Nodes[1].Presenter = presenterPair.Second;
			}

			private static PresenterPair GetColorTheme(DrawCallInfo drawCall)
			{
				var presenterPair = presenters[(int)ColorPair.Unknown];
				if (drawCall.ProfilingInfo.Owners != null) {
					var pi = drawCall.ProfilingInfo;
					if (pi.IsPartOfScene) {
						presenterPair = presenters[(int)ColorPair.Scene];
					} else {
						presenterPair = presenters[(int)ColorPair.UI];
					}
					if (pi.Owners is List<object> list) {
						bool isOwnersSet = true;
						foreach (var item in list) {
							isOwnersSet &= item != null;
						}
						if (!isOwnersSet) {
							presenterPair = presenters[(int)ColorPair.Unknown];
						}
					} else if (pi.Owners == null) {
						presenterPair = presenters[(int)ColorPair.Unknown];
					}
				}
				return presenterPair;
			}
		}
	}
}
