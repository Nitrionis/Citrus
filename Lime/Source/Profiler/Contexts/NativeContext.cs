#if PROFILER

using System;
using System.Collections.Generic;
using System.IO;
using Lime.Profiler.Graphics;
using Yuzu.Binary;

namespace Lime.Profiler.Contexts
{
	using Task = System.Threading.Tasks.Task;

	public sealed class NativeContext : IProfilerContext
	{
		private readonly MemoryStream memoryStream;
		private readonly BinaryWriter binaryWriter;
		private readonly BinaryReader binaryReader;
		private readonly BinaryDeserializer deserializer;
		private readonly BinarySerializer serializer;
		private readonly Queue<IRequest> requests;
		private readonly FrameClipboard frameClipboard;

		private IProfilerDatabase database;
		private FrameDataResponse lastFrameDataResponse;
		private long lastProcessedFrame;

		private volatile bool isDataSelectionRequestCompleted;
		
		/// <inheritdoc />
		public event Action<ProfiledFrame> FrameProfilingFinished;
		
		/// <inheritdoc />
		public event Action<ProfilerOptions> ProfilerOptionsReceived;

		public NativeContext()
		{
			memoryStream = new MemoryStream();
			binaryWriter = new BinaryWriter(memoryStream);
			binaryReader = new BinaryReader(memoryStream);
			deserializer = new BinaryDeserializer();
			serializer = new BinarySerializer();
			requests = new Queue<IRequest>();
			frameClipboard = new FrameClipboard();
			isDataSelectionRequestCompleted = true;
		}

		/// <inheritdoc />
		public void Attached(IProfilerDatabase database)
		{
			if (this.database != null) {
				throw new InvalidOperationException("Profiler: NativeContext re-attachment is not supported!");
			}
			this.database = database;
			lastProcessedFrame = database.ProfiledFramesCount;
		}

		/// <inheritdoc />
		public void Detached() { }

		/// <inheritdoc />
		public void MainWindowUpdating()
		{
			bool isLoopBreakRequested = false;
			while (requests.Count > 0 && !isLoopBreakRequested) {
				switch (requests.Peek()) {
					case IDataSelectionRequest dataSelectionRequest:
						if (!dataSelectionRequest.IsRunning) {
							bool shouldUseCachedResponse =
								dataSelectionRequest is FrameDataRequest frameDataRequest &&
								lastFrameDataResponse != null &&
								lastFrameDataResponse.IsSucceed &&
								lastFrameDataResponse.ProfiledFrame.Identifier == frameDataRequest.FrameIdentifier;
							dataSelectionRequest.IsRunning = true;
							isDataSelectionRequestCompleted = false;
							database.PreventProfilingWhileRunning(new Task(() => {
								var responseProcessor = dataSelectionRequest.AsyncResponseProcessor;
								if (shouldUseCachedResponse) {
									responseProcessor.ProcessResponseAsync(lastFrameDataResponse);
								} else {
									memoryStream.Position = 0;
									dataSelectionRequest.FetchData(database, binaryWriter, serializer);
									memoryStream.Position = 0;
									var response = deserializer.FromReader(binaryReader);
									ProcessResponse(response, responseProcessor);
								}
								isDataSelectionRequestCompleted = true;
							}));
						} else {
							isLoopBreakRequested = true;
						}
						if (isDataSelectionRequestCompleted) {
							requests.Dequeue();
						}
						break;
					case IOptionsChangeRequest optionsChangeRequest:
						optionsChangeRequest.Execute(database);
						requests.Dequeue();
						break;
				}
			}
			while (lastProcessedFrame < database.LastAvailableFrame) {
				FrameProfilingFinished?.Invoke(database.GetFrame(++lastProcessedFrame).CommonData);
			}
			ProfilerOptionsReceived?.Invoke(new ProfilerOptions {
				ProfilerEnabled = database.ProfilerEnabled,
				BatchBreakReasonsRequired = database.BatchBreakReasonsRequired,
				IsSceneUpdateFrozen = database.IsSceneUpdateFrozen,
				OverdrawModeEnabled = Overdraw.Enabled
			});
		}

		private void ProcessResponse(object response, IAsyncResponseProcessor asyncResponseProcessor)
		{
			if (response is IDataSelectionResponseBuilder dataResponse) {
				var data = dataResponse.Build(frameClipboard, binaryReader);
				if (data is FrameDataResponse frameDataResponse) {
					lastFrameDataResponse = frameDataResponse;
				}
				asyncResponseProcessor.ProcessResponseAsync(data);
			} else {
				throw new System.Exception("Profiler: Wrong response!");
			}
		}

		/// <inheritdoc />
		public void RunRequest(IRequest request) => requests.Enqueue(request);
	}
}

#endif // PROFILER
