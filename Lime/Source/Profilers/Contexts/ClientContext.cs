using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;
using Lime.Graphics.Platform;
using System.Threading;

namespace Lime.Profilers.Contexts
{
	/// <summary>
	/// The client is a data source for profiling.
	/// The client can be an instance of the engine or a game.
	/// Compiled game instance is always client.
	/// </summary>
	public class ClientContext : NetworkContext
	{
		private readonly Network.Client client;

		private readonly ConcurrentQueue<Request> requests;
		private readonly Queue<Response> unserializedResponses;
		private int serializedResponsesCount = 0;

		private long lastProcessedUpdateIndex;

		public override bool IsProfilingEnabled
		{
			get => GpuProfiler.Instance.IsEnabled;
			set => GpuProfiler.Instance.IsEnabled = value;
		}

		public override bool IsDrawCallsRenderTimeEnabled
		{
			get => GpuProfiler.Instance.IsDeepProfiling;
			set => GpuProfiler.Instance.IsDeepProfiling = value;
		}

		public override bool IsSceneOnlyDrawCallsRenderTime
		{
			get => GpuProfiler.Instance.IsSceneOnlyDeepProfiling;
			set => GpuProfiler.Instance.IsSceneOnlyDeepProfiling = value;
		}

		public ClientContext()
		{
			GpuHistory = GpuProfiler.Instance;
			CpuHistory = CpuProfiler.Instance;
			client = new Network.Client();
			client.OnReceived = RemoteProfilerMessageReceived;
			requests = new ConcurrentQueue<Request>();
			unserializedResponses = new Queue<Response>();
			lastProcessedUpdateIndex = CpuHistory.ProfiledUpdatesCount;
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint) => client.TryLaunch(ipEndPoint);

		public override void LocalDeviceUpdateStarted()
		{
			if (IsActiveContext && client.IsConnected) {
				bool isStatisticsSent = false;
				while (lastProcessedUpdateIndex < CpuHistory.ProfiledUpdatesCount) {
					var update = CpuHistory.GetUpdate(lastProcessedUpdateIndex);
					var frame = GpuHistory.GetFrame(update.FrameIndex);
					if (frame.IsCompleted) {
						Debug.Write("Client frame.IsCompleted");
						client.LazySerializeAndSend(
							new FrameStatistics {
								// todo there is no point in additional copying
								GpuInfo = frame.LightweightClone(),
								CpuInfo = update.LightweightClone(),
								Response = ProcessRequest()
							}
						);
						isStatisticsSent = true;
						lastProcessedUpdateIndex += 1;
					} else {
						break;
					}
				}
				if (!isStatisticsSent) {
					client.LazySerializeAndSend(
						new FrameStatistics {
							Response = ProcessRequest(),
							Options = GetCurrentOptions()
						}
					);
				}
			}
			for (int i = serializedResponsesCount; i > 0; i--) {
				FinalizeResponse(unserializedResponses.Dequeue());
			}
			Interlocked.Add(ref serializedResponsesCount, -serializedResponsesCount);
		}

		public override void LocalDeviceFrameRenderCompleted()
		{
			
		}

		private void RemoteProfilerMessageReceived()
		{
			if (IsActiveContext) {
				Network.IItem item;
				if (client.Received.TryDequeue(out item)) {
					if (item is Request request) {
						requests.Enqueue(request);
						return;
					}
					throw new InvalidOperationException();
				}
			}
		}

		private Response AcquireResponse() => new Response {
			OnSerialized = ResponseSerialized
		};

		private Response ProcessRequest()
		{
			Request request;
			Response response = null;
			if (!requests.IsEmpty && requests.TryDequeue(out request)) {
				if (request.Options != null) {
					ChangeProfilerOptions(request.Options);
				}
				if (request.GpuDrawCallsResultsForFrame && GpuHistory.IsFrameIndexValid(request.FrameIndex)) {
					response = response ?? AcquireResponse();
					response.FrameIndex = request.FrameIndex;
					var frame = GpuHistory.GetFrame(request.FrameIndex);
					List<ProfilingResult> drawCalls = null;
					if (frame.IsDeepProfilingEnabled && frame.IsCompleted) {
						drawCalls = frame.DrawCalls;
						frame.DrawCalls = new List<ProfilingResult>();
					}
					response.DrawCalls = drawCalls;
				}
				unserializedResponses.Enqueue(response);
			}
			return response;
		}

		private void ChangeProfilerOptions(ProfilerOptions options)
		{
			var gpuProfiler = GpuProfiler.Instance;
			if (ProfilerOptions.HasField(options.ProfilingEnabled)) {
				gpuProfiler.IsEnabled =
					ProfilerOptions.StateToBool(options.ProfilingEnabled);
			}
			if (ProfilerOptions.HasField(options.DrawCallsRenderTimeEnabled)) {
				gpuProfiler.IsDeepProfiling =
					ProfilerOptions.StateToBool(options.DrawCallsRenderTimeEnabled);
			}
			if (ProfilerOptions.HasField(options.SceneOnlyDrawCallsRenderTime)) {
				gpuProfiler.IsSceneOnlyDeepProfiling =
					ProfilerOptions.StateToBool(options.SceneOnlyDrawCallsRenderTime);
			}
		}

		private void ResponseSerialized() => Interlocked.Increment(ref serializedResponsesCount);

		public override void Completed()
		{
			client.RequestClose();
			while (unserializedResponses.Count > 0) {
				FinalizeResponse(unserializedResponses.Dequeue());
			}
			base.Completed();
		}

		private void FinalizeResponse(Response response) // data race
		{
			if (GpuHistory.IsFrameIndexValid(response.FrameIndex)) {
				// restoring the state of history
				if (response.DrawCalls != null) {
					var frame = GpuHistory.GetFrame(response.FrameIndex);
					frame.DrawCalls = response.DrawCalls;
				}
			}
		}

		private ProfilerOptions GetCurrentOptions() => new ProfilerOptions {
			ProfilingEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsEnabled),
			DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsDeepProfiling),
			SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(GpuProfiler.Instance.IsSceneOnlyDeepProfiling),
		};
	}
}
