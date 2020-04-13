
namespace Lime.Profilers
{
	public interface ITimePeriod
	{
		/// <summary>
		/// Timestamp in microseconds.
		/// </summary>
		uint Start { get; set; }

		/// <summary>
		/// Timestamp in microseconds.
		/// </summary>
		uint Finish { get; set; }
	}
}
