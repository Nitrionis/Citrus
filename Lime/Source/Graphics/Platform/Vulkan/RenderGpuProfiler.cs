using System;
using System.Collections.Generic;
using Lime.Graphics.Platform.Profiling;

namespace Lime.Graphics.Platform.Vulkan
{
	/// <summary>
	/// RenderGpuProfiler implementation for Vulkan.
	/// </summary>
	internal unsafe class RenderGpuProfiler : Profiling.RenderGpuProfiler
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
			public long FrameIndex;
			public uint TimestepsCount;
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
		private bool isDeepProfilingRequired = false;

		public override bool IsDeepProfiling
		{
			get => isDeepProfilingEnabled;
			set => isDeepProfilingRequired = value;
		}

		private bool isDrawCallDeepProfilingStarted;

		public RenderGpuProfiler(
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

		private PoolInfo AcquireQueryPool(long frameIndex, int minCapacity)
		{
			var poolInfo = freeQueryPools.Count > 0 ? freeQueryPools.Dequeue() : new PoolInfo();
			if (poolInfo.Handle == null) {
				poolInfo.Handle = CreateQueryPool((uint)minCapacity);
				poolInfo.Capacity = minCapacity;
			} else if (poolInfo.Capacity < minCapacity) {
				device.DestroyQueryPool(poolInfo.Handle);
				poolInfo.Handle = CreateQueryPool((uint)minCapacity);
				poolInfo.Capacity = minCapacity;
			}
			poolInfo.FrameIndex = frameIndex;
			return poolInfo;
		}

		private void EnqueuePoolToRecording(PoolInfo pool, ulong frameCompletedFenceValue, uint timestepsCount)
		{
			pool.FrameCompletedFenceValue = frameCompletedFenceValue;
			pool.TimestepsCount = timestepsCount;
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
			isProfilingEnabled &= isSupported;
			if (isProfilingEnabled) {
				currentPool = AcquireQueryPool(resultsBuffer.FrameIndex, timestamps.Length);
			}
			isDeepProfilingEnabled = isProfilingEnabled && isDeepProfilingRequired;
			commandBuffer = SharpVulkan.CommandBuffer.Null;
			nextTimestampIndex = TimestampsReserved;
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

		public void DrawCallStart(GpuCallInfo profilingInfo, int vertexCount, PrimitiveTopology topology)
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
					(!isSceneOnlyDeepProfiling || profilingInfo.IsPartOfScene) &&
					nextTimestampIndex < timestamps.Length - TimestampsPerDrawCall;
				if (isDrawCallDeepProfilingStarted) {
					var profilingResult = GpuUsage.Acquire();
					profilingResult.GpuCallInfo = profilingInfo;
					profilingResult.VerticesCount = vertexCount;
					profilingResult.TrianglesCount = trianglesCount;
					profilingResult.RenderPassIndex = profilingInfo.CurrentRenderPassIndex;
					resultsBuffer.DrawCalls.Add(profilingResult);
					commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.TopOfPipe, currentPool.Handle, nextTimestampIndex++);
					commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, currentPool.Handle, nextTimestampIndex++);
				} else {
					profilingInfo.Free();
				}
				resultsBuffer.FullDrawCallCount++;
			} else {
				profilingInfo.Free();
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
			bool isNeedResize = timestamps.Length - TimestampsPerDrawCall <= nextTimestampIndex;
			if (isNeedResize) {
				Array.Resize(ref timestamps, GetNextTimestampsBufferCapacity());
			}
			resultsBuffer.IsDeepProfilingEnabled = isDeepProfilingEnabled && !isNeedResize;
			if (isProfilingEnabled) {
				EnqueuePoolToRecording(currentPool, frameCompletedFenceValue, nextTimestampIndex);
			}
			bool hasCompletedFrame =
				recordingQueryPools.Count > 0 &&
				recordingQueryPools.Peek().FrameCompletedFenceValue <= lastCompletedFenceValue;
			if (hasCompletedFrame) {
				var pool = recordingQueryPools.Dequeue();
				fixed (ulong* ptr = timestamps) {
					device.GetQueryPoolResults(
						pool.Handle,
						firstQuery: 0,
						pool.TimestepsCount,
						dataSize: sizeof(ulong) * pool.TimestepsCount,
						data: (IntPtr)ptr,
						stride: sizeof(ulong),
						SharpVulkan.QueryResultFlags.Is64Bits);
				}
				var frame = GetFrame(pool.FrameIndex);
				frame.FullGpuRenderTime = 0.001 * CalculateDeltaTime(timestamps[0], timestamps[1]);
				if (frame.IsDeepProfilingEnabled) {
					int drawCallCount = frame.IsSceneOnlyDeepProfiling ?
						frame.SceneDrawCallCount : frame.FullDrawCallCount;
					for (int i = 0, t = TimestampsReserved; i < drawCallCount; i++) {
						var dc = frame.DrawCalls[i];
						bool isContainsRenderingTime =
							!frame.IsSceneOnlyDeepProfiling ||
							dc.GpuCallInfo.IsPartOfScene;
						if (isContainsRenderingTime) {
							dc.StartTime              = CalculateDeltaTime(timestamps[0], timestamps[t++]);
							dc.AllPreviousFinishTime  = CalculateDeltaTime(timestamps[0], timestamps[t++]);
							dc.FinishTime             = CalculateDeltaTime(timestamps[0], timestamps[t++]);
						}
					}
				}
				frame.IsCompleted = true;
				freeQueryPools.Enqueue(pool);
			}
			base.FrameRenderFinished();
		}

		private int GetNextTimestampsBufferCapacity() =>
			TimestampsReserved + TimestampsPerDrawCall * GetNextSize(
				Math.Max(maxDrawCallsCount, resultsBuffer.FullDrawCallCount));

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
				(ts2 & timestampValidBitsMask) - (ts1 & timestampValidBitsMask) :
				(ts2 & timestampValidBitsMask) + (timestampValidBitsMask - (ts1 & timestampValidBitsMask))
			) * timestampPeriod / 1000.0);
	}
}
