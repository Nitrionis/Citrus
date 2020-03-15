using System;
using System.Collections.ObjectModel;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
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

			public Item Reset()
			{
				DeltaTime = 0;
				return this;
			}

			public Item LightweightClone() => new Item {
				DeltaTime = DeltaTime,
				FrameIndex = FrameIndex,
				UpdateIndex = UpdateIndex
			};
		}

		protected readonly Item[] items;

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
	}
}
