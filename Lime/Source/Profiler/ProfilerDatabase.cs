#if PROFILER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lime.Profiler
{
	/// <summary>
	/// Performs terminal requests.
	/// </summary>
	public class ProfilerDatabase : IProfilerDatabase
	{
		/// <summary>
		/// This is a value greater than the number of buffers in swapchain.
		/// </summary>
		private const uint MaxSwapchainBuffersCount = 10;

		private static ProfilerDatabase instance;

		[ThreadStatic]
		private static ThreadInfo threadInfo;

		/// <summary>
		/// Indicates whether data collection is allowed on the current thread.
		/// </summary>
		/// <remarks>
		/// <para>Essentially also indicates whether the profiler is enabled or not.</para>
		/// <para>The value is set at the time of launching some process (update or rendering).</para>
		/// </remarks>
		public static bool IsDataCollectionAllowed => threadInfo != ThreadInfo.Unknown;

		[ThreadStatic]
		private static RingPool<ReferenceTable.RowIndex> ownersPool;

		/// <summary>
		/// Provides access to the owners pool for the current thread.
		/// </summary>
		public static RingPool<ReferenceTable.RowIndex> OwnersPool => ownersPool;

		private static readonly ConditionalWeakTable<Type, TypeId> nativeTypesTable =
			new ConditionalWeakTable<Type, TypeId>();

		// Store thread-dependent data in arrays to access them by thread index.
		private static readonly RingPool<CpuUsage>[] cpuUsagesPools = new RingPool<CpuUsage>[3];
		private static readonly ThreadDependentData[] threadDependentData = new ThreadDependentData[3];

		private static readonly Queue<long> unfinishedFrames = new Queue<long>();
		private static Frame[] profiledFrames;

		/// <remarks>
		/// We use this to ensure the correct order of resource release
		/// in cases where no rendering occurs after the update.
		/// </remarks>
		private static Queue<(RingPool.ListDescriptor, RingPool.ListDescriptor)> renderResourcesQueue;

		private static bool isUpdateProfilerEnabled;
		private static bool isRenderProfilerEnabled;
		private static bool isProfilingRequired;

		/// <inheritdoc/>
		public bool ProfilerEnabled
		{
			get => isUpdateProfilerEnabled;
			set => isProfilingRequired = value;
		}

		/// <inheritdoc/>
		public int FrameLifespan { get; }

		/// <inheritdoc/>
		public long ProfiledFramesCount { get; private set; }

		/// <inheritdoc/>
		public long LastAvailableFrame { get; private set; }

		/// <inheritdoc/>
		public ConditionalWeakTable<Type, TypeId> NativeTypesTable { get; }

		/// <inheritdoc/>
		public ReferenceTable NativeReferenceTable { get; }

		/// <inheritdoc/>
		public RingPool<ReferenceTable.RowIndex> UpdateOwnersPool { get; }

		/// <inheritdoc/>
		public RingPool<ReferenceTable.RowIndex> RenderOwnersPool { get; }

		/// <inheritdoc/>
		public RingPool<CpuUsage> UpdateCpuUsagesPool { get; }

		/// <inheritdoc/>
		public RingPool<CpuUsage> RenderCpuUsagesPool { get; }

		/// <inheritdoc/>
		public RingPool<GpuUsage> GpuUsagesPool { get; }

		/// <inheritdoc/>
		public bool CanAccessFrame(long identifier) =>
			identifier > 0 &&
			identifier >= ProfiledFramesCount - FrameLifespan &&
			identifier <= LastAvailableFrame;

		/// <inheritdoc/>
		public Frame GetFrame(long identifier) =>
			CanAccessFrame(identifier) ? CalculatedFramePlace(identifier) : null;

		/// <summary>
		/// Returns a reference to the expected location of the frame with this identifier.
		/// </summary>
		private static Frame CalculatedFramePlace(long identifier) =>
			profiledFrames[identifier % profiledFrames.Length];

		/// <summary>
		/// Ensures that a description has been created for this object.
		/// </summary>
		/// <remarks>
		/// Can only be called in the update thread.
		/// </remarks>
		public static void EnsureDescriptionFor(IProfileableObject @object)
		{
			if (@object == null) return;
			var nativeReferenceTable = instance.NativeReferenceTable;
			nativeReferenceTable.EnsureDescriptionFor(@object);
			nativeReferenceTable.UpdateFrameIndexFor(@object.RowIndex, instance.ProfiledFramesCount);
		}

		/// <summary>
		/// Creates a record in the database of new CPU usage.
		/// </summary>
		/// <remarks>Can be called from render and update threads.</remarks>
		public static CpuUsageStartInfo CpuUsageStarted()
		{
			var threadInfo = ProfilerDatabase.threadInfo;
			if (threadInfo != ThreadInfo.Unknown) {
				return new CpuUsageStartInfo(threadInfo, Stopwatch.GetTimestamp());
			} else {
				return new CpuUsageStartInfo(threadInfo, 0);
			}
		}

		/// <summary>
		/// Create and saves the CpuUsage structure in the database.
		/// </summary>
		/// <param name="targetType">The type of object for which the work is performed.</param>
		/// <remarks>Can be called from render and update threads.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, IProfileableObject @object,
			CpuUsage.Reasons reason, Type targetType) =>
			CpuUsageFinished(usageInfo, new Owners(@object), reason, targetType);

		/// <summary>
		/// Create and saves the CpuUsage structure in the database.
		/// </summary>
		/// <remarks>Can be called from render and update threads.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, IProfileableObject @object,
			CpuUsage.Reasons reason, ITypeIdentifierProvider typeIdentifierProvider) =>
			CpuUsageFinished(usageInfo, new Owners(@object), reason, typeIdentifierProvider.Identifier);

		/// <summary>
		/// Create and saves the CpuUsage structure in the database.
		/// </summary>
		/// <remarks>Can be called from render and update threads.</remarks>
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, Owners owners,
			CpuUsage.Reasons reason, TypeIdentifier typeIdentifier)
		{
			if (usageInfo.ThreadInfo != ThreadInfo.Unknown) {
				var pool = cpuUsagesPools[(int)usageInfo.ThreadInfo];
				pool.AddToNewestList(new CpuUsage {
					Reason = reason,
					Owners = owners,
					TypeIdentifier = typeIdentifier,
					StartTime = usageInfo.StartTime,
					FinishTime = Stopwatch.GetTimestamp()
				});
			}
		}

		/// <summary>
		/// Create and saves the CpuUsage structure in the database.
		/// </summary>
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, Owners owners,
			CpuUsage.Reasons reason, Type targetType)
		{
			if (usageInfo.ThreadInfo != ThreadInfo.Unknown) {
				var threadInfo = threadDependentData[(int)usageInfo.ThreadInfo];
				threadInfo.CpuUsagesPool.AddToNewestList(new CpuUsage {
					Reason = reason,
					Owners = owners,
					TypeIdentifier = new TypeIdentifier(threadInfo.GetTypeId(targetType)),
					StartTime = usageInfo.StartTime,
					FinishTime = Stopwatch.GetTimestamp()
				});
			}
		}

		/// <summary>
		/// Ensures that the specified type will be assigned some number.
		/// </summary>
		public static TypeIdentifier EnsureNumberFor(Type type) =>
			new TypeIdentifier(nativeTypesTable.GetOrCreateValue(type).Value);

		private static CpuUsageStartInfo fullUpdateCpuUsage;
		private static bool isUpdateMainWindowTarget;

		internal static void Updating(bool isMainWindowTarget)
		{
			isUpdateMainWindowTarget = isMainWindowTarget;
			if (!isMainWindowTarget) return;
			isUpdateProfilerEnabled = isProfilingRequired;
			// From Static to ThreadStatic.
			threadInfo = isUpdateProfilerEnabled ? ThreadInfo.Update : ThreadInfo.Unknown;
			ownersPool = instance.UpdateOwnersPool;
			const long InvalidValue = long.MaxValue;
			if (isUpdateProfilerEnabled) {
				var frame = CalculatedFramePlace(instance.ProfiledFramesCount);
				FreeResourcesForNextUpdate(frame);

				frame.CommonData.Identifier = instance.ProfiledFramesCount;
				frame.CommonData.UpdateThreadElapsedTime = InvalidValue;
				frame.CommonData.UpdateThreadStartTime = Stopwatch.GetTimestamp();

				frame.DrawCommandsState = Frame.DrawCommandsExecution.NotSubmittedToGpu;
				frame.UpdateCpuUsagesList = instance.UpdateCpuUsagesPool.AcquireList();
				frame.RenderCpuUsagesList = RingPool.ListDescriptor.Null;
				frame.DrawingGpuUsagesList = RingPool.ListDescriptor.Null;

				++instance.ProfiledFramesCount;
				fullUpdateCpuUsage = CpuUsageStarted();
			}
			if (instance.ProfiledFramesCount > 1) {
				var previousFrame = CalculatedFramePlace(instance.ProfiledFramesCount - 2);
				if (previousFrame.CommonData.UpdateThreadElapsedTime == InvalidValue) {
					previousFrame.CommonData.UpdateThreadElapsedTime =
						Stopwatch.GetTimestamp() - previousFrame.CommonData.UpdateThreadStartTime;
				}
			}
		}

		internal static void Updated()
		{
			if (!isUpdateMainWindowTarget) return;
			var nativeReferenceTable = instance.NativeReferenceTable;
			// 10000 is just some value that adjusts the frequency of garbage collection.
			if (nativeReferenceTable.NewDescriptionsCount > 10000) {
				Console.WriteLine("Profiler ReferenceTable garbage collection starting!");
				long minFrameIndexToStayAlive =
					instance.ProfiledFramesCount - instance.FrameLifespan - 2;
				nativeReferenceTable.CollectGarbage(minFrameIndexToStayAlive);
			}
			if (isUpdateProfilerEnabled) {
				var frame = CalculatedFramePlace(instance.ProfiledFramesCount - 1);
				var garbageCollections =
					frame.CommonData.UpdateThreadGarbageCollections ?? new int[GC.MaxGeneration];
				for (int i = 0; i < garbageCollections.Length; i++) {
					garbageCollections[i] = GC.CollectionCount(i);
				}
				frame.CommonData.UpdateThreadGarbageCollections = garbageCollections;
				frame.CommonData.EndOfUpdateMemory = GC.GetTotalMemory(forceFullCollection: false);
				//frame.CommonData.EndOfUpdatePhysicalMemory = Process.GetCurrentProcess().WorkingSet64;
				frame.CommonData.UpdateBodyElapsedTime =
					Stopwatch.GetTimestamp() - frame.CommonData.UpdateThreadStartTime;
				CpuUsageFinished(fullUpdateCpuUsage, Owners.Empty, CpuUsage.Reasons.FullUpdate, TypeIdentifier.Empty);
			}
			DenyInputFromThread();
		}

		private static CpuUsageStartInfo syncBlockCpuUsage;
		private static bool isSyncMainWindowTarget;
		private static long renderThreadTargetFrameIndex;

		/// <summary>
		/// Occurs at the end of update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		internal static void SyncStarted(bool isMainWindowTarget)
		{
			isSyncMainWindowTarget = isMainWindowTarget;
			if (!isMainWindowTarget) return;
			if (resetCommand.IsInitiated) {
				Initialize(resetCommand.FrameLifespan);
			}
			isRenderProfilerEnabled = isUpdateProfilerEnabled;
			// Passing the index of the current frame to the rendering thread.
			renderThreadTargetFrameIndex = instance.ProfiledFramesCount - 1;
			while (unfinishedFrames.Count > 0) {
				var frame = CalculatedFramePlace(unfinishedFrames.Peek());
				if (
					frame.DrawCommandsState == Frame.DrawCommandsExecution.Completed ||
					frame.DrawCommandsState == Frame.DrawCommandsExecution.NotSubmittedToGpu
					) {
					long frameIdentifier = unfinishedFrames.Dequeue();
					if (renderThreadTargetFrameIndex - 1 - frameIdentifier > MaxSwapchainBuffersCount) {
						throw new System.Exception("Profiler: Incorrect behavior detected!");
					}
					instance.LastAvailableFrame = frame.CommonData.Identifier;
				} else {
					break;
				}
			}
			if (isRenderProfilerEnabled) {
				syncBlockCpuUsage = CpuUsageStarted();
			}
		}

		/// <summary>
		/// Occurs at the end of update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		internal static void SyncFinishing()
		{
			if (!isSyncMainWindowTarget) return;
			if (isRenderProfilerEnabled) {
				CpuUsageFinished(syncBlockCpuUsage, Owners.Empty, CpuUsage.Reasons.SyncBodyExecution, TypeIdentifier.Empty);
			}
		}

		private static CpuUsageStartInfo fullRenderCpuUsage;
		private static bool isRenderMainWindowTarget;

		internal static void Rendering(bool isMainWindowTarget)
		{
			isRenderMainWindowTarget = isMainWindowTarget;
			if (!isMainWindowTarget) return;
			threadInfo = isRenderProfilerEnabled ? ThreadInfo.Render : ThreadInfo.Unknown;
			ownersPool = isRenderProfilerEnabled ? instance.RenderOwnersPool : null;
			const long InvalidValue = long.MaxValue;
			if (isRenderProfilerEnabled) {
				var frame = CalculatedFramePlace(renderThreadTargetFrameIndex);
				FreeResourcesForNextRender();

				frame.CommonData.RenderThreadStartTime = Stopwatch.GetTimestamp();
				frame.CommonData.RenderThreadElapsedTime = InvalidValue;

				frame.RenderCpuUsagesList = instance.RenderCpuUsagesPool.AcquireList();
				frame.DrawingGpuUsagesList = instance.GpuUsagesPool.AcquireList();
				renderResourcesQueue.Enqueue((frame.RenderCpuUsagesList, frame.DrawingGpuUsagesList));

				fullRenderCpuUsage = CpuUsageStarted();
			}
			if (renderThreadTargetFrameIndex > 0) {
				var previousFrame = CalculatedFramePlace(renderThreadTargetFrameIndex - 1);
				if (previousFrame.CommonData.RenderThreadElapsedTime == InvalidValue) {
					previousFrame.CommonData.RenderThreadElapsedTime =
						Stopwatch.GetTimestamp() - previousFrame.CommonData.RenderThreadStartTime;
				}
			}
		}

		internal static void Rendered()
		{
			if (!isRenderMainWindowTarget) return;
			if (isRenderProfilerEnabled) {
				var frame = CalculatedFramePlace(renderThreadTargetFrameIndex);
				var garbageCollections =
					frame.CommonData.RenderThreadGarbageCollections ?? new int[GC.MaxGeneration];
				for (int i = 0; i < garbageCollections.Length; i++) {
					garbageCollections[i] = GC.CollectionCount(i);
				}
				frame.CommonData.RenderThreadGarbageCollections = garbageCollections;
				frame.CommonData.EndOfRenderMemory = GC.GetTotalMemory(forceFullCollection: false);
				//frame.CommonData.EndOfRenderPhysicalMemory = Process.GetCurrentProcess().WorkingSet64;
				frame.CommonData.RenderBodyElapsedTime =
					Stopwatch.GetTimestamp() - frame.CommonData.RenderThreadStartTime;
				CpuUsageFinished(fullRenderCpuUsage, Owners.Empty, CpuUsage.Reasons.FullRender, TypeIdentifier.Empty);
			}
			DenyInputFromThread();
		}

		private static void DenyInputFromThread()
		{
			threadInfo = ThreadInfo.Unknown;
			ownersPool = null;
		}

		private static void FreeResourcesForNextUpdate(Frame frame)
		{
			if (!frame.UpdateCpuUsagesList.IsNull) {
				var usagesPool = instance.UpdateCpuUsagesPool;
				var ownersPool = instance.UpdateOwnersPool;
				foreach (var usage in usagesPool.Enumerate(frame.UpdateCpuUsagesList)) {
					if (usage.Owners.IsListDescriptor) {
						ownersPool.FreeOldestList(usage.Owners.AsListDescriptor);
					}
				}
				usagesPool.FreeOldestList(frame.UpdateCpuUsagesList);
			}
		}

		private static void FreeResourcesForNextRender()
		{
			var (cpuUsages, gpuUsages) = renderResourcesQueue.Dequeue();
			var ownersPool = instance.RenderOwnersPool;
			foreach (var usage in instance.RenderCpuUsagesPool.Enumerate(cpuUsages)) {
				if (usage.Owners.IsListDescriptor) {
					ownersPool.FreeOldestList(usage.Owners.AsListDescriptor);
				}
			}
			foreach (var usage in instance.GpuUsagesPool.Enumerate(gpuUsages)) {
				if (usage.Owners.IsListDescriptor) {
					ownersPool.FreeOldestList(usage.Owners.AsListDescriptor);
				}
			}
			instance.RenderCpuUsagesPool.FreeOldestList(cpuUsages);
			instance.GpuUsagesPool.FreeOldestList(gpuUsages);
		}

		private static void Initialize(uint frameLifespan)
		{
			threadInfo = ThreadInfo.Unknown;
			isUpdateProfilerEnabled = false;
			isRenderProfilerEnabled = false;
			frameLifespan = Math.Max(frameLifespan, MaxSwapchainBuffersCount + 1);
			instance = new ProfilerDatabase(frameLifespan);
			cpuUsagesPools[(int)ThreadInfo.Update] = instance.UpdateCpuUsagesPool;
			cpuUsagesPools[(int)ThreadInfo.Render] = instance.RenderCpuUsagesPool;
			threadDependentData[(int)ThreadInfo.Update] = new ThreadDependentData(instance.UpdateCpuUsagesPool, new Dictionary<Type, int>());
			threadDependentData[(int)ThreadInfo.Render] = new ThreadDependentData(instance.RenderCpuUsagesPool, new Dictionary<Type, int>());
			renderResourcesQueue = new Queue<(RingPool.ListDescriptor, RingPool.ListDescriptor)>((int)frameLifespan);
			profiledFrames = new Frame[frameLifespan];
			for (int i = 0; i < frameLifespan; i++) {
				var frame = new Frame();
				frame.DrawingGpuUsagesList = instance.GpuUsagesPool.AcquireList();
				frame.RenderCpuUsagesList = instance.RenderCpuUsagesPool.AcquireList();
				frame.UpdateCpuUsagesList = instance.UpdateCpuUsagesPool.AcquireList();
				profiledFrames[i] = frame;
				renderResourcesQueue.Enqueue((frame.RenderCpuUsagesList, frame.DrawingGpuUsagesList));
			}
		}

		private ProfilerDatabase(uint frameLifespan)
		{
			FrameLifespan = (int)frameLifespan;
			ProfiledFramesCount = 0;
			LastAvailableFrame = -1;
			NativeTypesTable = nativeTypesTable;
			NativeReferenceTable = new ReferenceTable();
			UpdateOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			RenderOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			UpdateCpuUsagesPool = new RingPool<CpuUsage>();
			RenderCpuUsagesPool = new RingPool<CpuUsage>();
			GpuUsagesPool = new RingPool<GpuUsage>();
		}


		private struct ThreadDependentData
		{
			private readonly Dictionary<Type, int> typesDictionary;

			public readonly RingPool<CpuUsage> CpuUsagesPool;

			public bool IsThreadSupported => CpuUsagesPool != null;

			public ThreadDependentData(RingPool<CpuUsage> cpuUsagesPool, Dictionary<Type, int> typesDictionary)
			{
				CpuUsagesPool = cpuUsagesPool;
				this.typesDictionary = typesDictionary;
			}

			public int GetTypeId(Type type)
			{
				if (!typesDictionary.TryGetValue(type, out int value)) {
					return FindAndAddToDictionary(type);
				}
				return value;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private int FindAndAddToDictionary(Type type)
			{
				int value = instance.NativeTypesTable.GetOrCreateValue(type).Value;
				typesDictionary.Add(type, value);
				return value;
			}
		}

		/// <summary>
		/// Allows you to identify the structure within one frame.
		/// </summary>
		public struct CpuUsageStartInfo
		{
			public readonly ThreadInfo ThreadInfo;
			public readonly long StartTime;

			public CpuUsageStartInfo(ThreadInfo threadInfo, long startTime)
			{
				ThreadInfo = threadInfo;
				StartTime = startTime;
			}
		}

		public enum ThreadInfo : int
		{
			Unknown = 0,
			Update = 1,
			Render = 2,
		}

		private static bool firstInitialization = true;
		private static ResetCommand resetCommand = new ResetCommand { IsInitiated = false };

		/// <summary>
		/// Allows you to reinitialize the profiler's database,
		/// the previously accumulated resources will be released.
		/// </summary>
		internal static void Reinitialize(uint frameLifespan = 500)
		{
			if (firstInitialization) {
				firstInitialization = false;
				Initialize(frameLifespan);
			} else {
				throw new NotImplementedException("Profiler: Reinitialize not implemented!");
				resetCommand = new ResetCommand { FrameLifespan = frameLifespan };
			}
		}

		private struct ResetCommand
		{
			public uint FrameLifespan;

			public bool IsInitiated
			{
				get => FrameLifespan != uint.MaxValue;
				set => FrameLifespan = value ? throw new InvalidOperationException() : uint.MaxValue;
			}
		}
	}
}

#endif // PROFILER
