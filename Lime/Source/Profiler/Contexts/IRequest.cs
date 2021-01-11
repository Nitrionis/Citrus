#if PROFILER

using System.IO;
using Lime.Profiler.Graphics;
using Lime.Profiler.Network;
using Yuzu;

namespace Lime.Profiler.Contexts
{
	/// <summary>
	/// Basic interface for all requests.
	/// </summary>
	public interface IRequest { }

	/// <summary>
	/// Used to change profiling options.
	/// </summary>
	internal interface IOptionsChangeRequest : IRequest, IMessage
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
	internal interface IDataSelectionRequest : IRequest, IMessage
	{
		/// <summary>
		/// Allows you to determine if processing of this request has started.
		/// </summary>
		bool IsRunning { get; set; }

		/// <summary>
		/// Handles response of the data selection request on the terminal side.
		/// </summary>
		IAsyncResponseProcessor AsyncResponseProcessor { get; }

		/// <summary>
		/// Fetch data on a database side.
		/// </summary>
		/// <remarks>
		/// Profiling data collection and scene update will be paused before this method starts executing.
		/// </remarks>
		void FetchData(IProfilerDatabase database, BinaryWriter writer);
	}

	/// <summary>
	/// Processes a response to a data selection request.
	/// </summary>
	public interface IAsyncResponseProcessor
	{
		/// <summary>
		/// Handles the response on the terminal side.
		/// </summary>
		/// <param name="response">
		/// <para>The response object that is considered valid only within the method call.</para>
		/// <para>Attempts to access data from outside the method will cause undefined behavior.</para>
		/// </param>
		void ProcessResponseAsync(object response);
	}

	/// <inheritdoc cref="IAsyncResponseProcessor"/>
	public abstract class AsyncResponseProcessor<ResponseType> : IAsyncResponseProcessor
	{
		/// <inheritdoc/>
		public void ProcessResponseAsync(object response) => ProcessResponseAsync((ResponseType)response);
		
		/// <inheritdoc cref="IAsyncResponseProcessor.ProcessResponseAsync"/>
		protected abstract void ProcessResponseAsync(ResponseType response);
	}
	
	/// <summary>
	/// Common interface for all responses.
	/// </summary>
	public interface IDataSelectionResponse
	{
		bool IsSucceed { get; set; }
	}
	
	/// <summary>
	/// Basic interface for all responses containing data.
	/// </summary>
	internal interface IDataSelectionResponseBuilder
	{
		/// <summary>
		/// <para>Called immediately after this object is deserialized.</para>
		/// <para>Allows you to custom deserialize data following an object.</para>
		/// </summary>
		IDataSelectionResponse Build(FrameClipboard clipboard, BinaryReader reader);
	}

	internal class ChangeProfilerEnabled : IOptionsChangeRequest
	{
		[YuzuMember]
		private bool value;

		public ChangeProfilerEnabled(bool value) => this.value = value;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.ProfilerEnabled = value;
	}

	internal class ChangeBatchBreakReasonsRequired : IOptionsChangeRequest
	{
		[YuzuMember]
		private bool value;

		public ChangeBatchBreakReasonsRequired(bool value) => this.value = value;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.BatchBreakReasonsRequired = value;
	}

	internal class ChangeSceneUpdateFrozen : IOptionsChangeRequest
	{
		[YuzuMember]
		private UpdateSkipOptions value;

		public ChangeSceneUpdateFrozen(UpdateSkipOptions value) => this.value = value;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => database.SetSceneUpdateFrozen(value);
	}

	internal class ChangeOverdrawEnabled : IOptionsChangeRequest
	{
		[YuzuMember]
		private bool value;

		public ChangeOverdrawEnabled(bool value) => this.value = value;

		/// <inheritdoc/>
		public void Execute(IProfilerDatabase database) => Overdraw.Enabled = value;
	}
	
	internal interface IContextOptionsChangeRequest : IMessage {}
	
	internal class ContinueSendProfilerOptions : IContextOptionsChangeRequest {}
}

#endif // PROFILER
