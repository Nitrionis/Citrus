using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		private readonly Stopwatch stopwatch;
		private readonly Queue<Item> freeItems;
		private readonly Queue<Item> unconfirmedHistory;
		private Item lastUnconfirmed;
		private Item resultsBuffer;
		private long expectedNextFrameIndex;
		private long expectedNextUpdateIndex;

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
			if (isMainWindow) {
				Instance.NextUpdateStarted();
			}
		}

		private void NextUpdateStarted()
		{
			resultsBuffer = AcquireResultsBuffer();
			if (lastUnconfirmed != null) {
				lastUnconfirmed.DeltaTime = (float)stopwatch.Elapsed.TotalMilliseconds;
			}
			lastUnconfirmed = resultsBuffer;
			unconfirmedHistory.Enqueue(resultsBuffer);
			stopwatch.Restart();
			Updating?.Invoke();
		}

		/// <summary>
		/// Must be called when the rendering of the previous update is guaranteed to be completed.
		/// After that, the rendering for the current update begins.
		/// </summary>
		public static void RenderingFencePassed(bool isMainWindow)
		{
			if (isMainWindow) {
				Instance.TryConfirmUpdate();
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
			}
		}
	}
}
