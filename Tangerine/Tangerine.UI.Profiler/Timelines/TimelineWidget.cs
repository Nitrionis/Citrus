// ReSharper disable CompareOfFloatsByEqualityOperator

#if PROFILER

using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Timelines
{
	internal class TimelineWidget : Widget
	{
		protected const float ScaleScrollingSpeed = 1f / 1200f;
		protected const float MinMicrosecondsPerPixel = 0.2f;
		protected const float MaxMicrosecondsPerPixel = 64f;
		
		protected readonly TimelineRuler ruler;
		protected readonly Widget contentContainer;
		protected readonly ThemedScrollView horizontalScrollView;
		protected readonly ThemedScrollView verticalScrollView;
		
		protected bool IsContentRebuildingRequired { get; private set; }
		protected bool IsScaleChanged { get; private set; }
		protected bool IsHorizontalScrollPositionChanged { get; private set; }

		protected float MicrosecondsPerPixel => cachedMicrosecondsPerPixel;
		
		private float cachedMicrosecondsPerPixel;
		private float cachedHorizontalScrollPosition;
		
		private float itemHeight = 20;

		public float ItemHeight
		{
			get { return itemHeight; }
			set {
				if (itemHeight != value) {
					itemHeight = value;
					IsContentRebuildingRequired = true;
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
					IsContentRebuildingRequired = true;
				}
			}
		}
		
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
						IsScaleChanged = true;
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
					IsHorizontalScrollPositionChanged = true;
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
				yield return null;
			}
		}
	}
}

#endif // PROFILER