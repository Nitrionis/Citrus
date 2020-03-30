using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Collections;
using GpuHistory = Lime.Graphics.Platform.ProfilerHistory;
using GpuProfiler = Lime.Graphics.Platform.PlatformProfiler;
using Lime.Graphics.Platform;
using Yuzu;

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
		private readonly ConcurrentQueue<StatisticsWrapper> awaitingFinalization;
		private readonly Action<StatisticsWrapper> Serialized;

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

			networkMember = client = new Network.Client();
			client.OnReceived = RemoteProfilerMessageReceived;

			requests = new ConcurrentQueue<Request>();
			awaitingFinalization = new ConcurrentQueue<StatisticsWrapper>();
			Serialized = statistics => awaitingFinalization.Enqueue(statistics);

			lastProcessedUpdateIndex = CpuHistory.ProfiledUpdatesCount;
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint) => client.TryLaunch(ipEndPoint);

		public override void LocalDeviceUpdateStarted()
		{
			if (IsActiveContext && client.IsConnected) {
				bool isStatisticsSent = false;
				while (lastProcessedUpdateIndex < CpuHistory.ProfiledUpdatesCount) {
					var update = CpuHistory.GetUpdate(lastProcessedUpdateIndex);
					var frame = !GpuHistory.IsFrameIndexValid(update.FrameIndex) ?
						null : GpuHistory.GetFrame(update.FrameIndex);
					if (frame == null || frame.IsCompleted) {
						var statistics = new StatisticsWrapper() {
							Serialized       = Serialized,
							Update           = update.LightweightClone(),
							Frame            = frame?.LightweightClone(),
							NativeDrawCalls  = frame?.DrawCalls,
							Options          = GetCurrentOptions(),
							Response         = ProcessRequest()
						};
						frame.DrawCalls = new List<ProfilingResult>();
						client.LazySerializeAndSend(statistics);
						isStatisticsSent = true;
						lastProcessedUpdateIndex += 1;
					} else {
						break;
					}
				}
				if (!isStatisticsSent) {
					var statistics = new StatisticsWrapper() {
						Serialized  = Serialized,
						Response    = ProcessRequest(),
						Options     = GetCurrentOptions()
					};
					client.LazySerializeAndSend(statistics);
				}
			}
		}

		public override void LocalDeviceFrameRenderCompleted()
		{
			StatisticsWrapper statistics;
			while (!awaitingFinalization.IsEmpty && awaitingFinalization.TryDequeue(out statistics)) {
				RestoreNativeFrame(statistics);
			}
		}

		/// <remarks>Asynchronous call.</remarks>
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

		/// <remarks>At least in sync with update.</remarks>
		private Response ProcessRequest()
		{
			Request request;
			Response response = null;
			if (!requests.IsEmpty && requests.TryDequeue(out request)) {
				if (request.Options != null) {
					ChangeProfilerOptions(request.Options);
				}
			}
			return response;
		}

		/// <remarks>At least in sync with update.</remarks>
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

		/// <remarks>At least in sync with update.</remarks>
		public override void Completed()
		{
			client.RequestClose();
			base.Completed();
		}

		/// <remarks>At least in sync with rendering.</remarks>
		private void RestoreNativeFrame(StatisticsWrapper statistics)
		{
			// Try restore the state of history.
			if (
				statistics.Frame != null &&
				GpuHistory.IsFrameIndexValid(statistics.Frame.FrameIndex)
				)
			{
				var frame = GpuHistory.GetFrame(statistics.Frame.FrameIndex);
				frame.DrawCalls = statistics.NativeDrawCalls;
			}
		}

		private ProfilerOptions GetCurrentOptions() => new ProfilerOptions {
			ProfilingEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsEnabled),
			DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsDeepProfiling),
			SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(GpuProfiler.Instance.IsSceneOnlyDeepProfiling),
		};

		private class StatisticsWrapper : Statistics
		{
			public List<ProfilingResult> NativeDrawCalls;

			public Action<StatisticsWrapper> Serialized;

			/// <remarks>Asynchronous call.</remarks>
			[YuzuAfterSerialization]
			private void AfterSerialization() => Serialized?.Invoke(this);

			/// <remarks>Asynchronous call.</remarks>
			[YuzuBeforeSerialization]
			private void BeforeSerialization()
			{
				if (Frame != null) {
					if (Frame.DrawCalls.Capacity < NativeDrawCalls.Count) {
						Frame.DrawCalls.Capacity = NativeDrawCalls.Count;
					}
					foreach (var drawCall in NativeDrawCalls) {
						var pi = drawCall.ProfilingInfo;
						var newDrawCall = new ProfilingResult {
							ProfilingInfo = new ProfilingInfo {
								IsPartOfScene  = pi.IsPartOfScene,
								Material       = pi.Material.GetType().Name,
								Owners         = ConvertOwnersToText(pi.Owners)
							},
							RenderPassIndex        = drawCall.RenderPassIndex,
							StartTime              = drawCall.StartTime,
							AllPreviousFinishTime  = drawCall.AllPreviousFinishTime,
							FinishTime             = drawCall.FinishTime,
							TrianglesCount         = drawCall.TrianglesCount,
							VerticesCount          = drawCall.VerticesCount
						};
						Frame.DrawCalls.Add(newDrawCall);
					}
				}
			}

			private object ConvertOwnersToText(object nativeOwners)
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
