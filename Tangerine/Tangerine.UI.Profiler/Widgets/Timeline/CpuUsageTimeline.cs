using System;
using System.Text.RegularExpressions;
using Lime;
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

		public CpuUsageTimeline() { }

		public void Rebuild(CpuHistory.Item update)
		{
			lastUpdate = update;
			ResetContainer();
			update.NodesResults.Sort(0, update.NodesResults.Count, new TimePeriodComparer<CpuUsage>());
			for (int i = 0; i < update.NodesResults.Count; i++) {
				var usage = update.NodesResults[i];
				if (usage.Finish - usage.Start > 0) {
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
			cpuUsageWidget.Width = length / MicrosecondsInPixel;
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
			public readonly CpuUsage CpuUsage;

			private bool isSceneFilterPassed = true;
			private bool isContainsTargetNode = true;

			private IPresenter originalPresenter;

			private static readonly IPresenter[] presenters = new IPresenter[] {
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageUnselected),
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageAnimation),
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageUpdate),
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageGesture),
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageRenderPreparation),
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageRender),
				new WidgetFlatFillHitTestPresenter(ColorTheme.Current.Profiler.CpuUsageOwnerUnknown)
			};

			public CpuUsageWidget(CpuUsage cpuUsage, Action<CpuUsage> cpuUsageSelected)
			{
				CpuUsage = cpuUsage;
				Visible = false;
				HitTestTarget = true;
				Clicked += () => cpuUsageSelected?.Invoke(cpuUsage);
				Id = "CpuUsageWidget";
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
				Presenter = isSceneFilterPassed && isContainsTargetNode ? originalPresenter : presenters[0];

			private static IPresenter GetColorTheme(CpuUsage cpuUsage)
			{
				if (cpuUsage.Owner != null) {
					switch (cpuUsage.Reason) {
						case CpuUsage.UsageReason.Animation:          return presenters[1];
						case CpuUsage.UsageReason.Update:             return presenters[2];
						case CpuUsage.UsageReason.Gesture:            return presenters[3];
						case CpuUsage.UsageReason.RenderPreparation:  return presenters[4];
						case CpuUsage.UsageReason.Render:             return presenters[5];
						default: throw new NotImplementedException();
					}
				} else {
					return presenters[6];
				}
			}
		}
	}
}
