using System.Collections.Generic;
using Lime.Graphics.Platform;
using Yuzu;

namespace Lime.Profilers
{
	public class CpuHistory
	{
		public class Item
		{
			[YuzuRequired]
			public long UpdateIndex;

			[YuzuRequired]
			public long FrameIndex;

			[YuzuRequired]
			public float DeltaTime;

			[YuzuRequired]
			public List<CpuUsage> NodesResults = new List<CpuUsage>();

			public Item Reset()
			{
				DeltaTime = 0;
				foreach (var r in NodesResults) {
					r.Free();
				}
				NodesResults.Clear();
				return this;
			}

			/// <summary>
			/// Copy without NodesResults.
			/// </summary>
			public Item LightweightClone() => new Item {
				DeltaTime = DeltaTime,
				FrameIndex = FrameIndex,
				UpdateIndex = UpdateIndex
			};
		}

		protected readonly Item[] items;

		private Item freeItem;
		private long protectedIndex;

		/// <summary>
		/// The last profiled update.
		/// </summary>
		public Item LastUpdate { get; protected set; }

		/// <summary>
		/// The total number of profiled updates from the moment the engine starts.
		/// </summary>
		public long ProfiledUpdatesCount { get; protected set; }

		public CpuHistory()
		{
			freeItem = new Item();
			protectedIndex = -1;
			items = new Item[GpuHistory.HistoryFramesCount];
			for (int i = 0; i < items.Length; i++) {
				items[i] = new Item();
			}
		}

		public Item GetUpdate(long index) => items[index % items.Length];

		public bool IsUpdateIndexValid(long index) =>
			index > 0 &&
			index < ProfiledUpdatesCount &&
			index > ProfiledUpdatesCount - GpuHistory.HistoryFramesCount;

		/// <summary>
		/// Ensures that the update is not reset.
		/// </summary>
		/// <remarks>
		/// Only one update can be locked at a time.
		/// Previous update will be automatically unlocked.
		/// </remarks>
		public bool TryLockUpdate(long updateIndex)
		{
			if (IsUpdateIndexValid(updateIndex)) {
				protectedIndex = updateIndex % items.Length;
				return true;
			} else {
				return false;
			}
		}

		protected Item SafeResetUpdate(long updateIndex)
		{
			long itemIndex = updateIndex % items.Length;
			if (itemIndex == protectedIndex) {
				var protectedUpdate = items[itemIndex];
				items[itemIndex] = freeItem.Reset();
				freeItem = protectedUpdate;
				protectedIndex = -1;
			}
			return items[itemIndex].Reset();
		}
	}
}
