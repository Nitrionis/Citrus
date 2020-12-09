#if PROFILER

using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Lime.Profiler
{
	public class Frame
	{
		public ProfiledFrame CommonData;
		public DrawCommandsExecution DrawCommandsState;
		public RingPool.ListDescriptor DrawingGpuUsagesList;
		public RingPool.ListDescriptor UpdateCpuUsagesList;
		public RingPool.ListDescriptor RenderCpuUsagesList;

		public enum DrawCommandsExecution
		{
			NotSubmittedToGpu,
			SubmittedToGpu,
			Completed
		}
	}

	public class TypeId
	{
		private static int counter;
		public readonly int Value;
		public TypeId() => Value = Interlocked.Increment(ref counter);
	}

	internal interface IProfilerDatabase
	{
		/// <summary>
		/// Allows you to enable or disable the profiler.
		/// </summary>
		bool ProfilerEnabled { get; set; }

		/// <summary>
		/// If requested, the profiler will collect batch break reasons.
		/// </summary>
		bool BatchBreakReasonsRequired { get; set; }

		/// <summary>
		/// The number of frames during which the profiled frame data is stored in the database.
		/// </summary>
		int FrameLifespan { get; }

		/// <summary>
		/// The number of frames for which profiling has started.
		/// </summary>
		long ProfiledFramesCount { get; }

		/// <summary>
		/// Identifier of the last frame for which profiling is complete.
		/// </summary>
		long LastAvailableFrame { get; }

		/// <summary>
		/// Allows you to get a type by identifier.
		/// </summary>
		ConditionalWeakTable<Type, TypeId> NativeTypesTable { get; }

		/// <summary>
		/// Stores a description for each object that the profiler interacted with.
		/// </summary>
		ReferenceTable NativeReferenceTable { get; }

		/// <summary>
		/// If a <see cref="CpuUsage"/> structure was created in the update
		/// thread and contains several owners, they must be placed in this pool.
		/// </summary>
		RingPool<ReferenceTable.RowIndex> UpdateOwnersPool { get; }

		/// <summary>
		/// If a <see cref="CpuUsage"/> or <see cref="GpuUsage"/> structure was created in the
		/// render thread and contains several owners, they must be placed in this pool.
		/// </summary>
		/// <remarks>
		/// Used to store the indices of the nodes from which batches are created.
		/// </remarks>
		RingPool<ReferenceTable.RowIndex> RenderOwnersPool { get; }

		/// <summary>
		/// CPU usage pool for update thread.
		/// </summary>
		RingPool<CpuUsage> UpdateCpuUsagesPool { get; }

		/// <summary>
		/// CPU usage pool for render thread.
		/// </summary>
		RingPool<CpuUsage> RenderCpuUsagesPool { get; }

		/// <summary>
		/// GPU usage pool for render thread.
		/// </summary>
		RingPool<GpuUsage> GpuUsagesPool { get; }

		/// <summary>
		/// Checks if the frame with the specified identifier can be accessed.
		/// </summary>
		bool CanAccessFrame(long identifier);

		/// <summary>
		/// Returns the frame with specified identifier or null if it is impossible to obtain.
		/// </summary>
		Frame GetFrame(long identifier);
	}
}

#endif // PROFILER
