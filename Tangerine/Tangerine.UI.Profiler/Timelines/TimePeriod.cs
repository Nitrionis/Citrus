#if PROFILER

using System;

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

		public TimePeriod(float startTime, float finishTime)
		{
			if (startTime > finishTime) {
				throw new ArgumentException();
			}
			StartTime = startTime;
			FinishTime = finishTime;
		}
	}
}

#endif // PROFILER