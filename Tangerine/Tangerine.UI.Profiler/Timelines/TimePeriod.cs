#if PROFILER

namespace Tangerine.UI.Timelines
{
	internal struct TimePeriod
	{
		/// <summary>
		/// Start time in microseconds.
		/// </summary>
		public float StartTime;

		/// <summary>
		/// Finish time in microseconds.
		/// </summary>
		public float FinishTime;

		/// <summary>
		/// Time period duration in microseconds.
		/// </summary>
		public float Duration => FinishTime - StartTime;
	}
}

#endif // PROFILER