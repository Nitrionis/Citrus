#if PROFILER

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Lime.Profiler.Network;
using Lime.Profiler.Formatting;

namespace Lime.Profiler.Contexts
{
	using Task = System.Threading.Tasks.Task;
	
	public class RemoteTerminalContext : IProfilerContext, IInterrupter
	{
		private readonly Server server;
		private readonly Queue<IRequest> requests;
		private readonly ConcurrentQueue<object> responses;
		private readonly FrameClipboard frameClipboard;
		
		private IProfilerDatabase database;
		private FrameDataResponse lastFrameDataResponse;
		private BinaryReader lastBinaryReader;
		
		private volatile bool isAttached;
		private volatile bool isDataSelectionRequestCompleted;
		private volatile bool isDeserializationPaused;

		public bool IsRemoteDeviceConnected => server.IsAlive;
		
		/// <inheritdoc />
		public bool IsSerializationPaused => false;

		/// <inheritdoc />
		public bool IsDeserializationPaused => isDeserializationPaused;
		
		/// <inheritdoc />
		public event Action<ProfiledFrame> FrameProfilingFinished;
		
		/// <inheritdoc />
		public event Action<ProfilerOptions> ProfilerOptionsReceived;

		public RemoteTerminalContext(IPEndPoint ipEndPoint)
		{
			requests = new Queue<IRequest>();
			responses = new ConcurrentQueue<object>();
			frameClipboard = new FrameClipboard();
			server = new Server(ipEndPoint, this);
			isDataSelectionRequestCompleted = true;
		}

		/// <inheritdoc />
		public void RunRequest(IRequest request)
		{
			if (server.IsAlive) {
				requests.Enqueue(request);
			}
		}

		/// <inheritdoc />
		public void Attached(IProfilerDatabase database)
		{
			if (this.database != null) {
				throw new InvalidOperationException("Profiler: RemoteTerminalContext re-attachment is not supported!");
			}
			this.database = database;
			isAttached = true;
		}

		/// <inheritdoc />
		public void MainWindowUpdating()
		{
			if (!server.IsAlive && server.IsRemoteDeviceHasBeenConnected) {
				if (isAttached) {
					ProfilerDatabase.Context = new NativeContext();
				}
				isAttached = false;
				return;
			}
			ProcessResponses();
			ProcessRequests();
		}

		private object PeekResponse()
		{
			if (!responses.TryPeek(out var response)) {
				throw new System.Exception("Profiler: Wrong behavior detected!");
			}
			return response;
		}
		
		private void DequeueResponse(object expectedResponse)
		{
			if (!responses.TryDequeue(out var r) || !ReferenceEquals(r, expectedResponse)) {
				throw new System.Exception("Profiler: Wrong behavior detected!");
			}
		}
		
		private void ProcessResponses()
		{
			while (!responses.IsEmpty) {
				var response = PeekResponse();
				switch (response) {
					case ProfiledFrame profiledFrame: 
						FrameProfilingFinished?.Invoke(profiledFrame);
						DequeueResponse(response);
						break;
					case ProfilerOptions profilerOptions: 
						ProfilerOptionsReceived?.Invoke(profilerOptions);
						DequeueResponse(response);
						break;
					case IDataSelectionResponse _: return;
					default: throw new System.Exception("Profiler: RemoteTerminalContext wrong response!");
				}
			}
		}

		private void ProcessRequests()
		{
			while (isAttached && requests.Count > 0) {
				switch (requests.Peek()) {
					case IDataSelectionRequest dataSelectionRequest:
						if (!dataSelectionRequest.IsRunning) {
							bool shouldUseCachedResponse =
								dataSelectionRequest is FrameDataRequest frameDataRequest &&
								lastFrameDataResponse != null &&
								lastFrameDataResponse.IsSuccessed &&
								lastFrameDataResponse.FrameIdentifier == frameDataRequest.FrameIdentifier;
							dataSelectionRequest.IsRunning = true;
							isDataSelectionRequestCompleted = false;
							if (!shouldUseCachedResponse) {
								server.LazySend(dataSelectionRequest);
							} else {
								var processor = dataSelectionRequest.ResponseProcessor;
								Task.Run(() => {
									processor.ProcessResponse(lastFrameDataResponse);
									isDataSelectionRequestCompleted = true;
								});
							}
						}
						if (!responses.IsEmpty) {
							var responseBuilder = PeekResponse();
							if (responseBuilder is IDataSelectionResponseBuilder builder) {
								var processor = dataSelectionRequest.ResponseProcessor;
								Task.Run(() => {
									try {
										var response = builder.Build(frameClipboard, lastBinaryReader);
										if (response is FrameDataResponse frameDataResponse) {
											lastFrameDataResponse = frameDataResponse;
										}
										processor.ProcessResponse(response);
										isDataSelectionRequestCompleted = true;
									} catch (IOException e) {
										Console.WriteLine(e.ToString());
										server.RequestClose();
									} catch (SocketException e) {
										Console.WriteLine(e.ToString());
										server.RequestClose();
									}
									isDeserializationPaused = false;
								});
								DequeueResponse(responseBuilder);
							}
						}
						if (isDataSelectionRequestCompleted) {
							server.LazySend(new ContinueSendProfilerOptions());
							requests.Dequeue();
						} else {
							return;
						}
						break;
					case IOptionsChangeRequest optionsChangeRequest:
						server.LazySend(optionsChangeRequest);
						requests.Dequeue();
						break;
				}
			}
		}
		
		/// <inheritdoc />
		public void Detached()
		{
			isAttached = false;
			if (server.IsAlive) {
				server.RequestClose();
			}
		}

		/// <inheritdoc />
		public void AfterSerialization(BinaryWriter writer, object @object) { }

		/// <inheritdoc />
		public void AfterDeserialization(BinaryReader reader, object @object)
		{
			if (isAttached) {
				switch (@object) {
					case ProfiledFrame profiledFrame:
					case ProfilerOptions profilerOptions:
						responses.Enqueue(@object);
						break;
					case IDataSelectionRequest dataSelectionRequest: break;
					case IDataSelectionResponseBuilder responseBuilder:
						isDeserializationPaused = true;
						lastBinaryReader = reader;
						responses.Enqueue(responseBuilder);
						break;
					default: throw new System.Exception("Profiler: RemoteTerminalContext wrong message!");
				}
			}
		}
	}
}

#endif // PROFILER