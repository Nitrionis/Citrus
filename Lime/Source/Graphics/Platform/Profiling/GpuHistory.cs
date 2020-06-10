using Yuzu;

namespace Lime.Graphics.Platform.Profiling
{
	public class GpuHistory
	{
		/// <summary>
		/// The maximum number of frames that are stored in history.
		/// </summary>
		public const int HistoryFramesCount = 160;

		public const int DrawCallPoolStartCapacity = 256 * HistoryFramesCount;

		/// <summary>
		/// The last profiled frame.
		/// </summary>
		public Item LastFrame { get; protected set; }

		/// <summary>
		/// The total number of profiled frames from the moment the engine starts.
		/// </summary>
		public long ProfiledFramesCount { get; protected set; }

		/// <summary>
		/// Describes the last <see cref="HistoryFramesCount"/> frames in history.
		/// </summary>
		protected readonly Item[] items;

		/// <summary>
		/// Pool for all draw calls.
		/// </summary>
		protected readonly RingPool<GpuUsage> DrawCallsPool;

		/// <summary>
		/// Stores draw call owners indices.
		/// </summary>
		public readonly OwnersPool OwnersPool;

		public GpuHistory(OwnersPool ownersPool)
		{
			items = new Item[HistoryFramesCount];
			for (int i = 0; i < items.Length; i++) {
				items[i] = new Item();
			}
			DrawCallsPool = new RingPool<GpuUsage>(DrawCallPoolStartCapacity);
			OwnersPool = ownersPool;
		}

		public Item GetFrame(long index) => items[index % items.Length];

		public bool IsFrameIndexValid(long index) =>
			index > 0 &&
			index < ProfiledFramesCount &&
			index > ProfiledFramesCount - HistoryFramesCount;

		protected Item ResetFrame(long frameIndex) =>
			items[frameIndex % items.Length].Reset(DrawCallsPool, OwnersPool);

		/// <summary>
		/// Frame statistics.
		/// </summary>
		public class Item
		{
			/// <summary>
			/// Locked when overwriting data.
			/// </summary>
			[YuzuExclude]
			public readonly object LockObject = new object();

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
			/// Profiling result list handle for individual draw calls.
			/// </summary>
			[YuzuRequired]
			public uint DrawCallsDescriptor = RingPool<GpuUsage>.InvalidDescriptor;

			public Item Reset(RingPool<GpuUsage> drawCallsPool, OwnersPool ownersPool)
			{
				FrameIndex = -1;

				SceneDrawCallCount   = 0;
				SceneVerticesCount   = 0;
				SceneTrianglesCount  = 0;
				SceneSavedByBatching = 0;

				FullDrawCallCount    = 0;
				FullGpuRenderTime    = 0;
				FullVerticesCount    = 0;
				FullTrianglesCount   = 0;
				FullSavedByBatching  = 0;

				IsDeepProfilingEnabled = false;
				IsSceneOnlyDeepProfiling = false;
				if (DrawCallsDescriptor != RingPool<GpuUsage>.InvalidDescriptor) {
					foreach (var dc in drawCallsPool.Enumerate(DrawCallsDescriptor)) {
						ownersPool.FreeOldest(dc.Owners);
					}
					drawCallsPool.FreeOldestList(DrawCallsDescriptor);
				}
				DrawCallsDescriptor = RingPool<GpuUsage>.InvalidDescriptor;
				IsCompleted = false;

				return this;
			}

			public Item ShallowClone() => new Item {
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
				DrawCallsDescriptor       = DrawCallsDescriptor
			};
		}
	}
}
