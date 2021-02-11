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
		
		private readonly Queue<Task> contentReadingTasks;
		private readonly Queue<Task> contentRebuildingTasks;
		
		private long activeFrameIdentifier;
		private bool isContentRebuildingRequired;
		private bool isContentChanged;
		private float contentMicrosecondsPerPixel;
		private float requestedMicrosecondsPerPixel;
		private float relativeScale;
		private TimePeriod timePeriod;
		
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
		
		private SpacingParameters SpacingParameters => 
			new SpacingParameters {
				MicrosecondsPerPixel = MicrosecondsPerPixel,
				TimePeriodHeight = ItemHeight,
				TimePeriodVerticalMargin = ItemMargin
			};

		private bool AreContentRebuildingTasksCompleted =>
			preloader.IsAttemptCompleted &&
			contentRebuildingTasks.Count == 0 &&
			timelineContent.NewestContentModificationTask.IsCompleted;
		
		private readonly Queue<Func<Task, Task>> externalContentRebuildingRequests;
		
		public Timeline(
			TimelineContent<TUsage, TLabel> timelineContent,
			TimelineLabels<TLabel> timelineLabels)
		{
			preloader = new TimelineFramePreloader();
			this.timelineContent = timelineContent;
			this.timelineLabels = timelineLabels;
			timelineMesh = new TimelineMesh();
			timelineHitTest = new TimelineHitTest();

			contentReadingTasks = new Queue<Task>();
			contentRebuildingTasks = new Queue<Task>();

			DefaultFilter = (usage, pool, clipboard) => true;
			filter = DefaultFilter;

			contentContainer.Presenter = new TimelinePresenter(this);
			horizontalScrollView.Content.Clicked += OnContentClicked;
			Updated += delta => OnUpdated();

			contentMicrosecondsPerPixel = 1f;
			requestedMicrosecondsPerPixel = 1f;
			relativeScale = 1f;

			externalContentRebuildingRequests = new Queue<Func<Task, Task>>();
			
			ResetScale(contentDuration: 1f);
		}

		public void SetFrame(long frameIdentifier) => preloader.Load(frameIdentifier);

		public void AddContentRebuildingRequest(Func<Task, Task> rebuildingRequest) =>
			externalContentRebuildingRequests.Enqueue(rebuildingRequest);
		
		private void SetColors(Color4[] colors)
		{
			var vectors = ((TimelinePresenter)contentContainer.Presenter).RectangleMaterial.Colors;
			for (int i = 0; i < colors.Length; i++) {
				vectors[i] = colors[i].ToVector4();
			}
		}

		private void OnContentClicked()
		{
			if (AreContentRebuildingTasksCompleted) {
				var mp = horizontalScrollView.Content.LocalMousePosition();
				contentReadingTasks.Enqueue(timelineHitTest.RunAsyncHitTest(
					mousePosition: new TimelineHitTest.ClickPoint {
						Timestamp = mp.X * MicrosecondsPerPixel,
						VerticalPosition = mp.Y
					},
					items: timelineContent.GetHitTestTargets()));
			}
		}

		private TimePeriod CalculateVisibleTimePeriod() 
		{
			float scrollPosition = horizontalScrollView.ScrollPosition - OriginalContentWidth / 3;
			return new TimePeriod {
				StartTime = scrollPosition * MicrosecondsPerPixel,
				FinishTime = (scrollPosition + OriginalContentWidth / 3) * MicrosecondsPerPixel
			};
		}
		
		private void OnUpdated()
		{
			relativeScale = MicrosecondsPerPixel / contentMicrosecondsPerPixel;
			if (preloader.IsAttemptCompleted) {
				var frame = preloader.Frame;
				if (activeFrameIdentifier != frame.Identifier) {
					activeFrameIdentifier = frame.Identifier;
					if (frame.StopwatchFrequency == 0) {
						ResetScale(contentDuration: 1);
						Debug.Write("Get frame failed");
					} else {
						ResetScale(contentDuration:
							(frame.RenderThreadStartTime +
							 frame.RenderBodyElapsedTicks -
							 frame.UpdateThreadStartTime) / (frame.StopwatchFrequency / 1_000_000));
					}
					var spacingParameters = SpacingParameters;
					requestedMicrosecondsPerPixel = spacingParameters.MicrosecondsPerPixel;
					contentRebuildingTasks.Enqueue(
						timelineContent.RebuildAsync(
							preloader.Frame.Identifier,
							Task.WhenAll(contentReadingTasks),
							Filter,
							spacingParameters));
					contentReadingTasks.Clear();
				} else {
					ProcessContentRebuildingResults();
					RunContentRebuildingTasks();
					if (AreContentRebuildingTasksCompleted) {
						RunContentReadingTasks();
					}
				}
			}
		}

		private void ProcessContentRebuildingResults()
		{
			float contentHeight = verticalScrollView.Content.Height;
			Color4[] changedColors = null;
			while (contentRebuildingTasks.Count > 0) {
				var task = contentRebuildingTasks.Peek();
				if (task.IsCompleted) {
					contentRebuildingTasks.Dequeue();
					switch (task) {
						case Task<ContentChanges<Color4[]>> t:
							if (!t.Result.IsTaskSkipped) {
								changedColors = t.Result.Value;
								contentMicrosecondsPerPixel = t.Result.SpacingParameters.MicrosecondsPerPixel;
								contentHeight = t.Result.ContentHeight;
							}
							break;
						case Task<ContentChanges<EmptyData>> t:
							if (!t.Result.IsTaskSkipped) {
								contentMicrosecondsPerPixel = t.Result.SpacingParameters.MicrosecondsPerPixel;
								contentHeight = t.Result.ContentHeight;
							}
							break;
					}
					isContentChanged = true;
				} else {
					break;
				}
			}
			relativeScale = MicrosecondsPerPixel / contentMicrosecondsPerPixel;
			verticalScrollView.Content.Height = contentHeight;
			if (changedColors != null) {
				SetColors(changedColors);
			}
		}

		public void RunContentRebuildingTasks()
		{
			if (Math.Abs(requestedMicrosecondsPerPixel - MicrosecondsPerPixel) > 1e-2) {
				var spacingParameters = SpacingParameters;
				requestedMicrosecondsPerPixel = spacingParameters.MicrosecondsPerPixel;
				contentRebuildingTasks.Enqueue(timelineContent.SetSpacingParametersAsync(
					Task.WhenAll(contentReadingTasks),
					spacingParameters));
				contentReadingTasks.Clear();
			}
			if (isContentRebuildingRequired) {
				isContentRebuildingRequired = false;
				var spacingParameters = SpacingParameters;
				requestedMicrosecondsPerPixel = spacingParameters.MicrosecondsPerPixel;
				contentRebuildingTasks.Enqueue(timelineContent.RebuildAsync(
					preloader.Frame.Identifier,
					Task.WhenAll(contentReadingTasks),
					Filter,
					spacingParameters));
				contentReadingTasks.Clear();
			}
			while (externalContentRebuildingRequests.Count > 0) {
				var request = externalContentRebuildingRequests.Dequeue();
				var waitingTask = Task.WhenAll(contentReadingTasks);
				contentRebuildingTasks.Enqueue(request(waitingTask));
				contentReadingTasks.Clear();
			}
		}

		private void RunContentReadingTasks()
		{
			var timePeriod = CalculateVisibleTimePeriod();
			bool isTimePeriodChanged =
				Math.Abs(timePeriod.StartTime - this.timePeriod.StartTime) > 1e-2 ||
				Math.Abs(timePeriod.FinishTime - this.timePeriod.FinishTime) > 1e-2;
			if (isContentChanged || isTimePeriodChanged) {
				this.timePeriod = timePeriod;
				contentReadingTasks.Enqueue(
					timelineMesh.RebuildAsync(
						timelineContent.GetRectangles(timePeriod)));
				contentReadingTasks.Enqueue(
					timelineLabels.RebuildAsync(
						timelineContent.GetVisibleLabels(timePeriod)));
			}
			while (contentReadingTasks.Count > 0 && contentReadingTasks.Peek().IsCompleted) {
				contentReadingTasks.Dequeue();
			}
			isContentChanged = false;
		}
		
		private class TimelinePresenter : IPresenter
		{
			public readonly ChartsMaterial RectangleMaterial = new ChartsMaterial();

			private readonly Timeline<TUsage, TLabel> timeline;

			public TimelinePresenter(Timeline<TUsage, TLabel> timeline) => this.timeline = timeline;
			
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.CaptureRenderState(node.AsWidget);
				ro.RectangleMaterial = RectangleMaterial;
				ro.RelativeScale = timeline.relativeScale;
				ro.MeshRenderObject = timeline.timelineMesh.GetRenderObject();
				ro.LabelsRenderObject = timeline.timelineLabels.GetRenderObject(
					timeline.CalculateVisibleTimePeriod(),
					1f / timeline.MicrosecondsPerPixel);

				// todo
				ro.MicrosecondsPerPixel = timeline.MicrosecondsPerPixel;
				ro.Width = timeline.ContentDuration / timeline.MicrosecondsPerPixel;

				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public float RelativeScale;
				public ChartsMaterial RectangleMaterial;
				public TimelineMesh.RenderObject MeshRenderObject;
				public TimelineLabels<TLabel>.RenderObject LabelsRenderObject;

				public float MicrosecondsPerPixel;
				public float Width;

				public override void Render()
				{
					Renderer.Flush();
					PrepareRenderState();

					Renderer.DrawRect(Vector2.Zero, 25 * Vector2.One, Color4.Red);
					Renderer.DrawRect(new Vector2(Width, 0), new Vector2(Width + 25, 25), Color4.Red);

					RectangleMaterial.Matrix =
						Renderer.FixupWVP(
							Matrix44.CreateScale(RelativeScale, 1f, 1f) *
							(Matrix44)LocalToWorldTransform *
							Renderer.ViewProjection);
					RectangleMaterial.Apply(0);
					MeshRenderObject.Render();
					LabelsRenderObject.Render();
				}
			}
		}
	}
}

#endif // PROFILER
