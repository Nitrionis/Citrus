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

		private struct FrameUpdatePair
		{
			public GpuHistory.Item Frame;
			public CpuHistory.Item Update;
		}

		/// <remarks>
		/// Do not send frames that are still rendering on the GPU.
		/// </remarks>
		private readonly Queue<FrameUpdatePair> unfinished;

		private readonly ConcurrentQueue<Request> requests;

		private readonly Queue<Response> unserializedResponses;
		private int serializedResponsesCount = 0;

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
			unfinished = new Queue<FrameUpdatePair>();
			requests = new ConcurrentQueue<Request>();
			unserializedResponses = new Queue<Response>();
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint) => client.TryLaunch(ipEndPoint);

		public override void LocalDeviceFrameRenderCompleted()
		{
			if (IsActiveContext && client.IsConnected) {
				if (GpuProfiler.Instance.IsEnabled) {
					unfinished.Enqueue(new FrameUpdatePair {
						Frame = GpuHistory.LastFrame,
						Update = CpuHistory.LastUpdate
					});
				}
				bool isOptionsSent = false;
				while (unfinished.Count > 0 && unfinished.Peek().Frame.IsCompleted) {
					var frameUpdatePair = unfinished.Dequeue();
					Request request;
					Response response = null;
					if (!requests.IsEmpty && requests.TryDequeue(out request)) {
						response = ProcessRequest(request);
						unserializedResponses.Enqueue(response);
					}
					client.LazySerializeAndSend(
						new FrameStatistics {
							GpuInfo = frameUpdatePair.Frame.LightweightClone(),
							CpuInfo = frameUpdatePair.Update.LightweightClone(),
							Response = response
						}
					);
					isOptionsSent = true;
				}
				if (!isOptionsSent) {
					client.LazySerializeAndSend(
						new FrameStatistics {
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

		private Response ProcessRequest(Request request)
		{
			Response response = null;
			if (request.Options != null) {
				ChangeProfilerOptions(request.Options);
			}
			if (request.GpuDrawCallsResultsForFrame && IsFrameIndexValid(request.FrameIndex)) {
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

		private bool IsFrameIndexValid(long index) =>
			0 < index && index < GpuHistory.ProfiledFramesCount &&
			GpuHistory.ProfiledFramesCount - index < GpuHistory.HistoryFramesCount;

		public override void Completed()
		{
			client.RequestClose();
			while (unserializedResponses.Count > 0) {
				FinalizeResponse(unserializedResponses.Dequeue());
			}
			base.Completed();
		}

		private void FinalizeResponse(Response response)
		{
			if (IsFrameIndexValid(response.FrameIndex)) {
				// restoring the state of history
				if (response.DrawCalls != null) {
					var frame = GpuHistory.GetFrame(response.FrameIndex);
					frame.DrawCalls = response.DrawCalls;
				}
			}
		}

		protected void SetProfilingEnabled(bool value) =>
			GpuProfiler.Instance.IsEnabled = value;

		protected void SetDrawCallsRenderTimeEnabled(bool value) =>
			GpuProfiler.Instance.IsDeepProfiling = value;

		protected void SetSceneOnlyDrawCallsRenderTime(bool value) =>
			GpuProfiler.Instance.IsSceneOnlyDeepProfiling = value;

		private ProfilerOptions GetCurrentOptions() => new ProfilerOptions {
			ProfilingEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsEnabled),
			DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsDeepProfiling),
			SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(GpuProfiler.Instance.IsSceneOnlyDeepProfiling),
		};
	}
}
