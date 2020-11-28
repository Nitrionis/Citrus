#if PROFILER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleToAttribute("ProfilerUnitTests")]

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

		[ThreadStatic]
		private static bool isDataCollectionAllowed;

		/// <summary>
		/// Indicates whether data collection is allowed on the current thread.
		/// </summary>
		/// <remarks>
		/// <para>Essentially also indicates whether the profiler is enabled or not.</para>
		/// <para>The value is set at the time of launching some process (update or rendering).</para>
		/// </remarks>
		public static bool IsDataCollectionAllowed => isDataCollectionAllowed;

		/// <summary>
		/// It lets you know whether the collection of data is allowed to render thread.
		/// </summary>
		public static bool IsRenderThreadDataCollectionAllowed { get; private set; }

		private static bool isUpdateThreadDataCollectionRequired;

		/// <summary>
		/// Allows you to enable and disable the profiler.
		/// </summary>
		/// <remarks>
		/// Changes will take effect from the next frame.
		/// </remarks>
		public static bool IsDataCollectionRequired { get; set; } = true;

		/// <summary>
		/// Indicates whether the main application window is currently processing or not.
		/// </summary>
		/// <remarks>
		/// Since several windows will be processed sequentially, it is possible
		/// to transmit through static variable information about whether the main
		/// window is currently processing or not.
		/// </remarks>
		public static bool IsMainWindowUpdateTarget { get; private set; }

		/// <summary>
		/// Indicates whether the main application window is currently processing or not.
		/// </summary>
		/// <remarks>
		/// Since several windows will be processed sequentially, it is possible
		/// to transmit through static variable information about whether the main
		/// window is currently processing or not.
		/// </remarks>
		public static bool IsMainWindowRenderTarget { get; private set; }

		private static long renderThreadTargetFrameIndex;

		[ThreadStatic]
		private static RingPool<ReferenceTable.RowIndex> ownersPool;

		/// <summary>
		/// Provides access to the owners pool for the current thread.
		/// </summary>
		public static RingPool<ReferenceTable.RowIndex> OwnersPool => ownersPool;

		/// <inheritdoc/>
		public int FrameLifespan { get; private set; }

		/// <inheritdoc/>
		public long ProfiledFramesCount { get; private set; }

		/// <inheritdoc/>
		public long LastAvailableFrame { get; private set; }

		/// <inheritdoc/>
		public TypesTable NativeTypesTable { get; private set; }

		/// <inheritdoc/>
		public ReferenceTable NativeReferenceTable { get; private set; }

		/// <inheritdoc/>
		public RingPool<ReferenceTable.RowIndex> UpdateOwnersPool { get; private set; }

		/// <inheritdoc/>
		public RingPool<ReferenceTable.RowIndex> RenderOwnersPool { get; private set; }

		/// <inheritdoc/>
		public RingPool<CpuUsage> UpdateCpuUsagesPool { get; private set; }

		/// <inheritdoc/>
		public RingPool<CpuUsage> RenderCpuUsagesPool { get; private set; }

		/// <inheritdoc/>
		public RingPool<GpuUsage> GpuUsagesPool { get; private set; }

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
			ProfiledFrames[identifier % ProfiledFrames.Length];

		/// <summary>
		/// Frame indexes for which profiling data collection has not been completed.
		/// </summary>
		private static readonly Queue<long> unfinishedFrames = new Queue<long>();

		/// <summary>
		/// A pool that contains general profiling data for each frame.
		/// </summary>
		internal static Frame[] ProfiledFrames { get; private set; }

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
		/// <remarks>
		/// <para>Can be called from render and update threads.</para>
		/// <para>Must end with a <see cref="CpuUsageFinished"/> method call.</para>
		/// </remarks>
		public static CpuUsageStartInfo CpuUsageStarted()
		{
			var threadInfo = ProfilerDatabase.threadInfo;
			if (threadInfo != ThreadInfo.Unknown) {
				return new CpuUsageStartInfo {
					ThreadInfo = threadInfo,
					StartTime = Stopwatch.GetTimestamp()
				};
			} else {
				return new CpuUsageStartInfo {
					ThreadInfo = threadInfo
				};
			}
		}

		/// <param name="targetType">The type of object for which the work is performed.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, IProfileableObject @object, CpuUsage.Reasons reason, Type targetType) =>
			CpuUsageFinished(usageInfo, @object == null ? Owners.Empty : new Owners(@object.RowIndex), reason, targetType);

		/// <param name="targetTypeId">The number of type of object for which the work is performed.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, IProfileableObject @object, CpuUsage.Reasons reason, int targetTypeId) =>
			CpuUsageFinished(usageInfo, @object == null ? Owners.Empty : new Owners(@object.RowIndex), reason, targetTypeId);

		/// <summary>
		/// Records the end time of CPU usage.
		/// </summary>
		/// <param name="targetTypeId">The number of type of object for which the work is performed.</param>
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, Owners owners, CpuUsage.Reasons reason, int targetTypeId)
		{
			if (usageInfo.ThreadInfo != ThreadInfo.Unknown) {
				var pool = cpuUsagesPools[(int)usageInfo.ThreadInfo];
				pool.AddToNewestList(new CpuUsage {
					Reason = reason,
					Owners = owners,
					TypeId = targetTypeId,
					StartTime = usageInfo.StartTime,
					FinishTime = Stopwatch.GetTimestamp()
				});
			}
		}

		/// <summary>
		/// Records the end time of CPU usage.
		/// </summary>
		/// <param name="targetTypeId">The number of type of object for which the work is performed.</param>
		public static void CpuUsageFinished(CpuUsageStartInfo usageInfo, Owners owners, CpuUsage.Reasons reason, Type targetType)
		{
			if (usageInfo.ThreadInfo != ThreadInfo.Unknown) {
				var threadInfo = threadDependentData[(int)usageInfo.ThreadInfo];
				threadInfo.CpuUsagesPool.AddToNewestList(new CpuUsage {
					Reason = reason,
					Owners = owners,
					TypeId = threadInfo.GetTypeId(targetType),
					StartTime = usageInfo.StartTime,
					FinishTime = Stopwatch.GetTimestamp()
				});
			}
		}

		/// <remarks>
		/// Store thread-dependent data in arrays to access them by thread index.
		/// </remarks>
		private static readonly RingPool<CpuUsage>[] cpuUsagesPools = new RingPool<CpuUsage>[3];

		private static readonly ThreadDependentData[] threadDependentData = new ThreadDependentData[3];

		private struct ThreadDependentData
		{
			public readonly RingPool<CpuUsage> CpuUsagesPool;
			public readonly Dictionary<Type, int> TypesDictionary;

			public bool IsThreadSupported => CpuUsagesPool != null;

			public ThreadDependentData(RingPool<CpuUsage> cpuUsagesPools, Dictionary<Type, int> typesDictionary)
			{
				CpuUsagesPool = cpuUsagesPools;
				TypesDictionary = typesDictionary;
			}

			public int GetTypeId(Type type)
			{
				if (!TypesDictionary.TryGetValue(type, out int value)) {
					return FindAndAddToDictionary(type);
				}
				return value;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private int FindAndAddToDictionary(Type type)
			{
				int value = TypeNumberTable.GetOrCreateValue(type).Value;
				TypesDictionary.Add(type, value);
				return value;
			}
		}

		/// <summary>
		/// Used to get type by number.
		/// </summary>
		internal static ConditionalWeakTable<Type, TypeId> TypeNumberTable = new ConditionalWeakTable<Type, TypeId>();

		/// <summary>
		/// Ensures that the specified type will be assigned some number.
		/// </summary>
		public static int EnsureNumberFor(Type type) => TypeNumberTable.GetOrCreateValue(type).Value;



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

		private static CpuUsageStartInfo fullUpdateCpuUsage;

		internal static void Updating(bool isMainWindowTarget)
		{
			IsMainWindowUpdateTarget = isMainWindowTarget;
			isUpdateThreadDataCollectionRequired = IsDataCollectionRequired;
			// From Static to ThreadStatic.
			isDataCollectionAllowed = isUpdateThreadDataCollectionRequired && IsMainWindowUpdateTarget;
			threadInfo = isDataCollectionAllowed ? ThreadInfo.Update : ThreadInfo.Unknown;
			ownersPool = instance.UpdateOwnersPool;
			const uint InvalidValue = uint.MaxValue;
			if (isDataCollectionAllowed) {
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
			//var sw = Stopwatch.StartNew();
			var nativeReferenceTable = instance.NativeReferenceTable;
			// 10000 is just some value that adjusts the frequency of garbage collection.
			if (nativeReferenceTable.NewDescriptionsCount > 10000) {
				Console.WriteLine("Profiler ReferenceTable garbage collection starting!");
				long minFrameIndexToStayAlive =
					instance.ProfiledFramesCount - instance.FrameLifespan - 2;
				nativeReferenceTable.CollectGarbage(minFrameIndexToStayAlive);
			}
			if (isDataCollectionAllowed) {
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
				CpuUsageFinished(fullUpdateCpuUsage, Owners.Empty, CpuUsage.Reasons.FullUpdate, -1);
			}
			DenyInputFromThread();
			//Console.WriteLine("{0}", sw.Elapsed.TotalMilliseconds);
		}

		private static CpuUsageStartInfo syncBlockCpuUsage;

		/// <summary>
		/// Occurs at the end of update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		internal static void SyncStarted()
		{
			if (resetCommand.IsInitiated) {
				Initialize(resetCommand.FrameLifespan);
			}
			IsMainWindowRenderTarget = IsMainWindowUpdateTarget;
			IsRenderThreadDataCollectionAllowed = isUpdateThreadDataCollectionRequired && IsMainWindowRenderTarget;
			// Passing the index of the current frame to the rendering thread.
			renderThreadTargetFrameIndex = instance.ProfiledFramesCount - 1;
			while (unfinishedFrames.Count > 0) {
				var frame = CalculatedFramePlace(unfinishedFrames.Peek());
				if (
					frame.DrawCommandsState == Frame.DrawCommandsExecution.Completed ||
					frame.DrawCommandsState == Frame.DrawCommandsExecution.NotSubmittedToGpu
					)
				{
					long frameIdentifier = unfinishedFrames.Dequeue();
					if (renderThreadTargetFrameIndex - 1 - frameIdentifier > MaxSwapchainBuffersCount) {
						throw new System.Exception("Profiler: Incorrect behavior detected!");
					}
					instance.LastAvailableFrame = frame.CommonData.Identifier;
				} else {
					break;
				}
			}
			if (IsRenderThreadDataCollectionAllowed) {
				syncBlockCpuUsage = CpuUsageStarted();
			}
		}

		/// <summary>
		/// Occurs at the end of update and before render. The previous render is guaranteed to be completed.
		/// </summary>
		internal static void SyncFinishing()
		{
			if (IsRenderThreadDataCollectionAllowed) {
				CpuUsageFinished(syncBlockCpuUsage, Owners.Empty, CpuUsage.Reasons.SyncBodyExecution, -1);
			}
		}

		private static CpuUsageStartInfo fullRenderCpuUsage;

		internal static void Rendering()
		{
			isDataCollectionAllowed = IsRenderThreadDataCollectionAllowed;
			threadInfo = isDataCollectionAllowed ? ThreadInfo.Render : ThreadInfo.Unknown;
			ownersPool = instance.RenderOwnersPool;
			const uint InvalidValue = uint.MaxValue;
			if (isDataCollectionAllowed) {
				var frame = CalculatedFramePlace(renderThreadTargetFrameIndex);
				FreeResourcesForNextRender();

				frame.CommonData.RenderThreadStartTime = Stopwatch.GetTimestamp();
				frame.CommonData.RenderThreadElapsedTime = InvalidValue;

				frame.RenderCpuUsagesList = instance.RenderCpuUsagesPool.AcquireList();
				frame.DrawingGpuUsagesList = instance.GpuUsagesPool.AcquireList();

				drawcallsData.Enqueue(new DrawcallsData {
					CpuUsages = frame.RenderCpuUsagesList,
					GpuUsages = frame.DrawingGpuUsagesList
				});
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
			if (isDataCollectionAllowed) {
				var frame = CalculatedFramePlace(renderThreadTargetFrameIndex);
				var garbageCollections =
					frame.CommonData.RenderThreadGarbageCollections ?? new int[3];
				for (int i = 0; i < garbageCollections.Length; i++) {
					garbageCollections[i] = GC.CollectionCount(i);
				}
				frame.CommonData.RenderThreadGarbageCollections = garbageCollections;
				frame.CommonData.EndOfRenderMemory = GC.GetTotalMemory(forceFullCollection: false);
				//frame.CommonData.EndOfRenderPhysicalMemory = Process.GetCurrentProcess().WorkingSet64;
				frame.CommonData.RenderBodyElapsedTime =
					Stopwatch.GetTimestamp() - frame.CommonData.RenderThreadStartTime;
				CpuUsageFinished(fullRenderCpuUsage, Owners.Empty, CpuUsage.Reasons.FullRender, -1);
			}
			DenyInputFromThread();
		}

		private static void DenyInputFromThread()
		{
			isDataCollectionAllowed = false;
			threadInfo = ThreadInfo.Unknown;
			ownersPool = null;
		}

		private static void FreeResourcesForNextUpdate(Frame frame)
		{
			if (!frame.UpdateCpuUsagesList.IsNull) {
				var usagesPool = instance.UpdateCpuUsagesPool;
				var ownersPool = instance.UpdateOwnersPool;
				//Console.WriteLine("{0}", usagesPool.GetLength(frame.UpdateCpuUsagesList));
				foreach (var usage in usagesPool.Enumerate(frame.UpdateCpuUsagesList)) {
					if (usage.Owners.IsListDescriptor) {
						ownersPool.FreeOldestList(usage.Owners.AsListDescriptor);
					}
				}
				usagesPool.FreeOldestList(frame.UpdateCpuUsagesList);
			}
		}

		private struct DrawcallsData
		{
			public RingPool.ListDescriptor CpuUsages;
			public RingPool.ListDescriptor GpuUsages;
		}

		/// <summary>
		/// We use this to ensure the correct order of resource release
		/// in cases where no rendering occurs after the update.
		/// </summary>
		private static Queue<DrawcallsData> drawcallsData;

		private static void FreeResourcesForNextRender()
		{
			var outdated = drawcallsData.Dequeue();
			var ownersPool = instance.RenderOwnersPool;
			foreach (var usage in instance.RenderCpuUsagesPool.Enumerate(outdated.CpuUsages)) {
				if (usage.Owners.IsListDescriptor) {
					ownersPool.FreeOldestList(usage.Owners.AsListDescriptor);
				}
			}
			foreach (var usage in instance.GpuUsagesPool.Enumerate(outdated.GpuUsages)) {
				if (usage.Owners.IsListDescriptor) {
					ownersPool.FreeOldestList(usage.Owners.AsListDescriptor);
				}
			}
			instance.RenderCpuUsagesPool.FreeOldestList(outdated.CpuUsages);
			instance.GpuUsagesPool.FreeOldestList(outdated.GpuUsages);
		}

		private static void Initialize(uint frameLifespan)
		{
			threadInfo = ThreadInfo.Unknown;
			isDataCollectionAllowed = false;
			frameLifespan = Math.Max(frameLifespan, MaxSwapchainBuffersCount + 1);
			instance = new ProfilerDatabase(frameLifespan);
			IsMainWindowUpdateTarget = false;
			IsMainWindowRenderTarget = false;
			cpuUsagesPools[(int)ThreadInfo.Update] = instance.UpdateCpuUsagesPool;
			cpuUsagesPools[(int)ThreadInfo.Render] = instance.RenderCpuUsagesPool;
			threadDependentData[(int)ThreadInfo.Update] = new ThreadDependentData(instance.UpdateCpuUsagesPool, new Dictionary<Type, int>());
			threadDependentData[(int)ThreadInfo.Render] = new ThreadDependentData(instance.RenderCpuUsagesPool, new Dictionary<Type, int>());
			drawcallsData = new Queue<DrawcallsData>((int)frameLifespan);
			ProfiledFrames = new Frame[frameLifespan];
			for (int i = 0; i < frameLifespan; i++) {
				var frame = new Frame();
				frame.DrawingGpuUsagesList = instance.GpuUsagesPool.AcquireList();
				frame.RenderCpuUsagesList = instance.RenderCpuUsagesPool.AcquireList();
				frame.UpdateCpuUsagesList = instance.UpdateCpuUsagesPool.AcquireList();
				ProfiledFrames[i] = frame;
				drawcallsData.Enqueue(new DrawcallsData {
					CpuUsages = frame.RenderCpuUsagesList,
					GpuUsages = frame.DrawingGpuUsagesList
				});
			}
		}

		private ProfilerDatabase(uint frameLifespan)
		{
			FrameLifespan = (int)frameLifespan;
			ProfiledFramesCount = 0;
			LastAvailableFrame = -1;
			NativeReferenceTable = new ReferenceTable();
			UpdateOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			RenderOwnersPool = new RingPool<ReferenceTable.RowIndex>();
			UpdateCpuUsagesPool = new RingPool<CpuUsage>();
			RenderCpuUsagesPool = new RingPool<CpuUsage>();
			GpuUsagesPool = new RingPool<GpuUsage>();
		}

		internal class TypeId
		{
			private static int counter;
			public readonly int Value;
			public TypeId() => Value = Interlocked.Increment(ref counter);
		}

		/// <summary>
		/// Allows you to identify the structure within one frame.
		/// </summary>
		public struct CpuUsageStartInfo
		{
			public ThreadInfo ThreadInfo;
			public long StartTime;
		}

		public enum ThreadInfo : int
		{
			Unknown = 0,
			Update = 1,
			Render = 2,
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
