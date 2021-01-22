#if PROFILER

using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	internal class Timeline : Widget
	{
		private const float ScaleScrollingSpeed = 1f / 1200f;
		private const float MinMicrosecondsPerPixel = 0.2f;
		private const float MaxMicrosecondsPerPixel = 32f;
		
		private readonly TimelineRuler ruler;
		private readonly Widget contentContainer;
		private readonly ThemedScrollView horizontalScrollView;
		private readonly ThemedScrollView verticalScrollView;

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
		
		public Timeline()
		{
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
				//Presenter = contentPresenter
			};
			horizontalScrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect,
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
			horizontalScrollView.Content.Tasks.Insert(0, new Task(HorizontalScrollTask()));
			verticalScrollView.Content.Tasks.Insert(0, new Task(VerticalScrollTask()));
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
					isMeshRebuildRequired = true;
				}
				yield return null;
			}
		}
		
		protected struct TimelineState
		{
			/*/// <summary>
			/// todo it is not TimelineState
			/// 
			/// </summary>
			public List<TimePeriod> TimePeriods;
			
			/// <summary>
			/// todo it is not TimelineState
			/// todo rectangles already scaled
			/// List of all rectangles that the user will see if the timeline is large enough to show them all.
			/// </summary>
			public List<Rectangle> Rectangles;*/
			
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
	}
}

#endif // PROFILER