#if PROFILER

using System;

namespace Lime.Profiler.Contexts
{
	/// <summary>
	/// Handles requests from the terminal and responses from the base.
	/// </summary>
	public interface IProfilerContext
	{
		/// <summary>
		/// Sends a request to the database.
		/// </summary>
		void RunRequest(IRequest request);

		/// <summary>
		/// Called when data for the next frame is received.
		/// </summary>
		/// <remarks>Invoked in the update thread.</remarks>
		event Action<ProfiledFrame> FrameProfilingFinished;

		/// <summary>
		/// Called when profiler options is received.
		/// </summary>
		/// <remarks>Invoked in the update thread.</remarks>
		event Action<ProfilerOptions> ProfilerOptionsReceived;

		/// <summary>
		/// Starts context initialization.
		/// </summary>
		void Attached(IProfilerDatabase database);

		/// <summary>
		/// Called before updating the main application window.
		/// </summary>
		void MainWindowUpdating();

		/// <summary>
		/// Called when the context is detached from the profiler.
		/// </summary>
		void Detached();
	}
}

#endif // PROFILER
