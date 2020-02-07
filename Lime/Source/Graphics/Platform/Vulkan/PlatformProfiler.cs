using System;
using System.Collections.Generic;

namespace Lime.Graphics.Platform.Vulkan
{
	/// <summary>
	/// PlatformProfiler implementation for Vulkan.
	/// </summary>
	internal unsafe class PlatformProfiler : Platform.PlatformProfiler
	{
		/// <summary>
		/// Reserved place for the start and end timestamp of the frame.
		/// </summary>
		private const int TimestampsReserved = 2;

		/// <summary>
		/// Wrap each draw call with three timestamps:
		/// <list type="bullet">
		/// <item><description>Start of the rendering of the draw call.</description></item>
		/// <item><description>All previous draw calls have completed execution.</description></item>
		/// <item><description>The draw call completed execution.</description></item>
		/// </list>
		/// </summary>
		private const int TimestampsPerDrawCall = 3;

		private readonly SharpVulkan.Device device;
		private SharpVulkan.CommandBuffer commandBuffer;

		private class PoolInfo
		{
			public SharpVulkan.QueryPool Handle;
			public ulong FrameCompletedFenceValue;
			public long ProfiledFrameIndex;
			public int Capacity;
		}

		private Queue<PoolInfo> freeQueryPools;
		private Queue<PoolInfo> recordingQueryPools;
		private PoolInfo currentPool;

		private readonly double timestampPeriod;
		private readonly ulong timestampValidBitsMask;

		private ulong[] timestamps;
		private uint nextTimestampIndex = TimestampsReserved;

		private readonly bool isSupported;
		private bool isDeepProfilingEnabled = false;
		private bool deepProfilingNextState = false;

		public override bool IsDeepProfiling
		{
			get => isDeepProfilingEnabled;
			set => deepProfilingNextState = value && isSupported;
		}

		private bool isDrawCallDeepProfilingStarted;

		public PlatformProfiler(
				SharpVulkan.Device device,
				SharpVulkan.PhysicalDeviceLimits limits,
				SharpVulkan.QueueFamilyProperties queueFamilyProperties
			)
		{
			this.device = device;
			isSupported = 0 < queueFamilyProperties.TimestampValidBits;

			timestampPeriod = limits.TimestampPeriod;
			timestampValidBitsMask = ulong.MaxValue >> (64 - (int)queueFamilyProperties.TimestampValidBits);
			timestamps = new ulong[TimestampsReserved + DrawCallBufferStartSize * TimestampsPerDrawCall];

			freeQueryPools = new Queue<PoolInfo>();
			recordingQueryPools = new Queue<PoolInfo>();
		}

		private PoolInfo AcquireQueryPoolWithoutFence(long profiledFrameIndex, int minCapacity)
		{
			var poolInfo = freeQueryPools.Count > 0 ? freeQueryPools.Dequeue() : new PoolInfo();
			if (poolInfo.Handle == null) {
				poolInfo.Handle = CreateQueryPool((uint)minCapacity);
				poolInfo.Capacity = timestamps.Length;
			} else if (poolInfo.Capacity < timestamps.Length) {
				device.DestroyQueryPool(poolInfo.Handle);
				poolInfo.Handle = CreateQueryPool((uint)minCapacity);
				poolInfo.Capacity = timestamps.Length;
			}
			poolInfo.ProfiledFrameIndex = profiledFrameIndex;
			return poolInfo;
		}

		private void EnqueuePoolToRecording(PoolInfo pool, ulong frameCompletedFenceValue)
		{
			pool.FrameCompletedFenceValue = frameCompletedFenceValue;
			recordingQueryPools.Enqueue(pool);
		}

		private SharpVulkan.QueryPool CreateQueryPool(uint size)
		{
			var createInfo = new SharpVulkan.QueryPoolCreateInfo {
				StructureType = SharpVulkan.StructureType.QueryPoolCreateInfo,
				QueryType = SharpVulkan.QueryType.Timestamp,
				QueryCount = size
			};
			return device.CreateQueryPool(ref createInfo);
		}

		internal override void FrameRenderStarted(bool isMainWindowTarget)
		{
			base.FrameRenderStarted(isMainWindowTarget);
			isProfilingEnabled &= isSupported && currentPool != null;
		}

		public void FirstTimestamp(SharpVulkan.CommandBuffer commandBuffer)
		{
			if (isProfilingEnabled) {
				this.commandBuffer = commandBuffer;
				commandBuffer.ResetQueryPool(currentPool.Handle, 0, (uint)timestamps.Length);
				commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, currentPool.Handle, 0);
			}
		}

