#if PROFILER

using Lime;

namespace Tangerine.UI.Timelines
{
	internal struct TimelineState
	{
		/// <summary>
		/// Defines the time interval visible by users.
		/// </summary>
		/// <remarks>
		/// Values are measured in microseconds where 0 corresponds to the beginning of the frame.
		/// </remarks>
		public TimePeriod VisibleTimePeriod;
		
		/// <summary>
		/// Defines the scale of the timeline.
		/// </summary>
		public float MicrosecondsPerPixel;

		/// <summary>
		/// Additional scaling factor along the horizontal axis.
		/// </summary>
		public float RelativeScale;
		
		/// <summary>
		/// Not scalable vertical distance in pixels between time intervals.
		/// </summary>
		public float TimeIntervalVerticalMargin;
			
		/// <summary>
		/// Not scalable height in pixels of one time interval.
		/// </summary>
		public float TimeIntervalHeight;
			
		/// <summary>
		/// Mouse position relative to widget with time intervals.
		/// </summary>
		public Vector2 LocalMousePosition;

		public PeriodPositions.SpacingParameters SpacingParameters => 
			new PeriodPositions.SpacingParameters {
				MicrosecondsPerPixel = MicrosecondsPerPixel,
				TimePeriodVerticalMargin = TimeIntervalVerticalMargin,
				TimePeriodHeight = TimeIntervalHeight
			};
	}
}

#endif // PROFILER