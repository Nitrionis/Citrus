using System.Collections.Generic;
using System.Diagnostics;

namespace Lime.Graphics.Platform.Profiling
{
	internal static class StopwatchExtension
	{
		public static uint ElapsedMicroseconds(this Stopwatch stopwatch) =>
			(uint)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / 1_000_000L));
	}

	internal static class RenderCpuProfiler
	{
		private static bool isEnabled;

		public static Stopwatch Stopwatch { get; private set; } = new Stopwatch();
		public static List<CpuUsage> CpuUsages { get; private set; } = new List<CpuUsage>();

		public static CpuUsage NodeCpuUsageStarted(IReferencesTableCompatible node, object manager)
		{
			if (isEnabled) {
				var usage = CpuUsage.Acquire(CpuUsage.UsageReasons.NodeRender);
				usage.Owners = node;
				usage.IsPartOfScene =
					manager == null ||
					SceneProfilingInfo.NodeManager == null ||
					ReferenceEquals(SceneProfilingInfo.NodeManager, manager);
				usage.Start = Stopwatch.ElapsedMicroseconds();
				CpuUsages.Add(usage);
				return usage;
			} else return null;
		}

		public static void NodeCpuUsageFinished(CpuUsage usage)
		{
			if (usage != null) {
				usage.Finish = Stopwatch.ElapsedMicroseconds();
			}
		}

		public static CpuUsage BatchCpuUsageStarted(IRenderBatch batch)
		{
			if (isEnabled) {
				var profilingInfo = (RenderBatchOwnersInfo)batch;
				var usage = CpuUsage.Acquire(CpuUsage.UsageReasons.BatchRender);
				usage.Owners         = profilingInfo.Owners;
				usage.IsPartOfScene  = profilingInfo.IsPartOfScene;
				usage.Start          = Stopwatch.ElapsedMicroseconds();
				CpuUsages.Add(usage);
				return usage;
			} else return null;
		}

		public static void BatchCpuUsageFinished(CpuUsage usage) => NodeCpuUsageFinished(usage);

		/// <summary>
		/// Must be called before frame rendering.
		/// </summary>
		/// <param name="stopwatch">Stopwatch launched at the beginning of the update.</param>
		/// <param name="isEnabled">Indicates whether profiling is enabled for this frame or not.</param>
		public static void PrepareForRender(Stopwatch stopwatch, bool isEnabled)
		{
			RenderCpuProfiler.isEnabled = isEnabled;
			Stopwatch = stopwatch;
		}
	}
}
