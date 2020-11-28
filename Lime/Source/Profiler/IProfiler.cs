#if PROFILER

using System;

namespace Lime.Profiler
{
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>
	/// The properties assignment is delayed.
	/// If the new value cannot be applied to the property,
	/// the property will retain its previous value.
	/// </remarks>
	public interface IProfiler
	{
		/// <summary>
		/// Turns the profiler on and off.
		/// </summary>
		/// <remarks>
		/// In the off state, the profiler consumes an insignificant amount of processor time.
		/// </remarks>
		bool Enabled { get; set; }

		/// <summary>
		/// Invoked when profiling of a frame is completed.
		/// </summary>
		/// <remarks>
		/// Only the main application window is involved in profiling.
		/// For each reterned frame <see cref="ProfiledFrame.Identifier"/> =
		/// previous returned frame <see cref="ProfiledFrame.Identifier"/> + 1.
		/// </remarks>
		event Action<ProfiledFrame> FrameProfilingFinished;

		/// <summary>
		/// Request detailed profiling data for each node for a specified frame.
		/// </summary>
		/// <param name="frmeIdentifier">This is a <see cref="ProfiledFrame.Identifier"/>.</param>
		/// <remarks>
		/// During a request for new data, the state of the previous data will be undefined.
		/// </remarks>
		void RequestNodesTiming(ulong frmeIdentifier);

		/// <summary>
		/// Returns detailed profiling data for each node for a specified frame.
		/// </summary>
		/// <remarks>
		/// During a request for new data, the state of the previous data will be undefined.
		/// </remarks>
		event Action<NodesTiming> NodesTimingReceived;

		/// <summary>
		/// Request a complete cleanup of history and release of all resources.
		/// </summary>
		/// <param name="disable">If true set Enabled = false before cleanup.</param>
		/// <remarks>
		/// If the profiler is enabled before performing cleanup, profiling storage will be reallocated.
		/// </remarks>
		void RequestCleanup(bool disable = false);
	}
}

#endif // PROFILER
