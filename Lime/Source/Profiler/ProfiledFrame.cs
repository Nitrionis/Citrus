#if PROFILER

using System;
using Yuzu;

namespace Lime.Profiler
{
	/// <summary>
	/// Describes the resources spent by the CPU and GPU to create a frame.
	/// </summary>
	/// <remarks>
	/// In cases where objects are not rendered after updating, some parameters will be set to 0.
	/// </remarks>
	public struct ProfiledFrame
	{
		/// <summary>
		/// Unique identifier (number) of the frame.
		/// </summary>
		/// <remarks>
		/// Always 1 more than the previous profiled frame.
		/// Only frames for which profiling is enabled are numbered.
		/// </remarks>
		[YuzuMember]
		public ulong Identifier;

		/// <summary>
		/// The time in microseconds elapsed between the start of processing
		/// this frame and the start of processing the next frame in update thread.
		/// </summary>
		[YuzuMember]
		public uint UpdateThreadElapsedTime;

		/// <summary>
		/// Start time of processing this frame in the update thread relative to application launch.
		/// </summary>
		[YuzuMember]
		public TimeSpan UpdateThreadStartTime;

		/// <summary>
		/// The time in microseconds elapsed between the start of processing
		/// this frame and the start of processing the next frame in render thread.
		/// </summary>
		[YuzuMember]
		public uint RenderThreadElapsedTime;

		/// <summary>
		/// Start time of processing this frame in the render thread relative to application launch.
		/// </summary>
		[YuzuMember]
		public TimeSpan RenderThreadStartTime;

		/// <summary>
		/// The time elapsed between the start and finish of rendering this frame on GPU.
		/// </summary>
		/// <remarks>
		/// The timer starts when all GPU commands from the previous frame have finished executing
		/// and stops when all GPU commands from the current frame have finished executing.
		/// </remarks>
		[YuzuMember]
		public uint GpuElapsedTime;

		/// <summary>
		/// The number of garbage collections for each generation of objects since application launch.
		/// These metrics are recorded right before the next frame update is launched.
		/// </summary>
		/// <remarks>
		/// The data was obtained using the method <see cref="GC.CollectionCount(int)"/>.
		/// </remarks>
		[YuzuMember]
		public int[] UpdateThreadGarbageCollections;

		/// <summary>
		/// The number of garbage collections for each generation of objects since application launch.
		/// These metrics are recorded right before the next frame render is launched.
		/// </summary>
		/// <remarks>
		/// The data was obtained using the method <see cref="GC.CollectionCount(int)"/>.
		/// </remarks>
		[YuzuMember]
		public int[] RenderThreadGarbageCollections;

		/// <summary>
		/// The number of scene draw calls that were processed by the GPU.
		/// </summary>
		[YuzuMember]
		public int SceneDrawCallCount;

		/// <summary>
		/// The number of vertices in the scene.
		/// </summary>
		[YuzuMember]
		public int SceneVerticesCount;

		/// <summary>
		/// The number of polygons in the scene.
		/// </summary>
		[YuzuMember]
		public int SceneTrianglesCount;

		/// <summary>
		/// Shows how much the number of scene draw calls has been reduced.
		/// </summary>
		/// <example>
		/// There are 5 objects. Due to batching, they are rendered in one draw call.
		/// Then 4 is added to the value of this field.
		/// </example>
		[YuzuMember]
		public int SceneSavedByBatching;

		/// <summary>
		/// The total number of draw calls that were processed by the GPU in the frame.
		/// </summary>
		/// <remarks>
		/// Includes <see cref="SceneDrawCallCount"/>.
		/// </remarks>
		[YuzuMember]
		public int FullDrawCallCount;

		/// <summary>
		/// The total number of vertices in the frame.
		/// </summary>
		/// <remarks>
		/// Includes <see cref="SceneVerticesCount"/>.
		/// </remarks>
		[YuzuMember]
		public int FullVerticesCount;

		/// <summary>
		/// The total number of triangles in the frame.
		/// </summary>
		/// <remarks>
		/// Includes <see cref="SceneTrianglesCount"/>.
		/// </remarks>
		[YuzuMember]
		public int FullTrianglesCount;

		/// <summary>
		/// Shows how much the number of draw calls has been reduced.
		/// </summary>
		/// <remarks>
		/// Includes <see cref="SceneSavedByBatching"/>.
		/// </remarks>
		/// <example>
		/// There are 5 objects. Due to batching, they are rendered in one draw call.
		/// Then 4 is added to the value of this field.
		/// </example>
		[YuzuMember]
		public int FullSavedByBatching;
	}
}

#endif // PROFILER
