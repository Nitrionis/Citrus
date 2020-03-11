using System;

namespace Lime.Graphics.Platform
{
	internal abstract class PlatformProfiler : ProfilerHistory
	{
		public static PlatformProfiler Instance { get; private set; }

		/// <summary>
		/// Use to send a signal that the frame has been sent to the GPU.
		/// </summary>
		public Action FrameRenderCompleted;

		protected bool isProfilingEnabled = true;
		protected bool isProfilingRequired = true;

		/// <summary>
		/// Completely stops profiling. Will be applied in the next frame.
		/// </summary>
		public bool IsEnabled
		{
			get => isProfilingEnabled;
			set => isProfilingRequired = value;
		}

		/// <summary>
		/// Turns deep profiling on and off. Will be applied in the next frame.
		/// </summary>
		public abstract bool IsDeepProfiling { get; set; }

		protected bool isSceneOnlyDeepProfiling;
		private bool isSceneOnlyDeepProfilingRequired;

		/// <summary>
		/// If enabled, objects that are not part of the scene will not participate
		/// in deep profiling. Will be applied in the next frame.
		/// </summary>
		public bool IsSceneOnlyDeepProfiling
		{
			get => isSceneOnlyDeepProfiling;
			set => isSceneOnlyDeepProfilingRequired = value;
		}

		protected ProfilerHistory.Item resultsBuffer;

		/// <summary>
		/// Profiler processes only the main window.
		/// </summary>
		protected bool isMainWindowTarget;

		protected int maxDrawCallsCount { get; private set; } = DrawCallBufferStartSize;

		protected PlatformProfiler()
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			ProfiledFramesCount = 1;
			resultsBuffer = items[0].Reset();
			resultsBuffer.FrameIndex = 0;
			LastFrame = resultsBuffer;
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
		/// Invoked after sending a frame to the GPU.
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
			LastFrame = GetFrame(ProfiledFramesCount - 1);
			var buffer = SafeResetFrame(ProfiledFramesCount);
			buffer.FrameIndex = ProfiledFramesCount++;
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
