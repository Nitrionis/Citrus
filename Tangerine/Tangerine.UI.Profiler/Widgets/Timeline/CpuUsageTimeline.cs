using System;
using System.Text.RegularExpressions;
using Lime;
using Lime.Graphics.Platform;
using Lime.Profilers;

namespace Tangerine.UI.Timeline
{
	/// <summary>
	/// Shows the application’s CPU-side activity.
	/// </summary>
	internal class CpuUsageTimeline : TimelineContainer
	{
		private CpuHistory.Item lastUpdate;

		private bool isSceneOnly;

		public bool IsSceneOnly
		{
			get { return isSceneOnly; }
			set {
				isSceneOnly = value;
				foreach (var node in container.Nodes) {
					((CpuUsageWidget)node).SceneFilterChanged(value);
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
					((CpuUsageWidget)node).TargetNodeChanged(value);
				}
			}
		}

		public Action<CpuUsage> CpuUsageSelected;

		public CpuUsageTimeline()
		{
			Id = "CPU Timeline";
			container.Id = "CPU Timeline Container";
			verticalScrollView.Id = "CPU Timeline VerticalScrollView";
			horizontalScrollView.Id = "CPU Timeline HorizontalScrollView";
			ruler.Id = "CPU TimelineRuler";
		}

		public void Rebuild(CpuHistory.Item update)
		{
			lastUpdate = update;
			ResetContainer();
			update.NodesResults.Sort(0, update.NodesResults.Count, new TimePeriodComparer<CpuUsage>());
			//container.AddNode(new CpuUsageWidget(
			//	new CpuUsage() {
			//		Start = 0,
			//		Finish = 1000,
			//		Owner = null,
			//		Reasons = CpuUsage.UsageReasons.Update
			//	},
			//	CpuUsageSelected));
			for (int i = 0; i < update.NodesResults.Count; i++) {
				var usage = update.NodesResults[i];
				if (!CheckTargetNode(CpuUsageWidget.SpecialIdRegex, usage)) {
					var widget = new CpuUsageWidget(usage, CpuUsageSelected);
					widget.Visible = IsItemVisible(widget);
					container.AddNode(widget);
				}
			}
			container.Width = CalculateHistoryWidth();
			UpdateItemsPositions();
		}

		protected override float CalculateHistoryWidth() =>
			lastUpdate == null ? 5000 :
			lastUpdate.DeltaTime * 1000 / MicrosecondsInPixel;

		protected override void UpdateItemTransform(Node widget)
		{
			var cpuUsageWidget = (CpuUsageWidget)widget;
			var usage = cpuUsageWidget.CpuUsage;
			uint length = usage.Finish - usage.Start;
			if (length < MicrosecondsInPixel) {
				length = (uint)MicrosecondsInPixel;
			}
			var period = new TimePeriod(usage.Start, usage.Start + length);
			cpuUsageWidget.Position = AcquirePosition(period);
			cpuUsageWidget.Width = Math.Max(1, length / MicrosecondsInPixel);
		}

		public static bool CheckTargetNode(Regex regex, CpuUsage cpuUsage)
		{
			if (regex == null) {
				return true;
			}
			if (cpuUsage.Owner is Node node) {
				return node.Id != null && regex.IsMatch(node.Id);
			} else if (cpuUsage.Owner is string id) {
				return regex.IsMatch(id);
			}
			return false;
		}

		private class CpuUsageWidget : Widget
		{
			public static readonly string SpecialIdentifier = ",._";
			public static readonly Regex SpecialIdRegex = new Regex(SpecialIdentifier, RegexOptions.Compiled);

			public readonly CpuUsage CpuUsage;

			private bool isSceneFilterPassed = true;
			private bool isContainsTargetNode = true;

			private IPresenter originalPresenter;

			private static readonly IPresenter unselectedPresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageUnselected);
			private static readonly IPresenter animationPresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageAnimation);
			private static readonly IPresenter updatePresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageUpdate);
			private static readonly IPresenter gesturePresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageGesture);
			private static readonly IPresenter preparationPresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageRenderPreparation);
			private static readonly IPresenter nodeRenderPresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageNodeRender);
			private static readonly IPresenter ownerUnknownPresenter =
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageOwnerUnknown);

			public CpuUsageWidget(CpuUsage cpuUsage, Action<CpuUsage> cpuUsageSelected)
			{
				CpuUsage = cpuUsage;
				Visible = false;
				HitTestTarget = true;
				Clicked += () => cpuUsageSelected?.Invoke(cpuUsage);
				Id = SpecialIdentifier;
				Height = TimelineContainer.ItemHeight;
				originalPresenter = GetColorTheme(cpuUsage);
				DecorateWidget();
			}

			public void TargetNodeChanged(Regex regex)
			{
				isContainsTargetNode = CheckTargetNode(regex, CpuUsage);
				DecorateWidget();
			}

			public void SceneFilterChanged(bool value)
			{
				isSceneFilterPassed = !value || CpuUsage.IsPartOfScene;
				DecorateWidget();
			}

			private void DecorateWidget() =>
				Presenter = isSceneFilterPassed && isContainsTargetNode ? originalPresenter : unselectedPresenter;

			private static IPresenter GetColorTheme(CpuUsage cpuUsage)
			{
				if (cpuUsage.Owner != null || (cpuUsage.Reasons & CpuUsage.UsageReasons.NoOwnerFlag) != 0) {
					switch (cpuUsage.Reasons) {
						case CpuUsage.UsageReasons.Animation:          return animationPresenter;
						case CpuUsage.UsageReasons.Update:             return updatePresenter;
						case CpuUsage.UsageReasons.Gesture:            return gesturePresenter;
						case CpuUsage.UsageReasons.RenderPreparation:  return preparationPresenter;
						case CpuUsage.UsageReasons.NodeRender:         return nodeRenderPresenter;
						case CpuUsage.UsageReasons.BatchRender:        return nodeRenderPresenter;
						default: throw new NotImplementedException();
					}
				} else {
					return ownerUnknownPresenter;
				}
			}
		}
	}
}
