#if PROFILER

using System.IO;
using Yuzu;

namespace Lime.Profiler.Network
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
		void Execute(IProfilerDatabase database, BinaryWriter writer);
	}

	/// <summary>
	/// Basic interface for all responses containing data.
	/// </summary>
	internal interface IDataSelectionResponse
	{
		/// <summary>
		/// <para>Called immediately after this object is deserialized.</para>
		/// <para>Allows you to custom deserialize data following an object.</para>
		/// </summary>
		void DeserializeTail(BinaryReader reader);
	}

	internal class ChangeProfilerEnabled : IOptionsChangeCommand
	{
		[YuzuMember]
		private bool value;

		public ChangeProfilerEnabled(bool value) => this.value = value;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.ProfilerEnabled = value;
	}

	internal class ChangeBatchBreakReasonsRequired : IOptionsChangeCommand
	{
		[YuzuMember]
		private bool value;

		public ChangeBatchBreakReasonsRequired(bool value) => this.value = value;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.BatchBreakReasonsRequired = value;
	}
}

#endif // PROFILER
