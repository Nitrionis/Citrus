#if PROFILER

using System.IO;
using Yuzu;

namespace Lime.Profiler.Contexts
{
	/// <summary>
	/// Basic interface for all requests.
	/// </summary>
	internal interface IRequest
	{
		/// <summary>
		/// This is the  <see cref="ProfiledFrame.Identifier"/>.
		/// </summary>
		long FrameIdentifier { get; }
	}

	/// <summary>
	/// Used to change profiling options.
	/// </summary>
	internal interface IOptionsChangeRequest : IRequest
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
	internal interface IDataSelectionRequest : IRequest
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
		void DeserializeTail(FrameClipboard clipboard, BinaryReader reader);
	}

	internal class ChangeProfilerEnabled : IOptionsChangeRequest
	{
		[YuzuMember]
		private bool value;

		[YuzuMember]
		public long FrameIdentifier { get; }

		public ChangeProfilerEnabled(bool value, long frameIdentifier)
		{
			this.value = value;
			FrameIdentifier = frameIdentifier;
		}

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.ProfilerEnabled = value;
	}

	internal class ChangeBatchBreakReasonsRequired : IOptionsChangeRequest
	{
		[YuzuMember]
		private bool value;

		[YuzuMember]
		public long FrameIdentifier { get; }

		public ChangeBatchBreakReasonsRequired(bool value, long frameIdentifier)
		{
			this.value = value;
			FrameIdentifier = frameIdentifier;
		}

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.BatchBreakReasonsRequired = value;
	}
}

#endif // PROFILER
