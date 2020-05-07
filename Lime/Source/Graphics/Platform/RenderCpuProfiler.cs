using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Lime.Graphics.Platform
{
	internal static class StopwatchExtension
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ElapsedMicroseconds(this Stopwatch stopwatch) =>
			(uint)(stopwatch.ElapsedTicks / (Stopwatch.Frequency / 1000000L));
	}

	internal static class RenderCpuProfiler
	{
		private static bool isEnabled;

		public static Stopwatch Stopwatch { get; private set; } = new Stopwatch();
		public static List<CpuUsage> CpuUsages { get; private set; } = new List<CpuUsage>();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CpuUsage NodeCpuUsageStarted(object node, object manager)
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void NodeCpuUsageFinished(CpuUsage usage)
		{
			if (usage != null) {
				usage.Finish = Stopwatch.ElapsedMicroseconds();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CpuUsage BatchCpuUsageStarted(IRenderBatch batch)
		{
			if (isEnabled) {
				var profilingInfo = (RenderBatchProfiler)batch;
				var usage = CpuUsage.Acquire(CpuUsage.UsageReasons.BatchRender);
				usage.Owners = profilingInfo.DrawCallsOwners;
				usage.IsPartOfScene = profilingInfo.IsPartOfScene;
				usage.Start = Stopwatch.ElapsedMicroseconds();
				CpuUsages.Add(usage);
				return usage;
			} else return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void BatchCpuUsageFinished(CpuUsage usage) => NodeCpuUsageFinished(usage);

		public static void PrepareForRender(Stopwatch stopwatch, bool isEnabled)
		{
			RenderCpuProfiler.isEnabled = isEnabled;
			Stopwatch = stopwatch;
		}
	}
}
