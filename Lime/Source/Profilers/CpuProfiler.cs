using System;
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

		private Item resultsBuffer;
		private Stopwatch stopwatch;

		public CpuProfiler()
		{
			stopwatch = new Stopwatch();
			LastUpdate = new Item {
				FrameIndex = Item.FrameIndexUnset
			};
			ProfiledUpdatesCount = 1;
			resultsBuffer = items[0].Reset();
			resultsBuffer.UpdateIndex = 0;
			resultsBuffer.FrameIndex = GpuProfiler.Instance.IsEnabled ?
				GpuProfiler.Instance.ProfiledFramesCount : Item.FrameIndexUnset;
		}

		public static void UpdateStarted(bool isMainWindow) =>
			Instance.NextUpdateStarted(isMainWindow);

		private void NextUpdateStarted(bool isMainWindow)
		{
			if (isMainWindow) {
				if (LastUpdate.FrameIndex == GpuProfiler.Instance.ProfiledFramesCount) {
					// Drop last update
					ProfiledUpdatesCount--;
					LastUpdate = items[(ProfiledUpdatesCount - 1) % items.Length];
				} else {
					resultsBuffer = AcquireResultsBuffer();
				}
				LastUpdate.DeltaTime = stopwatch.ElapsedMilliseconds;
				stopwatch.Restart();
			}
		}

		public static void UpdateFinished(bool isMainWindow)
		{
			if (isMainWindow) {
				Instance.LastUpdate = Instance.resultsBuffer;
				Instance.ProfiledUpdatesCount++;
			}
		}

		private Item AcquireResultsBuffer()
		{
			var buffer = items[ProfiledUpdatesCount % items.Length].Reset();
			buffer.UpdateIndex = ProfiledUpdatesCount - 1;
			buffer.FrameIndex = GpuProfiler.Instance.ProfiledFramesCount;
			return buffer;
		}
	}
}
