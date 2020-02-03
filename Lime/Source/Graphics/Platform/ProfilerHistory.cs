using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Yuzu;

namespace Lime.Graphics.Platform
{
	public class ProfilerHistory
	{
		/// <summary>
		/// Frame statistics.
		/// </summary>
		public class Item
		{
			/// <summary>
			/// Frame index. Frames not involved in profiling are not indexed.
			/// Only frames from the main window are involved in profiling,
			/// and only if the profiler is active.
			/// </summary>
			[YuzuRequired]
			public long ProfiledFrameIndex;

			/// <summary>
			/// Indicates whether deep profiling is enabled in the frame.
			/// </summary>
			[YuzuRequired]
			public bool IsDeepProfilingEnabled;

			/// <summary>
			/// The number of draw calls that were processed by the GPU.
			/// Includes only scene draw calls.
			/// </summary>
			[YuzuRequired]
			public int SceneDrawCallCount;

			/// <summary>
			/// The total number of vertices in the scene.
			/// </summary>
			[YuzuRequired]
			public int SceneVerticesCount;

			/// <summary>
			/// The number of polygons that will be processed by the GPU.
			/// The value is calculated based on the primitives topology.
			/// </summary>
			[YuzuRequired]
			public int SceneTrianglesCount;

			/// <summary>
			/// Shows how much the number of draw calls has been reduced.
			/// Includes only scene rendering calls.
			/// </summary>
			[YuzuRequired]
			public int SceneSavedByBatching;

			/// <summary>
			/// Render time in milliseconds of scene and Tangerine interface.
			/// </summary>
			[YuzuRequired]
			public double FullGpuRenderTime;

			/// <summary>
			/// The number of draw calls that were processed by the GPU.
			/// Includes Scene and Tangerine interface draw calls.
			/// </summary>
			[YuzuRequired]
			public int FullDrawCallCount;

			/// <summary>
			/// Scene and Tangerine interface vertices count.
			/// </summary>
			[YuzuRequired]
			public int FullVerticesCount;

			/// <summary>
			/// Scene and Tangerine interface triangles count.
			/// The value is calculated based on the primitives topology.
			/// </summary>
			[YuzuRequired]
			public int FullTrianglesCount;

			/// <summary>
			/// Shows how much the number of draw calls has been reduced.
			/// Includes engine interface rendering calls.
			/// </summary>
			[YuzuRequired]
			public int FullSavedByBatching;

			/// <summary>
			/// Stores the results of all draw calls.
			/// </summary>
			[YuzuExclude]
			public readonly List<ProfilingResult> DrawCalls;


			public Item()
			{
				DrawCalls = new List<ProfilingResult>(DrawCallBufferStartSize);
			}

			public Item Reset()
			{
				SceneDrawCallCount = 0;
				SceneVerticesCount = 0;
				SceneTrianglesCount = 0;
				SceneSavedByBatching = 0;

				FullDrawCallCount = 0;
				FullGpuRenderTime = 0;
				FullVerticesCount = 0;
				FullTrianglesCount = 0;
				FullSavedByBatching = 0;

				if (IsDeepProfilingEnabled) {
					foreach (var dc in DrawCalls) {
						dc.Free();
					}
				}
				IsDeepProfilingEnabled = false;

				return this;
			}
		}

		/// <summary>
		/// The maximum number of frames that are stored in history.
		/// </summary>
		public const int HistoryFramesCount = 160;

		public const int DrawCallBufferStartSize = 256;

		/// <summary>
		/// The last profiled frame.
		/// </summary>
		public Item LastFrame { get; protected set; }

		/// <summary>
		/// Locked frame will not be overwritten.
		/// </summary>
		private Item lockedItem = new Item();

		private long lockedIndex = HistoryFramesCount + 1;

		private readonly Item[] items;
		public readonly ReadOnlyCollection<Item> Items;

		/// <summary>
		/// The total number of profiled frames from the moment the engine starts.
		/// </summary>
		public long ProfiledFramesCount { get; private set; }

		protected ProfilerHistory()
		{
			items = new Item[HistoryFramesCount];
			Items = Array.AsReadOnly(items);
			for (int i = 0; i < items.Length; i++) {
				items[i] = new Item();
			}
		}

		protected Item AcquireResultsBuffer() => items[ProfiledFramesCount++ % items.Length].Reset();

		/// <summary>
		/// Ensures that the frame is not reset.
		/// </summary>
		/// <param name="index">Item index in <see cref="Items"/></param>
		/// <remarks>
		/// Only one frame can be locked at a time.
		/// Previous frame will be automatically unlocked.
		/// </remarks>
		public Item LockFrame(long index)
		{
			if (ProfiledFramesCount - lockedIndex < HistoryFramesCount) {
				SwapItemWithLocked(lockedIndex);
			}
			var item = SwapItemWithLocked(index);
			lockedIndex = index;
			return item;
		}

		private Item SwapItemWithLocked(long index)
		{
			index %= items.Length;
			var item = items[index];
			items[index] = lockedItem;
			lockedItem = item;
			return item;
		}
	}
}
