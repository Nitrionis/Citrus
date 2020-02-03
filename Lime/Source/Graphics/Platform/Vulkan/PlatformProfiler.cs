using System;

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
		private SharpVulkan.QueryPool queryPool;

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
				ref SharpVulkan.Device device,
				ref SharpVulkan.PhysicalDeviceLimits limits,
				ref SharpVulkan.QueueFamilyProperties queueFamilyProperties
			)
		{
			this.device = device;
			isSupported = 0 < queueFamilyProperties.TimestampValidBits;
			timestampPeriod = limits.TimestampPeriod;
			timestampValidBitsMask = ulong.MaxValue >> (64 - (int)queueFamilyProperties.TimestampValidBits);
			timestamps = new ulong[TimestampsReserved + DrawCallBufferStartSize * TimestampsPerDrawCall];
			queryPool = CreateQueryPool((uint)timestamps.Length);
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
		}

		public void FirstTimestamp(SharpVulkan.CommandBuffer commandBuffer)
		{
			if (isProfilingEnabled) {
				this.commandBuffer = commandBuffer;
				this.commandBuffer.ResetQueryPool(queryPool, 0, (uint)timestamps.Length);
				this.commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, queryPool, 0);
			}
		}

		public void LastTimestamp()
		{
			if (isProfilingEnabled) {
				commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, queryPool, 1);
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
					(profilingInfo.IsPartOfScene || !isSceneOnly) &&
					nextTimestampIndex <= timestamps.Length - TimestampsPerDrawCall;
				if (isDrawCallDeepProfilingStarted) {
					var profilingResult = ProfilingResult.Acquire();
					profilingResult.ProfilingInfo = profilingInfo;
					profilingResult.VerticesCount = vertexCount;
					profilingResult.TrianglesCount = trianglesCount;
					profilingResult.RenderPassIndex = profilingInfo.CurrentRenderPassIndex;
					resultsBuffer.DrawCalls.Add(profilingResult);
					commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.TopOfPipe, queryPool, nextTimestampIndex++);
					commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, queryPool, nextTimestampIndex++);
				}
				resultsBuffer.FullDrawCallCount++;
			}
		}

		public void DrawCallEnd()
		{
			if (isDrawCallDeepProfilingStarted) {
				commandBuffer.WriteTimestamp(SharpVulkan.PipelineStageFlags.BottomOfPipe, queryPool, nextTimestampIndex++);
				isDrawCallDeepProfilingStarted = false;
			}
		}

		private void ResizeTimestampsBuffer()
		{
			int nextSize = TimestampsReserved + TimestampsPerDrawCall * GetNextSize(resultsBuffer.FullDrawCallCount);
			Array.Resize(ref timestamps, nextSize);
			device.DestroyQueryPool(queryPool);
			queryPool = CreateQueryPool((uint)timestamps.Length);
		}

		internal override void FrameRenderFinished()
		{
			bool isNeedResize = timestamps.Length < TimestampsReserved +
				resultsBuffer.FullDrawCallCount * TimestampsPerDrawCall;
			if (isProfilingEnabled) {
				uint queryCount = isNeedResize || !isDeepProfilingEnabled ? 2 : nextTimestampIndex;
				fixed (ulong* ptr = timestamps) {
					// Actually, at the moment the frame is completely
					// rendered and we will get the results instantly.
					device.GetQueryPoolResults(
						queryPool,
						firstQuery: 0,
						queryCount,
						dataSize: sizeof(ulong) * queryCount,
						data: (IntPtr)ptr,
						stride: sizeof(ulong),
						SharpVulkan.QueryResultFlags.Is64Bits/* | SharpVulkan.QueryResultFlags.Wait*/);
				}
				resultsBuffer.FullGpuRenderTime = 0.001 * CalculateDeltaTime(timestamps[0], timestamps[1]);
				if (isNeedResize) {
					ResizeTimestampsBuffer();
				} else if (isDeepProfilingEnabled) {
					for (int i = 0, t = TimestampsReserved; i < resultsBuffer.FullDrawCallCount; i++) {
						var dc = resultsBuffer.DrawCalls[i];
						dc.StartTime = CalculateDeltaTime(timestamps[0], timestamps[t++]);
						dc.AllPreviousFinishTime = CalculateDeltaTime(timestamps[0], timestamps[t++]);
						dc.FinishTime = CalculateDeltaTime(timestamps[0], timestamps[t++]);
					}
				}
			}
			commandBuffer = SharpVulkan.CommandBuffer.Null;
			nextTimestampIndex = TimestampsReserved;
			resultsBuffer.IsDeepProfilingEnabled = isDeepProfilingEnabled && isNeedResize;
			base.FrameRenderFinished();
			isDeepProfilingEnabled = deepProfilingNextState;
		}

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
		private uint CalculateDeltaTime(ulong ts1, ulong ts2) => (uint)Math.Min(uint.MaxValue, (ts2 >= ts1 ?
				ts2 & timestampValidBitsMask - ts1 & timestampValidBitsMask :
				ts2 & timestampValidBitsMask + (timestampValidBitsMask - ts1 & timestampValidBitsMask)
			) * timestampPeriod / 1000.0);
	}
}
