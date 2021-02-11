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

		protected readonly TimelineRuler ruler;
		protected readonly Widget leftContentBorder;
		protected readonly Widget contentContainer;
		protected readonly Widget rightContentBorder;
		protected readonly ThemedScrollView horizontalScrollView;
		protected readonly ThemedScrollView verticalScrollView;

		private float scale = 1f;

		/// <summary>
		/// Duration of a time interval in microseconds in which all timeline content can be placed.
		/// </summary>
		//public float ContentDuration { get; private set; };

		public float ContentDuration
		{
			get => 16_000;
			private set {}
		}

		protected float OriginalMicrosecondsPerPixel => ContentDuration / Width;
		
		protected float MicrosecondsPerPixel => scale * OriginalMicrosecondsPerPixel;

		/// <remarks>
		/// We increase the width of the content by 3 times because we need an
		/// equal distribution of the width between the content and each border.
		/// </remarks>
		protected float OriginalContentWidth => 3f * Width;
		
		/// <summary>
		/// The position of the scroll in the horizontal scroll view at which all content
		/// is visible provided that it has the original width.
		/// </summary>
		private float OriginalScrollPosition => Width;
		
		private float OriginalScale => 1f;
		
		protected TimelineWidget()
		{
			Layout = new VBoxLayout();
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineTasksBackground);
			ruler = new TimelineRuler(smallStepSize: 10, smallStepsPerBig: 10) {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 32,
				StartTime = 0,
				TextColor = ColorTheme.Current.Profiler.TimelineRulerText,
				TimestampsColor = ColorTheme.Current.Profiler.TimelineRulerStep
			};
			AddNode(ruler);
			leftContentBorder = new Widget() {
				Presenter = new WidgetFlatFillPresenter(Color4.Gray),
				MinMaxHeight = 100,
				Height = 100
			};
			contentContainer = new Widget {
				Id = "Profiler timeline content"
			};
			rightContentBorder = new Widget() {
				Presenter = new WidgetFlatFillPresenter(Color4.Gray),
				MinMaxHeight = 100,
				Height = 100
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
			verticalScrollView.Content.Layout = new HBoxLayout();
			verticalScrollView.Content.AddNode(leftContentBorder);
			verticalScrollView.Content.AddNode(contentContainer);
			verticalScrollView.Content.AddNode(rightContentBorder);
			AddNode(horizontalScrollView);
			horizontalScrollView.Content.Tasks.Insert(0, new Task(HorizontalScrollTask()));
			verticalScrollView.Content.Tasks.Insert(0, new Task(VerticalScrollTask()));
			Tasks.Add(ScaleScrollTask);
		}
		
		/// <param name="contentDuration">Value for <see cref="ContentDuration"/>.</param>
		public void ResetScale(float contentDuration)
		{
			scale = OriginalScale;
			ContentDuration = contentDuration;
			float newWidth = OriginalContentWidth;
			horizontalScrollView.Content.MinMaxWidth = newWidth;
			horizontalScrollView.Content.Width = newWidth;
			horizontalScrollView.ScrollPosition = OriginalScrollPosition;
			verticalScrollView.Content.MinMaxWidth = newWidth;
			verticalScrollView.Content.Width = newWidth;
			float segmentWidth = newWidth / 3;
			leftContentBorder.MinMaxWidth = segmentWidth;
			contentContainer.MinMaxWidth = segmentWidth;
			rightContentBorder.MinMaxWidth = segmentWidth;
			ruler.MicrosecondsPerPixel = MicrosecondsPerPixel;
			ruler.StartTime = ContentDuration; // todo 0
			OnResetScale();
		}

		protected virtual void OnResetScale() { }

		private IEnumerator<object> ScaleScrollTask()
		{
			var input = horizontalScrollView.Input;
			while (true) {
				if (
					input.IsKeyPressed(Key.Control) &&
					(input.WasKeyPressed(Key.MouseWheelDown) || input.WasKeyPressed(Key.MouseWheelUp))
					)
				{
					var sv = horizontalScrollView;
					scale += input.WheelScrollAmount * ScaleScrollingSpeed;
					scale = Mathf.Clamp(scale, Width / OriginalContentWidth, 32f);
					float sp = sv.ScrollPosition;
					float mp = sv.Content.LocalMousePosition().X;
					float oldWidth = sv.Content.Width;
					float newWidth = OriginalContentWidth / scale;
					sv.Content.MinMaxWidth = newWidth;
					sv.Content.Width = newWidth;
					float segmentWidth = newWidth / 3;
					leftContentBorder.MinMaxWidth = segmentWidth;
					contentContainer.MinMaxWidth = segmentWidth;
					rightContentBorder.MinMaxWidth = segmentWidth;
					sv.ScrollPosition = (mp / oldWidth - (mp - sp) / newWidth) * newWidth;
					ruler.MicrosecondsPerPixel = MicrosecondsPerPixel;
				}
				yield return null;
			}
		}

		private IEnumerator<object> HorizontalScrollTask()
		{
			var input = horizontalScrollView.Input;
			while (true) {
				bool isHorizontalMode =
					!input.IsKeyPressed(Key.Shift) &&
					!input.IsKeyPressed(Key.Control);
				horizontalScrollView.Behaviour.CanScroll = isHorizontalMode;
				verticalScrollView.Behaviour.StopScrolling();
				ruler.StartTime = horizontalScrollView.ScrollPosition * MicrosecondsPerPixel - ContentDuration;
				//horizontalScrollView.ScrollPosition += 0.1f;
				yield return null;
			}
		}

		private IEnumerator<object> VerticalScrollTask()
		{
			var input = verticalScrollView.Input;
			while (true) {
				bool isVerticalMode = input.IsKeyPressed(Key.Shift) && !input.IsKeyPressed(Key.Control);
				verticalScrollView.Behaviour.CanScroll = isVerticalMode;
				horizontalScrollView.Behaviour.StopScrolling();
				yield return null;
			}
		}
	}
}

#endif // PROFILER
