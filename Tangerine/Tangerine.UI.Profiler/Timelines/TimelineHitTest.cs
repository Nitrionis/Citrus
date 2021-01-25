#if PROFILER

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime;

namespace Tangerine.UI.Timelines
{
	using Task = System.Threading.Tasks.Task;
	
	internal class TimelineHitTest
	{
		public const int InvalidItemIndex = -1;
		
		private Task<int> newestTask;
		private long newestTaskId;
		
		public TimelineHitTest()
		{
			newestTask = Task.CompletedTask;
		}
		
		/// <summary>
		/// 
		/// </summary>
		/// <param name="mousePosition">
		/// 
		/// </param>
		/// <param name="items">
		/// 
		/// </param>
		/// <returns>
		///
		/// </returns>
		public Task<int> RunAsyncHitTest(Vector2 mousePosition, IEnumerable<ItemInfo> items)
		{
			var mousePositionCopy = mousePosition;
			var itemsIteratorCopy = items;
			var currentTaskId = Interlocked.Increment(ref newestTaskId);
			newestTask = newestTask.ContinueWith((previousTask) => {
				if (currentTaskId != Interlocked.Read(ref newestTaskId)) {
					// It makes no sense to execute old queries.
					return InvalidItemIndex;
				}
				return RunHitTest(mousePositionCopy, itemsIteratorCopy);
			});
			return newestTask;
		}

		private static int RunHitTest(Vector2 mousePosition, IEnumerable<ItemInfo> items)
		{
			
			return InvalidItemIndex;
		}
		
		public struct ClickPoint
		{
			public float Timestamp;
			public float VerticalPosition;
		}
		
		public struct ItemInfo
		{
			public TimePeriod TimePeriod;
			public float VerticalPosition;
		}
	}
}

#endif // PROFILER