using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Lime.Graphics.Platform;
using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;

namespace Lime.Profilers
{
	internal class CpuProfiler : CpuHistory
	{
		public static CpuProfiler Instance { get; private set; }

		public static void Initialize()
		{
			if (Instance == null) {
				Instance = new CpuProfiler();
			}
		}

		private const int UnconfirmedHistorySize = 2;

		private readonly Queue<Item> freeItems;
		private readonly Queue<Item> unconfirmedHistory;
		private Stopwatch updateStopwatch;
		private Stopwatch renderStopwatch;
		private Item lastUnconfirmed;
		private Item resultsBuffer;
		private long expectedNextFrameIndex;
		private long expectedNextUpdateIndex;
		private bool isUpdateMainWindow;
		private bool isRenderMainWindow;
		private bool isEnabled;

		/// <remarks>
		/// Since rendering can be performed in a separate thread, write
		/// its results in a separate buffer and then merge with the main.
		/// </remarks>
		private List<CpuUsage> renderCpuUsages = new List<CpuUsage>();

		public Action Updating;

		public CpuProfiler()
		{
			updateStopwatch = new Stopwatch();
			renderStopwatch = new Stopwatch();
			freeItems = new Queue<Item>(UnconfirmedHistorySize);
			unconfirmedHistory = new Queue<Item>(UnconfirmedHistorySize);
			for (int i = 0; i < UnconfirmedHistorySize; i++) {
				freeItems.Enqueue(new Item());
			}
			ProfiledUpdatesCount = 0;
			expectedNextUpdateIndex = 0;
			expectedNextFrameIndex = 0;
		}

		/// <summary>
		/// Must be called when main window update started.
		/// </summary>
		public static void UpdateStarted(bool isMainWindow)
		{
			Instance.isUpdateMainWindow = isMainWindow;
			if (isMainWindow) {
				Instance.NextUpdateStarted();
			}
		}

		private void NextUpdateStarted()
		{
			if (isEnabled) {
				resultsBuffer = AcquireResultsBuffer();
				if (lastUnconfirmed != null) {
					lastUnconfirmed.DeltaTime = (float)renderStopwatch.Elapsed.TotalMilliseconds;
				}
				lastUnconfirmed = resultsBuffer;
				unconfirmedHistory.Enqueue(resultsBuffer);
			}
			updateStopwatch.Restart();
			Updating?.Invoke();
		}

		/// <summary>
		/// Must be called when the rendering of the previous update is guaranteed to be completed.
		/// After that, the rendering for the current update begins.
		/// </summary>
		public static void RenderingFencePassed() => Instance.NextRenderingFencePassed();

		private void NextRenderingFencePassed()
		{
			isRenderMainWindow = isUpdateMainWindow;
			if (isUpdateMainWindow) {
				isEnabled = GpuProfiler.Instance.IsEnabled;
				TryConfirmUpdate();
				var stopwatch = renderStopwatch;
				renderStopwatch = updateStopwatch;
				updateStopwatch = stopwatch;
			}
		}

		private Item AcquireResultsBuffer()
		{
			var buffer = freeItems.Dequeue();
			buffer.UpdateIndex = expectedNextUpdateIndex++;
			buffer.FrameIndex = expectedNextFrameIndex++;
			return buffer;
		}

		private void TryConfirmUpdate()
		{
			if (unconfirmedHistory.Count == 2) {
				var update = unconfirmedHistory.Dequeue();
				int index = (int)(ProfiledUpdatesCount++ % items.Length);
				if (update.FrameIndex >= GpuProfiler.Instance.ProfiledFramesCount) {
					update.FrameIndex = -1;
					unconfirmedHistory.Peek().FrameIndex -= 1;
					expectedNextFrameIndex -= 1;
				}
				freeItems.Enqueue(items[index].Reset());
				items[index] = LastUpdate = update;
				LastUpdate.NodesResults.AddRange(renderCpuUsages);
				Instance.renderCpuUsages.Clear();
			}
		}

#if LIME_PROFILER
		/// <summary>
		/// Use for update thread.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CpuUsage NodeCpuUsageStarted(Node node, CpuUsage.UsageReason reason)
		{
			if (Instance.isEnabled && Instance.isUpdateMainWindow) {
				var usage = CpuUsage.Acquire(reason);
				usage.Reason = reason;
				usage.Owner = node;
				usage.IsPartOfScene =
					node.Manager == null ||
					SceneProfilingInfo.NodeManager == null ||
					ReferenceEquals(SceneProfilingInfo.NodeManager, node.Manager);
				usage.Start = Instance.CurrentTime(Instance.updateStopwatch);
				Instance.resultsBuffer.NodesResults.Add(usage);
				return usage;
			} else {
				return null;
			}
		}

		/// <summary>
		/// Use for update thread.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NodeCpuUsageFinished(CpuUsage usage)
		{
			if (Instance.isEnabled && Instance.isUpdateMainWindow) {
				usage.Finish = Instance.CurrentTime(Instance.updateStopwatch);
			}
		}

		/// <summary>
		/// Use for parallel rendering thread.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CpuUsage NodeRenderCpuUsageStarted(object node, object manager)
		{
			if (Instance.isEnabled && Instance.isRenderMainWindow) {
				var usage = CpuUsage.Acquire(CpuUsage.UsageReason.Render);
				usage.Reason = CpuUsage.UsageReason.Render;
				usage.Owner = node;
				usage.IsPartOfScene =
					manager == null ||
					SceneProfilingInfo.NodeManager == null ||
					ReferenceEquals(SceneProfilingInfo.NodeManager, manager);
				usage.Start = Instance.CurrentTime(Instance.renderStopwatch);
				Instance.renderCpuUsages.Add(usage);
				return usage;
			} else {
				return null;
			}
		}

		/// <summary>
		/// Use for parallel rendering thread.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NodeRenderCpuUsageFinished(CpuUsage usage)
		{
			if (Instance.isEnabled && Instance.isRenderMainWindow) {
				usage.Finish = Instance.CurrentTime(Instance.renderStopwatch);
			}
		}
#endif
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint CurrentTime(Stopwatch stopwatch) => (uint)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / 1000000L));
	}
}
