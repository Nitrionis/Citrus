using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Collections;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;
using Lime.Graphics.Platform;

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
						client.LazySerializeAndSend(
							new FrameStatistics {
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
		}

		public override void LocalDeviceFrameRenderCompleted()
		{
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

		private ResponseWithBehavior AcquireResponse() => new ResponseWithBehavior {
			OnSerialized = ResponseSerialized
		};

		private Response ProcessRequest()
		{
			Request request;
			ResponseWithBehavior response = null;
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
					response.NativeDrawCalls = drawCalls;
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

		private void FinalizeResponse(Response response)
		{
			if (GpuHistory.IsFrameIndexValid(response.FrameIndex)) {
				// restoring the state of history
				var responseWithBehavior = (ResponseWithBehavior)response;
				if (responseWithBehavior.NativeDrawCalls != null) {
					var frame = GpuHistory.GetFrame(response.FrameIndex);
					frame.DrawCalls = responseWithBehavior.NativeDrawCalls;
				}
			}
		}

		private ProfilerOptions GetCurrentOptions() => new ProfilerOptions {
			ProfilingEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsEnabled),
			DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsDeepProfiling),
			SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(GpuProfiler.Instance.IsSceneOnlyDeepProfiling),
		};

		private class ResponseWithBehavior : Response
		{
			public List<ProfilingResult> NativeDrawCalls;

			public ResponseWithBehavior() => OnSerializing = BeforeSerialization;

			private void BeforeSerialization()
			{
				if (NativeDrawCalls != null) {
					DrawCalls = new List<ProfilingResult>(NativeDrawCalls.Count);
					foreach (var drawCall in NativeDrawCalls) {
						var pi = drawCall.ProfilingInfo;
						var newDrawCall = new ProfilingResult {
							ProfilingInfo = new ProfilingInfo {
								IsPartOfScene  = pi.IsPartOfScene,
								Material       = pi.Material.GetType().Name,
								Owners         = GetOwners(pi.Owners)
							},
							RenderPassIndex        = drawCall.RenderPassIndex,
							StartTime              = drawCall.StartTime,
							AllPreviousFinishTime  = drawCall.AllPreviousFinishTime,
							FinishTime             = drawCall.FinishTime,
							TrianglesCount         = drawCall.TrianglesCount,
							VerticesCount          = drawCall.VerticesCount
						};
						DrawCalls.Add(newDrawCall);
					}
				}
			}

			private object GetOwners(object nativeOwners)
			{
				if (nativeOwners == null) {
					return null;
				} else if (nativeOwners is IList list) {
					var processedOwners = new List<object>(list.Count);
					foreach (var item in list) {
						if (item == null) {
							processedOwners.Add(null);
						} else {
							var node = (Node)item;
							processedOwners.Add(string.IsNullOrEmpty(node.Id) ? "Node id unset" : node.Id);
						}
					}
					return processedOwners;
				} else if (nativeOwners is Node node) {
					return string.IsNullOrEmpty(node.Id) ? "Node id unset" : node.Id;
				}
				throw new InvalidOperationException();
			}
		}
	}
}
