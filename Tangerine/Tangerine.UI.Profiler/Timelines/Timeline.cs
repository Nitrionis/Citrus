#if PROFILER

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lime;
using Tangerine.UI.Charts;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	using SpacingParameters = PeriodPositions.SpacingParameters;

	internal class Timeline<TUsage, TLabel> : TimelineWidget
		where TUsage : struct
		where TLabel : struct, ITimelineItemLabel
	{
		private readonly TimelineFramePreloader preloader;
		private readonly TimelineContent<TUsage, TLabel> timelineContent;
		private readonly TimelineLabels<TLabel> timelineLabels;
		private readonly TimelineMesh timelineMesh;
		private readonly TimelineHitTest timelineHitTest;

		private readonly Queue<Action<Task>> contentReadingProtoTasks;
		private readonly Queue<Task> runningContentReadingTasks;
		private readonly Queue<Task> contentRebuildingTasks;

		private TimelineState timelineState;
		private long activeFrameIdentifier;
		private bool isContentRebuildingRequired;
		
		public readonly TimelineContent.Filter<TUsage> DefaultFilter;
		
		private TimelineContent.Filter<TUsage> filter;

		public TimelineContent.Filter<TUsage> Filter
		{
			get { return filter; }
			set {
				filter = value;
				isContentRebuildingRequired = true;
			}
		}

		private float itemHeight = DefaultItemHeight;

		public float ItemHeight
		{
			get { return itemHeight; }
			set {
				if (itemHeight != value) {
					itemHeight = value;
					isContentRebuildingRequired = true;
				}
			}
		}

		private float itemMargin = DefaultItemMargin;

		public float ItemMargin
		{
			get { return itemMargin; }
			set {
				if (itemMargin != value) {
					itemMargin = value;
					isContentRebuildingRequired = true;
				}
			}
		}

		public Timeline(
			TimelineContent<TUsage, TLabel> timelineContent,
			TimelineLabels<TLabel> timelineLabels)
		{
			preloader = new TimelineFramePreloader();
			this.timelineContent = timelineContent;
			this.timelineLabels = timelineLabels;
			timelineMesh = new TimelineMesh();
			timelineHitTest = new TimelineHitTest();

			contentReadingProtoTasks = new Queue<Action<Task>>();
			runningContentReadingTasks = new Queue<Task>();
			contentRebuildingTasks = new Queue<Task>();

			DefaultFilter = (usage, pool, clipboard) => true;
			filter = DefaultFilter;

			contentContainer.Presenter = new TimelinePresenter();
			horizontalScrollView.Content.Clicked += OnContentClicked;
			Updated += delta => OnUpdated();
		}

		public void SetFrame(long frameIdentifier) => preloader.Load(frameIdentifier);

		private void SetColors(Color4[] colors)
		{
			var vectors = ((TimelinePresenter)contentContainer.Presenter).RectangleMaterial.Colors;
			for (int i = 0; i < colors.Length; i++) {
				vectors[i] = colors[i].ToVector4();
			}
		}

		private void OnContentClicked()
		{
			if (
				preloader.IsAttemptCompleted &&
				contentRebuildingTasks.Count == 0 &&
				timelineContent.NewestContentModificationTask.IsCompleted
				) 
			{
				var mp = horizontalScrollView.Content.LocalMousePosition();
				contentRebuildingTasks.Enqueue(timelineHitTest.RunAsyncHitTest(
					mousePosition: new TimelineHitTest.ClickPoint {
						Timestamp = mp.X * timelineState.MicrosecondsPerPixel * timelineState.RelativeScale,
						VerticalPosition = mp.Y
					},
					items: timelineContent.GetHitTestTargets()));
			}
		}

		private void OnUpdated()
		{
			TimePeriod CalculateVisibleTimePeriod() {
				float scrollPosition = horizontalScrollView.ScrollPosition;
				return new TimePeriod {
					StartTime = Math.Max(0, scrollPosition - ruler.SmallStepSize) / MicrosecondsPerPixel,
					FinishTime = (scrollPosition + horizontalScrollView.Width) / MicrosecondsPerPixel
				};
			}

			if (preloader.IsAttemptCompleted) {
				if (activeFrameIdentifier != preloader.Frame.Identifier) {
					activeFrameIdentifier = preloader.Frame.Identifier;
					contentRebuildingTasks.Enqueue(timelineContent.RebuildAsync(
						preloader.Frame.Identifier,
						Task.WhenAll(runningContentReadingTasks),
						Filter));
					runningContentReadingTasks.Clear();
					contentReadingProtoTasks.Clear();
					isFilterChanged = false;
				} else {
					if (timelineState.MicrosecondsPerPixel != MicrosecondsPerPixel) {
						timelineState.MicrosecondsPerPixel = MicrosecondsPerPixel;
						contentRebuildingTasks.Enqueue(timelineContent.SetSpacingParametersAsync(
							Task.WhenAll(runningContentReadingTasks),
							timelineState.SpacingParameters));
						runningContentReadingTasks.Clear();
					}

					if (isFilterChanged) {
						contentRebuildingTasks.Enqueue(timelineContent.RebuildAsync());
						isFilterChanged = false;
					}

					if (
						contentRebuildingTasks.Count == 0 &&
						timelineContent.NewestContentModificationTask.IsCompleted
						) {
					}
				}
			}

			if (
				preloader.IsAttemptCompleted &&
				contentRebuildingTasks.Count == 0 &&
				timelineContent.NewestContentModificationTask.IsCompleted
				) {
			}

			/*var visibleTimePeriod = CalculateVisibleTimePeriod();
			timelineState = new TimelineState {
				VisibleTimePeriod = visibleTimePeriod,
				MicrosecondsPerPixel = ,
				RelativeScale = ,
				TimeIntervalHeight = ItemHeight,
				TimeIntervalVerticalMargin = ItemMargin
			};
			contentContainer.Width = ;*/

			isContentRebuildingRequired = false;
		}

		private class TimelinePresenter : IPresenter
		{
			public readonly ChartsMaterial RectangleMaterial = new ChartsMaterial();

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var timeline = (Timeline<TUsage, TLabel>) node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(node.AsWidget);
				ro.RectangleMaterial = RectangleMaterial;
				var timelineState = timeline.timelineState;
				ro.RelativeScale = timelineState.RelativeScale;
				ro.MeshRenderObject = timeline.timelineMesh.GetRenderObject();
				ro.LabelsRenderObject = timeline.timelineLabels.GetRenderObject(
					new TimePeriod {
						StartTime = timelineState.VisibleTimePeriod.StartTime * timelineState.RelativeScale,
						FinishTime = timelineState.VisibleTimePeriod.FinishTime * timelineState.RelativeScale
					},
					1f / timelineState.MicrosecondsPerPixel * timelineState.RelativeScale);
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public float RelativeScale;
				public ChartsMaterial RectangleMaterial;
				public TimelineMesh.RenderObject MeshRenderObject;
				public TimelineLabels<TLabel>.RenderObject LabelsRenderObject;

				public override void Render()
				{
					Renderer.Flush();
					PrepareRenderState();
					RectangleMaterial.Matrix =
						Renderer.FixupWVP(
							Matrix44.CreateScale(RelativeScale, 1f, 1f) *
							(Matrix44) LocalToWorldTransform *
							Renderer.ViewProjection);
					RectangleMaterial.Apply(0);
					MeshRenderObject.Render();
					LabelsRenderObject.Render();
				}
			}
		}

		private struct HitTestProtoTask
		{
			public TimelineHitTest.ClickPoint MousePosition;
			public IEnumerable<TimelineHitTest.ItemInfo> Items;
		}
	}
}

#endif // PROFILER
