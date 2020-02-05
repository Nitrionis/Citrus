using System;
using System.Collections.Generic;

namespace Lime.Graphics.Platform
{
	public abstract class PlatformProfiler : ProfilerHistory
	{
		public static PlatformProfiler Instance { get; private set; }

		/// <summary>
		/// Use to send a signal that frame render completed.
		/// </summary>
		public Action OnFrameRenderCompleted;

		protected bool isProfilingEnabled = true;
		protected bool isProfilingRequired = true;

		/// <summary>
		/// Completely stops profiling. Will be applied in the next frame.
		/// </summary>
		public bool IsActive = true;

		/// <summary>
		/// Turns deep profiling on and off. Will be applied in the next frame.
		/// </summary>
		public abstract bool IsDeepProfiling { get; set; }

		protected bool isSceneOnly = true;

		/// <summary>
		/// If enabled, objects that are not part of the scene will not participate
		/// in deep profiling. Will be applied in the next frame.
		/// </summary>
		public bool IsSceneOnly = true;

		protected ProfilerHistory.Item resultsBuffer;

		/// <summary>
		/// Profiler processes only the main window.
		/// </summary>
		protected bool isMainWindowTarget;

		private int maxDrawCallSCount = DrawCallBufferStartSize;

		protected PlatformProfiler()
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			resultsBuffer = AcquireResultsBuffer();
		}

		/// <summary>
		/// It is called once per frame before writing commands to the command buffer.
		/// </summary>
		internal virtual void FrameRenderStarted(bool isMainWindowTarget) =>
			isProfilingEnabled = isProfilingRequired && isMainWindowTarget;

		/// <summary>
		/// Called after frame rendering is completed.
		/// </summary>
		internal virtual void FrameRenderFinished()
		{
			if (isProfilingEnabled) {
				resultsBuffer.SceneSavedByBatching = RenderBatchProfiler.SceneSavedByBatching;
				resultsBuffer.FullSavedByBatching = RenderBatchProfiler.FullSavedByBatching;
				RenderBatchProfiler.Reset();
				LastFrame = resultsBuffer;
				resultsBuffer = AcquireResultsBuffer();
			}
			OnFrameRenderCompleted?.Invoke();
			isProfilingRequired = IsActive;
			maxDrawCallSCount = Math.Max(maxDrawCallSCount, LastFrame.FullDrawCallCount);
			if (LastFrame.DrawCalls.Capacity <= maxDrawCallSCount) {
				LastFrame.DrawCalls.Capacity = GetNextSize(maxDrawCallSCount);
			}
			if (resultsBuffer.DrawCalls.Capacity <= maxDrawCallSCount) {
				resultsBuffer.DrawCalls.Capacity = GetNextSize(maxDrawCallSCount);
			}
		}

		protected int CalculateTrianglesCount(int vertexCount, PrimitiveTopology topology) =>
			vertexCount < 3 ? 0 : topology == PrimitiveTopology.TriangleStrip ? vertexCount - 2 : vertexCount / 3;

		protected int GetNextSize(int size) => size * 3 / 2;
	}

	public class RenderBatchProfiler
	{
		public static int FullSavedByBatching { get; protected set; }
		public static int SceneSavedByBatching { get; protected set; }

		public static void Reset()
		{
			FullSavedByBatching = 0;
			SceneSavedByBatching = 0;
		}

		protected bool isPartOfScene;
		protected List<object> drawCallsOwners = new List<object>();

		public void ProcessNode(object node, object manager)
		{
			drawCallsOwners.Add(node);
			bool isPartOfScene = ReferenceEquals(ProfilingInfo.SceneNodeManager, manager);
			this.isPartOfScene |= isPartOfScene;
		}
	}
}
