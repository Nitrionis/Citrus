using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Collections;
using GpuHistory = Lime.Graphics.Platform.GpuHistory;
using GpuProfiler = Lime.Graphics.Platform.RenderGpuProfiler;
using Lime.Graphics.Platform;
using Yuzu;

namespace Lime.Profilers.Contexts
{
	/// <summary>
	/// The client is a data source for profiling.
	/// The client can only be an instance of the game.
	/// </summary>
	public class ClientContext : NetworkContext
	{
		private readonly Network.Client client;

		private readonly ConcurrentQueue<Request> requests;
		private readonly ConcurrentQueue<StatisticsWrapper> framesAwaitingFinalization;
		private readonly ConcurrentQueue<StatisticsWrapper> updatesAwaitingFinalization;
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
			framesAwaitingFinalization = new ConcurrentQueue<StatisticsWrapper>();
			updatesAwaitingFinalization = new ConcurrentQueue<StatisticsWrapper>();
			Serialized = statistics => {
				updatesAwaitingFinalization.Enqueue(statistics);
				framesAwaitingFinalization.Enqueue(statistics);
			};

			lastProcessedUpdateIndex = CpuHistory.ProfiledUpdatesCount;
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint) => client.TryLaunch(ipEndPoint);

		public override void LocalDeviceUpdateStarted()
		{
			StatisticsWrapper statistics = null;
			if (IsActiveContext && client.IsConnected) {
				while (lastProcessedUpdateIndex < CpuHistory.ProfiledUpdatesCount) {
					var update = CpuHistory.GetUpdate(lastProcessedUpdateIndex);
					var frame = !GpuHistory.IsFrameIndexValid(update.FrameIndex) ?
						null : GpuHistory.GetFrame(update.FrameIndex);
					if (frame == null || frame.IsCompleted) {
						statistics = new StatisticsWrapper() {
							Serialized       = Serialized,
							Update           = update.LightweightClone(),
							Frame            = frame?.LightweightClone(),
							NativeDrawCalls  = frame?.DrawCalls,
							NativeCpuUsages  = update.NodesResults,
							Options          = GetCurrentOptions(),
							Response         = ProcessRequest()
						};
						if (frame != null) {
							frame.DrawCalls = new List<GpuUsage>();
						}
						update.NodesResults = new List<CpuUsage>();
						client.LazySerializeAndSend(statistics);
						lastProcessedUpdateIndex += 1;
					} else {
						break;
					}
				}
				if (statistics == null) {
					client.LazySerializeAndSend(new Statistics() {
						Response = ProcessRequest(),
						Options = GetCurrentOptions()
					});
				}
			}
			while (!updatesAwaitingFinalization.IsEmpty && updatesAwaitingFinalization.TryDequeue(out statistics)) {
				RestoreNativeUpdate(statistics);
			}
		}

