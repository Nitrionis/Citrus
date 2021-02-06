// ReSharper disable CompareOfFloatsByEqualityOperator

#if PROFILER

using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	internal class TimelineWidget : Widget
	{
		public const float DefaultItemHeight = 20;
		public const float DefaultItemMargin = 2;

		protected const float ScaleScrollingSpeed = 1f / 1200f;
		protected const float MinMicrosecondsPerPixel = 0.2f;
		protected const float MaxMicrosecondsPerPixel = 64f;
		
		protected readonly TimelineRuler ruler;
		protected readonly Widget contentContainer;
		protected readonly ThemedScrollView horizontalScrollView;
		protected readonly ThemedScrollView verticalScrollView;

		/// <summary>
		/// Duration of a time interval in microseconds in which all timeline content can be placed.
		/// </summary>
		protected float ContentDuration { get; set; }
		
		protected float MicrosecondsPerPixel { get; private set; }

		protected TimelineWidget()
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
				Id = "Profiler timeline content"
			};
			horizontalScrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
				Anchors = Anchors.LeftRightTopBottom,
				HitTestTarget = true,
				HitTestMethod = HitTestMethod.BoundingRect
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
		}
		
		protected TimePeriod CalculateVisibleTimePeriod()
		{
			float scrollPosition = horizontalScrollView.ScrollPosition;
			return new TimePeriod {
				StartTime = Math.Max(0, scrollPosition - ruler.SmallStepSize) / MicrosecondsPerPixel,
				FinishTime = (scrollPosition + horizontalScrollView.Width) / MicrosecondsPerPixel
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
					MicrosecondsPerPixel += Input.WheelScrollAmount / ScaleScrollingSpeed;
					MicrosecondsPerPixel = Mathf.Clamp(
						MicrosecondsPerPixel, MinMicrosecondsPerPixel, MaxMicrosecondsPerPixel);
					ruler.RulerScale = MicrosecondsPerPixel;
					
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
				yield return null;
			}
		}

		private IEnumerator<object> VerticalScrollTask()
		{
			while (true) {
				bool isVerticalMode = Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Control);
				verticalScrollView.Behaviour.CanScroll = isVerticalMode;
				horizontalScrollView.Behaviour.StopScrolling();
				yield return null;
			}
		}
	}
}

#endif // PROFILER