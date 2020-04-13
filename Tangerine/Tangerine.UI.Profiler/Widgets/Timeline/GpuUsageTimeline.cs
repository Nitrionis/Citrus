using System;
using System.Collections;
using System.Text.RegularExpressions;
using Lime;
using Lime.Profilers;
using GpuUsage = Lime.Graphics.Platform.GpuUsage;
using Frame = Lime.Graphics.Platform.GpuHistory.Item;

namespace Tangerine.UI.Timeline
{
	/// <summary>
	/// Shows the application’s GPU-side activity.
	/// </summary>
	internal class GpuUsageTimeline : TimelineContainer<GpuUsageTimeline.DrawCallRect>
	{
		private Frame lastFrame;

		public Action<GpuUsage> GpuUsageSelected;

		public GpuUsageTimeline()
		{
			Id = "GPU Timeline";
			ruler.Id = "GPU TimelineRuler";
			container.Id = "GPU Timeline Container";
			verticalScrollView.Id = "GPU Timeline VerticalScrollView";
			horizontalScrollView.Id = "GPU Timeline HorizontalScrollView";
			TimePeriodSelected = arg => GpuUsageSelected?.Invoke(arg.GpuUsage);
		}

		public void Rebuild(Frame frame)
		{
			lastFrame = frame;
			ResetContainer();
			int drawCallsCount = frame.IsSceneOnlyDeepProfiling ?
				frame.SceneDrawCallCount : frame.FullDrawCallCount;
			frame.DrawCalls.Sort(0, drawCallsCount, new TimePeriodComparer<GpuUsage>());
			mesh.Vertices = new TimelineMaterial.Vertex[frame.DrawCalls.Count * 2 * Rectangle.VertexCount];
			mesh.Indices = new ushort[frame.DrawCalls.Count * 2 * Rectangle.IndexCount];
			items.Clear();
			for (int i = 0; i < frame.DrawCalls.Count; i++) {
				items.Add(new DrawCallRect(this, i, frame.DrawCalls[i]));
			}
			UpdateHistorySize();
			RebuildVertexBuffer();
			RecheckItemVisibility();
		}

		protected override float CalculateHistoryWidth() =>
			lastFrame == null ? 5000 :
			(float)lastFrame.FullGpuRenderTime * 1000 / MicrosecondsPerPixel;

		public static bool CheckTargetNode(Regex regex, GpuUsage gpuUsage)
		{
			if (regex == null) {
				return true;
			}
			var pi = gpuUsage.GpuCallInfo;
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

		internal class DrawCallRect : IItem
		{
			public const int RectsCount = 2;

			private readonly GpuUsageTimeline timeline;
			public readonly GpuUsage GpuUsage;

			private class ColorPair
			{
				public Color4 First;
				public Color4 Second;
			}

			private bool isSceneFilterPassed = true;
			private bool isContainsTargetNode = true;

			private int vertexBufferOffset;

			public ITimePeriod TimePeriod { get; private set; }
			public Vector2 Position { get; private set; }

			public int VertexCount { get; }
			public int IndexCount { get; }

			private ColorPair originalColorPair;

			private static readonly ColorPair unknownColorPair = new ColorPair {
				First = ColorTheme.Current.Profiler.DrawCallUnknownOne,
				Second = ColorTheme.Current.Profiler.DrawCallUnknownTwo
			};
			private static readonly ColorPair sceneColorPair = new ColorPair {
				First = ColorTheme.Current.Profiler.DrawCallSceneOne,
				Second = ColorTheme.Current.Profiler.DrawCallSceneTwo
			};
			private static readonly ColorPair uiColorPair = new ColorPair {
				First = ColorTheme.Current.Profiler.DrawCallUiOne,
				Second = ColorTheme.Current.Profiler.DrawCallUiTwo
			};
			private static readonly ColorPair unselectedColorPair = new ColorPair {
				First = ColorTheme.Current.Profiler.DrawCallUnselectedOne,
				Second = ColorTheme.Current.Profiler.DrawCallUnselectedTwo
			};

			public DrawCallRect(GpuUsageTimeline timeline, int selfIndex, GpuUsage drawCall)
			{
				this.timeline = timeline;
				vertexBufferOffset = selfIndex * RectsCount * Rectangle.VertexCount;
				GpuUsage = drawCall;
				TimePeriod = new TimePeriod(drawCall.Start, drawCall.Finish);
				VertexCount = RectsCount * Rectangle.VertexCount;
				IndexCount = RectsCount * Rectangle.IndexCount;
				originalColorPair = GetColorTheme(drawCall);
				UpdateMeshColorsSelfSegment();
			}

			public void TargetNodeChanged(Regex regex)
			{
				isContainsTargetNode = CheckTargetNode(regex, GpuUsage);
				UpdateMeshColorsSelfSegment();
			}

			public void SceneFilterChanged(bool value)
			{
				isSceneFilterPassed = !value || GpuUsage.GpuCallInfo.IsPartOfScene;
				UpdateMeshColorsSelfSegment();
			}

			public void UpdateMeshPositionsSelfSegment()
			{
				var dc = GpuUsage;
				uint firstLength = Math.Max((uint)timeline.MicrosecondsPerPixel, dc.AllPreviousFinishTime - dc.StartTime);
				uint secondLength = Math.Max((uint)timeline.MicrosecondsPerPixel, dc.FinishTime - dc.AllPreviousFinishTime);
				TimePeriod.Finish = dc.Start + firstLength + secondLength;
				float firstWidth = Math.Max(1, firstLength / timeline.MicrosecondsPerPixel);
				float secondWidth = Math.Max(1, secondLength / timeline.MicrosecondsPerPixel);
				Position = timeline.AcquirePosition(TimePeriod);
				var secondPosition = new Vector2(Position.X + firstWidth, Position.Y);
				Rectangle.WriteVerticesTo(
					timeline.mesh, vertexBufferOffset,
					Position, new Vector2(firstWidth, ItemHeight));
				Rectangle.WriteVerticesTo(
					timeline.mesh, vertexBufferOffset + Rectangle.VertexCount,
					secondPosition, new Vector2(secondWidth, ItemHeight));
			}

			private void UpdateMeshColorsSelfSegment()
			{
				var colorPair = isSceneFilterPassed && isContainsTargetNode ? originalColorPair : unselectedColorPair;
				Rectangle.WriteColorsTo(timeline.mesh, vertexBufferOffset, colorPair.First);
				Rectangle.WriteColorsTo(timeline.mesh, vertexBufferOffset + Rectangle.VertexCount, colorPair.Second);
			}

			public void RebuildMeshIndicesSelfSegment(int dstOffset)
			{
				int startVertex = vertexBufferOffset;
				Rectangle.WriteIndicesTo(timeline.mesh, dstOffset, startVertex);
				Rectangle.WriteIndicesTo(timeline.mesh, dstOffset + Rectangle.IndexCount, startVertex + Rectangle.VertexCount);
			}

			private static ColorPair GetColorTheme(GpuUsage drawCall)
			{
				var colorPair = unknownColorPair;
				var pi = drawCall.GpuCallInfo;
				if (pi.Owners != null) {
					if (pi.IsPartOfScene) {
						colorPair = sceneColorPair;
					} else {
						colorPair = uiColorPair;
					}
					if (pi.Owners is IList list) {
						bool isOwnersSet = true;
						foreach (var item in list) {
							isOwnersSet &= item != null;
						}
						if (!isOwnersSet) {
							colorPair = unknownColorPair;
						}
					}
				}
				return colorPair;
			}
		}
	}
}
