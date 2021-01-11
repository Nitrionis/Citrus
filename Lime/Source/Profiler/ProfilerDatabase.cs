#if PROFILER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Lime.Profiler.Graphics;

namespace Lime.Profiler
{
	using Task = System.Threading.Tasks.Task;
	using IProfilerContext = Contexts.IProfilerContext;

	/// <summary>
	/// Performs terminal requests.
	/// </summary>
	public class ProfilerDatabase : IProfilerDatabase
	{
		/// <summary>
		/// This is a value greater than the number of buffers in swapchain.
		/// </summary>
		private const uint MaxUnfinishedFramesQueueLength = 10;

		private static ProfilerDatabase instance;

		private static IProfilerContext activeContext;
		private static volatile IProfilerContext requestedContext;

		/// <summary>
		/// Responsible for the data flow from the database to the terminal and vice versa.
		/// </summary>
		internal static IProfilerContext Context
		{
			get => activeContext;
			set => requestedContext = (value ?? throw new InvalidOperationException());
		}

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

		/// <summary>
		/// If true, you must to skip the scene update.
		/// </summary>
		public static bool MustSkipSceneUpdate => instance.IsSceneUpdateFrozen;

		/// <remarks>Access only from render thread.</remarks>
		public static bool IsBatchBreakReasonsRequired => RenderThreadScope.IsBatchBreakReasonsRequired;

		/// <remarks>
		/// We use this to ensure the correct order of resource release
		/// in cases where no rendering occurs after the update.
		/// </remarks>
		private static Queue<(RingPool.ListDescriptor, RingPool.ListDescriptor)> renderResourcesQueue;

		/// <inheritdoc/>
		public bool ProfilerEnabled
		{
			get => UpdateThreadScope.ProfilingEnabled;
			set => UpdateThreadScope.ProfilingEnabled = value;
		}

		/// <inheritdoc/>
		public bool BatchBreakReasonsRequired
		{
			get => RenderThreadScope.IsBatchBreakReasonsRequired;
			set => RenderThreadScope.IsBatchBreakReasonsRequired = value;
		}

		/// <inheritdoc/>
		public bool IsSceneUpdateFrozen =>
			UpdateThreadScope.SceneUpdateSkipOptions != UpdateSkipOptions.NoSkip;

		/// <inheritdoc/>
		public void SetSceneUpdateFrozen(UpdateSkipOptions options) =>
			UpdateThreadScope.SceneUpdateSkipOptions = options;

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

		private readonly Queue<Task> dataReadTasks;

