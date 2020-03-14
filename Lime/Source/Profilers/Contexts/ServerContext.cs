using System;
using System.Net;

namespace Lime.Profilers.Contexts
{
	/// <summary>
	/// The server is the receiver and data visualizer.
	/// The server can only be an instance of the engine.
	/// </summary>
	public class ServerContext : NetworkContext
	{
		private readonly Network.Server server;

		public IPEndPoint LocalEndpoint => (IPEndPoint)server.Listener.LocalEndpoint;

		private class SparsedGpuHistory : Graphics.Platform.ProfilerHistory
		{
			public void Enqueue(Item item)
			{
				long index = item.FrameIndex % items.Length;
				items[index].Reset();
				items[index] = item;
				if (ProfiledFramesCount < item.FrameIndex + 1) {
					ProfiledFramesCount = item.FrameIndex + 1;
					LastFrame = item;
				}
			}
		}

		private SparsedGpuHistory sparsedGpuHistory;

		private class SparsedCpuHistory : CpuHistory
		{
			public void Enqueue(Item item)
			{
				long index = item.UpdateIndex % items.Length;
				items[index].Reset();
				items[index] = item;
				if (ProfiledUpdatesCount < item.UpdateIndex + 1) {
					ProfiledUpdatesCount = item.UpdateIndex + 1;
					LastUpdate = item;
				}
			}
		}

		private SparsedCpuHistory sparsedCpuHistory;

		public Action<Response> OnResponseReceived;

		private bool isProfilingEnabled;

		public override bool IsProfilingEnabled
		{
			get => isProfilingEnabled;
			set => SetProfilingEnabled(value);
		}

		private bool isDrawCallsRenderTimeEnabled;

		public override bool IsDrawCallsRenderTimeEnabled
		{
			get => isDrawCallsRenderTimeEnabled;
			set => SetDrawCallsRenderTimeEnabled(value);
		}

		private bool isSceneOnlyDrawCallsRenderTime;

		public override bool IsSceneOnlyDrawCallsRenderTime
		{
			get => isSceneOnlyDrawCallsRenderTime;
			set => SetSceneOnlyDrawCallsRenderTime(value);
		}

		public ServerContext()
		{
			sparsedGpuHistory = new SparsedGpuHistory();
			sparsedCpuHistory = new SparsedCpuHistory();
			GpuHistory = sparsedGpuHistory;
			CpuHistory = sparsedCpuHistory;
			server = new Network.Server();
		}

		public override bool TryLaunch(IPEndPoint ipEndPoint) => server.TryLaunch(ipEndPoint);

		public override void LocalDeviceFrameRenderCompleted()
		{
			Network.IItem item;
			while (!server.Received.IsEmpty && server.Received.TryDequeue(out item)) {
				if (item is FrameStatistics frame) {
					if (frame.GpuInfo != null) {
						sparsedGpuHistory.Enqueue(frame.GpuInfo);
					}
					if (frame.CpuInfo != null) {
						sparsedCpuHistory.Enqueue(frame.CpuInfo);
					}
					Application.MainWindow.Invalidate();
					UpdateProfilerOptions(frame);
					if (frame.Response != null) {
						ProcessResponse(frame.Response);
					}
				}
			}
			server.TryReceive();
		}

		private void UpdateProfilerOptions(FrameStatistics frame)
		{
			if (frame.GpuInfo != null) {
				isProfilingEnabled = true;
				isDrawCallsRenderTimeEnabled = frame.GpuInfo.IsDeepProfilingEnabled;
				isSceneOnlyDrawCallsRenderTime = frame.GpuInfo.IsSceneOnlyDeepProfiling;
			}
			if (frame.Options != null) {
				isProfilingEnabled =
					ProfilerOptions.StateToBool(frame.Options.ProfilingEnabled);
				isDrawCallsRenderTimeEnabled =
					ProfilerOptions.StateToBool(frame.Options.DrawCallsRenderTimeEnabled);
				isSceneOnlyDrawCallsRenderTime =
					ProfilerOptions.StateToBool(frame.Options.SceneOnlyDrawCallsRenderTime);
			}
		}

		private void ProcessResponse(Response response)
		{
			if (response.FrameIndex != -1 && response.DrawCalls != null) {
				var frame = base.GpuHistory.GetFrame(response.FrameIndex);
				frame.IsCompleted = true;
				frame.DrawCalls = response.DrawCalls;
			}
			OnResponseReceived?.Invoke(response);
		}

		public override void Completed()
		{
			server.RequestClose();
			base.Completed();
		}

		protected void SetProfilingEnabled(bool value) =>
			SendRequest(new ProfilerOptions { ProfilingEnabled = ProfilerOptions.StateOf(value) });

		protected void SetDrawCallsRenderTimeEnabled(bool value) =>
			SendRequest(new ProfilerOptions { DrawCallsRenderTimeEnabled = ProfilerOptions.StateOf(value) });

		protected void SetSceneOnlyDrawCallsRenderTime(bool value) =>
			SendRequest(new ProfilerOptions { SceneOnlyDrawCallsRenderTime = ProfilerOptions.StateOf(value) });

		private void SendRequest(ProfilerOptions options) =>
			server.SerializeAndSend(new Request { Options = options });
	}
}