		public override void LocalDeviceFrameRenderCompleted()
		{
			StatisticsWrapper statistics;
			while (!framesAwaitingFinalization.IsEmpty && framesAwaitingFinalization.TryDequeue(out statistics)) {
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

		/// <remarks>At least in sync with rendering.</remarks>
		private void RestoreNativeUpdate(StatisticsWrapper statistics)
		{
			// Try restore the state of history.
			if (
				statistics.Update != null &&
				CpuHistory.IsUpdateIndexValid(statistics.Update.UpdateIndex)
				)
			{
				var update = CpuHistory.GetUpdate(statistics.Update.UpdateIndex);
				update.NodesResults = statistics.NativeCpuUsages;
			}
		}

		private ProfilerOptions GetCurrentOptions() => new ProfilerOptions {
			ProfilingEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsEnabled),
			DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(GpuProfiler.Instance.IsDeepProfiling),
			SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(GpuProfiler.Instance.IsSceneOnlyDeepProfiling),
		};

		private class StatisticsWrapper : Statistics
		{
			public List<CpuUsage> NativeCpuUsages;
			public List<GpuUsage> NativeDrawCalls;

			public Action<StatisticsWrapper> Serialized;

			/// <remarks>Asynchronous with respect to updating and rendering.</remarks>
			[YuzuBeforeSerialization]
			private void BeforeSerialization()
			{
				if (Update.NodesResults.Capacity < NativeCpuUsages.Count) {
					Update.NodesResults.Capacity = NativeCpuUsages.Count;
				}
				if (Frame != null) {
					if (Frame.DrawCalls.Capacity < NativeDrawCalls.Count) {
						Frame.DrawCalls.Capacity = NativeDrawCalls.Count;
					}
					foreach (var usage in NativeCpuUsages) {
						var usageCopy = MemoryManager<CpuUsage>.Acquire();
						usageCopy.Reasons        = usage.Reasons;
						usageCopy.IsPartOfScene  = usage.IsPartOfScene;
						usageCopy.Start          = usage.Start;
						usageCopy.Finish         = usage.Finish;
						usageCopy.Owners         = null;
						if (usage.Reasons.Include(CpuUsage.UsageReasons.BatchRender)) {
							while (Frame.DrawCalls.Count < NativeDrawCalls.Count) {
								var dc = NativeDrawCalls[Frame.DrawCalls.Count];
								if (dc.GpuCallInfo.Owners == usage.Owners) {
									usageCopy.Owners = Frame.DrawCalls.Count;
									break;
								}
								Frame.DrawCalls.Add(CopyGpuUsage(dc));
							}
							if (usageCopy.Owners == null) {
								usageCopy.Owners = ConvertOwnersListToText((IList)usage.Owners);
							}
						} else {
							usageCopy.Owners = ((Node)usage.Owners).Id ?? "Node id unset";
						}
						while (Frame.DrawCalls.Count < NativeDrawCalls.Count) {
							Frame.DrawCalls.Add(CopyGpuUsage(NativeDrawCalls[Frame.DrawCalls.Count]));
						}
						Update.NodesResults.Add(usageCopy);
					}
				} else {
					foreach (var usage in NativeCpuUsages) {
						Update.NodesResults.Add(CopyCpuUsage(usage));
					}
				}
			}

			/// <remarks>Asynchronous with respect to updating and rendering.</remarks>
			[YuzuAfterSerialization]
			private void AfterSerialization()
			{
				if (Frame != null) {
					foreach (var drawCall in Frame.DrawCalls) {
						MemoryManager<GpuCallInfo>.Free(drawCall.GpuCallInfo);
						MemoryManager<GpuUsage>.Free(drawCall);
					}
				}
				if (Update != null) {
					foreach (var usage in Update.NodesResults) {
						MemoryManager<CpuUsage>.Free(usage);
					}
				}
				Serialized?.Invoke(this);
			}

			/// <remarks>Asynchronous with respect to updating and rendering.</remarks>
			[YuzuAfterDeserialization]
			private void AfterDeserialization()
			{
				if (Frame != null) {
					foreach (var usage in Update.NodesResults) {
						if (usage.Reasons.Include(CpuUsage.UsageReasons.BatchRender) && usage.Owners is int index) {
							usage.Owners = Frame.DrawCalls[index].GpuCallInfo.Owners;
						}
					}
				}
			}

			private GpuUsage CopyGpuUsage(GpuUsage usage)
			{
				var info = usage.GpuCallInfo;
				var infoCopy = MemoryManager<GpuCallInfo>.Acquire();
				infoCopy.IsPartOfScene  = info.IsPartOfScene;
				infoCopy.Material       = info.Material.GetType().Name;
				infoCopy.Owners         = ConvertOwnersToText(info.Owners);
				var usageCopy = MemoryManager<GpuUsage>.Acquire();
				usageCopy.GpuCallInfo            = infoCopy;
				usageCopy.RenderPassIndex        = usage.RenderPassIndex;
				usageCopy.StartTime              = usage.StartTime;
				usageCopy.AllPreviousFinishTime  = usage.AllPreviousFinishTime;
				usageCopy.FinishTime             = usage.FinishTime;
				usageCopy.TrianglesCount         = usage.TrianglesCount;
				usageCopy.VerticesCount          = usage.VerticesCount;
				return usageCopy;
			}

			private CpuUsage CopyCpuUsage(CpuUsage usage)
			{
				var usageCopy = MemoryManager<CpuUsage>.Acquire();
				usageCopy.Reasons        = usage.Reasons;
				usageCopy.Owners         = ConvertOwnersToText(usage.Owners);
				usageCopy.IsPartOfScene  = usage.IsPartOfScene;
				usageCopy.Start          = usage.Start;
				usageCopy.Finish         = usage.Finish;
				return usageCopy;
			}

			private object ConvertOwnersToText(object nativeOwners)
			{
				switch (nativeOwners) {
					case null: return null;
					case Node node: return string.IsNullOrEmpty(node.Id) ? "Node id unset" : node.Id;
					case IList list: return ConvertOwnersListToText(list);
					default: throw new InvalidOperationException();
				}
			}

			private object ConvertOwnersListToText(IList owners)
			{
				var processedOwners = new List<object>(owners.Count);
				foreach (var item in owners) {
					if (item == null) {
						processedOwners.Add(null);
					} else {
						var node = (Node)item;
						processedOwners.Add(string.IsNullOrEmpty(node.Id) ? "Node id unset" : node.Id);
					}
				}
				return processedOwners;
			}

			private class MemoryManager<T> where T : class, new()
			{
				private static readonly Stack<T> freeInstances = new Stack<T>();

				public static T Acquire() => freeInstances.Count > 0 ? freeInstances.Pop() : new T();

				public static void Free(T item) => freeInstances.Push(item);
			}
		}
	}
}
