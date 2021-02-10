#if PROFILER

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Lime.Profiler.Formatting;
using Lime.Profiler.Graphics;
using Lime.Profiler.Network;
using Yuzu.Binary;

namespace Lime.Profiler.Contexts
{
	using Task = System.Threading.Tasks.Task;
	
	public sealed class RemoteDatabaseContext : IProfilerContext, IInterrupter
	{
		private readonly Client client;
		private readonly ConcurrentQueue<object> requests;
		private readonly BinarySerializer serializer;
		
		private IProfilerDatabase database;
		private long lastProcessedFrame;
		private bool shouldSendProfilerOptions;
		
		private volatile bool isAttached;
		private volatile TaskCompletionSource<bool> taskCompletionSource;
		
		/// <inheritdoc />
		public bool IsSerializationPaused => false;
		
		/// <inheritdoc />
		public bool IsDeserializationPaused => false;
		
		/// <inheritdoc />
		public event Action<ProfiledFrame> FrameProfilingFinished;
		
		/// <inheritdoc />
		public event Action<ProfilerOptions> ProfilerOptionsReceived;
		
		public RemoteDatabaseContext(IPEndPoint ipEndPoint)
		{
			requests = new ConcurrentQueue<object>();
			serializer = new BinarySerializer();
			client = new Client(ipEndPoint, this);
			shouldSendProfilerOptions = true;
		}
		
		/// <inheritdoc />
		public void RunRequest(IRequest request) => throw new InvalidOperationException();

		/// <inheritdoc />
		public void Attached(IProfilerDatabase database)
		{
			if (this.database != null) {
				throw new InvalidOperationException("Profiler: RemoteDatabaseContext re-attachment is not supported!");
			}
			this.database = database;
			lastProcessedFrame = database.ProfiledFramesCount;
			isAttached = true;
		}

		/// <inheritdoc />
		public void MainWindowUpdating()
		{
			if (!client.IsAlive) {
				if (isAttached) {
					ProfilerDatabase.Context = new NativeContext();
				}
				if (!taskCompletionSource?.Task?.IsCompleted ?? false) {
					taskCompletionSource?.SetResult(true);
				}
				isAttached = false;
				return;
			}
			ProcessRequests();
			if (shouldSendProfilerOptions) {
				client.LazySend(new ProfilerOptions {
					ProfilerEnabled = database.ProfilerEnabled,
					BatchBreakReasonsRequired = database.BatchBreakReasonsRequired,
					IsSceneUpdateFrozen = database.IsSceneUpdateFrozen,
					OverdrawModeEnabled = Overdraw.Enabled
				});
				while (lastProcessedFrame < database.LastAvailableFrame) {
					var frame = database.GetFrame(++lastProcessedFrame);
					if (frame != null) {
						client.LazySend(frame.CommonData);	
					}
				}
			}
		}

		private void ProcessRequests()
		{
			while (isAttached && !requests.IsEmpty) {
				var request = PeekRequest();
				switch (request) {
					case IDataSelectionRequest dataSelectionRequest:
						shouldSendProfilerOptions = false;
						if (!dataSelectionRequest.IsRunning) {
							dataSelectionRequest.IsRunning = true;
							taskCompletionSource = new TaskCompletionSource<bool>(
								TaskCreationOptions.RunContinuationsAsynchronously);
							database.PreventProfilingWhileRunning(new Task(async () => {
								client.LazySend(dataSelectionRequest);
								await taskCompletionSource.Task;
							}));
						}
						if (taskCompletionSource.Task.IsCompleted) {
							DequeueRequest(dataSelectionRequest);
						} else {
							return;
						}
						break;
					case IOptionsChangeRequest optionsChangeRequest: 
						optionsChangeRequest.Execute(database);
						DequeueRequest(optionsChangeRequest);
						break;
					case ContinueSendProfilerOptions optionsSendContinueEvent:
						shouldSendProfilerOptions = true;
						DequeueRequest(optionsSendContinueEvent);
						break;
				}
			}
		}
		
		private object PeekRequest()
		{
			if (!requests.TryPeek(out var response)) {
				throw new System.Exception("Profiler: Wrong behavior detected!");
			}
			return response;
		}
		
		private void DequeueRequest(object expectedRequest)
		{
			if (!requests.TryDequeue(out var r) || !ReferenceEquals(r, expectedRequest)) {
				throw new System.Exception("Profiler: Wrong behavior detected!");
			}
		}
		
		/// <inheritdoc />
		public void Detached()
		{
			isAttached = false;
			if (client.IsAlive) {
				client.RequestClose();
			}
		}

		/// <inheritdoc />
		public void AfterSerialization(BinaryWriter writer, object @object)
		{
			if (@object is IDataSelectionRequest dataSelectionRequest) {
				try {
					dataSelectionRequest.FetchData(database, writer, serializer);
				} finally {
					taskCompletionSource.SetResult(true);
				}
			}
		}

		/// <inheritdoc />
		public void AfterDeserialization(BinaryReader reader, object @object)
		{
			if (isAttached) {
				switch (@object) {
					case IDataSelectionRequest dataSelectionRequest:
					case IOptionsChangeRequest optionsChangeRequest:
					case ContinueSendProfilerOptions optionsSendContinueEvent: 
						requests.Enqueue(@object);
						break;
					default: throw new System.Exception("Profiler: RemoteDatabaseContext wrong request!");
				}
			}
		}
	}
}

#endif // PROFILER