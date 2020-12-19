#if PROFILER

namespace Tangerine.UI
{
	public static class RemoteStopwatchExtension
	{
		public static float Frequency;

		public static float TicksToSeconds(this long value) => value / Frequency;

		public static float TicksToMilliseconds(this long value) => value / (Frequency / 1_000);
		
		public static float TicksToMicroseconds(this long value) => value / (Frequency / 1_000_000);
	}
}

#endif // PROFILER