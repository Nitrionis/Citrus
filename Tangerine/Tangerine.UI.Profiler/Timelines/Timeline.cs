#if PROFILER

using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal abstract class Timeline<TUsage, TLabel> : Widget 
		where TUsage : struct
		where TLabel : struct, ITimelineItemLabel
	{
		private const float ScaleScrollingSpeed = 1f / 1200f;
		private const float MinMicrosecondsPerPixel = 0.2f;
		private const float MaxMicrosecondsPerPixel = 64f;

		private readonly TimelineContent<TUsage, TLabel> timelineContent;
		private readonly TimelineLabels<TLabel> timelineLabels;
		private readonly TimelineMesh timelineMesh;
		private readonly TimelineHitTest timelineHitTest;

		protected readonly Queue<Task> contentModificationTasks;
		protected readonly Queue<Task> allTasks;
		
		private readonly TimelineRuler ruler;
		private readonly Widget contentContainer;
		private readonly ThemedScrollView horizontalScrollView;
		private readonly ThemedScrollView verticalScrollView;

		private TimelineState timelineState;
		private Vector2 cachedContainerSize;
		private float cachedHorizontalScrollPosition;
		private float cachedVerticalScrollPosition;
		private float cachedMicrosecondsPerPixel;
		private bool isMeshRebuildRequired;
		
		private float itemHeight = 20;

		public float ItemHeight
		{
			get { return itemHeight; }
			set {
				if (itemHeight != value) {
					itemHeight = value;
					isMeshRebuildRequired = true;
				}
			}
		}

		private float itemMargin = 2;

		public float ItemMargin
		{
			get { return itemMargin; }
			set {
				if (itemMargin != value) {
					itemMargin = value;
					isMeshRebuildRequired = true;
				}
			}
		}
		
		protected Timeline(TimelineContent<TUsage, TLabel> timelineContent, TimelineLabels<TLabel> timelineLabels)
		{
			contentModificationTasks = new Queue<Task>();
			allTasks = new Queue<Task>();
			
			// todo init timelineState
			
			this.timelineContent = timelineContent;
			this.timelineLabels = timelineLabels;
			timelineMesh = new TimelineMesh();
			timelineHitTest = new TimelineHitTest();
			
			Layout = new VBoxLayout();
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineTasksBackground);
			ruler = new TimelineRuler(smallStepSize: 10, smallStepsPerBig: 10) {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 32,
				RulerOffset = 0,
				TextColor = ColorTheme.Current.Profiler.TimelineRulerText,
				TimestampsColor = ColorTheme.Current.Profiler.TimelineRulerStep
			};
			AddNode(ruler);
			contentContainer = new Widget {
				Id = "Profiler timeline content",
				// todo Presenter = contentPresenter
			};
			horizontalScrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect,
				// todo run async hit test
				Clicked = () => throw new NotImplementedException()
			};
			horizontalScrollView.Content.Layout = new HBoxLayout();
			verticalScrollView = new ThemedScrollView(ScrollDirection.Vertical) {
				Anchors = Anchors.LeftRightTopBottom
			};
			verticalScrollView.Content.Layout = new VBoxLayout();
			horizontalScrollView.Content.AddNode(verticalScrollView);
			verticalScrollView.Content.AddNode(contentContainer);
			AddNode(horizontalScrollView);
			horizontalScrollView.Content.Tasks.Insert(0, new Lime.Task(HorizontalScrollTask()));
			verticalScrollView.Content.Tasks.Insert(0, new Lime.Task(VerticalScrollTask()));
			Tasks.Add(ScaleScrollTask);
			Updated += (delta) => {
				TimePeriod CalculateVisibleTimePeriod() {
					float scrollPosition = horizontalScrollView.ScrollPosition;
					return new TimePeriod {
						StartTime = Math.Max(0, scrollPosition - ruler.SmallStepSize) * cachedMicrosecondsPerPixel,
						FinishTime = (scrollPosition + horizontalScrollView.Width) * cachedMicrosecondsPerPixel
					};
				}
				isMeshRebuildRequired |= cachedContainerSize != Size;
			};
		}

		protected abstract TimelineContent<TUsage, TLabel> CreateTimelineContent();
		
		protected abstract TimelineLabels<TLabel> CreateTimelineLabels();
		
		private IEnumerator<object> ScaleScrollTask()
		{
			while (true) {
				if (
					Input.IsKeyPressed(Key.Control) &&
					(Input.WasKeyPressed(Key.MouseWheelDown) || Input.WasKeyPressed(Key.MouseWheelUp))
					)
				{
					float microsecondsPerPixel = cachedMicrosecondsPerPixel;
					microsecondsPerPixel += Input.WheelScrollAmount / ScaleScrollingSpeed;
					microsecondsPerPixel = Mathf.Clamp(
						microsecondsPerPixel, MinMicrosecondsPerPixel, MaxMicrosecondsPerPixel);
					ruler.RulerScale = microsecondsPerPixel;
					if (cachedMicrosecondsPerPixel != microsecondsPerPixel) {
						cachedMicrosecondsPerPixel = microsecondsPerPixel;
						isMeshRebuildRequired = true;
						// todo mesh rebuild
						// todo matrix rebuild
						// todo content rebuild

						var waitingTask = Task.
						var scaleTask = timelineContent.SetSpacingParametersAsync();
						contentModificationTasks.Enqueue(scaleTask);
					}
				}
				yield return null;
			}
		}

		private IEnumerator<object> HorizontalScrollTask()
		{
			while (true) {
				bool isHorizontalMode = !Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Control);
				horizontalScrollView.Behaviour.CanScroll = isHorizontalMode;
				verticalScrollView.Behaviour.StopScrolling();
				ruler.RulerOffset = horizontalScrollView.ScrollPosition;
				if (cachedHorizontalScrollPosition != horizontalScrollView.ScrollPosition) {
					cachedHorizontalScrollPosition = horizontalScrollView.ScrollPosition;
					isMeshRebuildRequired = true;
					// todo mesh rebuild
				}
				yield return null;
			}
		}

		private IEnumerator<object> VerticalScrollTask()
		{
			while (true) {
				bool isVerticalMode = Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Control);
				verticalScrollView.Behaviour.CanScroll = isVerticalMode;
				horizontalScrollView.Behaviour.StopScrolling();
				if (cachedVerticalScrollPosition != horizontalScrollView.ScrollPosition) {
					cachedVerticalScrollPosition = horizontalScrollView.ScrollPosition;
					// todo ? isMeshRebuildRequired = true;
					// todo update matrix?
				}
				yield return null;
			}
		}
		
		protected struct TimelineState
		{
			/// <summary>
			/// Defines the time interval visible by users.
			/// </summary>
			/// <remarks>
			/// Values are measured in microseconds where 0 corresponds to the beginning of the frame.
			/// </remarks>
			public TimePeriod VisibleTimePeriod;
			
			/// <summary>
			/// Defines the scale of the timeline.
			/// </summary>
			public float MicrosecondsPerPixel;

			/// <summary>
			/// Not scalable vertical distance in pixels between time intervals.
			/// </summary>
			public float TimeIntervalVerticalMargin;
			
			/// <summary>
			/// Not scalable height in pixels of one time interval.
			/// </summary>
			public float TimeIntervalHeight;
			
			/// <summary>
			/// Mouse position relative to widget with time intervals.
			/// </summary>
			public Vector2 LocalMousePosition;

			public PeriodPositions.SpacingParameters SpacingParameters => 
				new PeriodPositions.SpacingParameters {
					MicrosecondsPerPixel = MicrosecondsPerPixel,
					TimePeriodVerticalMargin = TimeIntervalVerticalMargin,
					TimePeriodHeight = TimeIntervalHeight
				};
		}

		private class TimelinePresenter : IPresenter
		{
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var ro = RenderObjectPool<RenderObject>.Acquire();
				// todo ro.CaptureRenderState();
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public override void Render()
				{
					Renderer.Flush();
					PrepareRenderState();
					// todo render mesh
					// todo render labels
				}
			}
		}
	}
}

#endif // PROFILER