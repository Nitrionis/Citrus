using System.Collections.Generic;
using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	public class GpuHistory
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
			public long FrameIndex;

			/// <summary>
			/// This field indicates whether frame profiling is complete.
			/// Some data will not be available until the rendering of the frame is completed.
			/// </summary>
			[YuzuRequired]
			public bool IsCompleted;

			/// <summary>
			/// Indicates the status of the IsDeepProfilingEnabled profiler options for the frame.
			/// </summary>
			[YuzuRequired]
			public bool IsDeepProfilingEnabled;

			/// <summary>
			/// Indicates the status of the IsSceneOnlyDeepProfiling profiler options for the frame.
			/// </summary>
			[YuzuRequired]
			public bool IsSceneOnlyDeepProfiling;

			/// <summary>
			/// The number of scene draw calls that were processed by the GPU.
			/// </summary>
			[YuzuRequired]
			public int SceneDrawCallCount;

			/// <summary>
			/// The number of vertices in the scene.
			/// </summary>
			[YuzuRequired]
			public int SceneVerticesCount;

			/// <summary>
			/// The number of polygons in the scene.
			/// </summary>
			[YuzuRequired]
			public int SceneTrianglesCount;

			/// <summary>
			/// Shows how much the number of draw calls has been reduced.
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
			/// </summary>
			[YuzuRequired]
			public int FullTrianglesCount;

			/// <summary>
			/// Shows how much the number of draw calls has been reduced.
			/// Includes <see cref="SceneSavedByBatching"/>.
			/// </summary>
			[YuzuRequired]
			public int FullSavedByBatching;

			/// <summary>
			/// Stores the results of all draw calls.
			/// </summary>
			[YuzuRequired]
			public List<GpuUsage> DrawCalls;


			public Item()
			{
				DrawCalls = new List<GpuUsage>();
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
						NativeOwnersPool.FreeOldest(dc.Owners);
					}
					DrawCalls.Clear();
				}
				IsDeepProfilingEnabled = false;
				IsSceneOnlyDeepProfiling = false;
				IsCompleted = false;

				return this;
			}

			/// <summary>
			/// Copy without draw calls.
			/// </summary>
			public Item LightweightClone() => new Item {
				FrameIndex                = FrameIndex,
				IsCompleted               = IsCompleted,
				IsDeepProfilingEnabled    = IsDeepProfilingEnabled,
				IsSceneOnlyDeepProfiling  = IsSceneOnlyDeepProfiling,
				SceneDrawCallCount        = SceneDrawCallCount,
				SceneVerticesCount        = SceneVerticesCount,
				SceneTrianglesCount       = SceneTrianglesCount,
				SceneSavedByBatching      = SceneSavedByBatching,
				FullGpuRenderTime         = FullGpuRenderTime,
				FullDrawCallCount         = FullDrawCallCount,
				FullVerticesCount         = FullVerticesCount,
				FullTrianglesCount        = FullTrianglesCount,
				FullSavedByBatching       = FullSavedByBatching,
			};
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
		/// The total number of profiled frames from the moment the engine starts.
		/// </summary>
		public long ProfiledFramesCount { get; protected set; }

		protected readonly Item[] items;

		private Item freeItem;
		private long protectedIndex;

		public GpuHistory()
		{
			freeItem = new Item();
			protectedIndex = -1;
			items = new Item[HistoryFramesCount];
			for (int i = 0; i < items.Length; i++) {
				items[i] = new Item();
			}
		}

		/// <summary>
		/// Ensures that the frame is not reset.
		/// </summary>
		/// <remarks>
		/// Only one frame can be locked at a time.
		/// Previous frame will be automatically unlocked.
		/// </remarks>
		public bool TryLockFrame(long frameIndex)
		{
			if (IsFrameIndexValid(frameIndex)) {
				protectedIndex = frameIndex % items.Length;
				return true;
			} else {
				return false;
			}
		}

		protected Item SafeResetFrame(long frameIndex)
		{
			long itemIndex = frameIndex % items.Length;
			if (frameIndex == protectedIndex) {
				var protectedFrame = items[itemIndex];
				items[itemIndex] = freeItem.Reset();
				freeItem = protectedFrame;
				protectedIndex = -1;
			}
			return items[itemIndex].Reset();
		}

		public Item GetFrame(long index) => items[index % items.Length];

		public bool IsFrameIndexValid(long index) =>
			index > 0 &&
			index < ProfiledFramesCount &&
			index > ProfiledFramesCount - HistoryFramesCount;
	}
}