		/// <inheritdoc/>
		public void PreventProfilingWhileRunning(Task task)
		{
			if (task.Status != TaskStatus.Created) {
				throw new InvalidOperationException("Profiler: The task should not be started!");
			}
			UpdateThreadScope.SceneUpdateSkipOptions = UpdateSkipOptions.SkipAll;
			dataReadTasks.Enqueue(task);
		}

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
			if (@object == null || threadInfo != ThreadInfo.Update) return;
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
			return new CpuUsageStartInfo(threadInfo, threadInfo == ThreadInfo.Unknown ? 0 : Stopwatch.GetTimestamp());
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, Owners owners,
			CpuUsage.Reasons reason, ITypeIdentifierProvider typeIdentifierProvider) =>
			CpuUsageFinished(usageInfo, owners, reason, typeIdentifierProvider.Identifier);

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
		/// Create and saves the CpuUsage structure in the database.
		/// </summary>
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, CpuUsage.Reasons reason)
		{
			if (usageInfo.ThreadInfo != ThreadInfo.Unknown) {
				var threadInfo = threadDependentData[(int)usageInfo.ThreadInfo];
				threadInfo.CpuUsagesPool.AddToNewestList(new CpuUsage {
					Reason = reason,
					Owners = Owners.Empty,
					TypeIdentifier = TypeIdentifier.Empty,
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

		/// <summary>
		/// Called before updating the main application window.
		/// </summary>
		internal static event Action MainWindowUpdating;

		private const long InvalidElapsedTime = long.MaxValue;

		internal static void Updating(bool isMainWindowTarget)
		{
			UpdateThreadScope.Updating(isMainWindowTarget);
			if (isMainWindowTarget) {
				MainWindowUpdating?.Invoke();
			}
		}

		internal static void Updated() => UpdateThreadScope.Updated();

		/// <summary>
		/// Occurs at the end of update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		internal static void SyncStarted() => UpdateThreadScope.SyncStarted();

		/// <summary>
		/// Occurs at the end of update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		internal static void SyncFinishing() => UpdateThreadScope.SyncFinishing();

		internal static void Rendering(bool isMainWindowTarget) => RenderThreadScope.Rendering(isMainWindowTarget);

		internal static void Rendered() => RenderThreadScope.Rendered();

		internal static void SwappingSwapchainBuffer() => RenderThreadScope.SwappingSwapchainBuffer();

		internal static void SwappedSwapchainBuffer() => RenderThreadScope.SwappedSwapchainBuffer();

		private static class UpdateThreadScope
		{
			private static bool isMainWindowTarget;
			private static long targetFrameIndex;

			private static CpuUsageStartInfo fullUpdateCpuUsage;
			private static CpuUsageStartInfo syncBlockCpuUsage;

			private static bool profilingEnabled;
			private static bool profilingRequired;

			public static bool ProfilingEnabled
			{
				get => profilingEnabled;
				set => profilingRequired = value;
			}

			private static UpdateSkipOptions currentSceneUpdateSkipOptions;
			private static UpdateSkipOptions nextSceneUpdateSkipOptions;

			public static UpdateSkipOptions SceneUpdateSkipOptions
			{
				get => currentSceneUpdateSkipOptions;
				set => nextSceneUpdateSkipOptions = value;
			}

			public static void Updating(bool isMainWindowTarget)
			{
				UpdateThreadScope.isMainWindowTarget = isMainWindowTarget;
				if (!isMainWindowTarget) {
					return;
				}
				SetNextProfilerEnabledState();
				SetNextSceneUpdateSkipOptions();
				// From Static to ThreadStatic.
				threadInfo = profilingEnabled ? ThreadInfo.Update : ThreadInfo.Unknown;
				ownersPool = instance.UpdateOwnersPool;
				if (instance.ProfiledFramesCount > 0) {
					var previousFrame = CalculatedFramePlace(instance.ProfiledFramesCount - 1);
					if (previousFrame.CommonData.UpdateThreadElapsedTicks == InvalidElapsedTime) {
						previousFrame.CommonData.UpdateThreadElapsedTicks =
							Stopwatch.GetTimestamp() - previousFrame.CommonData.UpdateThreadStartTime;
					}
				}
				if (profilingEnabled) {
					targetFrameIndex = instance.ProfiledFramesCount++;
					while (poolExpandingCpuUsages != null && poolExpandingCpuUsages.Count > 0) {
						instance.UpdateCpuUsagesPool.AddToNewestList(poolExpandingCpuUsages.Dequeue());
					}

					var frame = CalculatedFramePlace(targetFrameIndex);
					FreeResourcesForNextUpdate(frame);

					frame.CommonData = new ProfiledFrame {
						Identifier = targetFrameIndex,
						StopwatchFrequency = Stopwatch.Frequency,
						UpdateThreadElapsedTicks = InvalidElapsedTime,
						UpdateThreadStartTime = Stopwatch.GetTimestamp(),
						UpdateThreadGarbageCollections = frame.CommonData.UpdateThreadGarbageCollections,
						RenderThreadGarbageCollections = frame.CommonData.RenderThreadGarbageCollections
					};

					frame.DrawCommandsState = Frame.DrawCommandsExecution.NotSubmittedToGpu;
					frame.UpdateCpuUsagesList = instance.UpdateCpuUsagesPool.AcquireList();
					frame.RenderCpuUsagesList = RingPool.ListDescriptor.Null;
					frame.DrawingGpuUsagesList = RingPool.ListDescriptor.Null;

					fullUpdateCpuUsage = CpuUsageStarted();
					unfinishedFrames.Enqueue(frame.CommonData.Identifier);
				}
				if (activeContext != requestedContext) {
					activeContext?.Detached();
					activeContext = requestedContext;
					activeContext.Attached(instance);
				}
				activeContext?.MainWindowUpdating();
			}

			public static void Updated()
			{
				if (!isMainWindowTarget) return;
				var nativeReferenceTable = instance.NativeReferenceTable;
				// 10000 is just some value that adjusts the frequency of garbage collection.
				if (nativeReferenceTable.NewDescriptionsCount > 10000) {
					Console.WriteLine("Profiler ReferenceTable garbage collection starting!");
					long minFrameIndexToStayAlive = targetFrameIndex - instance.FrameLifespan - 1;
					nativeReferenceTable.CollectGarbage(minFrameIndexToStayAlive);
				}
				if (profilingEnabled) {
					var frame = CalculatedFramePlace(targetFrameIndex);
					var garbageCollections =
						frame.CommonData.UpdateThreadGarbageCollections ?? new int[GC.MaxGeneration + 1];
					for (int i = 0; i < garbageCollections.Length; i++) {
						garbageCollections[i] = GC.CollectionCount(i);
					}
					frame.CommonData.UpdateThreadGarbageCollections = garbageCollections;
					frame.CommonData.EndOfUpdateMemory = GC.GetTotalMemory(forceFullCollection: false);
					// It takes a long time to get this parameter.
					//frame.CommonData.EndOfUpdatePhysicalMemory = Process.GetCurrentProcess().WorkingSet64;
					frame.CommonData.UpdateBodyElapsedTicks =
						Stopwatch.GetTimestamp() - frame.CommonData.UpdateThreadStartTime;
					CpuUsageFinished(fullUpdateCpuUsage, CpuUsage.Reasons.FullUpdate);
				}
				DenyInputFromThread();
			}

			private static void SetNextProfilerEnabledState()
			{
				var dataReadTasks = instance.dataReadTasks;
				while (dataReadTasks.Count > 0) {
					var task = dataReadTasks.Peek();
					long framesCount = instance.ProfiledFramesCount;
					long lastFrame = instance.LastAvailableFrame;
					if (task.Status == TaskStatus.Created && framesCount == lastFrame + 1) {
						task.Start();
						break;
					}
					if (!task.IsCompleted) {
						break;
					}
					dataReadTasks.Dequeue();
				}
				profilingEnabled = profilingRequired && dataReadTasks.Count == 0;
			}

			public static void SyncStarted()
			{
				if (!isMainWindowTarget) {
					return;
				}
				// Passing the index of the current frame to the rendering thread.
				RenderThreadScope.SetNextTargetFrameIndex(targetFrameIndex);
				RenderThreadScope.ProfilingEnabled = profilingEnabled;
				if (targetFrameIndex > 1) {
					// Handle the case when the frame was not rendered.
					var frame = CalculatedFramePlace(targetFrameIndex - 2);
					if (frame.CommonData.RenderThreadElapsedTicks == InvalidElapsedTime) {
						frame.CommonData.RenderThreadElapsedTicks = frame.CommonData.RenderBodyElapsedTicks;
					}
				}
				while (unfinishedFrames.Count > 0) {
					if (unfinishedFrames.Count > MaxUnfinishedFramesQueueLength) {
						throw new System.Exception("Profiler: Incorrect behavior detected!");
					}
					var frame = CalculatedFramePlace(unfinishedFrames.Peek());
					if (
						frame.CommonData.UpdateThreadElapsedTicks != InvalidElapsedTime &&
						frame.CommonData.RenderThreadElapsedTicks != InvalidElapsedTime && (
						frame.DrawCommandsState == Frame.DrawCommandsExecution.Completed ||
						frame.DrawCommandsState == Frame.DrawCommandsExecution.NotSubmittedToGpu)
						)
					{
						instance.LastAvailableFrame = unfinishedFrames.Dequeue();
					} else {
						break;
					}
				}
				if (profilingEnabled) {
					syncBlockCpuUsage = CpuUsageStarted();
				}
			}

			public static void SyncFinishing()
			{
				if (!isMainWindowTarget) {
					return;
				}
				if (profilingEnabled) {
					CpuUsageFinished(syncBlockCpuUsage, CpuUsage.Reasons.SyncBodyExecution);
				}
			}

			private static void SetNextSceneUpdateSkipOptions()
			{
				if (nextSceneUpdateSkipOptions == UpdateSkipOptions.SkipAllAfterNext) {
					nextSceneUpdateSkipOptions = UpdateSkipOptions.SkipAll;
					currentSceneUpdateSkipOptions = UpdateSkipOptions.NoSkip;
				} else {
					currentSceneUpdateSkipOptions = nextSceneUpdateSkipOptions;
				}
			}
		}

		private static class RenderThreadScope
		{
			private static bool isMainWindowTarget;
			private static long targetFrameIndex;
			private static long previousTargetFrameIndex;

			private static CpuUsageStartInfo fullRenderCpuUsage;
			private static CpuUsageStartInfo swapBufferCpuUsage;

			private static bool profilingEnabled;
			private static bool profilingRequired;

			public static bool ProfilingEnabled
			{
				get => profilingEnabled;
				set => profilingRequired = value;
			}

			public static bool ProfilingRequired { get; set; }

			private static bool batchBreakReasonsRequired;
			private static bool batchBreakReasonsEnabled;

			public static bool IsBatchBreakReasonsRequired
			{
				get => batchBreakReasonsEnabled;
				set => batchBreakReasonsRequired = value;
			}

			public static void SetNextTargetFrameIndex(long index) => targetFrameIndex = index;

			public static void Rendering(bool isMainWindowTarget)
			{
				RenderThreadScope.isMainWindowTarget = isMainWindowTarget;
				if (!isMainWindowTarget) {
					return;
				}
				RenderBatchStatistics.Reset();
				profilingEnabled = profilingRequired;
				batchBreakReasonsEnabled = batchBreakReasonsRequired;
				if (targetFrameIndex > 0) {
					int offset = previousTargetFrameIndex == targetFrameIndex ? 0 : 1;
					var previousFrame = CalculatedFramePlace(targetFrameIndex - offset);
					if (previousFrame.CommonData.RenderThreadElapsedTicks == InvalidElapsedTime) {
						previousFrame.CommonData.RenderThreadElapsedTicks =
							Stopwatch.GetTimestamp() - previousFrame.CommonData.RenderThreadStartTime;
					}
				}
				threadInfo = profilingEnabled ? ThreadInfo.Render : ThreadInfo.Unknown;
				ownersPool = profilingEnabled ? instance.RenderOwnersPool : null;
				batchBreakReasonsEnabled = batchBreakReasonsRequired;
				if (profilingEnabled) {
					while (poolExpandingCpuUsages != null && poolExpandingCpuUsages.Count > 0) {
						instance.UpdateCpuUsagesPool.AddToNewestList(poolExpandingCpuUsages.Dequeue());
					}

					var frame = CalculatedFramePlace(targetFrameIndex);
					FreeResourcesForNextRender();

					frame.CommonData.RenderThreadStartTime = Stopwatch.GetTimestamp();
					frame.CommonData.RenderThreadElapsedTicks = InvalidElapsedTime;

					frame.RenderCpuUsagesList = instance.RenderCpuUsagesPool.AcquireList();
					frame.DrawingGpuUsagesList = instance.GpuUsagesPool.AcquireList();
					renderResourcesQueue.Enqueue((frame.RenderCpuUsagesList, frame.DrawingGpuUsagesList));

					fullRenderCpuUsage = CpuUsageStarted();
				}
				previousTargetFrameIndex = targetFrameIndex;
			}

			public static void Rendered()
			{
				if (!isMainWindowTarget) {
					return;
				}
				if (profilingEnabled) {
					var frame = CalculatedFramePlace(targetFrameIndex);
					var garbageCollections =
						frame.CommonData.RenderThreadGarbageCollections ?? new int[GC.MaxGeneration + 1];
					for (int i = 0; i < garbageCollections.Length; i++) {
						garbageCollections[i] = GC.CollectionCount(i);
					}
					frame.CommonData.RenderThreadGarbageCollections = garbageCollections;
					frame.CommonData.EndOfRenderMemory = GC.GetTotalMemory(forceFullCollection: false);
					// It takes a long time to get this parameter.
					//frame.CommonData.EndOfRenderPhysicalMemory = Process.GetCurrentProcess().WorkingSet64;
					frame.CommonData.RenderBodyElapsedTicks =
						Stopwatch.GetTimestamp() - frame.CommonData.RenderThreadStartTime;
					frame.CommonData.FullSavedByBatching = RenderBatchStatistics.FullSavedByBatching;
					frame.CommonData.SceneSavedByBatching = RenderBatchStatistics.SceneSavedByBatching;
					var rendererStatistics = PlatformRendererStatistics.Instance;
					frame.CommonData.FullDrawCallCount = rendererStatistics.FullDrawCallsCount;
					frame.CommonData.SceneDrawCallCount = rendererStatistics.SceneDrawCallsCount;
					frame.CommonData.FullVerticesCount = rendererStatistics.FullVertexCount;
					frame.CommonData.SceneVerticesCount = rendererStatistics.SceneVertexCount;
					frame.CommonData.FullTrianglesCount = rendererStatistics.FullTrianglesCount;
					frame.CommonData.SceneTrianglesCount = rendererStatistics.SceneTrianglesCount;

					CpuUsageFinished(fullRenderCpuUsage, CpuUsage.Reasons.FullRender);
				}
				DenyInputFromThread();
			}

			public static void SwappingSwapchainBuffer() =>
				swapBufferCpuUsage = new CpuUsageStartInfo(threadInfo, Stopwatch.GetTimestamp());

			public static void SwappedSwapchainBuffer()
			{
				if (profilingEnabled && isMainWindowTarget) {
					var frame = CalculatedFramePlace(targetFrameIndex);
					if (swapBufferCpuUsage.ThreadInfo == ThreadInfo.Render) {
						CpuUsageFinished(swapBufferCpuUsage, CpuUsage.Reasons.WaitForPreviousRendering);
					}
					frame.CommonData.WaitForAcquiringSwapchainBuffer =
						Stopwatch.GetTimestamp() - swapBufferCpuUsage.StartTime;
				}
			}
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
						var list = usage.Owners.AsListDescriptor;
						if (!list.IsNull) {
							ownersPool.FreeOldestList(list);
						}
					}
				}
				usagesPool.FreeOldestList(frame.UpdateCpuUsagesList);
			}
		}

		private static void FreeResourcesForNextRender()
		{
			void FreeOwnersList(Owners owners) {
				if (owners.IsListDescriptor) {
					var list = owners.AsListDescriptor;
					if (!list.IsNull) {
						instance.RenderOwnersPool.FreeOldestList(list);
					}
				}
			}
			var (cpuUsages, gpuUsages) = renderResourcesQueue.Dequeue();
			foreach (var usage in instance.RenderCpuUsagesPool.Enumerate(cpuUsages)) {
				FreeOwnersList(usage.Owners);
			}
			foreach (var usage in instance.GpuUsagesPool.Enumerate(gpuUsages)) {
				FreeOwnersList(usage.Owners);
			}
			instance.RenderCpuUsagesPool.FreeOldestList(cpuUsages);
			instance.GpuUsagesPool.FreeOldestList(gpuUsages);
		}

		private static void Initialize(uint frameLifespan)
		{
			threadInfo = ThreadInfo.Unknown;
			frameLifespan = Math.Max(frameLifespan, MaxUnfinishedFramesQueueLength + 1);
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
			UpdateOwnersPool.InternalStorageExpanding += OwnersPoolExpanding;
			UpdateOwnersPool.InternalStorageExpanded += OwnersPoolExpanded;
			RenderOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			RenderOwnersPool.InternalStorageExpanding += OwnersPoolExpanding;
			RenderOwnersPool.InternalStorageExpanded += OwnersPoolExpanded;
			UpdateCpuUsagesPool = new RingPool<CpuUsage>();
			UpdateCpuUsagesPool.InternalStorageExpanding += UsagesPoolExpanding;
			UpdateCpuUsagesPool.InternalStorageExpanded += UsagesPoolExpanded;
			RenderCpuUsagesPool = new RingPool<CpuUsage>();
			RenderCpuUsagesPool.InternalStorageExpanding += UsagesPoolExpanding;
			RenderCpuUsagesPool.InternalStorageExpanded += UsagesPoolExpanded;
			GpuUsagesPool = new RingPool<GpuUsage>();
			dataReadTasks = new Queue<Task>();
		}

		[ThreadStatic]
		private static Queue<CpuUsage> poolExpandingCpuUsages;

		[ThreadStatic]
		private static long poolExpandingTimestamp;

		private static void PoolExpanded()
		{
			if (poolExpandingCpuUsages == null) {
				poolExpandingCpuUsages = new Queue<CpuUsage>();
			}
			poolExpandingCpuUsages.Enqueue(new CpuUsage {
				Reason = CpuUsage.Reasons.ProfilerDatabaseResizing,
				Owners = Owners.Empty,
				TypeIdentifier = TypeIdentifier.Empty,
				StartTime = poolExpandingTimestamp,
				FinishTime = Stopwatch.GetTimestamp()
			});
		}

		private void OwnersPoolExpanding() => poolExpandingTimestamp = Stopwatch.GetTimestamp();

		private void OwnersPoolExpanded(int capacity)
		{
			if (capacity >= 500_000) {
				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			}
			PoolExpanded();
		}

		private void UsagesPoolExpanding() => poolExpandingTimestamp = Stopwatch.GetTimestamp();

		private void UsagesPoolExpanded(int capacity)
		{
			if (capacity >= 262_144) {
				GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
			}
			PoolExpanded();
		}

		private struct ThreadDependentData
		{
			private readonly Dictionary<Type, int> typesDictionary;

			public readonly RingPool<CpuUsage> CpuUsagesPool;
			
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
			}
		}
	}

	public enum UpdateSkipOptions
	{
		NoSkip,
		SkipAllAfterNext,
		SkipAll
	}
}

#endif // PROFILER
