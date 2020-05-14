using System;

namespace Lime.Graphics.Platform.Profiling
{
	internal abstract class RenderGpuProfiler : GpuHistory
	{
		public static RenderGpuProfiler Instance { get; private set; }

		/// <summary>
		/// Use to send a signal that the frame has been sent to the GPU.
		/// </summary>
		public Action FrameRenderCompleted;

		protected bool isProfilingEnabled = true;
		protected bool isProfilingRequired = true;

		/// <summary>
		/// Completely stops profiling.
		/// </summary>
		public bool IsEnabled
		{
			get => isProfilingEnabled;
			set => isProfilingRequired = value;
		}

		/// <summary>
		/// Turns deep profiling on and off.
		/// </summary>
		public abstract bool IsDeepProfiling { get; set; }

		protected bool isSceneOnlyDeepProfiling;
		private bool isSceneOnlyDeepProfilingRequired;

		/// <summary>
		/// If enabled, objects that are not part of the scene will not participate in deep profiling.
		/// </summary>
		public bool IsSceneOnlyDeepProfiling
		{
			get => isSceneOnlyDeepProfiling;
			set => isSceneOnlyDeepProfilingRequired = value;
		}

		protected GpuHistory.Item resultsBuffer;

		/// <summary>
		/// Profiler processes only the main window.
		/// </summary>
		protected bool isMainWindowTarget;

		protected int maxDrawCallsCount { get; private set; } = DrawCallBufferStartSize;

		protected RenderGpuProfiler()
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			ProfiledFramesCount = 0;
			resultsBuffer = items[0].Reset();
			resultsBuffer.FrameIndex = 0;
			LastFrame = GetFrame(HistoryFramesCount - 1);
			CheckDrawCallsBufferCapacity();
		}

		/// <summary>
		/// It is called once per frame before writing commands to the command buffer.
		/// </summary>
		internal virtual void FrameRenderStarted(bool isMainWindowTarget)
		{
			this.isMainWindowTarget = isMainWindowTarget;
			isProfilingEnabled = isProfilingRequired && isMainWindowTarget;
			RenderBatchProfiler.Reset();
		}

		/// <summary>
		/// Invoked after sending rendering commands to the GPU.
		/// </summary>
		internal virtual void FrameRenderFinished()
		{
			if (isProfilingEnabled) {
				resultsBuffer.SceneSavedByBatching = RenderBatchProfiler.SceneSavedByBatching;
				resultsBuffer.FullSavedByBatching = RenderBatchProfiler.FullSavedByBatching;
				resultsBuffer.IsSceneOnlyDeepProfiling = isSceneOnlyDeepProfiling;
				resultsBuffer = AcquireResultsBuffer();
			}
			if (isMainWindowTarget) {
				FrameRenderCompleted?.Invoke();
				isSceneOnlyDeepProfiling = isSceneOnlyDeepProfilingRequired;
				CheckDrawCallsBufferCapacity();
			}
		}

		private Item AcquireResultsBuffer()
		{
			LastFrame = GetFrame(ProfiledFramesCount + HistoryFramesCount);
			var buffer = SafeResetFrame(++ProfiledFramesCount);
			buffer.FrameIndex = ProfiledFramesCount;
			return buffer;
		}

		private void CheckDrawCallsBufferCapacity()
		{
			maxDrawCallsCount = Math.Max(maxDrawCallsCount, LastFrame.FullDrawCallCount);
			if (resultsBuffer.DrawCalls.Capacity <= maxDrawCallsCount) {
				resultsBuffer.DrawCalls.Capacity = GetNextSize(maxDrawCallsCount);
			}
		}

		protected int CalculateTrianglesCount(int vertexCount, PrimitiveTopology topology) =>
			vertexCount < 3 ? 0 : topology == PrimitiveTopology.TriangleStrip ? vertexCount - 2 : vertexCount / 3;

		protected int GetNextSize(int size) => size * 3 / 2;
	}
}
