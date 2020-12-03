#if PROFILER

using System;
using Lime.Profiler.Network;

namespace Lime.Profiler.Contexts
{
	/// <summary>
	/// Basic interface for all commands.
	/// </summary>
	internal interface ICommand { }

	/// <summary>
	/// Used to change profiling options.
	/// </summary>
	internal interface IOptionsChangeCommand : ICommand
	{
		/// <remarks>
		/// <para>This method should not access data in the database.</para>
		/// <para>Profiling does not stop during this method.</para>
		/// </remarks>
		void Execute(IProfilerDatabase database);
	}

	/// <summary>
	/// Used to query data from IProfilerDatabase.
	/// </summary>
	internal interface IDataSelectionCommand : ICommand
	{
		/// <remarks>
		/// <para>After executing this method, this object will be sent back.</para>
		/// <para>Profiling data collection and scene update will be paused before this method starts executing.</para>
		/// </remarks>
		void Execute(IProfilerDatabase database);
	}

	/// <summary>
	/// Handles requests from the terminal and responses from the base.
	/// </summary>
	public interface IConnection
	{
		/// <summary>
		/// Allows you to enable or disable the profiler.
		/// </summary>
		bool ProfilerEnabled { get; set; }

		/// <summary>
		/// If requested, the profiler will collect batch break reasons.
		/// </summary>
		bool BatchBreakReasonsRequired { get; set; }

		
	}
}

#endif // PROFILER
