using System;
using System.Collections;
using System.Text.RegularExpressions;
using Lime;
using Lime.Graphics.Platform;
using Lime.Profilers;
using UsageReasons = Lime.Graphics.Platform.CpuUsage.UsageReasons;

namespace Tangerine.UI.Timeline
{
	/// <summary>
	/// Shows the application’s CPU-side activity.
	/// </summary>
	internal class CpuUsageTimeline : TimelineContainer<CpuUsageTimeline.CpuUsageRect>
	{
		private CpuHistory.Item lastUpdate;

		public Action<CpuUsage> CpuUsageSelected;

		public CpuUsageTimeline()
		{
			Id = "CPU Timeline";
			ruler.Id = "CPU TimelineRuler";
			container.Id = "CPU Timeline Container";
			verticalScrollView.Id = "CPU Timeline VerticalScrollView";
			horizontalScrollView.Id = "CPU Timeline HorizontalScrollView";
			TimePeriodSelected = arg => CpuUsageSelected?.Invoke(arg.CpuUsage);
		}

		public void Rebuild(CpuHistory.Item update)
		{
			lastUpdate = update;
			ResetContainer();
			update.NodesResults.Sort(0, update.NodesResults.Count, new TimePeriodComparer<CpuUsage>());
			mesh.Vertices = new TimelineMaterial.Vertex[update.NodesResults.Count * Rectangle.VertexCount];
			mesh.Indices = new ushort[update.NodesResults.Count * Rectangle.IndexCount];
			items.Clear();
			for (int i = 0; i < update.NodesResults.Count; i++) {
				items.Add(new CpuUsageRect(this, i, update.NodesResults[i]));
			}
			UpdateHistorySize();
			RebuildVertexBuffer();
			RecheckItemVisibility();
		}

		protected override float CalculateHistoryWidth() =>
			lastUpdate == null ? 5000 :
			lastUpdate.DeltaTime * 1000 / MicrosecondsPerPixel;

		public static bool CheckTargetNode(Regex regex, CpuUsage cpuUsage)
		{
			if (regex == null || cpuUsage.Owners == null) {
				return true;
			}
			if (cpuUsage.Reasons.Include(UsageReasons.BatchRender)) {
				foreach (var item in (IList)cpuUsage.Owners) {
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
			} else {
				if (cpuUsage.Owners is Node node) {
					return node.Id != null && regex.IsMatch(node.Id);
				} else {
					return regex.IsMatch((string)cpuUsage.Owners);
				}
			}
			return false;
		}

		internal class CpuUsageRect : IItem
		{
			public const int RectsCount = 1;

			private readonly CpuUsageTimeline timeline;
			public readonly CpuUsage CpuUsage;

			private bool isSceneFilterPassed = true;
			private bool isContainsTargetNode = true;

			private int vertexBufferOffset;

			public ITimePeriod TimePeriod { get; private set; }
			public Vector2 Position { get; private set; }

			public int VertexCount { get; }
			public int IndexCount { get; }

			private Color4 originalColor;

			private static readonly Color4 unselectedColor    = ColorTheme.Current.Profiler.CpuUsageUnselected;
			private static readonly Color4 animationColor     = ColorTheme.Current.Profiler.CpuUsageAnimation;
			private static readonly Color4 updateColor        = ColorTheme.Current.Profiler.CpuUsageUpdate;
			private static readonly Color4 gestureColor       = ColorTheme.Current.Profiler.CpuUsageGesture;
			private static readonly Color4 preparationColor   = ColorTheme.Current.Profiler.CpuUsageRenderPreparation;
			private static readonly Color4 nodeRenderColor    = ColorTheme.Current.Profiler.CpuUsageNodeRender;
			private static readonly Color4 batchRenderColor   = ColorTheme.Current.Profiler.CpuUsageBatchRender;
			private static readonly Color4 ownerUnknownColor  = ColorTheme.Current.Profiler.CpuUsageOwnerUnknown;

			public CpuUsageRect(CpuUsageTimeline timeline, int selfIndex, CpuUsage cpuUsage)
			{
				this.timeline = timeline;
				vertexBufferOffset = selfIndex * RectsCount * Rectangle.VertexCount;
				CpuUsage = cpuUsage;
				TimePeriod = new TimePeriod(cpuUsage.Start, cpuUsage.Finish);
				VertexCount = RectsCount * Rectangle.VertexCount;
				IndexCount = RectsCount * Rectangle.IndexCount;
				originalColor = GetColorTheme(cpuUsage);
				UpdateMeshColorsSelfSegment();
			}

			public void TargetNodeChanged(Regex regex)
			{
				isContainsTargetNode = CheckTargetNode(regex, CpuUsage);
				UpdateMeshColorsSelfSegment();
			}

			public void SceneFilterChanged(bool value)
			{
				isSceneFilterPassed = !value || CpuUsage.IsPartOfScene;
				UpdateMeshColorsSelfSegment();
			}

			public void UpdateMeshPositionsSelfSegment()
			{
				uint length = Math.Max((uint)(2 * timeline.MicrosecondsPerPixel), CpuUsage.Finish - CpuUsage.Start);
				float width = Math.Max(1, (CpuUsage.Finish - CpuUsage.Start) / timeline.MicrosecondsPerPixel);
				TimePeriod.Finish = TimePeriod.Start + length;
				Position = timeline.AcquirePosition(TimePeriod);
				Rectangle.WriteVerticesTo(timeline.mesh, vertexBufferOffset, Position, new Vector2(width, ItemHeight));
			}

			private void UpdateMeshColorsSelfSegment()
			{
				var color = isSceneFilterPassed && isContainsTargetNode ? originalColor : unselectedColor;
				Rectangle.WriteColorsTo(timeline.mesh, vertexBufferOffset, color);
			}

			public void RebuildMeshIndicesSelfSegment(int dstOffset) =>
				Rectangle.WriteIndicesTo(timeline.mesh, dstOffset, vertexBufferOffset);

			private static Color4 GetColorTheme(CpuUsage cpuUsage)
			{
				if (cpuUsage.Owners != null) {
					Color4 GetBaseColor()
					{
						switch (cpuUsage.Reasons & UsageReasons.ReasonBits) {
							case UsageReasons.Animation:         return animationColor;
							case UsageReasons.Update:            return updateColor;
							case UsageReasons.Gesture:           return gestureColor;
							case UsageReasons.RenderPreparation: return preparationColor;
							case UsageReasons.NodeRender:        return nodeRenderColor;
							case UsageReasons.BatchRender:       return batchRenderColor;
							default: throw new NotImplementedException();
						}
					}
					var color = GetBaseColor();
					if (cpuUsage.Reasons.Include(UsageReasons.BatchRender)) {
						bool isOwnersSet = true;
						foreach (var item in (IList)cpuUsage.Owners) {
							isOwnersSet &= item != null;
						}
						if (!isOwnersSet) {
							color = ownerUnknownColor;
						}
					}
					return color;
				} else return ownerUnknownColor;
			}
		}
	}
}
