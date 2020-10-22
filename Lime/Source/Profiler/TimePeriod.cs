using Yuzu;

namespace Lime.Profiler
{
	public struct TimePeriod
	{
		/// <summary>
		/// Start time in microseconds.
		/// </summary>
		[YuzuMember]
		public uint StartTime;

		/// <summary>
		/// Finish time in microseconds.
		/// </summary>
		[YuzuMember]
		public uint FinishTime;
	}
}
