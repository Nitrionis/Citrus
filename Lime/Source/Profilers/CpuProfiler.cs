using System;
using System.Diagnostics;
using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;

namespace Lime.Profilers
{
	internal class CpuProfiler : CpuHistory
	{
		public static CpuProfiler Instance { get; private set; }

		private const long FrameIndexUnset = -1;
		private const long FrameIndexPendingConfirmation = -2;

		private CpuHistory.Item resultsBuffer;
		private Stopwatch stopwatch;
		private long previousFrameIndex;

		public CpuProfiler()
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = this;
			stopwatch = new Stopwatch();
			LastUpdate = new Item {
				FrameIndex = FrameIndexUnset
			};
			ProfiledUpdatesCount = 1;
			resultsBuffer = items[0].Reset();
			resultsBuffer.UpdateIndex = 0;
			resultsBuffer.FrameIndex = GpuProfiler.Instance.IsEnabled ?
				FrameIndexPendingConfirmation : FrameIndexUnset;
		}

		public static void UpdateStarted() => Instance.NextUpdateStarted();

		private void NextUpdateStarted()
		{
			if (LastUpdate.FrameIndex == FrameIndexPendingConfirmation) {
				long frameIndex = GpuProfiler.Instance.LastFrame.FrameIndex;
				if (previousFrameIndex < frameIndex) {
					LastUpdate.FrameIndex = frameIndex;
					previousFrameIndex = frameIndex;
				}
				else {
					DropLastFrame();
				}
			}
			LastUpdate.DeltaTime = stopwatch.ElapsedMilliseconds;
			stopwatch.Restart();
		}

		public static void UpdateFinished()
		{
			Instance.resultsBuffer = Instance.AcquireResultsBuffer();
			Instance.resultsBuffer.FrameIndex =
				GpuProfiler.Instance == null || GpuProfiler.Instance.IsEnabled ?
					FrameIndexPendingConfirmation : FrameIndexUnset;
		}

		private Item AcquireResultsBuffer()
		{
			LastUpdate = items[(ProfiledUpdatesCount - 1) % items.Length];
			var buffer = items[ProfiledUpdatesCount % items.Length].Reset();
			buffer.UpdateIndex = ProfiledUpdatesCount++;
			return buffer;
		}

		private void DropLastFrame()
		{
			ProfiledUpdatesCount--;
			LastUpdate = items[(ProfiledUpdatesCount - 1) % items.Length];
		}
	}
}
