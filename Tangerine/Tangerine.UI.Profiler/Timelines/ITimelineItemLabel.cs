#if PROFILER

namespace Tangerine.UI.Timelines
{
	internal interface ITimelineItemLabel
	{
		/// <summary>
		/// Label caption.
		/// </summary>
		string Text { get; }
		
		/// <summary>
		/// Label width in pixels.
		/// </summary>
		float Width { get; }
		
		/// <summary>
		///  Time period of a timeline item.
		/// </summary>
		TimePeriod Period { get; }
		
		/// <summary>
		/// Defines the vertical location of an item, where a <= b.
		/// </summary>
		Range VerticalLocation { get; }
	}
}

#endif // PROFILER