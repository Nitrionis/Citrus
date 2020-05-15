using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lime.Graphics.Platform.Profiling;

namespace Lime.Profilers
{
	internal class CpuProfiler : CpuHistory
	{
		private const int UnconfirmedHistorySize = 2;

		public static CpuProfiler Instance { get; private set; }

		public static void Initialize()
		{
			if (Instance == null) {
				Instance = new CpuProfiler();
			}
		}

		private readonly Queue<Item> freeItems;
		private readonly Queue<Item> unconfirmedHistory;
		private Stopwatch stopwatch;
		private Item lastUnconfirmed;
		private Item resultsBuffer;
		private long expectedNextFrameIndex;
		private long expectedNextUpdateIndex;
		private bool isMainWindow;
		private bool isEnabled;

		public Action Updating;

		public CpuProfiler()
		{
			stopwatch = new Stopwatch();
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
			Instance.isMainWindow = isMainWindow;
			if (isMainWindow) {
				Instance.NextUpdateStarted();
			}
		}

		private void NextUpdateStarted()
		{
			if (isEnabled) {
				resultsBuffer = AcquireResultsBuffer();
				if (lastUnconfirmed != null) {
					lastUnconfirmed.DeltaTime = (float)RenderCpuProfiler.Stopwatch.Elapsed.TotalMilliseconds;
					lastUnconfirmed.Memory = GC.GetTotalMemory(false);
					lastUnconfirmed.GcGen0 = GC.CollectionCount(0);
					lastUnconfirmed.GcGen1 = GC.CollectionCount(1);
					lastUnconfirmed.GcGen2 = GC.CollectionCount(2);
				}
				lastUnconfirmed = resultsBuffer;
				unconfirmedHistory.Enqueue(resultsBuffer);
			}
			stopwatch.Restart();
			Updating?.Invoke();
		}

		/// <summary>
		/// Must be called when the rendering of the previous update is guaranteed to be completed.
		/// After that, the rendering for the current update begins.
		/// </summary>
		public static void RenderingFencePassed() => Instance.NextRenderingFencePassed();

		private void NextRenderingFencePassed()
		{
			if (isMainWindow) {
				isEnabled = RenderGpuProfiler.Instance.IsEnabled;
				TryConfirmUpdate();
				Stopwatch stopwatch = this.stopwatch;
				this.stopwatch = RenderCpuProfiler.Stopwatch;
				RenderCpuProfiler.PrepareForRender(stopwatch, isMainWindow && isEnabled);
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
				if (update.FrameIndex >= RenderGpuProfiler.Instance.ProfiledFramesCount) {
					update.FrameIndex = -1;
					unconfirmedHistory.Peek().FrameIndex -= 1;
					expectedNextFrameIndex -= 1;
				}
				freeItems.Enqueue(SafeResetUpdate(index));
				items[index] = LastUpdate = update;
				LastUpdate.NodesResults.AddRange(RenderCpuProfiler.CpuUsages);
				RenderCpuProfiler.CpuUsages.Clear();
			}
		}

		/// <summary>
		/// Use for update thread.
		/// </summary>
		public static CpuUsage NodeCpuUsageStarted(Node node, CpuUsage.UsageReasons reason)
		{
			if (Instance.isEnabled && Instance.isMainWindow) {
				var usage = CpuUsage.Acquire(reason);
				usage.Owners = node;
				usage.IsPartOfScene =
					node.Manager == null ||
					SceneProfilingInfo.NodeManager == null ||
					ReferenceEquals(SceneProfilingInfo.NodeManager, node.Manager);
				usage.Start = Instance.stopwatch.ElapsedMicroseconds();
				Instance.resultsBuffer.NodesResults.Add(usage);
				return usage;
			} else return null;
		}

		/// <summary>
		/// Use for update thread.
		/// </summary>
		public static void NodeCpuUsageFinished(CpuUsage usage)
		{
			if (usage != null) {
				usage.Finish = Instance.stopwatch.ElapsedMicroseconds();
			}
		}
	}
}
