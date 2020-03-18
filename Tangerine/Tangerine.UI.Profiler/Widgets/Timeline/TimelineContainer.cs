using System.Collections.Generic;
using Lime;
using Lime.Profilers;

namespace Tangerine.UI.Timeline
{
	internal abstract class TimelineContainer : Widget
	{
		protected const float ItemTopMargin = 2;
		protected const float ItemHeight = 20;

		/// <summary>
		/// Timeline with timestamps above container elements.
		/// </summary>
		protected readonly TimelineRuler ruler;

		protected float MicrosecondsInPixel;

		/// <summary>
		/// Contains widgets representing time intervals.
		/// </summary>
		protected readonly Widget container;

		protected readonly ThemedScrollView horizontalScrollView;
		protected readonly ThemedScrollView verticalScrollView;

		/// <summary>
		/// Used to search for free space to visualize the item.
		/// </summary>
		protected readonly List<uint> freeSpaceOfLines = new List<uint>();

		protected TimelineContainer()
		{
			Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Profiler.TimelineBackground);
			Layout = new VBoxLayout();
			MinMaxHeight = 100;

			Input.AcceptMouseBeyondWidget = false;
			Input.AcceptMouseThroughDescendants = true;

			ruler = new TimelineRuler(10, 10) {
				Anchors = Anchors.LeftRight,
				MinMaxHeight = 32,
				Offset = 0
			};

			container = new Widget {
				MinMaxHeight = 64
			};

			horizontalScrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
				Anchors = Anchors.LeftRightTopBottom
			};
			horizontalScrollView.Content.Layout = new HBoxLayout();

			verticalScrollView = new ThemedScrollView(ScrollDirection.Vertical) {
				Anchors = Anchors.LeftRightTopBottom
			};
			verticalScrollView.Content.Layout = new VBoxLayout();

			horizontalScrollView.Content.AddNode(verticalScrollView);
			verticalScrollView.Content.AddNode(container);

			horizontalScrollView.Content.Tasks.Insert(0, new Task(HorizontalScrollTask()));
			verticalScrollView.Content.Tasks.Insert(0, new Task(VerticalScrollTask()));

			Tasks.Add(ScaleScrollTask);

			AddNode(ruler);
			AddNode(horizontalScrollView);
		}

		protected abstract void UpdateItemTransform(Node widget);

		protected abstract float CalculateHistoryWidth();

		protected virtual bool IsItemVisible(Widget item)
		{
			float start = horizontalScrollView.ScrollPosition;
			float end = start + horizontalScrollView.Size.X;
			float left = item.Position.X;
			float right = item.Position.X + item.Size.X;
			return start < right && left < end;
		}

		private void CheckItemsVisible()
		{
			foreach (var n in container.Nodes) {
				var node = n as Widget;
				node.Visible = IsItemVisible(node);
			}
		}

		protected void UpdateItemsPositions()
		{
			freeSpaceOfLines.Clear();
			foreach (var w in container.Nodes) {
				UpdateItemTransform(w);
			}
			float pcw = container.Width;
			float ncw = CalculateHistoryWidth();
			float nch = freeSpaceOfLines.Count * (ItemHeight + ItemTopMargin) + ItemTopMargin;
			float hsvp = horizontalScrollView.ScrollPosition;
			UpdateHistorySize(new Vector2(ncw, nch));
			UpdateScrollPosition(hsvp, pcw, ncw);
		}

		private void UpdateScrollPosition(float hsvp, float pcw, float ncw)
		{
			float lmp = LocalMousePosition().X;
			float pos = Mathf.Clamp((lmp + hsvp) / pcw - lmp / ncw, 0, 1);
			horizontalScrollView.ScrollPosition = pos * ncw;
		}

		protected void UpdateHistorySize(Vector2 newSize)
		{
			container.MinMaxSize = newSize;
			verticalScrollView.MinMaxWidth = Mathf.Max(newSize.X, horizontalScrollView.Width);
		}

		protected Vector2 AcquirePosition(ITimePeriod period)
		{
			int lineIndex = -1;
			for (int i = 0; i < freeSpaceOfLines.Count; i++) {
				if (freeSpaceOfLines[i] < period.Start) {
					lineIndex = i;
					break;
				}
			}
			if (lineIndex == -1) {
				lineIndex = freeSpaceOfLines.Count;
				freeSpaceOfLines.Add(period.Finish);
			} else {
				freeSpaceOfLines[lineIndex] = period.Finish;
			}
			return new Vector2(
				period.Start / MicrosecondsInPixel,
				ItemHeight * lineIndex + ItemTopMargin * (lineIndex + 1));
		}

		protected void ResetContainer()
		{
			MicrosecondsInPixel = 1.0f;
			ruler.RulerScale = 1.0f;
			container.Nodes.Clear();
			freeSpaceOfLines.Clear();
		}

		private IEnumerator<object> ScaleScrollTask()
		{
			while (true) {
				if (
					Input.IsKeyPressed(Key.Control) && !Input.IsMousePressed() &&
					(Input.WasKeyPressed(Key.MouseWheelDown) || Input.WasKeyPressed(Key.MouseWheelUp))
					)
				{
					MicrosecondsInPixel += Input.WheelScrollAmount / 1200;
					MicrosecondsInPixel = Mathf.Clamp(MicrosecondsInPixel, 0.2f, 10f);
					ruler.RulerScale = MicrosecondsInPixel;
					UpdateItemsPositions();
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
				ruler.Offset = horizontalScrollView.ScrollPosition;
				CheckItemsVisible();
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

		protected class TimePeriodComparer<T> : IComparer<T> where T : ITimePeriod
		{
			public int Compare(T a, T b) => a.Start.CompareTo(b.Start);
		}

		protected class TimePeriod : ITimePeriod
		{
			public uint Start { get; set; }
			public uint Finish { get; set; }

			public TimePeriod(uint start, uint finish)
			{
				Start = start;
				Finish = finish;
			}
		}
	}
}
