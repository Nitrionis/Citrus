#if PROFILER

using System;

namespace Lime.Profiler
{
	/// <summary>
	/// Provides a facade for accessing profiling data and for switching profiler options.
	/// </summary>
	public static class ProfilerTerminal
	{
		/// <summary>
		/// Turns the profiler on and off.
		/// </summary>
		/// <remarks>
		/// In the off state, the profiler consumes a small amount of processor time.
		/// </remarks>
		public static bool Enabled { get; set; }

		/// <summary>
		/// Invoked when profiling of a frame is completed.
		/// </summary>
		/// <remarks>
		/// Only the main application window is involved in profiling.
		/// For each reterned frame <see cref="ProfiledFrame.Identifier"/> =
		/// previous returned frame <see cref="ProfiledFrame.Identifier"/> + 1.
		/// </remarks>
		public static event Action<ProfiledFrame> FrameProfilingFinished;

		/// <summary>
		/// Request a complete cleanup of history and release of all resources.
		/// </summary>
		/// <param name="disable">If true set Enabled = false before cleanup.</param>
		/// <remarks>
		/// If the profiler is enabled before performing cleanup, profiling storage will be reallocated.
		/// </remarks>
		public static void RequestCleanup(bool disable = false) { }
	}
}

#endif // PROFILER
