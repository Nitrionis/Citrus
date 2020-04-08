using System;
using System.Collections;
using System.Text.RegularExpressions;
using Lime;
using DrawCallInfo = Lime.Graphics.Platform.GpuUsage;
using Frame = Lime.Graphics.Platform.GpuHistory.Item;

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

		public DrawCallsTimeline()
		{
			Id = "GPU Timeline";
			container.Id = "GPU Timeline Container";
			verticalScrollView.Id = "GPU Timeline VerticalScrollView";
			horizontalScrollView.Id = "GPU Timeline HorizontalScrollView";
			ruler.Id = "GPU TimelineRuler";
		}

		public void Rebuild(Frame frame)
		{
			lastFrame = frame;
			ResetContainer();
			int drawCallsCount = frame.IsSceneOnlyDeepProfiling ?
				frame.SceneDrawCallCount : frame.FullDrawCallCount;
			frame.DrawCalls.Sort(0, drawCallsCount, new TimePeriodComparer<DrawCallInfo>());
			for (int i = 0; i < drawCallsCount; i++) {
				var dc = frame.DrawCalls[i];
				if (!CheckTargetNode(DrawCallWidget.SpecialIdRegex, dc)) {
					var widget = new DrawCallWidget(dc, DrawCallSelected);
					widget.Visible = IsItemVisible(widget);
					container.AddNode(widget);
				}
			}
			container.Width = CalculateHistoryWidth();
			UpdateItemsPositions();
		}

		protected override float CalculateHistoryWidth() =>
			lastFrame == null ? 2000 :
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
			var period = new TimePeriod(dc.Start, dc.Start + finalLength);
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
			var pi = drawCallInfo.GpuCallInfo;
			if (pi.Owners is IList list) {
				foreach (var item in list) {
					if (item != null) {
						if (item is Node node) {
							if (node.Id != null && regex.IsMatch(node.Id)) {
								return true;
							}
						} else {
							if (regex.IsMatch((string)item)) {
								return true;
							}
						}
					}
				}
			} else if (pi.Owners != null) {
				if (pi.Owners is Node node) {
					return node.Id != null && regex.IsMatch(node.Id);
				} else {
					return regex.IsMatch((string)pi.Owners);
				}
			}
			return false;
		}

		private class DrawCallWidget : Widget
		{
			public static readonly string SpecialIdentifier = ",._";
			public static readonly Regex SpecialIdRegex = new Regex(SpecialIdentifier, RegexOptions.Compiled);

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

			private bool isSceneFilterPassed = true;
			private bool isContainsTargetNode = true;

			public DrawCallWidget(DrawCallInfo drawCall, Action<DrawCallInfo> drawCallSelected)
			{
				DrawCallInfo = drawCall;
				Height = TimelineContainer.ItemHeight;
				Visible = false;
				HitTestTarget = true;
				Id = SpecialIdentifier;
				Clicked += () => drawCallSelected?.Invoke(DrawCallInfo);
				AddNode(new Widget {
					Height = TimelineContainer.ItemHeight,
					HitTestTarget = false,
					Id = SpecialIdentifier
				});
				AddNode(new Widget {
					Height = TimelineContainer.ItemHeight,
					HitTestTarget = false,
					Id = SpecialIdentifier
				});
				originalPresenterPair = GetColorTheme(drawCall);
				DecorateWidget();
			}

			public void TargetNodeChanged(Regex regex)
			{
				isContainsTargetNode = CheckTargetNode(regex, DrawCallInfo);
				DecorateWidget();
			}

			public void SceneFilterChanged(bool value)
			{
				var pi = DrawCallInfo.GpuCallInfo;
				isSceneFilterPassed = !value || pi.IsPartOfScene;
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
				var pi = drawCall.GpuCallInfo;
				if (pi.Owners != null) {
					if (pi.IsPartOfScene) {
						presenterPair = presenters[(int)ColorPair.Scene];
					} else {
						presenterPair = presenters[(int)ColorPair.UI];
					}
					if (pi.Owners is IList list) {
						bool isOwnersSet = true;
						foreach (var item in list) {
							isOwnersSet &= item != null;
						}
						if (!isOwnersSet) {
							presenterPair = presenters[(int)ColorPair.Unknown];
						}
					}
				}
				return presenterPair;
			}
		}
	}
}