		public void LastTimestamp()
		{
			if (isProfilingEnabled) {
				commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, currentPool.Handle, 1);
			}
		}

		public void DrawCallStart(ProfilingInfo profilingInfo, int vertexCount, PrimitiveTopology topology)
		{
			if (isProfilingEnabled) {
				resultsBuffer.FullVerticesCount += vertexCount;
				int trianglesCount = CalculateTrianglesCount(vertexCount, topology);
				resultsBuffer.FullTrianglesCount += trianglesCount;
				if (profilingInfo.IsPartOfScene) {
					resultsBuffer.SceneDrawCallCount++;
					resultsBuffer.SceneVerticesCount += vertexCount;
					resultsBuffer.SceneTrianglesCount += trianglesCount;
				}
				isDrawCallDeepProfilingStarted =
					isDeepProfilingEnabled &&
					nextTimestampIndex < timestamps.Length - TimestampsPerDrawCall;
				if (isDrawCallDeepProfilingStarted) {
					var profilingResult = ProfilingResult.Acquire();
					profilingResult.ProfilingInfo = profilingInfo;
					profilingResult.VerticesCount = vertexCount;
					profilingResult.TrianglesCount = trianglesCount;
					profilingResult.RenderPassIndex = profilingInfo.CurrentRenderPassIndex;
					resultsBuffer.DrawCalls.Add(profilingResult);
					commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.TopOfPipe, currentPool.Handle, nextTimestampIndex++);
					commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, currentPool.Handle, nextTimestampIndex++);
				}
				resultsBuffer.FullDrawCallCount++;
			}
		}

		public void DrawCallEnd()
		{
			if (isDrawCallDeepProfilingStarted) {
				isDrawCallDeepProfilingStarted = false;
				commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, currentPool.Handle, nextTimestampIndex++);
			}
		}

		public void FrameRenderFinished(ulong frameCompletedFenceValue, ulong lastCompletedFenceValue)
		{
			bool hasCompletedFrame =
				recordingQueryPools.Count > 0 &&
				recordingQueryPools.Peek().FrameCompletedFenceValue <= lastCompletedFenceValue;
			if (hasCompletedFrame) {
				var pool = recordingQueryPools.Dequeue();
				var frame = GetFrameInHistory(pool.ProfiledFrameIndex);
				uint queryCount = !frame.IsDeepProfilingEnabled ?
					(uint)TimestampsReserved : (uint)TimestampsCountFor(frame.FullDrawCallCount);
				fixed (ulong* ptr = timestamps) {
					device.GetQueryPoolResults(
						pool.Handle,
						firstQuery: 0,
						queryCount,
						dataSize: sizeof(ulong) * queryCount,
						data: (IntPtr)ptr,
						stride: sizeof(ulong),
						SharpVulkan.QueryResultFlags.Is64Bits);
				}
				frame.FullGpuRenderTime = 0.001 * CalculateDeltaTime(timestamps[0], timestamps[1]);
				for (int i = 0, t = TimestampsReserved; t < queryCount; i++) {
					var dc = frame.DrawCalls[i];
					dc.StartTime              = CalculateDeltaTime(timestamps[0], timestamps[t++]);
					dc.AllPreviousFinishTime  = CalculateDeltaTime(timestamps[0], timestamps[t++]);
					dc.FinishTime             = CalculateDeltaTime(timestamps[0], timestamps[t++]);
				}
				frame.IsDeepProfilingCompleted = true;
				freeQueryPools.Enqueue(pool);
			}
			bool isNeedResize = timestamps.Length < TimestampsCountFor(resultsBuffer.FullDrawCallCount);
			resultsBuffer.IsDeepProfilingEnabled = isDeepProfilingEnabled && !isNeedResize;
			if (isNeedResize) {
				Array.Resize(ref timestamps, GetNextTimestampsBufferCapacity());
			}
			if (isProfilingEnabled || currentPool == null) {
				if (currentPool != null) {
					EnqueuePoolToRecording(currentPool, frameCompletedFenceValue);
				}
				// Preparing a pool for the next frame.
				currentPool = AcquireQueryPoolWithoutFence(ProfiledFramesCount, timestamps.Length);
			}

			commandBuffer = SharpVulkan.CommandBuffer.Null;
			nextTimestampIndex = TimestampsReserved;

			base.FrameRenderFinished();
			isDeepProfilingEnabled = deepProfilingNextState;
		}

		private int GetNextTimestampsBufferCapacity() =>
			TimestampsReserved + TimestampsPerDrawCall * GetNextSize(
				Math.Max(maxDrawCallsCount, resultsBuffer.FullDrawCallCount));

		private int TimestampsCountFor(int drawCallsCount) =>
			TimestampsReserved + TimestampsPerDrawCall * drawCallsCount;

		/// <summary>
		/// Calculates the time difference between timestamps in microseconds.
		/// </summary>
		/// <param name="ts1">Timestamp that comes earlier in the timeline.</param>
		/// <param name="ts2">Timestamp that comes later in the timeline.</param>
		/// <returns>Delta time or uint.MaxValue if error.</returns>
		/// <remarks>
		///	An error was revealed in which one of the timestamps became the maximum value,
		///	which in itself is permissible, but the obtained time interval was calculated
		///	in days and months, which is impossible. This is probably a GPU bug. Nvidia GTX 1660 Ti.
		/// </remarks>
		private uint CalculateDeltaTime(ulong ts1, ulong ts2) => (uint)((ts2 >= ts1 ?
				ts2 & timestampValidBitsMask - ts1 & timestampValidBitsMask :
				ts2 & timestampValidBitsMask + (timestampValidBitsMask - ts1 & timestampValidBitsMask)
			) * timestampPeriod / 1000.0);
	}
}
