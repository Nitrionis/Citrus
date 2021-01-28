#if PROFILER

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal struct Range
	{
		public float A;
		public float B;
	}
	
	internal class TimelineHitTest
	{
		public const int InvalidItemIndex = -1;
		
		private Task<int> newestTask;
		private long newestTaskId;
		
		public TimelineHitTest() => newestTask = Task.FromResult(InvalidItemIndex);

		/// <summary>
		/// Asynchronously checks if the cursor position intersects elements until the first match.
		/// </summary>
		/// <param name="items">
		/// Items should not change until the request completes.
		/// </param>
		/// <returns>
		/// A task with a result containing the number of the element with which the cursor intersects.
		/// Use this task to find out when the request is completed.
		/// </returns>
		public Task<int> RunAsyncHitTest(ClickPoint mousePosition, IEnumerable<ItemInfo> items)
		{
			var mousePositionCopy = mousePosition;
			var itemsIteratorCopy = items;
			var currentTaskId = Interlocked.Increment(ref newestTaskId);
			newestTask = newestTask.ContinueWith((previousTask) => 
				currentTaskId != Interlocked.Read(ref newestTaskId) ? 
					InvalidItemIndex : RunHitTest(mousePositionCopy, itemsIteratorCopy));
			return newestTask;
		}

		private static int RunHitTest(ClickPoint mousePosition, IEnumerable<ItemInfo> items)
		{
			int itemIndex = -1;
			foreach (var item in items) {
				++itemIndex;
				if (
					item.TimePeriod.StartTime <= mousePosition.Timestamp &&
					item.TimePeriod.FinishTime >= mousePosition.Timestamp ||
					item.VerticalLocation.A <= mousePosition.VerticalPosition &&
					item.VerticalLocation.B >= mousePosition.VerticalPosition
					) 
				{
					return itemIndex;
				}
			}
			return InvalidItemIndex;
		}
		
		public struct ClickPoint
		{
			public float Timestamp;
			public float VerticalPosition;
		}

		public struct ItemInfo
		{
			/// <summary>
			/// Defines the horizontal location of the element.
			/// </summary>
			public TimePeriod TimePeriod;
			
			/// <summary>
			/// Defines the vertical location of the element, where a <= b.
			/// </summary>
			public Range VerticalLocation;
		}
	}
}

#endif // PROFILER